using System.Text.Json;
using Cpp2IL.Core.Extensions;
using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime;
using Polytopia.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static PopupBase;

namespace PolyMod.Patches;

internal static class Hub
{
    private const string HEADER_PREFIX = "<align=\"center\"><size=150%><b>";
    private const string HEADER_POSTFIX = "</b></size><align=\"left\">";
    private const int POPUP_WIDTH = 1400;
    public static bool isConfigPopupActive = false;

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

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartScreen), nameof(StartScreen.Start))]
    private static void StartScreen_Start()
    {
        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<UnityEngine.Object> allLocalizers = GameObject.FindObjectsOfTypeAll(Il2CppType.From(typeof(TMPLocalizer)));

        foreach (UnityEngine.Object item in allLocalizers)
        {
            TMPLocalizer? localizer = item.TryCast<TMPLocalizer>();
            if (localizer == null)
            {
                continue;
            }

            Transform? parent = localizer?.gameObject?.transform?.parent;
            if (parent == null)
            {
                continue;
            }

            string parentName = parent.name;

            if (parentName == "SettingsButton")
            {
                Transform? textTransform = parent.FindChild("DescriptionText");
                if (textTransform == null)
                {
                    return;
                }

                GameObject originalText = textTransform.gameObject;
                GameObject text = GameObject.Instantiate(originalText, originalText.transform.parent.parent.parent);
                text.name = "PolyModVersion";

                RectTransform rect = text.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(265, 40);
                rect.sizeDelta = new Vector2(500, rect.sizeDelta.y);
                rect.anchorMax = Vector2.zero;
                rect.anchorMin = Vector2.zero;

                TextMeshProUGUI textComponent = text.GetComponent<TextMeshProUGUI>();
                textComponent.fontSize = 18;
                textComponent.alignment = TextAlignmentOptions.BottomLeft;

                text.GetComponent<TMPLocalizer>().Text = $"PolyMod {Constants.POLYMOD_VERSION}";
                text.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            else if (parentName == "NewsButton")
            {
                GameObject originalButton = parent.gameObject;
                GameObject button = GameObject.Instantiate(originalButton, originalButton.transform.parent);
                button.name = "PolyModHubButton";
                button.transform.position = originalButton.transform.position - new Vector3(90, 0, 0);

                UIRoundButton buttonComponent = button.GetComponent<UIRoundButton>();
                buttonComponent.bg.sprite = Visual.BuildSprite(Plugin.GetResource("polymod_icon.png").ReadBytes());
                buttonComponent.bg.transform.localScale = new Vector3(1.2f, 1.2f, 0);
                buttonComponent.bg.color = Color.white;

                GameObject.Destroy(buttonComponent.icon.gameObject);
                GameObject.Destroy(buttonComponent.outline.gameObject);

                buttonComponent.OnClicked += (UIButtonBase.ButtonAction)PolyModHubButtonClicked;
            }
        }

        static void PolyModHubButtonClicked(int buttonId, BaseEventData eventData)
        {
            BasicPopup popup = PopupManager.GetBasicPopup();
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
            List<PopupBase.PopupButtonData> popupButtons = new()
            {
                new("buttons.back"),
                new(
                    "polymod.hub.discord",
                    callback: (UIButtonBase.ButtonAction)((_, _) =>
                        NativeHelpers.OpenURL(Constants.DISCORD_LINK, false))
                ),
                new(
                    "polymod.hub.config",
                    callback: (UIButtonBase.ButtonAction)((_, _) =>
                    {
                        ShowConfigPopup();
                    })
                )
            };
            if (Plugin.config.debug)
            {
                popupButtons.Add(new(
                    "polymod.hub.dump",
                    callback: (UIButtonBase.ButtonAction)((_, _) =>
                    {
                        Directory.CreateDirectory(Constants.DUMPED_DATA_PATH);
                        File.WriteAllTextAsync(
                            Path.Combine(Constants.DUMPED_DATA_PATH, "gameLogicData.json"),
                            PolytopiaDataManager.provider.LoadGameLogicData(VersionManager.GameLogicDataVersion)
                        );
                        File.WriteAllTextAsync(
                            Path.Combine(Constants.DUMPED_DATA_PATH, "avatarData.json"),
                            PolytopiaDataManager.provider.LoadAvatarData(1337)
                        );
                        foreach (var category in LocalizationManager.Sources[0].GetCategories())
                            File.WriteAllTextAsync(
                                Path.Combine(Constants.DUMPED_DATA_PATH, $"localization_{category}.csv"),
                                LocalizationManager.Sources[0].Export_CSV(category)
                            );
                        foreach (KeyValuePair<string, Mod> entry in Registry.mods)
                        {
                            foreach (Mod.File file in entry.Value.files)
                            {
                                if (Path.GetFileName(file.name) == "sprites.json")
                                {
                                    File.WriteAllBytes(Path.Combine(Constants.DUMPED_DATA_PATH, $"sprites_{entry.Key}.json"), file.bytes);
                                }
                            }
                        }
                        foreach (TribeData.Type type in Enum.GetValues(typeof(TribeData.Type)))
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
                                Path.Combine(Constants.DUMPED_DATA_PATH, $"preview_{type}.json"),
                                JsonSerializer.Serialize(previewTiles, new JsonSerializerOptions { WriteIndented = true })
                            );
                        }
                        NotificationManager.Notify(Localization.Get("polymod.hub.dumped"));
                    }),
                    closesPopup: false
                ));
                popupButtons.Add(new(
                    "polymod.hub.spriteinfo.update",
                    callback: (UIButtonBase.ButtonAction)((_, _) =>
                    {
                        UpdateSpriteInfos();
                    }),
                    closesPopup: false
                ));
            }
            popup.buttonData = popupButtons.ToArray();
            popup.ShowSetWidth(POPUP_WIDTH);
        }

        if (Main.dependencyCycle)
        {
            var popup = PopupManager.GetBasicPopup(new(
                Localization.Get("polymod.cycle"),
                Localization.Get("polymod.cycle.description"),
                new(new PopupBase.PopupButtonData[] {
                    new(
                        "buttons.exitgame",
                        PopupBase.PopupButtonData.States.None,
                        (Il2CppSystem.Action)Application.Quit,
                        closesPopup: false,
                        customColorStates: ColorConstants.redButtonColorStates
                    )
                })
            ));
            popup.IsUnskippable = true;
            popup.Show();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
    private static void GameManager_Update()
    {
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Tab) && !isConfigPopupActive)
        {
            ShowConfigPopup();
        }
    }

    internal static void UpdateSpriteInfos()
    {
        string message = string.Empty;
        Directory.CreateDirectory(Constants.DUMPED_DATA_PATH);

        foreach (var file in Directory.GetFiles(Constants.DUMPED_DATA_PATH))
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

    internal static void ShowConfigPopup()
    {
        BasicPopup polymodPopup = PopupManager.GetBasicPopup();

        polymodPopup.Header = Localization.Get("polymod.hub.config");
        polymodPopup.Description = "";

        polymodPopup.buttonData = CreateConfigPopupButtonData();
        polymodPopup.ShowSetWidth(POPUP_WIDTH);
        polymodPopup.Show();
    }

    internal static PopupButtonData[] CreateConfigPopupButtonData()
    {
        List<PopupButtonData> popupButtons = new()
        {
            new(Localization.Get("buttons.back"), PopupButtonData.States.None, (UIButtonBase.ButtonAction)OnBackButtonClicked, -1, true, null)
        };

        if (GameManager.Instance.isLevelLoaded)
        {
            popupButtons.Add(new PopupButtonData(Localization.Get("polymod.hub.spriteinfo.update"), PopupButtonData.States.None, (UIButtonBase.ButtonAction)OnUpdateSpritesButtonClicked, -1, true, null));
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
            popupButtons.Add(new PopupButtonData(debugButtonName, PopupButtonData.States.None, (UIButtonBase.ButtonAction)OnDebugButtonClicked, -1, true, null));
            popupButtons.Add(new PopupButtonData(autoUpdateButtonName, PopupButtonData.States.None, (UIButtonBase.ButtonAction)OnAutoUpdateButtonClicked, -1, true, null));
            popupButtons.Add(new PopupButtonData(includeAlphasButtonName, Plugin.config.autoUpdate ? PopupButtonData.States.None : PopupButtonData.States.Disabled, (UIButtonBase.ButtonAction)OnIncludeAlphasButtonClicked, -1, true, null));
        }
        return popupButtons.ToArray();

        void OnDebugButtonClicked(int buttonId, BaseEventData eventData)
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

        void OnAutoUpdateButtonClicked(int buttonId, BaseEventData eventData)
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

        void OnIncludeAlphasButtonClicked(int buttonId, BaseEventData eventData)
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

        void OnUpdateSpritesButtonClicked(int buttonId, BaseEventData eventData)
        {
            UpdateSpriteInfos();
            isConfigPopupActive = false;
        }

        void OnBackButtonClicked(int buttonId, BaseEventData eventData)
        {
            isConfigPopupActive = false;
        }
    }

    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Hub));
    }
}
