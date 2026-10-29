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

    private const string CLIP_CREATION_UNAVAILABLE =
        "this game build cannot create audio clips at runtime."
            + " AudioClip.Create marshals its arguments as spans, and"
            + " Il2CppSystem.ReadOnlySpan<T>.GetPinnableReference() is missing from its IL2CPP build."
            + " Custom audio stays disabled until the game ships calling AudioClip.Create and SetData";

    private static bool clipCreationUnavailable = false;

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
        WavDecoder.WavData wav = WavDecoder.Decode(data);
        if (clipCreationUnavailable)
            throw new NotSupportedException($"Cannot build {name}, {CLIP_CREATION_UNAVAILABLE}.");

        try
        {
            AudioClip clip = AudioClip.Create(name, wav.lengthSamples, wav.channels, wav.frequency, false);
            if (clip == null)
                throw new InvalidOperationException($"Unity returned no audio clip for {name}.");
            if (!clip.SetData(wav.samples, 0))
                throw new InvalidOperationException($"Unity rejected the samples for {name}.");
            return clip;
        }
        catch (MissingMethodException exception)
        {
            // Remembered so that a mod shipping many files reports this once rather than per file.
            clipCreationUnavailable = true;
            throw new NotSupportedException($"Cannot build {name}, {CLIP_CREATION_UNAVAILABLE}.", exception);
        }
    }

    /// <summary>
    /// Initializes the Audio manager by patching the necessary methods.
    /// </summary>
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Audio));
    }
}
