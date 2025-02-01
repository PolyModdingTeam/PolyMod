using Cpp2IL.Core.Extensions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PolyMod
{
    internal static class Visual
    {
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
            text.GetComponent<TMPLocalizer>().Text = $"PolyMod {(Plugin.DEV ? "Dev" : Plugin.VERSION)}";
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
            buttonObject.bg.sprite = SpritesLoader.BuildSprite(Plugin.GetResource("polymod_icon.png").ReadBytes());
            buttonObject.bg.transform.localScale = new Vector3(1.2f, 1.2f, 0);
            buttonObject.bg.color = Color.white;

            buttonObject.outline.gameObject.SetActive(false);
            buttonObject.iconContainer.gameObject.SetActive(false);
            buttonObject.OnClicked += (UIButtonBase.ButtonAction)PolyModHubButtonClicked;

            static void PolyModHubButtonClicked(int buttonId, BaseEventData eventData)
            {
                BasicPopup popup = PopupManager.GetBasicPopup();
                popup.Header = Localization.Get("polymod.hub");
                popup.Description = Localization.Get("polymod.hub.header") + "\n\n";
                foreach (var mod in ModLoader.mods.Values)
                {
                    popup.Description += Localization.Get("polymod.hub.mod", new Il2CppSystem.Object[] {
                        mod.name,
                        Localization.Get("polymod.hub.mod.status."
                            + Enum.GetName(typeof(ModLoader.Mod.Status), mod.status)!.ToLower()),
                        string.Join(", ", mod.authors),
                        mod.version.ToString()
                    });
                    popup.Description += "\n\n";
                }
                popup.Description += Localization.Get("polymod.hub.footer");
                List<PopupBase.PopupButtonData> popupButtons = new()
                {
                    new("buttons.back"),
                    new(
                        "polymod.hub.discord",
                        PopupBase.PopupButtonData.States.None,
                        (UIButtonBase.ButtonAction)((int _, BaseEventData _) =>
                            NativeHelpers.OpenURL("https://discord.gg/eWPdhWtfVy", false))
                    )
                };
                popup.buttonData = popupButtons.ToArray();
                popup.Show();
            }
        }

        internal static void Init()
        {
            Harmony.CreateAndPatchAll(typeof(Visual));
        }
    }
}