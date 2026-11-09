using System.Globalization;
using Cpp2IL.Core.Extensions;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using static PopupBase;

namespace PolyMod.Managers;

/// <summary>
/// The PolyMod hub. It adds the hub button and the version label to the start screen.
/// It shows the hub popup with the list of mods, and the config popup with the PolyMod settings.
/// </summary>
internal static class Hub
{
    /// <summary>
    /// A PolyMod setting that the config popup shows and that the user can toggle.
    /// </summary>
    /// <param name="buttonKey">The localization key of the button that toggles the setting.</param>
    /// <param name="nameKey">The localization key of the setting name.</param>
    /// <param name="descriptionKey">The localization key of the setting description.</param>
    /// <param name="isEnabled">The function that gets the current state of the setting.</param>
    /// <param name="toggle">The action that changes the setting to the opposite state.</param>
    private record Setting(
        string buttonKey,
        string nameKey,
        string descriptionKey,
        Func<bool> isEnabled,
        Action toggle
    );

    private const string HEADER_PREFIX = "<align=\"center\"><size=150%><b>";
    private const string HEADER_POSTFIX = "</b></size><align=\"left\">";

    private const string COLOR_MUTED = "#9AA0A6";
    private const string COLOR_ENABLED = "#4CC76A";
    private const string COLOR_DISABLED = "#E36A5B";
    private const string COLOR_WARNING = "#E0A52E";
    private const int CHECKSUM_LENGTH = 12;
    private const float HUB_POPUP_WIDTH_SHARE = 0.78f;
    private const float HUB_POPUP_MAX_WIDTH = 1400f;
    private const float SETTINGS_POPUP_WIDTH_SHARE = 0.5f;
    private const float SETTINGS_POPUP_MAX_WIDTH = 900f;
    private const float START_SCREEN_MARGIN = 50f;
    private const float VERSION_LABEL_FONT_SIZE = 18f;
    private const float VERSION_LABEL_GAP = 10f;
    /// <summary>The distance between the edge of the safe area and the version label.</summary>
    private const float VERSION_LABEL_MARGIN = 15f;
    private const float ROUND_BUTTON_TITLE_GAP = 10f;
    private const string INTRO = "intro.mp4";

    private const KeyCode HOTKEY_MODIFIER = KeyCode.LeftControl;
    private const KeyCode HOTKEY_MODIFIER_ALTERNATIVE = KeyCode.RightControl;
    private const KeyCode HOTKEY = KeyCode.Tab;

    private static readonly Setting[] SETTINGS = new Setting[]
    {
        new(
            "polymod.hub.config.debug",
            "polymod.debug",
            "polymod.debug.description",
            () => Plugin.config.debug,
            () =>
            {
                Plugin.config = Plugin.config with { debug = !Plugin.config.debug };
                Plugin.UpdateConsole();
            }
        ),
        new(
            "polymod.hub.config.coordinates",
            "polymod.coordinates",
            "polymod.coordinates.description",
            () => Plugin.config.showCoordinates,
            () => Plugin.config = Plugin.config with { showCoordinates = !Plugin.config.showCoordinates }
        ),
    };

    private static UIRoundButton_UI2? polyModButton = null;
    private static NotificationBadge? brokenModsBadge = null;
    private static TextField_UI2? polyModVersion = null;

    private static PopupBase? openPopup = null;

    /// <summary>
    /// Gets a value that tells if the last popup that the hub opened is still on the screen.
    /// If that popup is closed or destroyed, the property removes the reference to it and gives false.
    /// </summary>
    public static bool IsPopupOpen
    {
        get
        {
            if (openPopup != null)
            {
                try
                {
                    if (openPopup.IsShowing()) return true;
                }
                catch (Exception) { }
                openPopup = null;
            }
            return false;
        }
    }

