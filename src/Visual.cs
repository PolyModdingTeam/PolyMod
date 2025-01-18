using Cpp2IL.Core.Extensions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static PopupBase;

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
            button.gameObject.name = "PolyModButton";
            button.transform.position = originalButton.transform.position - new Vector3(90, 0, 0);
            button.active = true;
            Transform descriptionText = button.transform.Find("DescriptionText");
            descriptionText.gameObject.SetActive(true);
            descriptionText.GetComponentInChildren<TMPLocalizer>().Text = "PolyMod Hub";
            Transform iconContainer = button.transform.Find("IconContainer");
            iconContainer.GetComponentInChildren<Image>().sprite
                = SpritesLoader.BuildSprite(Plugin.GetResource("polymod_icon.png").ReadBytes());
            UIRoundButton buttonObject = button.GetComponent<UIRoundButton>();
            buttonObject.OnClicked += (UIButtonBase.ButtonAction)OnPolyModButtonClicked;

            void OnPolyModButtonClicked(int buttonId, BaseEventData eventData)
            {
                BasicPopup polymodPopup = PopupManager.GetBasicPopup();

                polymodPopup.Header = "PolyMod Hub";
                string polyModHubText = "Welcome! \nHere you can see the list of all currently loaded mods: \n\n";
                string[] keys = ModLoader.mods.Keys.ToArray();
                foreach (string key in keys)
                {
                    PolyMod.ModLoader.Mod mod = ModLoader.mods[key];
                    string modAuthors = "\nAuthors: ";
                    foreach (string author in mod.authors)
                    {
                        modAuthors += author;
                    }
                    polyModHubText += "Name: " + mod.name + "\nStatus: " + mod.GetPrettyStatus() + modAuthors + "\nVersion: " + mod.version + "\n\n";
                }
                polyModHubText += "Join our discord! Feel free to discuss mods, create them and ask for help!";
                polymodPopup.Description = polyModHubText;
                List<PopupButtonData> popupButtons = new()
                {
                    new(Localization.Get("buttons.back")),
                    new("OUR DISCORD", PopupButtonData.States.None, (UIButtonBase.ButtonAction)((int id, BaseEventData eventdata) => NativeHelpers.OpenURL("https://discord.gg/eWPdhWtfVy", false)), -1, true, null)
                };
                polymodPopup.buttonData = popupButtons.ToArray();
                polymodPopup.Show();
            }
        }

        internal static void Init()
        {
            Harmony.CreateAndPatchAll(typeof(Visual));
        }
    }
}