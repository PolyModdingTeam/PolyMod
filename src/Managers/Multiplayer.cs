using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;

namespace PolyMod.Managers
{
    public static class Multiplayer
    {
        internal static void Init()
        {
            Harmony.CreateAndPatchAll(typeof(Multiplayer));
            BuildConfigHelper.GetSelectedBuildConfig().buildServerURL = BuildServerURL.Custom;
            BuildConfigHelper.GetSelectedBuildConfig().customServerURL = Plugin.config.backendUrl;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MultiplayerScreen), nameof(MultiplayerScreen.Show))]
        public static void MultiplayerScreen_Show(MultiplayerScreen __instance)
        {
            __instance.multiplayerSelectionScreen.TournamentsButton.gameObject.SetActive(false);
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(ProfileScreen), nameof(ProfileScreen.Start))]
        public static void ProfileScreen_Start(ProfileScreen __instance)
        {
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartScreen), nameof(StartScreen.Start))]
        private static void StartScreen_Start(StartScreen __instance)
        {
            __instance.highscoreButton.gameObject.SetActive(false);
            __instance.weeklyChallengesButton.gameObject.SetActive(false);
        }
    }
}