    /// <summary>
    /// Prefix patch for <c>SplashController.LoadAndPlayClip</c>. It replaces the splash video with the PolyMod intro video.
    /// The patch writes the embedded <c>intro.mp4</c> resource to the persistent data folder and plays that file.
    /// The write starts asynchronously and the patch does not wait for it to complete.
    /// </summary>
    /// <param name="__instance">The splash controller that plays the video.</param>
    /// <returns>Always false, so the original method does not run.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SplashController), nameof(SplashController.LoadAndPlayClip))]
    private static bool SplashController_LoadAndPlayClip(SplashController __instance)
    {
        string path = Path.Combine(Application.persistentDataPath, INTRO);
        File.WriteAllBytesAsync(path, Plugin.GetResource(INTRO).ReadBytes());
        __instance.lastPlayTime = Time.realtimeSinceStartup;
        __instance.videoPlayer.url = path;
        __instance.videoPlayer.Play();
        return false;
    }

    /// <summary>
    /// Postfix patch for <c>StartScreen_UI2.Init</c>. It makes the PolyMod widgets on the start screen.
    /// It adds the hub button with the PolyMod icon and a badge that shows the number of broken mods.
    /// It also adds the label with the PolyMod version.
    /// If the mods have a dependency cycle, it shows the dependency cycle popup.
    /// </summary>
    /// <param name="transform">The parent transform that the game gives to the start screen.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.Init))]
    private static void StartScreen_UI2_Init(RectTransform transform)
    {
        polyModButton = UILibrary.NewRoundButton(transform).SetStyle(UIButtonBase_UI2.ButtonStyle.Suggested);
        polyModButton.bg.sprite = Visual.BuildSprite(Plugin.GetResource("polymod_icon.png").ReadBytes());
        polyModButton.OnClickedSignal.Add(Callback(ShowPolyModHub));

        brokenModsBadge = UILibrary.NewNotificationBadge(polyModButton.rectTransform).SetValue(CountBrokenMods());
        polyModButton.SetOverlay(brokenModsBadge, UIButtonBase_UI2.OverlayPosition.TopRight);

        // NewHeaderTinyText is the prefab a round button builds its own title from, so the label
        // keeps the game's header font rather than the lighter body font NewText would give it.
        polyModVersion = UILibrary.NewHeaderTinyText(transform);
        polyModVersion.gameObject.name = "PolyModVersion";
        polyModVersion.textField.fontSize = VERSION_LABEL_FONT_SIZE;
        polyModVersion.textField.color = Color.white;
        polyModVersion.Text = $"PolyMod {Plugin.VERSION}";

        if (Main.dependencyCycle)
            ShowDependencyCyclePopup();
    }

    /// <summary>
    /// Postfix patch for <c>StartScreen_UI2.RunLayout</c>. It puts the hub button and the version label in position.
    /// The hub button goes near the top-right corner of the safe area.
    /// The badge on the button shows the current number of broken mods.
    /// The version label goes in the bottom-left corner of the safe area.
    /// If the label touches the settings button, the patch moves the label above that button.
    /// If the widgets do not exist, the patch writes a warning to the log and stops.
    /// </summary>
    /// <param name="__instance">The start screen.</param>
    /// <param name="screenSize">The screen size that the game uses for this layout.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.RunLayout))]
    private static void StartScreen_UI2_RunLayout(StartScreen_UI2 __instance, ScreenBase_UI2.ScreenSize screenSize)
    {
        if (polyModButton == null || polyModVersion == null)
        {
            Plugin.logger.LogWarning("PolyMod start screen widgets are missing, skipping their layout.");
            return;
        }

        polyModButton.iconContainer.gameObject.SetActive(false);
        polyModButton.outline.gameObject.SetActive(false);
        polyModButton.bg.color = Color.white;
        // Setting the text re-runs the button's own layout, which also repositions the badge.
        polyModButton.Text = Localization.Get("polymod.hub");
        float titleDrop = polyModButton.titleTextField == null
            ? 0f
            : (polyModButton.titleTextField.GetHeight() + ROUND_BUTTON_TITLE_GAP) / 2f;
        polyModButton.SetPosition(
            screenSize.safeRect.Right - START_SCREEN_MARGIN * 2.5f,
            screenSize.safeRect.Top - START_SCREEN_MARGIN - titleDrop
        );
        brokenModsBadge?.SetValue(CountBrokenMods());

        // Anchored by its edges, not its centre, so a longer version string cannot run off screen.
        float versionX = screenSize.safeRect.Left + VERSION_LABEL_MARGIN + polyModVersion.GetHalfWidth();
        float versionY = screenSize.safeRect.Bottom + VERSION_LABEL_MARGIN + polyModVersion.GetHalfHeight();
        polyModVersion.SetPosition(versionX, versionY);

        // The bottom row of buttons is centred and of fixed width, so a narrow window walks it into
        // the version label. The row is already laid out by now, so lift the label clear of it.
        UIRoundButton_UI2 bottomRowStart = __instance.settingsButton;
        if (bottomRowStart != null
            && polyModVersion.GetRight() + VERSION_LABEL_GAP > bottomRowStart.GetLeft())
        {
            polyModVersion.SetPosition(
                versionX,
                bottomRowStart.GetTop() + polyModVersion.GetHalfHeight() + VERSION_LABEL_GAP
            );
        }
    }

