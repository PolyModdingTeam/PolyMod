using HarmonyLib;
using PolyMod.Managers;
using Polytopia.Data;
using UnityEngine;

namespace PolyMod.Loaders
{
    public static class AudioClipLoader
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MusicData), nameof(MusicData.GetNatureAudioClip))]
        private static bool MusicData_GetNatureAudioClip(ref AudioClip __result, TribeData.Type type, SkinType skinType)
        {
            AudioClip? audioClip = ModManager.GetAudioClip("nature", Utility.GetStyle(type, skinType));
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
            AudioClip? audioClip = ModManager.GetAudioClip("music", Utility.GetStyle(type, skinType));
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
            AudioClip? audioClip = ModManager.GetAudioClip(
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
            Harmony.CreateAndPatchAll(typeof(AudioClipLoader));
        }
    }
}