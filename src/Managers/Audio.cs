using HarmonyLib;
using Polytopia.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace PolyMod.Managers;

public static class Audio
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.SetupData))]
    private static void AudioManager_SetupData()
    {
        foreach (var item in Registry.customTribes)
        {
            if (PolytopiaDataManager.GetLatestGameLogicData().TryGetData(item, out TribeData data))
            {
                AudioManager.instance.climateTribeMap.Add(data.climate, item);
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MusicData), nameof(MusicData.GetNatureAudioClip))]
    private static bool MusicData_GetNatureAudioClip(ref AudioClip __result, TribeData.Type type, SkinType skinType)
    {
        AudioClip? audioClip = Registry.GetAudioClip("nature", Util.GetStyle(type, skinType));
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
        AudioClip? audioClip = Registry.GetAudioClip("music", Util.GetStyle(type, skinType));
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
        AudioClip? audioClip = Registry.GetAudioClip(
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
        return new AudioClip(new());
    }

    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Audio));
    }
}