    /// <summary>
    /// Postfix patch for <c>GameManager.Update</c>. It opens the hub popup when the user pushes Ctrl+Tab.
    /// The patch does nothing if a popup that the hub opened is still on the screen.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
    private static void GameManager_Update()
    {
        bool modifierHeld = Input.GetKey(HOTKEY_MODIFIER) || Input.GetKey(HOTKEY_MODIFIER_ALTERNATIVE);
        if (modifierHeld && Input.GetKeyDown(HOTKEY) && !IsPopupOpen)
            ShowPolyModHub();
    }

    /// <summary>
    /// Shows the hub popup. The popup has a header text, the list of mods, and a footer text.
    /// It has the back, Discord, and config buttons.
    /// In debug mode, it also has the dump and hot reload buttons, which do not close the popup.
    /// </summary>
    internal static void ShowPolyModHub()
    {
        BasicPopupLegacy popup = Visual.GetBasicPopupLegacy();
        popup.Header = Localization.Get("polymod.hub");
        popup.Description = string.Join(
            "\n\n",
            Localization.Get("polymod.hub.header", Args(HEADER_PREFIX, HEADER_POSTFIX)),
            BuildModList(),
            Localization.Get("polymod.hub.footer", Args(HEADER_PREFIX, HEADER_POSTFIX))
        );

        void OpenDiscord() => NativeHelpers.OpenURL(Plugin.DISCORD_LINK, false);

        List<PopupButtonData> buttons = new()
        {
            new("buttons.back"),
            new("polymod.hub.discord", callback: Callback(OpenDiscord)),
            new("polymod.hub.config", callback: Callback(ShowConfigPopup)),
        };
        if (Plugin.config.debug)
        {
            buttons.Add(new("polymod.hub.dump", callback: Callback(DumpData), closesPopup: false));
            buttons.Add(new("polymod.hub.hotreload", callback: Callback(HotReload), closesPopup: false));
        }

        popup.buttonData = buttons.ToArray();
        openPopup = popup;
        popup.ShowSetWidth(HUB_POPUP_WIDTH_SHARE, HUB_POPUP_MAX_WIDTH);
    }

    /// <summary>
    /// Shows the config popup with the list of PolyMod settings and their states.
    /// Each setting has a button that toggles the setting, writes the config to disk, and refreshes the list.
    /// These buttons do not close the popup. The back button opens the hub popup again.
    /// </summary>
    internal static void ShowConfigPopup()
    {
        BasicPopupLegacy popup = Visual.GetBasicPopupLegacy();
        popup.Header = Localization.Get("polymod.hub.config");
        popup.Description = BuildSettingsList();

        List<PopupButtonData> buttons = new()
        {
            new("buttons.back", callback: Callback(ShowPolyModHub)),
        };
        foreach (Setting setting in SETTINGS)
        {
            void OnSettingClicked()
            {
                setting.toggle();
                Plugin.WriteConfig();
                popup.Description = BuildSettingsList();
            }
            buttons.Add(new(setting.buttonKey, callback: Callback(OnSettingClicked), closesPopup: false));
        }

        popup.buttonData = buttons.ToArray();
        openPopup = popup;
        popup.ShowSetWidth(SETTINGS_POPUP_WIDTH_SHARE, SETTINGS_POPUP_MAX_WIDTH);
    }

