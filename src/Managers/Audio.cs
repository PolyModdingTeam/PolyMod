using HarmonyLib;
using UnityEngine;
using PolytopiaBackendBase.Common;

namespace PolyMod.Managers;

/// <summary>
/// The manager for custom audio.
/// It replaces game music, nature sounds, and sound effects with the registered audio clips of mods.
/// At this time, <see cref="BuildAudioClip"/> is a stub, so the registered clips have no audio data.
/// </summary>
public static class Audio
{
    /// <summary>
    /// A Harmony prefix on <c>MusicData.GetNatureAudioClip</c>.
    /// It finds a registered <c>nature</c> audio clip for the tribe and skin.
    /// </summary>
    /// <param name="__result">The audio clip that the game uses. The patch sets it to the mod clip, if one exists.</param>
    /// <param name="type">The tribe of the audio clip.</param>
    /// <param name="skinType">The skin of the audio clip.</param>
    /// <returns>False if a mod clip exists, which skips the original method. True if not.</returns>
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
    /// A Harmony prefix on <c>MusicData.GetMusicAudioClip</c>.
    /// It finds a registered <c>music</c> audio clip for the tribe and skin.
    /// </summary>
    /// <param name="__result">The audio clip that the game uses. The patch sets it to the mod clip, if one exists.</param>
    /// <param name="type">The tribe of the audio clip.</param>
    /// <param name="skinType">The skin of the audio clip.</param>
    /// <returns>False if a mod clip exists, which skips the original method. True if not.</returns>
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
    /// A Harmony prefix on <c>AudioSFXData.GetClip</c>.
    /// It finds a registered audio clip with the name of the sound effect type, for the skin.
    /// </summary>
    /// <param name="__result">The audio clip that the game uses. The patch sets it to the mod clip, if one exists.</param>
    /// <param name="id">The sound effect type.</param>
    /// <param name="skinType">The skin of the audio clip.</param>
    /// <returns>False if a mod clip exists, which skips the original method. True if not.</returns>
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
    /// A stub for the method that makes an audio clip from the bytes of an audio file.
    /// It does not read the data. It only gives a new empty audio clip.
    /// </summary>
    /// <param name="data">The bytes of the audio file. The method does not use them.</param>
    /// <param name="name">The name for the audio clip. The method does not use it.</param>
    /// <returns>A new empty audio clip with no name and no audio data.</returns>
    public static AudioClip BuildAudioClip(byte[] data, string name = "polymod_audio")
    {
        return new();
    }

    /// <summary>
    /// Applies all Harmony patches in the <see cref="Audio"/> class.
    /// </summary>
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Audio));
    }
}
