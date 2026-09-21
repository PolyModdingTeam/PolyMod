using System.Text.Json;
using Cpp2IL.Core.Extensions;
using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static PopupBase;
using PolytopiaBackendBase.Common;
using System.Text.Encodings.Web;
using Il2CppSystem.Linq;
using System.Text.RegularExpressions;


namespace PolyMod.Managers;

/// <summary>
/// Manages the PolyMod hub, including UI elements and popups.
/// </summary>
internal static class Hub
{
    private const string HEADER_PREFIX = "<align=\"center\"><size=150%><b>";
    private const string HEADER_POSTFIX = "</b></size><align=\"left\">";
    private const int POPUP_WIDTH = 1400;
    private static UIRoundButton_UI2? polyModButton = null;
    private static UIRoundButton_UI2? polyModVersion = null;

    /// <summary>
    /// Whether the configuration popup is currently active.
    /// </summary>
    public static bool isConfigPopupActive = false;

    /// <summary>
    /// Patches the splash screen to play a custom intro video.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SplashController), nameof(SplashController.LoadAndPlayClip))]
    private static bool SplashController_LoadAndPlayClip(SplashController __instance)
    {
        string name = "intro.mp4";
        string path = Path.Combine(Application.persistentDataPath, name);
        File.WriteAllBytesAsync(path, Plugin.GetResource(name).ReadBytes());
        __instance.lastPlayTime = Time.realtimeSinceStartup;
        __instance.videoPlayer.url = path;
        __instance.videoPlayer.Play();
        return false;
    }