    /// <summary>
    /// Shows a popup that tells the user that the mods have a dependency cycle.
    /// The user cannot skip the popup. Its only button closes the game.
    /// </summary>
    private static void ShowDependencyCyclePopup()
    {
        BasicPopup popup = PopupManager.GetBasicPopupWithData(
            new(
                Localization.Get("polymod.cycle"),
                Localization.Get("polymod.cycle.description"),
                new PopupButtonData[]
                {
                    new(
                        "buttons.exitgame",
                        PopupButtonData.States.None,
                        (Il2CppSystem.Action)Application.Quit,
                        closesPopup: false,
                        customColorStates: ColorConstants.redButtonColorStates
                    ),
                }
            )
        );
        popup.IsUnskippable = true;
        popup.Show();
    }

    /// <summary>
    /// Dumps the game data to disk and shows a popup with the path of the dump folder.
    /// The popup has a button that opens the dump folder. If the folder does not open, the method writes an error to the log.
    /// </summary>
    private static void DumpData()
    {
        Dumper.DumpAll();
        string path = Dumper.RootPath;

        BasicPopup popup = PopupManager.GetBasicPopup();
        popup.Header = Localization.Get("polymod.hub.dump");
        popup.Description = Localization.Get("polymod.hub.dumped", Args(path));

        void OpenDumpFolder()
        {
            try
            {
                Application.OpenURL(new Uri(path).AbsoluteUri);
            }
            catch (Exception exception)
            {
                Plugin.logger.LogError($"Could not open the dump folder at {path}: {exception}");
            }
        }

        popup.buttonData = new PopupButtonData[]
        {
            new("polymod.hub.openfolder", callback: Callback(OpenDumpFolder)),
            new("buttons.ok"),
        };
        popup.Show();
    }

    /// <summary>
    /// Shows a popup that tells the user that hot reload is not available.
    /// This method is a stub. It does not reload mods.
    /// </summary>
    private static void HotReload()
    {
        BasicPopup popup = PopupManager.GetBasicPopup();
        popup.Header = Localization.Get("polymod.hub.hotreload");
        popup.Description = Localization.Get("polymod.hotreload.unavailable");
        popup.SetDefaultOkbutton();
        popup.Show();
    }

    /// <summary>
    /// Makes the rich text list of mods for the hub popup.
    /// The list starts with a summary line that shows the number of loaded mods, the total number of mods, and the short global checksum.
    /// An entry for each mod follows the summary line.
    /// </summary>
    /// <returns>The rich text list of mods, or a muted text that tells the user that there are no mods.</returns>
    private static string BuildModList()
    {
        if (Registry.mods.Count == 0)
            return Muted(Localization.Get("polymod.hub.mods.none"));

        int loaded = 0;
        foreach (Mod mod in Registry.mods.Values)
        {
            if (mod.status == Mod.Status.Success) loaded++;
        }

        string summary = Localization.Get(
            "polymod.hub.summary",
            Args(loaded.ToString(CultureInfo.InvariantCulture), Registry.mods.Count.ToString(CultureInfo.InvariantCulture))
        );
        summary += "   " + Localization.Get(
            "polymod.hub.checksum",
            Args(ShortChecksum(Compatibility.checksum))
        );

        List<string> blocks = new() { Muted(summary) };
        foreach (Mod mod in Registry.mods.Values)
        {
            blocks.Add(BuildModEntry(mod));
        }
        return string.Join("\n\n", blocks);
    }

    /// <summary>
    /// Gets the first 12 characters of a checksum, which the hub shows.
    /// </summary>
    /// <param name="checksum">The full checksum.</param>
    /// <returns>The first 12 characters of the checksum, the full checksum if it is shorter, or "-" if the checksum is null or empty.</returns>
    private static string ShortChecksum(string checksum)
    {
        if (string.IsNullOrEmpty(checksum)) return "-";

        return checksum.Length <= CHECKSUM_LENGTH ? checksum : checksum.Substring(0, CHECKSUM_LENGTH);
    }

