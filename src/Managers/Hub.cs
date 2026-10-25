using System.Text.Json;
using Cpp2IL.Core.Extensions;
using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime;
using UnityEngine;
using static PopupBase;
using PolytopiaBackendBase.Common;
using System.Text.Encodings.Web;
using Il2CppSystem.Linq;
using PolyMod.Json;


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
    internal static void HotReload()
    {
        NotificationManager.Notify("FAILED: wip");
        // string message = string.Empty;
        // Directory.CreateDirectory(Plugin.DUMPED_DATA_PATH);

        // foreach (var file in Directory.GetFiles(Plugin.DUMPED_DATA_PATH))
        // {
        //     string? name = Path.GetFileNameWithoutExtension(file);
        //     List<string> subnames = new();
        //     if (name.Contains("sprites_"))
        //     {
        //         subnames = name.Split('_').ToList();
        //         Mod.File spriteInfo = new(Path.GetFileNameWithoutExtension(file), File.ReadAllBytes(file));
        //         Dictionary<string, Visual.SpriteInfo>? deserialized = Loader.LoadSpriteInfoFile(Registry.mods[subnames[1]], spriteInfo);
        //         if (deserialized != null)
        //         {
        //             foreach (var kvp in deserialized)
        //             {
        //                 Loader.UpdateSprite(kvp.Key);
        //             }
        //             message += Localization.Get("polymod.hotreload.success", new Il2CppSystem.Object[] { subnames[1] });
        //         }
        //     }
        // }
        // if (message == string.Empty)
        // {
        //     message = Localization.Get("polymod.hotreload.failure");
        // }
        // NotificationManager.Notify(message);
    }

    /// <summary>
    /// Dump all data.
    /// </summary>
    internal static void DumpData()
    {
        Directory.CreateDirectory(Plugin.DUMPED_DATA_PATH);
        DumpLogic();
        DumpLocalization();
        DumpSprites();
        DumpTribePreviewLegacy();
        DumpPrefabs();
        NotificationManager.Notify(Localization.Get("polymod.hub.dumped"));
    }

    internal static void DumpLogic()
    {
        Directory.CreateDirectory(Plugin.LOGIC_DUMP_PATH);
        for (int version = 1; version <= VersionManager.GameLogicDataVersion; version++)
        {
            File.WriteAllTextAsync(
                Path.Combine(Plugin.LOGIC_DUMP_PATH, $"GameLogicData{version}.json"),
                PolytopiaDataManager.provider.LoadGameLogicData(version)
            );
        }
        for (int version = 1; version <= VersionManager.AvatarDataVersion; version++)
        {
            File.WriteAllTextAsync(
                Path.Combine(Plugin.LOGIC_DUMP_PATH, $"AvatarData{version}.json"),
                PolytopiaDataManager.provider.LoadAvatarData(version)
            );
        }
    }

    internal static void DumpLocalization()
    {
        Directory.CreateDirectory(Plugin.LOCALIZATION_DUMP_PATH);

        // Languages dump
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
        // Full loc dump
        foreach (var category in source.GetCategories())
            File.WriteAllTextAsync(
                Path.Combine(Plugin.LOCALIZATION_DUMP_PATH, $"localization_{category}.csv"),
                source.Export_CSV(category)
            );
    }

    private static void DumpSprites()
    {
        Directory.CreateDirectory(Plugin.SPRITES_DUMP_PATH);
        SpriteAtlasManager spriteAtlasManager = GameManager.GetSpriteAtlasManager();

        for (int i = 0; i < AtlasData.raw.Count; i++)
        {
            if (spriteAtlasManager.loadedAtlases[i] == null) continue; // not loaded, skip

            string[] row = AtlasData.raw[i];
            string atlasName = row[0];
            string dir = Path.Combine(Plugin.SPRITES_DUMP_PATH, atlasName);
            Directory.CreateDirectory(dir);

            Dictionary<string, Visual.SpriteInfo> atlasSpriteInfo = new();
            for (int j = 1; j < row.Length; j++)
            {
                string spriteName = row[j];
                Sprite sprite = spriteAtlasManager.GetSprite(spriteName, atlasName);
                if (sprite == null) continue;

                (byte[] bytes, Visual.SpriteInfo spriteInfo) = Visual.SpriteToPng(sprite);
                File.WriteAllBytesAsync(Path.Combine(dir, spriteName + ".png"), bytes);
                atlasSpriteInfo.Add(spriteName, spriteInfo);
            }
            File.WriteAllTextAsync(
                Path.Combine(dir, $"sprites_{atlasName}.json"),
                JsonSerializer.Serialize(atlasSpriteInfo, new JsonSerializerOptions { WriteIndented = true, Converters = { new PolyMod.Json.Vector2Json() }})
            );
        }
    }

    internal static void DumpTribePreviewLegacy()
    {
        // TODO legacy tribe preview dump. i have to update that
        foreach (TribeType type in Enum.GetValues(typeof(TribeType)))
        {
            SaveStateData saveStateData = TribePreviewRegistry.GetTribePreviewData(type, SkinType.Default); // we should add to dump and create previews for skins

            List<TribePreviews.PreviewTile> previewTiles = new();

            SelectTribePopup popup = PopupManager.GetSelectTribePopup();
            if (DiskSerializationHelpers.FromLZ4CompressedByteArray<ClientSerializationWrapper>(
                saveStateData.byteArray, out ClientSerializationWrapper clientSerializationWrapper, out int version))
            {
                GameState currentGameState = clientSerializationWrapper.GetCurrentGameState();

                foreach (var tileData in currentGameState.Map.Tiles)
                {
                    TribePreviews.PreviewTile previewTile = new TribePreviews.PreviewTile
                    {
                        x = tileData.coordinates.x,
                        y = tileData.coordinates.y,
                        terrainType = tileData.terrain,
                        resourceType = tileData.resource != null ? tileData.resource.type : null,
                        unitType = tileData.unit != null ? tileData.unit.type : null,
                        improvementType = tileData.improvement != null ? tileData.improvement.type : null,
                        tileEffects = tileData.effects.ToArray().ToList()
                    };

                    previewTiles.Add(previewTile);
                }
            }
            File.WriteAllTextAsync(
                Path.Combine(Plugin.DUMPED_DATA_PATH, $"preview_{type}.json"),
                JsonSerializer.Serialize(previewTiles, new JsonSerializerOptions { WriteIndented = true })
            );
        }
    }

    internal static void DumpPrefabs()
    {
        Directory.CreateDirectory(Plugin.PREFABS_DUMP_PATH);
        PrefabsInfo prefabsInfo = new();
        foreach (KeyValuePair<SkinnedKey, Unit> keyValuePairs in Prefabs.unitsNew)
        {
            var unit = keyValuePairs.Value;
            SkinVisualsReference skinVisualsReference = unit.GetComponent<SkinVisualsReference>();
            if (skinVisualsReference == null || skinVisualsReference.visualParts == null)
            {
                Plugin.logger.LogWarning($"Dump skipped {keyValuePairs.Key.unitType}: no SkinVisualsReference");
                continue;
            }
            UnitPrefabInfo unitPrefabInfo = new()
            {
                headPositionMarker = unit.headPositionMarker.gameObject.name,
                skinnedKey = keyValuePairs.Key,
                visualParts = new()
            };
            foreach (var visualPart in skinVisualsReference.visualParts)
            {
                if (visualPart == null || visualPart.visualPart == null)
                {
                    continue;
                }

                var visualPartInfo = new VisualPartInfo { };
                visualPartInfo.CopyFromVisualPart(visualPart);
                unitPrefabInfo.visualParts.Add(visualPartInfo);

            }
            prefabsInfo.units.Add(unitPrefabInfo);
        }

        foreach (Il2CppSystem.Collections.Generic.KeyValuePair<Polytopia.Data.ImprovementData.Type, Building> keyValuePairs in PrefabManager.improvements)
        {
            var unit = keyValuePairs.Value;
            SkinVisualsReference skinVisualsReference = unit.GetComponent<SkinVisualsReference>();
            if (skinVisualsReference == null || skinVisualsReference.visualParts == null)
            {
                Plugin.logger.LogWarning($"Dump skipped {keyValuePairs.Key}: no SkinVisualsReference");
                continue;
            }
            ImprovementPrefabInfo improvementPrefabInfo = new()
            {
                improvementType = keyValuePairs.Key,
                visualParts = new()
            };
            foreach (var visualPart in skinVisualsReference.visualParts)
            {
                if (visualPart == null || visualPart.visualPart == null)
                {
                    continue;
                }

                var visualPartInfo = new VisualPartInfo { };
                visualPartInfo.CopyFromVisualPart(visualPart);
                improvementPrefabInfo.visualParts.Add(visualPartInfo);

            }
            prefabsInfo.improvements.Add(improvementPrefabInfo);
        }

        foreach (Il2CppSystem.Collections.Generic.KeyValuePair<Polytopia.Data.ResourceData.Type, Resource> keyValuePairs in PrefabManager.resources)
        {
            var unit = keyValuePairs.Value;
            SkinVisualsReference skinVisualsReference = unit.GetComponent<SkinVisualsReference>();
            if (skinVisualsReference == null || skinVisualsReference.visualParts == null)
            {
                Plugin.logger.LogWarning($"Dump skipped {keyValuePairs.Key}: no SkinVisualsReference");
                continue;
            }
            ResourcePrefabInfo resourcePrefabInfo = new()
            {
                resourceType = keyValuePairs.Key,
                visualParts = new()
            };
            foreach (var visualPart in skinVisualsReference.visualParts)
            {
                if (visualPart == null || visualPart.visualPart == null)
                {
                    continue;
                }

                var visualPartInfo = new VisualPartInfo { };
                visualPartInfo.CopyFromVisualPart(visualPart);
                resourcePrefabInfo.visualParts.Add(visualPartInfo);

            }
            prefabsInfo.resources.Add(resourcePrefabInfo);
        }

        File.WriteAllTextAsync(
            Path.Combine(Plugin.PREFABS_DUMP_PATH, $"prefabs.json"),
            JsonSerializer.Serialize(prefabsInfo, new JsonSerializerOptions { WriteIndented = true, Converters = { new Vector2Json() },})
        );
    }

    /// <summary>
    /// Shows the configuration popup.
    /// </summary>
    internal static void ShowConfigPopup()
    {
        BasicPopup polymodPopup = PopupManager.GetBasicPopup();
        polymodPopup.Header = Localization.Get("polymod.hub.config");
        polymodPopup.Description = "";
        polymodPopup.buttonData = CreateConfigPopupButtonData();
        polymodPopup.Show();
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
                "polymod.hub.hotreload",
                callback:  DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(HotReload),
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
            popupButtons.Add(new PopupButtonData(Localization.Get("polymod.hub.hotreload"), PopupButtonData.States.None, DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(OnUpdateSpritesButtonClicked), -1, true, null));
        }
        else
        {
            string debugButtonName = Localization.Get(
                Plugin.config.debug ? "polymod.hub.config.disable" : "polymod.hub.config.enable",
                new Il2CppSystem.Object[] { Localization.Get("polymod.debug",
                new Il2CppSystem.Object[]{}).ToUpperInvariant() }
            );
            popupButtons.Add(new PopupButtonData(debugButtonName, PopupButtonData.States.None, DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(OnDebugButtonClicked), -1, true, null));
        }
        return popupButtons.ToArray();

        void OnDebugButtonClicked()
        {
            Plugin.config = new(debug: !Plugin.config.debug);
            Plugin.WriteConfig();
            Plugin.UpdateConsole();
            NotificationManager.Notify(Localization.Get(
                "polymod.config.setto",
                new Il2CppSystem.Object[] { Localization.Get("polymod.debug",
                new Il2CppSystem.Object[]{}), Plugin.config.debug }
            ));
            isConfigPopupActive = false;
        }

        void OnUpdateSpritesButtonClicked()
        {
            HotReload();
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