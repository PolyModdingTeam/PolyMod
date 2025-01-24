using HarmonyLib;
using UnityEngine;

namespace PolyMod
{
    internal static class VersionChecker
    {
        private static bool sawIncompatibilityWarning;

        private static bool EqualNoRevision(this Il2CppSystem.Version self, Il2CppSystem.Version version)
        {
            return self.Major == version.Major && self.Minor == version.Minor && self.Build == version.Build;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartScreen), nameof(StartScreen.Start))]
        private static void StartScreen_Start()
        {
            if (!Plugin.DEV && !sawIncompatibilityWarning && !VersionManager.SemanticVersion.EqualNoRevision(Plugin.POLYTOPIA_VERSION))
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