    /// <summary>
    /// Makes the rich text entry of one mod for the hub popup.
    /// The entry shows the name, the version, and the status in its status color.
    /// It also shows the authors, the short checksum, and the description when the mod has them.
    /// </summary>
    /// <param name="mod">The mod to show.</param>
    /// <returns>The rich text entry of the mod.</returns>
    private static string BuildModEntry(Mod mod)
    {
        string status = Localization.Get(
            "polymod.hub.mod.status." + Enum.GetName(mod.status)!.ToLowerInvariant()
        );
        string title = $"<size=120%><b>{mod.name}</b></size>  " + Muted($"<size=90%>v{mod.version}</size>");
        string meta = $"<color={StatusColor(mod.status)}><size=90%><b>{status}</b></size></color>";
        if (mod.authors.Length > 0)
        {
            string authors = Localization.Get(
                "polymod.hub.mod.authors",
                Args(string.Join(", ", mod.authors))
            );
            meta += "   " + Muted($"<size=90%>{authors}</size>");
        }
        if (!string.IsNullOrEmpty(mod.checksum))
        {
            string modChecksum = Localization.Get(
                "polymod.hub.mod.checksum",
                Args(ShortChecksum(mod.checksum))
            );
            meta += "   " + Muted($"<size=90%>{modChecksum}</size>");
        }

        List<string> lines = new() { title, meta };
        if (!string.IsNullOrWhiteSpace(mod.description))
            lines.Add($"<size=95%>{mod.description}</size>");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Makes the rich text list of settings for the config popup.
    /// Each entry shows the setting name, its state in green or red, and a muted description.
    /// </summary>
    /// <returns>The rich text list of settings.</returns>
    private static string BuildSettingsList()
    {
        List<string> blocks = new();
        foreach (Setting setting in SETTINGS)
        {
            bool enabled = setting.isEnabled();
            string state = Localization.Get(enabled ? "polymod.on" : "polymod.off");
            string color = enabled ? COLOR_ENABLED : COLOR_DISABLED;
            blocks.Add(
                $"<b>{Localization.Get(setting.nameKey)}</b>   <color={color}><b>{state}</b></color>\n"
                    + Muted($"<size=85%>{Localization.Get(setting.descriptionKey)}</size>")
            );
        }
        return string.Join("\n\n", blocks);
    }

    /// <summary>
    /// Puts a text in a rich text color tag with the muted gray color.
    /// </summary>
    /// <param name="text">The text to change.</param>
    /// <returns>The text in the muted color.</returns>
    private static string Muted(string text) => $"<color={COLOR_MUTED}>{text}</color>";

    /// <summary>
    /// Gets the color that the hub uses to show a mod status.
    /// </summary>
    /// <param name="status">The status of the mod.</param>
    /// <returns>Green for <see cref="Mod.Status.Success"/>, amber for <see cref="Mod.Status.DependenciesUnsatisfied"/>, and red for all other values.</returns>
    private static string StatusColor(Mod.Status status) => status switch
    {
        Mod.Status.Success => COLOR_ENABLED,
        Mod.Status.DependenciesUnsatisfied => COLOR_WARNING,
        _ => COLOR_DISABLED,
    };

    /// <summary>
    /// Counts the mods that do not have the <see cref="Mod.Status.Success"/> status.
    /// </summary>
    /// <returns>The number of broken mods.</returns>
    private static int CountBrokenMods()
    {
        int broken = 0;
        foreach (Mod mod in Registry.mods.Values)
        {
            if (mod.status != Mod.Status.Success) broken++;
        }
        return broken;
    }

    /// <summary>
    /// Changes a managed action into an IL2CPP action that popup buttons can use.
    /// </summary>
    /// <param name="callback">The managed action.</param>
    /// <returns>The IL2CPP action that calls <paramref name="callback"/>.</returns>
    private static Il2CppSystem.Action Callback(Action callback)
        => DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(callback)!;

    /// <summary>
    /// Puts strings into an IL2CPP object array for the format arguments of <c>Localization.Get</c>.
    /// </summary>
    /// <param name="values">The strings to put into the array.</param>
    /// <returns>The IL2CPP object array with the strings in the same order.</returns>
    private static Il2CppSystem.Object[] Args(params string[] values)
    {
        Il2CppSystem.Object[] arguments = new Il2CppSystem.Object[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            arguments[i] = values[i];
        }
        return arguments;
    }

    /// <summary>
    /// Applies all Harmony patches in the <see cref="Hub"/> class.
    /// </summary>
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Hub));
    }
}
