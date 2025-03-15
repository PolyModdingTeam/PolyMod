using HarmonyLib;
using Polytopia.Data;
using UnityEngine;

namespace PolyMod.Managers
{
    public static class Audio
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.SetAmbienceClimate))]
        private static void AudioManager_SetAmbienceClimatePrefix(ref int climate) //TODO CHECK
        {
            if (climate > 16)
                climate = 1;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MusicData), nameof(MusicData.GetNatureAudioClip))]
        private static bool MusicData_GetNatureAudioClip(ref AudioClip __result, TribeData.Type type, SkinType skinType)
        {
            AudioClip? audioClip = Main.GetAudioClip("nature", Utility.GetStyle(type, skinType));
            if (audioClip != null)
            {
                __result = audioClip;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MusicData), nameof(MusicData.GetMusicAudioClip))]
        private static bool MusicData_GetMusicAudioClip(ref AudioClip __result, TribeData.Type type, SkinType skinType)
        {
            AudioClip? audioClip = Main.GetAudioClip("music", Utility.GetStyle(type, skinType));
            if (audioClip != null)
            {
                __result = audioClip;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioSFXData), nameof(AudioSFXData.GetClip))]
        private static bool AudioSFXData_GetClip(ref AudioClip __result, SFXTypes id, SkinType skinType)
        {
            AudioClip? audioClip = Main.GetAudioClip(
                EnumCache<SFXTypes>.GetName(id),
                EnumCache<SkinType>.GetName(skinType)
            );
            if (audioClip != null)
            {
                __result = audioClip;
                return false;
            }
            return true;
        }

        public static AudioClip BuildAudioClip(byte[] data)
        {
            string path = Path.Combine(Application.persistentDataPath, "temp.wav");
            File.WriteAllBytes(path, data);
            WWW www = new("file://" + path);
            while (!www.isDone) { }
            AudioClip audioClip = www.GetAudioClip(false);
            File.Delete(path);
            return audioClip;
        }

        internal static void Init()
        {
            Harmony.CreateAndPatchAll(typeof(Audio));
        }
    }
}