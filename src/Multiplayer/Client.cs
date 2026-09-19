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

        Plugin.logger.LogInfo("Multiplayer> ClientBase_SendCommand");
        Il2CppSystem.Threading.Tasks.Task<ServerResponse<BoolResponseViewModel>> task = new();
        var taskCompletionSource = new Il2CppSystem.Threading.Tasks.TaskCompletionSource<ServerResponse<BoolResponseViewModel>>();

        _ = ClientBase_SendCommand_Async(taskCompletionSource, __instance, command);

        task = taskCompletionSource.Task;

        return false;
    }

    /// <summary>
    /// Fetch GLD from server using ModGldVersion ID
    /// </summary>
    [Obsolete("This will be succeeded by ModMultiplayer in the future.")]
    private static string? FetchGldById(int modGldVersion)
    {
        try
        {
            if (!client.CurrentGameId.HasValue)
            {
                Console.Write("Tried to perform and send command but no GameId was set");
                return;
            }
            if (!ClientActionManager.CanReceiveCommand(command, client.GameState))
            {
                Console.Write("Tried to send invalid command");
                return;
            }
            uint currentResetId = client.resets;
            int count = client.GameState.CommandStack.Count;
            var list = new Il2CppSystem.Collections.Generic.List<CommandBase>();
            list.Add(command);
            client.ActionManager.ExecuteCommands(list);
            await client.SendCommandToServer(command, count);

            var serializedGameState = SerializationHelpers.ToByteArray(client.GameState, client.GameState.Version);

            var succ = GameStateSummary.FromGameStateByteArray(serializedGameState,
                out GameStateSummary stateSummary, out var gameState);

            var serializedGameSummary = SerializationHelpers.ToByteArray(stateSummary, gameState.Version);


            client.GameState.TryGetPlayer(client.GameState.CurrentPlayer, out PlayerState playerState);
            var currentPlayerId = "";
            if(playerState.AccountId.HasValue)
            {
                currentPlayerId = playerState.AccountId.Value.ToString();
            }
            var setupGameDataViewModel = new ModdedGameStateViewModel
            {
                gameId = client.gameId.ToString(),
                serializedGameState = serializedGameState,
                serializedGameSummary = serializedGameSummary,
                gameSettingsJson = "",
                currentPlayerId = currentPlayerId,
                IsEndTurnCommand = command.GetCommandType() == CommandType.EndTurn
            };



            var setupData = System.Text.Json.JsonSerializer.Serialize(setupGameDataViewModel);

            var serverResponse = await PolytopiaBackendAdapter.Instance.HubConnection.InvokeAsync<ServerResponse<BoolResponseViewModel>>(
                "UpdateGameStateModded",
                setupData,
                Il2CppSystem.Threading.CancellationToken.None
            );
            tcs.SetResult(serverResponse);
        }
        catch (Exception ex)
        {
            Plugin.logger.LogError("Multiplayer> Error during HandleSendCommandModded: " + ex.Message);
            tcs.SetException(new Il2CppSystem.Exception(ex.Message));
        }
    }
}
