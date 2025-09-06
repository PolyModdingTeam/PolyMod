using HarmonyLib;
using Polytopia.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace PolyMod.Managers;

/// <summary>
/// Manages custom audio for PolyMod.
/// </summary>
public static class Audio
{
    /// <summary>
    /// Patches the audio manager to set up data for custom tribes.
    /// </summary>
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

    /// <summary>
    /// Patches the music data to get custom nature audio clips.
    /// </summary>
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

    /// <summary>
    /// Patches the music data to get custom music audio clips.
    /// </summary>
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

    /// <summary>
    /// Patches the audio SFX data to get custom sound effect clips.
    /// </summary>
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

    /// <summary>
    /// Builds an audio clip from raw byte data.
    /// </summary>
    /// <param name="data">The byte data of the audio file.</param>
    /// <returns>The created audio clip.</returns>
    public static AudioClip BuildAudioClip(byte[] data)
    {
        // TODO: This is a placeholder. The actual implementation is tracked in issue #71.
        return new AudioClip(new());
    }

    /// <summary>
    /// Initializes the Audio manager by patching the necessary methods.
    /// </summary>
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Audio));
    }
}