    /// <summary>
    /// Patches the popup button container to correctly anchor buttons.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PopupButtonContainer), nameof(PopupButtonContainer.SetButtonData))]
    private static void PopupButtonContainer_SetButtonData(PopupButtonContainer __instance)
    {
        int num = __instance.buttons.Length;
        for (int i = 0; i < num; i++)
        {
            UITextButton uitextButton = __instance.buttons[i];
            Vector2 vector = new((num == 1) ? 0.5f : (i / (num - 1.0f)), 0.5f);
            uitextButton.rectTransform.anchorMin = vector;
            uitextButton.rectTransform.anchorMax = vector;
            uitextButton.rectTransform.pivot = vector;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.Init))]
    private static void StartScreen_UI2_Init(StartScreen_UI2 __instance, RectTransform transform)
    {
        polyModButton = UILibrary.NewRoundButton(transform).SetStyle(UIButtonBase_UI2.ButtonStyle.Suggested);
        polyModButton.bg.sprite = Visual.BuildSprite(Plugin.GetResource("polymod_icon.png").ReadBytes());
        polyModButton.OnClickedSignal.Add(DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(ShowPolyModHub));

        polyModVersion = UILibrary.NewRoundButton(transform).SetStyle(UIButtonBase_UI2.ButtonStyle.Suggested);
        polyModVersion.Text = $"PolyMod {Plugin.VERSION}";
        polyModVersion.titleTextField.textField.fontSize = 18f;

        if (Main.dependencyCycle)
        {
            BasicPopup popup = PopupManager.GetBasicPopupWithData(
                new(
                    Localization.Get("polymod.cycle"),
                    Localization.Get("polymod.cycle.description"),
                    new PopupBase.PopupButtonData[] {
                        new(
                            "buttons.exitgame",
                            PopupBase.PopupButtonData.States.None,
                            (Il2CppSystem.Action)Application.Quit,
                            closesPopup: false,
                            customColorStates: ColorConstants.redButtonColorStates
                        )
                    }
                )
            );

            popup.IsUnskippable = true;
            popup.Show();
        }

    }

    /// <summary>
    /// Patches the start screen to add the PolyMod hub button and version text.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.RunLayout))]
    private static void StartScreen_UI2_RunLayout(StartScreen_UI2 __instance, ScreenBase_UI2.ScreenSize screenSize)
    {
        if(polyModButton == null)
        {
            Plugin.logger.LogWarning("PolyMod Hub button is null when running layout!");
            return;
        }
        polyModButton.iconContainer.gameObject.SetActive(false);
        polyModButton.outline.gameObject.SetActive(false);
        polyModButton.bg.color = Color.white;
        polyModButton.Text = Localization.Get("polymod.hub");
		float num = 50f;
		polyModButton.SetPosition(screenSize.safeRect.Right - (num * 2.5f), screenSize.safeRect.Top - num);

        if(polyModVersion == null)
        {
            Plugin.logger.LogWarning("PolyMod Version is null when running layout!");
            return;
        }
        polyModVersion.iconContainer.gameObject.SetActive(false);
        polyModVersion.outline.gameObject.SetActive(false);
        polyModVersion.bg.gameObject.SetActive(false);
        polyModVersion.SetPosition(screenSize.safeRect.Left + num * 1.15f, screenSize.safeRect.Bottom + num * 1.15f);
    }

    /// <summary>
    /// Patches the game manager to handle key presses for the config popup.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
    private static void GameManager_Update()
    {
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Tab) && !isConfigPopupActive)
        {
            ShowConfigPopup();
        }
    }

    /// <summary>
    /// Updates sprite information from dumped files.
    /// </summary>
    internal static void UpdateSpriteInfos()
    {
        string message = string.Empty;
        Directory.CreateDirectory(Plugin.DUMPED_DATA_PATH);

        foreach (var file in Directory.GetFiles(Plugin.DUMPED_DATA_PATH))
        {
            string? name = Path.GetFileNameWithoutExtension(file);
            List<string> subnames = new();
            if (name.Contains("sprites_"))
            {
                subnames = name.Split('_').ToList();
                Mod.File spriteInfo = new(Path.GetFileNameWithoutExtension(file), File.ReadAllBytes(file));
                Dictionary<string, Visual.SpriteInfo>? deserialized = Loader.LoadSpriteInfoFile(Registry.mods[subnames[1]], spriteInfo);
                if (deserialized != null)
                {
                    foreach (var kvp in deserialized)
                    {
                        Loader.UpdateSprite(kvp.Key);
                    }
                    message += Localization.Get("polymod.spriteinfo.updated", new Il2CppSystem.Object[] { subnames[1] });
                }
            }
        }
        if (message == string.Empty)
        {
            message = Localization.Get("polymod.spriteinfo.notupdated");
        }
        NotificationManager.Notify(message);
    }

    /// <summary>
    /// Dump all data.
    /// </summary>
    internal static void DumpData()
    {
        Directory.CreateDirectory(Plugin.DUMPED_DATA_PATH);
        Directory.CreateDirectory(Plugin.LOGIC_DUMP_PATH);
        Directory.CreateDirectory(Plugin.LOCALIZATION_DUMP_PATH);
        File.WriteAllTextAsync(
            Path.Combine(Plugin.LOGIC_DUMP_PATH, "gameLogicData.json"),
            PolytopiaDataManager.provider.LoadGameLogicData(VersionManager.GameLogicDataVersion)
        );
        File.WriteAllTextAsync(
            Path.Combine(Plugin.LOGIC_DUMP_PATH, "avatarData.json"),
            PolytopiaDataManager.provider.LoadAvatarData(1337)
        );
        var source = LocalizationManager.Sources[0];
        foreach (var language in source.GetLanguages())
        {
            int languageIndex = source.GetLanguageIndex(language);
            var dict = new Dictionary<string, string>();

            foreach (var term in source.mTerms)
            {
                var translation = term.GetTranslation(languageIndex);

                if (!string.IsNullOrEmpty(translation))
                    dict[term.Term] = translation;
            }
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string json = JsonSerializer.Serialize(dict, options);

            File.WriteAllText(
                Path.Combine(Plugin.LOCALIZATION_DUMP_PATH, $"language_{source.mLanguages[languageIndex].Code}.json"),
                json
            );
        }
        foreach (var category in source.GetCategories())
            File.WriteAllTextAsync(
                Path.Combine(Plugin.LOCALIZATION_DUMP_PATH, $"localization_{category}.csv"),
                source.Export_CSV(category)
            );
        foreach (KeyValuePair<string, Mod> entry in Registry.mods)
        {
            foreach (Mod.File file in entry.Value.files)
            {
                Match spritesMatch = Regex.Match(Path.GetFileName(file.name), @"^sprites(?:_(.*))?\.json$");
                if (spritesMatch.Success)
                    File.WriteAllBytes(
                        Path.Combine(
                                Plugin.DUMPED_DATA_PATH,
                                $"{Path.GetFileNameWithoutExtension(file.name)}_{entry.Key}.json"
                        ),
                        file.bytes
                    );
            }
        }
        foreach (TribeType type in Enum.GetValues(typeof(TribeType)))
        {
            List<Visual.PreviewTile> previewTiles = new();
            SelectTribePopup popup = PopupManager.GetSelectTribePopup();
            for (int x = -3; x <= 3; x++)
            {
                for (int y = -7; y <= 7; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (popup.UIWorldPreview.worldPreviewData.TryGetData(pos, type, out UITileData tileData))
                    {
                        Visual.PreviewTile previewTile = new Visual.PreviewTile
                        {
                            x = tileData.Position.x,
                            y = tileData.Position.y,
                            terrainType = tileData.terrainType,
                            resourceType = tileData.resourceType,
                            unitType = tileData.unitType,
                            improvementType = tileData.improvementType
                        };
                        previewTiles.Add(previewTile);
                    }
                }
            }
            File.WriteAllTextAsync(
                Path.Combine(Plugin.DUMPED_DATA_PATH, $"preview_{type}.json"),
                JsonSerializer.Serialize(previewTiles, new JsonSerializerOptions { WriteIndented = true })
            );
        }
        NotificationManager.Notify(Localization.Get("polymod.hub.dumped"));
    }

    /// <summary>
    /// Shows the configuration popup.
    /// </summary>
    internal static void ShowConfigPopup()
    {
        BasicPopupLegacy polymodPopup = Visual.GetBasicPopupLegacy();
        polymodPopup.Header = Localization.Get("polymod.hub.config");
        polymodPopup.Description = "";
        polymodPopup.buttonData = CreateConfigPopupButtonData();
        polymodPopup.ShowSetWidth(POPUP_WIDTH);
    }

    internal static void ShowPolyModHub()
    {
        BasicPopupLegacy popup = Visual.GetBasicPopupLegacy();
        popup.Header = Localization.Get("polymod.hub");
        popup.Description = Localization.Get("polymod.hub.header", new Il2CppSystem.Object[] {
            HEADER_PREFIX,
            HEADER_POSTFIX
        }) + "\n\n";
        foreach (var mod in Registry.mods.Values)
        {
            popup.Description += Localization.Get("polymod.hub.mod", new Il2CppSystem.Object[] {
                mod.name,
                Localization.Get("polymod.hub.mod.status."
                    + Enum.GetName(typeof(Mod.Status), mod.status)!.ToLower()),
                string.Join(", ", mod.authors),
                mod.version.ToString(),
                mod.description ?? ""
            });
            popup.Description += "\n\n";
        }
        popup.Description += Localization.Get("polymod.hub.footer", new Il2CppSystem.Object[] {
            HEADER_PREFIX,
            HEADER_POSTFIX
        });

        void OpenDiscord()
        {
            NativeHelpers.OpenURL(Plugin.DISCORD_LINK, false);
        }

        List<PopupBase.PopupButtonData> popupButtons = new()
        {
            new("buttons.back"),
            new(
                "polymod.hub.discord",
                callback: DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(OpenDiscord)
            ),
            new(
                "polymod.hub.config",
                callback: DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(ShowConfigPopup)
            )
        };
        if (Plugin.config.debug)
        {
            popupButtons.Add(new(
                "polymod.hub.dump",
                callback: DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(DumpData),
                closesPopup: false
            ));
            popupButtons.Add(new(
                "polymod.hub.spriteinfo.update",
                callback:  DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(UpdateSpriteInfos),
                closesPopup: false
            ));
        }
        popup.buttonData = popupButtons.ToArray();
        popup.ShowSetWidth(POPUP_WIDTH);
    }

    /// <summary>
    /// Creates the button data for the configuration popup.
    /// </summary>
    /// <returns>An array of popup button data.</returns>
    internal static PopupButtonData[] CreateConfigPopupButtonData()
    {
        List<PopupButtonData> popupButtons = new()
        {
            new(Localization.Get("buttons.back"), PopupButtonData.States.None, DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(OnBackButtonClicked), -1, true, null)
        };

        if (GameManager.Instance.isLevelLoaded)
        {
            popupButtons.Add(new PopupButtonData(Localization.Get("polymod.hub.spriteinfo.update"), PopupButtonData.States.None, DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(OnUpdateSpritesButtonClicked), -1, true, null));
        }
        else
        {
            string debugButtonName = Localization.Get(
                Plugin.config.debug ? "polymod.hub.config.disable" : "polymod.hub.config.enable",
                new Il2CppSystem.Object[] { Localization.Get("polymod.debug",
                new Il2CppSystem.Object[]{}).ToUpperInvariant() }
            );
            string autoUpdateButtonName = Localization.Get(
                Plugin.config.autoUpdate ? "polymod.hub.config.disable" : "polymod.hub.config.enable",
                new Il2CppSystem.Object[] { Localization.Get("polymod.autoupdate",
                new Il2CppSystem.Object[]{}).ToUpperInvariant() }
            );
            string includeAlphasButtonName = Localization.Get(
                Plugin.config.updatePrerelease ? "polymod.hub.config.disable" : "polymod.hub.config.enable",
                new Il2CppSystem.Object[] { Localization.Get("polymod.autoupdate.alpha",
                new Il2CppSystem.Object[]{}).ToUpperInvariant() }
            );
            popupButtons.Add(new PopupButtonData(debugButtonName, PopupButtonData.States.None, DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(OnDebugButtonClicked), -1, true, null));
            popupButtons.Add(new PopupButtonData(autoUpdateButtonName, PopupButtonData.States.None, DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(OnAutoUpdateButtonClicked), -1, true, null));
            popupButtons.Add(new PopupButtonData(includeAlphasButtonName, Plugin.config.autoUpdate ? PopupButtonData.States.None : PopupButtonData.States.Disabled, DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(OnIncludeAlphasButtonClicked), -1, true, null));
        }
        return popupButtons.ToArray();

        void OnDebugButtonClicked()
        {
            Plugin.config = new(debug: !Plugin.config.debug, autoUpdate: Plugin.config.autoUpdate, updatePrerelease: Plugin.config.updatePrerelease);
            Plugin.WriteConfig();
            Plugin.UpdateConsole();
            NotificationManager.Notify(Localization.Get(
                "polymod.config.setto",
                new Il2CppSystem.Object[] { Localization.Get("polymod.debug",
                new Il2CppSystem.Object[]{}), Plugin.config.debug }
            ));
            isConfigPopupActive = false;
        }

        void OnAutoUpdateButtonClicked()
        {
            Plugin.config = new(debug: Plugin.config.debug, autoUpdate: !Plugin.config.autoUpdate, updatePrerelease: Plugin.config.updatePrerelease);
            Plugin.WriteConfig();
            Plugin.UpdateConsole();
            NotificationManager.Notify(Localization.Get(
                "polymod.config.setto",
                new Il2CppSystem.Object[] { Localization.Get("polymod.autoupdate",
                new Il2CppSystem.Object[]{}), Plugin.config.autoUpdate }
            ));
            isConfigPopupActive = false;
        }

        void OnIncludeAlphasButtonClicked()
        {
            Plugin.config = new(debug: Plugin.config.debug, autoUpdate: Plugin.config.autoUpdate, updatePrerelease: !Plugin.config.updatePrerelease);
            Plugin.WriteConfig();
            Plugin.UpdateConsole();
            NotificationManager.Notify(Localization.Get(
                "polymod.config.setto",
                new Il2CppSystem.Object[] { Localization.Get("polymod.autoupdate.alpha",
                new Il2CppSystem.Object[]{}), Plugin.config.updatePrerelease }
            ));
            isConfigPopupActive = false;
        }

        void OnUpdateSpritesButtonClicked()
        {
            UpdateSpriteInfos();
            isConfigPopupActive = false;
        }

        void OnBackButtonClicked()
        {
            isConfigPopupActive = false;
        }
    }

    /// <summary>
    /// Initializes the Hub manager by patching the necessary methods.
    /// </summary>
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Hub));
    }
}
