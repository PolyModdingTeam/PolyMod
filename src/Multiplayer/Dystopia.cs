using HarmonyLib;
using Il2CppInterop.Runtime;
using PolyMod.Managers;
using PolytopiaBackendBase;
using TMPro;

namespace PolyMod.Multiplayer;

/// <summary>
/// Integrates the Dystopia backend switcher natively into the multiplayer tab as a section at the bottom of the game list
/// </summary>
public static class Dystopia
{
    internal const string OFFICIAL_SERVER_URL = "https://polytopia-prod.net/";

    private record ServerEntry(string name, string url, bool official = false, bool disabled = false);

    private static readonly ServerEntry[] SERVERS =
    {
        new("Official", OFFICIAL_SERVER_URL, official: true),
        // Disabled until the production Dystopia server goes online.
        new("Dystopia", "https://polydystopia.xyz", disabled: true),
        new("Dystopia Dev", "https://dev.polydystopia.xyz"),
    };

    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Dystopia));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MultiplayerScreen), nameof(MultiplayerScreen.AddHotSeatGames))]
    private static void MultiplayerScreen_AddHotSeatGames(MultiplayerScreen __instance)
    {
        try
        {
            AddServerSection(__instance);
        }
        catch (System.Exception e)
        {
            Plugin.logger.LogWarning($"Dystopia> Failed to add server section: {e}");
        }
    }

    private static void AddServerSection(MultiplayerScreen screen)
    {
        string current = Normalize(Plugin.config.backendUrl);

        screen.AddHeader("polymod.dystopia", useExtraSpacer: true);

        string connectionState = Localization.Get(PolytopiaBackendAdapter.Instance.IsConnected
            ? "polymod.dystopia.connected"
            : "polymod.dystopia.disconnected");

        MultiplayerInfoRow infoRow = screen.AddInfoRow();
        infoRow.header.text = $"{Localization.Get("polymod.dystopia.server")}: {CurrentServerName()} ({connectionState})";
        infoRow.description.text = Compatibility.IsClientOnly()
            ? Localization.Get("polymod.dystopia.vanilla.description")
            : string.Format(Localization.Get("polymod.dystopia.modded.description"), ShortChecksum());

        foreach (ServerEntry server in SERVERS)
        {
            // The active server is named in the info row; the official backend stays blocked
            // (the info row explains why).
            if (server.official || server.disabled || Normalize(server.url) == current) continue;

            string url = server.url;
            AddServerButton(screen,
                string.Format(Localization.Get("polymod.dystopia.connect"), server.name),
                () => SwitchServer(url));
        }

        AddServerButton(screen, Localization.Get("polymod.dystopia.custom"), ShowCustomServerPopup);
    }

    private static void AddServerButton(MultiplayerScreen screen, string text, System.Action action)
    {
        // The private no-arg AddButtonRow returns the actual row (the public string overload
        // returns the prefab by mistake) and clears callbacks on reused rows.
        ButtonRow row = screen.AddButtonRow();
        row.buttonComp.text = text;
        row.buttonComp.OnClickedSignal.Add(DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(action));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MultiplayerSelectionScreen), nameof(MultiplayerSelectionScreen.OnEnable))]
    private static void MultiplayerSelectionScreen_OnEnable(MultiplayerSelectionScreen __instance)
    {
        UpdateStatusHeader(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MultiplayerSelectionScreen), nameof(MultiplayerSelectionScreen.OnBackendConnectionChanged))]
    private static void MultiplayerSelectionScreen_OnBackendConnectionChanged(MultiplayerSelectionScreen __instance)
    {
        UpdateStatusHeader(__instance);
    }

    private static void UpdateStatusHeader(MultiplayerSelectionScreen screen)
    {
        try
        {
            UIHorizontalList list = screen.ScreenSelectionList;
            if (list == null) return;

            if (string.IsNullOrEmpty(list.HeaderKey))
            {
                // Activates and sizes the header slot; the text itself is overridden below.
                list.HeaderKey = "polymod.dystopia";
            }

            string moddedState = Compatibility.IsClientOnly()
                ? Localization.Get("polymod.dystopia.vanilla")
                : $"{Localization.Get("polymod.dystopia.modded")} {ShortChecksum()}";
            list.header.Text = $"{CurrentServerName()} · {moddedState}";
        }
        catch (System.Exception e)
        {
            Plugin.logger.LogWarning($"Dystopia> Failed to update status header: {e}");
        }
    }

    private static string CurrentServerName()
    {
        string current = Normalize(Plugin.config.backendUrl);
        foreach (ServerEntry server in SERVERS)
        {
            if (Normalize(server.url) == current) return server.name;
        }

        return Plugin.config.backendUrl;
    }

    private static string ShortChecksum()
    {
        return Compatibility.checksum.Length >= 8 ? Compatibility.checksum[..8] : Compatibility.checksum;
    }

    private static void ShowCustomServerPopup()
    {
        SearchFriendCodePopup popup = PopupManager.GetPopup<SearchFriendCodePopup>("dystopiaCustomServerPopup");
        TMP_InputField input = popup.inputfield;

        input.onSubmit = new TMP_InputField.SubmitEvent();
        input.onEndEdit = new TMP_InputField.SubmitEvent();
        input.onValueChanged = new TMP_InputField.OnChangeEvent();
        input.onSelect = new TMP_InputField.SelectionEvent();
        input.onDeselect = new TMP_InputField.SelectionEvent();
        input.contentType = TMP_InputField.ContentType.Standard;
        input.characterLimit = 200;

        var placeholder = input.placeholder != null ? input.placeholder.TryCast<TMP_Text>() : null;
        if (placeholder != null)
        {
            placeholder.text = "Server URL or IP";
        }

        void OnConnect()
        {
            string url = input.text?.Trim() ?? "";
            if (url.Length == 0)
            {
                return;
            }
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }
            if (!System.Uri.TryCreate(url, System.UriKind.Absolute, out _))
            {
                NotificationManager.Notify("Invalid server URL");
                return;
            }
            popup.Hide();
            SwitchServer(url);
        }

        popup.Show();
        popup.Header = "Custom server";
        popup.Description = "Enter the server URL or IP:";
        popup.buttonContainer.ResetContainer();
        popup.buttonData = new PopupBase.PopupButtonData[]
        {
            new("buttons.back"),
            new(
                "Connect",
                callback: DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(OnConnect),
                closesPopup: false
            ),
        };
        input.SetTextWithoutNotify(Plugin.config.backendUrl);
    }

    private static async void SwitchServer(string url)
    {
        Plugin.logger.LogInfo($"Dystopia> Switching server to {url}");
        Plugin.config = Plugin.config with { backendUrl = url };
        Plugin.WriteConfig();

        PolytopiaBackendAdapter adapter = PolytopiaBackendAdapter.Instance;

        await adapter.CloseConnection();

        BuildConfig buildConfig = BuildConfigHelper.GetSelectedBuildConfig();
        buildConfig.buildServerURL = BuildServerURL.Custom;
        buildConfig.customServerURL = url;

        adapter.UseBackendUri(new Il2CppSystem.Uri(url));
        adapter.UseHttpClient<BackendHttpClient>();

        PurgeServerCaches();

        adapter.ConnectionStatus = ConnectionStatus.None;
        BackendEvents.BackendConnectionChanged(ConnectionStatus.Disconnected, false);

        GameManager.GetLoginManager().Login(false, true);
        Plugin.logger.LogInfo($"Dystopia> Reconnect to {url} initiated");
    }

    private static void PurgeServerCaches()
    {
        try
        {
            var remote = GameManager.GetRemoteGameDataManager();
            remote.gameDataCache.Clear();
            remote.matchmakingGameDataCache?.Clear();
            remote.gameIdCache.Clear();
            remote.hasLoadedGameDataCache = false;

            var lobbies = GameManager.GetLobbyManager();
            lobbies.cachedLobbies.Clear();
            lobbies.hasCachedLobbies = false;

            AccountManager.currentPlayerData = null;
            AccountManager.friends = null;
            AccountManager.friendViewModels = null;
            AccountManager.playersStatuses?.Clear();
            AccountManager.ClearCachedPlayerId();

            DeleteIfExists(Paths.GetUserProfileCachePath());
            DeleteIfExists(Paths.GetStartupDataPath());

            GameManager.GetWeeklyChallengeModel().ClearData();
            GameManager.ActionableGamesCount = 0;
        }
        catch (System.Exception e)
        {
            Plugin.logger.LogWarning($"Dystopia> Failed to purge some server caches: {e}");
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string Normalize(string url)
    {
        return url.TrimEnd('/').ToLowerInvariant();
    }
}
