using System.Globalization;
using LibCpp2IL;
using PolyMod.Managers;
using UnityEngine;
using PolytopiaBackendBase.Common;

namespace PolyMod;

/// <summary>
/// The central store of the mods and the mod content that PolyMod loads.
/// </summary>
public static class Registry
{
    /// <summary>
    /// The next free index for an automatically indexed enum value.
    /// PolyMod uses this one counter for all enum types, which include tribes, units, skins and tile effects.
    /// The counter starts at <see cref="Plugin.AUTOIDX_STARTS_FROM"/>.
    /// </summary>
    internal static int autoidx = Plugin.AUTOIDX_STARTS_FROM;

    /// <summary>
    /// The sprites that PolyMod made from mod textures, keyed by the lowercase sprite name.
    /// </summary>
    internal static Dictionary<string, Sprite> sprites = new();

    /// <summary>
    /// The textures from the PNG files of mods, keyed by the lowercase file name without the extension.
    /// </summary>
    internal static Dictionary<string, Texture2D> textures = new();

    /// <summary>
    /// The audio sources of mod audio files, keyed by the lowercase file name without the extension.
    /// Each audio source holds one audio clip.
    /// </summary>
    internal static Dictionary<string, AudioSource> audioClips = new();

    /// <summary>
    /// All registered mods, keyed by the mod id.
    /// This dictionary also contains the base game entry with the id <c>polytopia</c>.
    /// </summary>
    internal static Dictionary<string, Mod> mods = new();

    /// <summary>
    /// The custom preview tiles of tribes, keyed by the tribe id from the GLD.
    /// </summary>
    internal static Dictionary<string, TribePreviews.PreviewTile[]> tribePreviews = new();

    /// <summary>
    /// The sprite infos from the <c>sprites.json</c> and <c>sprites_*.json</c> files of mods, keyed by the lowercase sprite name.
    /// </summary>
    internal static Dictionary<string, Visual.SpriteInfo> spriteInfos = new();

    /// <summary>
    /// The asset bundles from the <c>.bundle</c> files of mods, keyed by the file name without the extension.
    /// </summary>
    internal static Dictionary<string, AssetBundle> assetBundles = new();

    /// <summary>
    /// The tribe types of the custom tribes that mods add.
    /// </summary>
    internal static List<TribeType> customTribes = new();

    /// <summary>
    /// The skin infos of the custom skins that mods add.
    /// </summary>
    internal static List<Visual.SkinInfo> skinInfo = new();

    /// <summary>
    /// The next free value for the style and the climate of a custom tribe.
    /// The counter starts at the value of the last <see cref="TribeType"/> member.
    /// </summary>
    internal static int climateAutoidx = (int)Enum.GetValues<TribeType>().Last();

    /// <summary>
    /// The prefab files that PolyMod loads after it merges the GLD patches.
    /// Each entry contains the mod and one of its files. A mod can have more than one entry.
    /// </summary>
    internal static List<(Mod mod, Mod.File file)> filesQueueToBeProcessedAfterGLDMerge = new();

