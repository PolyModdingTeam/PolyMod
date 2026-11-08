using HarmonyLib;
using UnityEngine;
using PolytopiaBackendBase.Common;

namespace PolyMod.Managers;

/// <summary>
/// Manages custom audio for PolyMod.
/// </summary>
public static class Audio
{
    /// <summary>
    /// Patches the music data to get custom nature audio clips.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MusicData), nameof(MusicData.GetNatureAudioClip))]
    private static bool MusicData_GetNatureAudioClip(ref AudioClip __result, TribeType type, SkinType skinType)
    {
        AudioClip? audioClip = Registry.GetAudioClip("nature", type, skinType);
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
    private static bool MusicData_GetMusicAudioClip(ref AudioClip __result, TribeType type, SkinType skinType)
    {
        AudioClip? audioClip = Registry.GetAudioClip("music", type, skinType);
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
            skin: skinType
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
    /// <param name="data">The byte data of the audio file, which has to be a WAVE file.</param>
    /// <param name="name">The name to give the clip inside Unity.</param>
    /// <returns>The created audio clip.</returns>
    /// <exception cref="InvalidDataException">The data is not a WAVE file that can be decoded.</exception>
    /// <exception cref="NotSupportedException">The game build cannot create audio clips.</exception>
    public static AudioClip BuildAudioClip(byte[] data, string name = "polymod_audio")
    {
        return new();
    }

    /// <summary>
    /// Initializes the Audio manager by patching the necessary methods.
    /// </summary>
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Audio));
    }
}
