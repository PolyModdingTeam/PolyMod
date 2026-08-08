using Cpp2IL.Core.Extensions;
using HarmonyLib;
using Il2CppInterop.Runtime;
using PolyMod.Managers;
using PolytopiaBackendBase;
using TMPro;
using UnityEngine;

namespace PolyMod.Multiplayer;

/// <summary>
/// Adds a "Dystopia" start-screen button with a popup to switch the backend server at runtime.
/// </summary>
public static class Dystopia
{
    private const int POPUP_WIDTH = 1400;

    internal const string OFFICIAL_SERVER_URL = "https://polytopia-prod.net/";

    private record ServerEntry(string name, string url, bool official = false);

    private static readonly ServerEntry[] SERVERS =
    {
        new("Official", OFFICIAL_SERVER_URL, official: true),
        new("Dystopia", "https://polydystopia.xyz"),
        new("Dystopia Dev", "https://dev.polydystopia.xyz"),
    };

    /// <summary>
    /// Default backend: the official server, except on Android
    /// </summary>
    internal static string DefaultServerUrl()
    {
        return Application.platform == RuntimePlatform.Android
            ? Client.DEFAULT_SERVER_URL
            : OFFICIAL_SERVER_URL;
    }

    private static UIRoundButton_UI2? dystopiaButton = null;

    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Dystopia));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.Init))]
    private static void StartScreen_UI2_Init(StartScreen_UI2 __instance, RectTransform transform)
    {
        if (dystopiaButton != null)
        {
            UnityEngine.Object.Destroy(dystopiaButton.gameObject);
        }
        dystopiaButton = UILibrary.NewRoundButton(transform).SetStyle(UIButtonBase_UI2.ButtonStyle.Suggested);
        dystopiaButton.bg.sprite = Visual.BuildSprite(Plugin.GetResource("dystopia_icon.png").ReadBytes());
        dystopiaButton.OnClickedSignal.Add(DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(ShowDystopiaPopup));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.RunLayout))]
    private static void StartScreen_UI2_RunLayout(StartScreen_UI2 __instance, ScreenBase_UI2.ScreenSize screenSize)
    {
        if (dystopiaButton == null)
        {
            Plugin.logger.LogWarning("Dystopia button is null when running layout!");
            return;
        }
        dystopiaButton.iconContainer.gameObject.SetActive(false);
        dystopiaButton.outline.gameObject.SetActive(false);
        dystopiaButton.bg.color = Color.white;
        dystopiaButton.Text = "Dystopia";
        float num = 50f;

        dystopiaButton.SetPosition(screenSize.safeRect.Right - (num * 4.0f), screenSize.safeRect.Top - num);
    }

    internal static void ShowDystopiaPopup()
    {
        string current = Normalize(Plugin.config.backendUrl);

        BasicPopupLegacy popup = Visual.GetBasicPopupLegacy();
        popup.Header = "Dystopia";
        popup.Description = $"Current server:\n{Plugin.config.backendUrl}";

        List<PopupBase.PopupButtonData> buttons = new()
        {
            new("buttons.back"),
        };
        foreach (ServerEntry server in SERVERS)
        {
            bool isActive = Normalize(server.url) == current;
            // The official server rejects our DeviceId login, so it stays greyed out on Android.
            bool isDisabled = isActive || (server.official && Application.platform == RuntimePlatform.Android);
            string url = server.url;
            buttons.Add(new(
                isActive ? server.name + " (active)" : server.name,
                isDisabled ? PopupBase.PopupButtonData.States.Disabled : PopupBase.PopupButtonData.States.None,
                DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => SwitchServer(url)))
            ));
        }
        buttons.Add(new(
            "Custom...",
            callback: DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(ShowCustomServerPopup)
        ));
        popup.buttonData = buttons.ToArray();
        popup.ShowSetWidth(POPUP_WIDTH);
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
