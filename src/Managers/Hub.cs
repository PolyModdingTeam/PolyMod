using Cpp2IL.Core.Extensions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PolyMod.Managers
{
    internal static class Hub
    {
        private const string HEADER_PREFIX = "<align=\"center\"><size=150%><b>";
        private const string HEADER_POSTFIX = "</b></size><align=\"left\">";

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
            GameObject originalText = GameObject.Find("SettingsButton/DescriptionText");
            GameObject text = GameObject.Instantiate(originalText, originalText.transform.parent.parent.parent);
            text.name = "PolyModVersion";
            RectTransform rect = text.GetComponent<RectTransform>();
            rect.anchoredPosition = new(265, 40);
            rect.sizeDelta = new(500, rect.sizeDelta.y);
            rect.anchorMax = new(0, 0);
            rect.anchorMin = new(0, 0);
            text.GetComponent<TextMeshProUGUI>().fontSize = 18;
            text.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.BottomLeft;
            text.GetComponent<TMPLocalizer>().Text = $"PolyMod {Plugin.VERSION}";
            text.AddComponent<LayoutElement>().ignoreLayout = true;

            GameObject originalButton = GameObject.Find("StartScreen/NewsButton");
            GameObject button = GameObject.Instantiate(originalButton, originalButton.transform.parent);
            button.gameObject.name = "PolyModHubButton";
            button.transform.position = originalButton.transform.position - new Vector3(90, 0, 0);
            button.active = true;

            Transform descriptionText = button.transform.Find("DescriptionText");
            descriptionText.gameObject.SetActive(true);
            descriptionText.GetComponentInChildren<TMPLocalizer>().Key = "polymod.hub";

            UIRoundButton buttonObject = button.GetComponent<UIRoundButton>();
            buttonObject.bg.sprite = Visual.BuildSprite(Plugin.GetResource("polymod_icon.png").ReadBytes());
            buttonObject.bg.transform.localScale = new Vector3(1.2f, 1.2f, 0);
            buttonObject.bg.color = Color.white;

            buttonObject.outline.gameObject.SetActive(false);
            buttonObject.iconContainer.gameObject.SetActive(false);
            buttonObject.OnClicked += (UIButtonBase.ButtonAction)PolyModHubButtonClicked;

            static void PolyModHubButtonClicked(int buttonId, BaseEventData eventData)
            {
                BasicPopup popup = PopupManager.GetBasicPopup();
                popup.Header = Localization.Get("polymod.hub");
                popup.Description = Localization.Get("polymod.hub.header", new Il2CppSystem.Object[] {
                    HEADER_PREFIX,
                    HEADER_POSTFIX
                }) + "\n\n";
                foreach (var mod in Main.mods.Values)
                {
                    popup.Description += Localization.Get("polymod.hub.mod", new Il2CppSystem.Object[] {
                        mod.name,
                        Localization.Get("polymod.hub.mod.status."
                            + Enum.GetName(typeof(Mod.Status), mod.status)!.ToLower()),
                        string.Join(", ", mod.authors),
                        mod.version.ToString()
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
                        callback: (UIButtonBase.ButtonAction)((int _, BaseEventData _) =>
                            NativeHelpers.OpenURL("https://discord.gg/eWPdhWtfVy", false))
                    )
                };
                if (Plugin.config.debug)
                    popupButtons.Add(new(
                        "polymod.hub.dump",
                        callback: (UIButtonBase.ButtonAction)((int _, BaseEventData _) =>
                        {
                            Directory.CreateDirectory(Plugin.DUMPED_DATA_PATH);
                            File.WriteAllTextAsync(
                                Path.Combine(Plugin.DUMPED_DATA_PATH, $"gameLogicData.json"),
                                PolytopiaDataManager.provider.LoadGameLogicData(VersionManager.GameLogicDataVersion)
                            );
                            File.WriteAllTextAsync(
                                Path.Combine(Plugin.DUMPED_DATA_PATH, $"avatarData.json"),
                                PolytopiaDataManager.provider.LoadAvatarData(1337)
                            );
                            NotificationManager.Notify(Localization.Get("polymod.hub.dumped"));
                        }),
                        closesPopup: false
                    ));
                popup.buttonData = popupButtons.ToArray();
                popup.ShowSetWidth(1000);
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

        internal static void Init()
        {
            Harmony.CreateAndPatchAll(typeof(Hub));
        }
    }
}