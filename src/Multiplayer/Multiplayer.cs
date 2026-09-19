using HarmonyLib;
using Il2CppMicrosoft.AspNetCore.SignalR.Client;
using PolyMod.Multiplayer.ViewModels;
using Polytopia.Data;
using PolytopiaBackendBase;
using PolytopiaBackendBase.Common;
using PolytopiaBackendBase.Game;
using PolytopiaBackendBase.Game.BindingModels;
using UnityEngine;
using Newtonsoft.Json;
using PolytopiaBackendBase.Auth;

namespace PolyMod.Multiplayer;

public static class Client
{
    internal const string DEFAULT_SERVER_URL = "https://dev.polydystopia.xyz";
    internal const string LOCAL_SERVER_URL = "http://localhost:5051/";
    private const string GldMarker = "##GLD:";
    internal static bool allowGldMods = false;

    // Cache parsed GLD by game Seed to handle rewinds/reloads
    private static readonly Dictionary<int, GameLogicData> _gldCache = new();
    private static readonly Dictionary<int, int> _versionCache = new(); // Seed -> modGldVersion

    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Client));
        BuildConfig buildConfig = BuildConfigHelper.GetSelectedBuildConfig();
        buildConfig.buildServerURL = BuildServerURL.Custom;
        buildConfig.customServerURL = Plugin.config.backendUrl;

        // Update BackendUri and HttpClient.BaseAddress since PolytopiaBackendAdapter.Instance
        // was statically initialized before plugins load, so it still points to polytopia-prod.net
        var uri = new Il2CppSystem.Uri(Plugin.config.backendUrl);
        PolytopiaBackendAdapter.Instance.UseBackendUri(uri);
        PolytopiaBackendAdapter.Instance.BackendHttpClient.BaseAddress = uri;

        Plugin.logger.LogInfo($"Multiplayer> Server URL set to: {Plugin.config.backendUrl}");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MultiplayerSelectionScreen), nameof(MultiplayerSelectionScreen.Awake))]
    public static void MultiplayerSelectionScreen_Awake(MultiplayerSelectionScreen __instance)
    {
        __instance.TournamentsButton.gameObject.SetActive(false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.Init))]
    private static void StartScreen_UI2_HideButtons(StartScreen_UI2 __instance)
    {
        __instance.highscoreButton.gameObject.SetActive(false);
        __instance.weeklyChallengeButton.gameObject.SetActive(false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.RunLayout))]
    private static void StartScreen_UI2_ReflowRoundButtons(StartScreen_UI2 __instance, ScreenBase_UI2.ScreenSize screenSize)
    {
        // RunLayout adds all four round buttons to a UITable unconditionally, so hiding the highscore button leaves a gap. Re-run the row without it so the rest recenter.
        UITable table = new();
        table.AddCell(__instance.settingsButton.Cast<IUILayoutable>());
        table.AddCell(__instance.throneRoomButton.Cast<IUILayoutable>());
        table.AddCell(__instance.aboutButton.Cast<IUILayoutable>());
        table.SetBottom(screenSize.safeRect.Bottom + __instance.settingsButton.GetHalfHeight() + 15f);
        table.margin = 20f;
        table.RunLayout();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SystemInfo), nameof(SystemInfo.deviceUniqueIdentifier), MethodType.Getter)]
    public static void SteamClient_get_SteamId(ref string  __result)
    {
        if (Plugin.config.overrideDeviceId != string.Empty)
        {
            __result = Plugin.config.overrideDeviceId;
        }
    }

    /// <summary>
    /// After GameState deserialization, check for trailing GLD version ID and set mockedGameLogicData.
    /// The server appends "##GLD:" + modGldVersion (int) after the normal serialized data.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameState), nameof(GameState.Deserialize))]
    [Obsolete("This will be succeeded by ModMultiplayer in the future.")]
    private static void Deserialize_Postfix(GameState __instance, BinaryReader __0)
    {
        if(!allowGldMods) return;

        Plugin.logger?.LogDebug("Deserialize_Postfix: Entered");

        try
        {
            var reader = __0;
            if (reader == null)
            {
                Plugin.logger?.LogWarning("Deserialize_Postfix: reader is null");
                return;
            }

            var position = reader.BaseStream.Position;
            var length = reader.BaseStream.Length;
            var remaining = length - position;

            Plugin.logger?.LogDebug($"Deserialize_Postfix: Stream position={position}, length={length}, remaining={remaining}");

            // Check if there's more data after normal deserialization
            if (position >= length)
            {
                Plugin.logger?.LogDebug("Deserialize_Postfix: No trailing data (position >= length)");

                var sd = __instance.Seed;
                if (_gldCache.TryGetValue(sd, out var cachedGld))
                {
                    __instance.mockedGameLogicData = cachedGld;
                    var cachedVersion = _versionCache.GetValueOrDefault(sd, -1);
                    Plugin.logger?.LogInfo($"Deserialize_Postfix: Applied cached GLD for Seed={sd}, ModGldVersion={cachedVersion}");
                }
                return;
            }

            Plugin.logger?.LogDebug($"Deserialize_Postfix: Found {remaining} bytes of trailing data, attempting to read marker");

            var marker = reader.ReadString();
            Plugin.logger?.LogDebug($"Deserialize_Postfix: Read marker string: '{marker}'");

            if (marker != GldMarker)
            {
                Plugin.logger?.LogDebug($"Deserialize_Postfix: Marker mismatch - expected '{GldMarker}', got '{marker}'");
                return;
            }

            Plugin.logger?.LogInfo($"Deserialize_Postfix: Found GLD marker '{GldMarker}'");

            var modGldVersion = reader.ReadInt32();
            Plugin.logger?.LogInfo($"Deserialize_Postfix: Found embedded ModGldVersion: {modGldVersion}");

            Plugin.logger?.LogDebug($"Deserialize_Postfix: Fetching GLD from server for version {modGldVersion}");
            var gldJson = FetchGldById(modGldVersion);
            if (string.IsNullOrEmpty(gldJson))
            {
                Plugin.logger?.LogError($"Deserialize_Postfix: Failed to fetch GLD for ModGldVersion: {modGldVersion}");
                return;
            }

            Plugin.logger?.LogDebug($"Deserialize_Postfix: Parsing GLD JSON ({gldJson.Length} chars)");

            var customGld = new GameLogicData();
            customGld.Parse(gldJson);
            __instance.mockedGameLogicData = customGld;

            // Cache for subsequent deserializations (rewinds, reloads)
            var seed = __instance.Seed;
            _gldCache[seed] = customGld;
            _versionCache[seed] = modGldVersion;

            Plugin.logger?.LogInfo($"Deserialize_Postfix: Successfully set mockedGameLogicData from ModGldVersion: {modGldVersion}, cached for Seed={seed}");
        }
        catch (EndOfStreamException)
        {
            Plugin.logger?.LogDebug("Deserialize_Postfix: EndOfStreamException - no trailing data");
        }
        catch (Exception ex)
        {
            Plugin.logger?.LogError($"Deserialize_Postfix: Exception: {ex.GetType().Name}: {ex.Message}");
            Plugin.logger?.LogDebug($"Deserialize_Postfix: Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Fetch GLD from server using ModGldVersion ID
    /// </summary>
    [Obsolete("This will be succeeded by ModMultiplayer in the future.")]
    private static string? FetchGldById(int modGldVersion)
    {
        if(!allowGldMods) return null;
        try
        {
            using var client = new HttpClient();
            var url = $"{Plugin.config.backendUrl.TrimEnd('/')}/api/mods/gld/{modGldVersion}";
            Plugin.logger?.LogDebug($"FetchGldById: Requesting URL: {url}");

            var response = client.GetAsync(url).Result;
            Plugin.logger?.LogDebug($"FetchGldById: Response status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var gld = response.Content.ReadAsStringAsync().Result;
                Plugin.logger?.LogInfo($"FetchGldById: Successfully fetched mod GLD ({gld.Length} chars)");
                return gld;
            }

            var errorContent = response.Content.ReadAsStringAsync().Result;
            Plugin.logger?.LogError($"FetchGldById: Failed with status {response.StatusCode}: {errorContent}");
        }
        catch (Exception ex)
        {
            Plugin.logger?.LogError($"FetchGldById: Exception: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                Plugin.logger?.LogError($"FetchGldById: Inner exception: {ex.InnerException.Message}");
            }
        }
        return null;
    }
}
