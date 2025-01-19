using System.Text.Json;
using HarmonyLib;
using I2.Loc;
using UnityEngine;

namespace PolyMod
{
    internal static class VersionChecker
    {
        private static bool sawIncompatibilityWarning;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartScreen), nameof(StartScreen.Start))]
        private static void StartScreen_Start()
        {
            // foreach (var item in LocalizationManager.Sources[0].mTerms)
            // {
            //     File.WriteAllTextAsync(Path.Combine(Plugin.BASE_PATH, "localiz", item.Term), JsonSerializer.Serialize(item.Languages));
            // }
            if (!Plugin.DEV && !sawIncompatibilityWarning && VersionManager.SemanticVersion != Plugin.POLYTOPIA_VERSION)
            {
                PopupManager.GetBasicPopup(new(
                    Localization.Get("polymod.version.mismatch"),
                    Localization.Get("polymod.version.mismatch.description"),
                    new(new PopupBase.PopupButtonData[] {
                        new("buttons.stay", customColorStates: ColorConstants.redButtonColorStates),
                        new("buttons.exitgame", PopupBase.PopupButtonData.States.None, (Il2CppSystem.Action)Application.Quit)
                    }))
                ).Show();
                sawIncompatibilityWarning = true;
            }
        }

        internal static void Init()
        {
            Harmony.CreateAndPatchAll(typeof(VersionChecker));
        }
    }
}