    /// <summary>
    /// Gets the mod sprite that is the best match for a base name, a tribe, a skin and a level.
    /// The method changes the tribe and the skin to their <c>EnumCache</c> names and calls <see cref="GetSpriteByText"/>.
    /// </summary>
    /// <param name="baseName">The base name of the sprite.</param>
    /// <param name="tribe">The tribe of the sprite, or <see cref="TribeType.None"/> for no tribe.</param>
    /// <param name="skin">The skin of the sprite, or <see cref="SkinType.Default"/> for no skin.</param>
    /// <param name="level">The level of the sprite, or -1 for no level.</param>
    /// <param name="outline">If true, the method finds the outline variant of the sprite.</param>
    /// <returns>The sprite that is the best match, or null if no sprite matches.</returns>
    public static Sprite? GetSprite(string baseName, TribeType tribe = TribeType.None, SkinType skin = SkinType.Default, int level = -1, bool outline = false)
    {
        string tribeText = (tribe == TribeType.None) ? "" : EnumCache<TribeType>.GetName(tribe);
        string skinText = (skin == SkinType.Default) ? "" : EnumCache<SkinType>.GetName(skin);
        string levelText = (level == -1) ? "" : level.ToString(CultureInfo.InvariantCulture);
        return GetSpriteByText(baseName, tribeText, skinText, levelText, outline);
    }
    /// <summary>
    /// Gets the mod sprite that is the best match for a base name, a tribe text, a skin text and a level text.
    /// </summary>
    /// <remarks>
    /// The method changes the base name, the tribe text and the skin text to lowercase.
    /// If the level text is not empty, the method adds <c>_level</c> to the base name.
    /// Then it tries these names in sequence: <c>base</c>, <c>base_tribe</c>, <c>base_skin</c> and <c>base_tribe_skin</c>.
    /// If <paramref name="outline"/> is true, the method adds <c>_outline</c> to each name.
    /// A sprite that the method finds later replaces a sprite that it found before.
    /// The result is the sprite with the most specific name that exists in <see cref="sprites"/>.
    /// </remarks>
    /// <param name="baseName">The base name of the sprite.</param>
    /// <param name="tribeText">The tribe part of the sprite name, or an empty string.</param>
    /// <param name="skinText">The skin part of the sprite name, or an empty string.</param>
    /// <param name="levelText">The level part of the sprite name, or an empty string for no level.</param>
    /// <param name="outline">If true, the method finds the outline variant of the sprite.</param>
    /// <returns>The sprite that is the best match, or null if no sprite matches.</returns>
    public static Sprite? GetSpriteByText(string baseName, string tribeText = "", string skinText = "", string levelText = "", bool outline = false)
    {
        Sprite? sprite = null;
        baseName = baseName.ToLowerInvariant();
        tribeText = tribeText.ToLowerInvariant();
        skinText = skinText.ToLowerInvariant();

        List<string> spriteNameVariants = new();

        if (levelText != string.Empty)
            baseName += $"_{levelText}";

        spriteNameVariants.Add(baseName);
        spriteNameVariants.Add(baseName + "_" + tribeText);
        spriteNameVariants.Add(baseName + "_" + skinText);
        spriteNameVariants.Add(baseName + "_" + tribeText + "_" + skinText);

        foreach (string spriteName in spriteNameVariants)
        {
            sprite = sprites.GetOrDefault(outline ? spriteName + "_outline" : spriteName, sprite);
        }

        return sprite;
    }

    /// <summary>
    /// Gets the mod audio clip that is the best match for a name, a tribe and a skin.
    /// The method changes the tribe and the skin to their <c>EnumCache</c> names and calls <see cref="GetAudioClipByText"/>.
    /// </summary>
    /// <param name="name">The base name of the audio clip.</param>
    /// <param name="tribe">The tribe of the audio clip, or <see cref="TribeType.None"/> for no tribe.</param>
    /// <param name="skin">The skin of the audio clip, or <see cref="SkinType.Default"/> for no skin.</param>
    /// <returns>The audio clip that is the best match, or null if no audio clip matches.</returns>
    public static AudioClip? GetAudioClip(string name, TribeType tribe = TribeType.None, SkinType skin = SkinType.Default)
    {
        string tribeText = (tribe == TribeType.None) ? "" : EnumCache<TribeType>.GetName(tribe);
        string skinText = (skin == SkinType.Default) ? "" : EnumCache<SkinType>.GetName(skin);
        return GetAudioClipByText(name, tribeText, skinText);
    }

    /// <summary>
    /// Gets the mod audio clip that is the best match for a base name, a tribe text and a skin text.
    /// </summary>
    /// <remarks>
    /// The method changes all three texts to lowercase.
    /// Then it tries these names in sequence: <c>base</c>, <c>base_tribe</c>, <c>base_skin</c> and <c>base_tribe_skin</c>.
    /// An audio source that the method finds later replaces an audio source that it found before.
    /// The result is the clip of the audio source with the most specific name that exists in <see cref="audioClips"/>.
    /// </remarks>
    /// <param name="baseName">The base name of the audio clip.</param>
    /// <param name="tribeText">The tribe part of the audio name, or an empty string.</param>
    /// <param name="skinText">The skin part of the audio name, or an empty string.</param>
    /// <returns>The clip of the audio source that is the best match, or null if no audio source matches.</returns>
    public static AudioClip? GetAudioClipByText(string baseName, string tribeText = "", string skinText = "")
    {
        AudioSource? audioSource = null;
        baseName = baseName.ToLowerInvariant();
        tribeText = tribeText.ToLowerInvariant();
        skinText = skinText.ToLowerInvariant();

        List<string> audioNameVariants = new()
        {
            baseName,
            baseName + "_" + tribeText,
            baseName + "_" + skinText,
            baseName + "_" + tribeText + "_" + skinText,
        };

        foreach (string audioName in audioNameVariants)
        {
            audioSource = audioClips.GetOrDefault(audioName, audioSource);
        }

        if (audioSource == null) return null;
        return audioSource.clip;
    }
}
