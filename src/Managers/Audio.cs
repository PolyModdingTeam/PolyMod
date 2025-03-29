using HarmonyLib;
using Polytopia.Data;
using UnityEngine;

namespace PolyMod.Managers;
public static class Audio
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.SetupData))]
    private static void AudioManager_SetupData()
    {
        foreach (var item in Registry.customTribes)
        {
            if (GameManager.GameState.GameLogicData.TryGetData(item, out TribeData data))
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
