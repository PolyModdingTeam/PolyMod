using Cpp2IL.Core.Extensions;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using static PopupBase;

namespace PolyMod.Managers;

internal static class Hub
{
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
	private const float HUB_POPUP_WIDTH_SHARE = 0.78f;
	private const float HUB_POPUP_MAX_WIDTH = 1400f;
	private const float SETTINGS_POPUP_WIDTH_SHARE = 0.5f;
	private const float SETTINGS_POPUP_MAX_WIDTH = 900f;
	private const float START_SCREEN_MARGIN = 50f;
	private const float VERSION_LABEL_FONT_SIZE = 18f;
	private const float VERSION_LABEL_GAP = 10f;
	/// <summary>Margin the game keeps between the safe area and its own corner widgets.</summary>
	private const float VERSION_LABEL_MARGIN = 15f;
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
				catch (Exception) {}
				openPopup = null;
			}
			return false;
		}
	}

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
	/// Patches the start screen layout to place the PolyMod hub button and the version label.
	/// </summary>
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
		polyModButton.SetPosition(
			screenSize.safeRect.Right - START_SCREEN_MARGIN * 2.5f,
			screenSize.safeRect.Top - START_SCREEN_MARGIN
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

	[HarmonyPostfix]
	[HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
	private static void GameManager_Update()
	{
		bool modifierHeld = Input.GetKey(HOTKEY_MODIFIER) || Input.GetKey(HOTKEY_MODIFIER_ALTERNATIVE);
		if (modifierHeld && Input.GetKeyDown(HOTKEY) && !IsPopupOpen)
			ShowPolyModHub();
	}

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

	private static void HotReload()
	{
		BasicPopup popup = PopupManager.GetBasicPopup();
		popup.Header = Localization.Get("polymod.hub.hotreload");
		popup.Description = Localization.Get("polymod.hotreload.unavailable");
		popup.SetDefaultOkbutton();
		popup.Show();
	}

	private static string BuildModList()
	{
		if (Registry.mods.Count == 0)
			return Muted(Localization.Get("polymod.hub.mods.none"));

		int loaded = 0;
		foreach (Mod mod in Registry.mods.Values)
		{
			if (mod.status == Mod.Status.Success) loaded++;
		}

		List<string> blocks = new()
		{
			Muted(Localization.Get(
				"polymod.hub.summary",
				Args(loaded.ToString(), Registry.mods.Count.ToString())
			)),
		};
		foreach (Mod mod in Registry.mods.Values)
		{
			blocks.Add(BuildModEntry(mod));
		}
		return string.Join("\n\n", blocks);
	}

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

		List<string> lines = new() { title, meta };
		if (!string.IsNullOrWhiteSpace(mod.description))
			lines.Add($"<size=95%>{mod.description}</size>");

		return string.Join("\n", lines);
	}

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

	private static string Muted(string text) => $"<color={COLOR_MUTED}>{text}</color>";

	private static string StatusColor(Mod.Status status) => status switch
	{
		Mod.Status.Success => COLOR_ENABLED,
		Mod.Status.DependenciesUnsatisfied => COLOR_WARNING,
		_ => COLOR_DISABLED,
	};

	private static int CountBrokenMods()
	{
		int broken = 0;
		foreach (Mod mod in Registry.mods.Values)
		{
			if (mod.status != Mod.Status.Success) broken++;
		}
		return broken;
	}

	private static Il2CppSystem.Action Callback(Action callback)
		=> DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(callback)!;

	private static Il2CppSystem.Object[] Args(params string[] values)
	{
		Il2CppSystem.Object[] arguments = new Il2CppSystem.Object[values.Length];
		for (int i = 0; i < values.Length; i++)
		{
			arguments[i] = values[i];
		}
		return arguments;
	}

	internal static void Init()
	{
		Harmony.CreateAndPatchAll(typeof(Hub));
	}
}
