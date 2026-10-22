using LibCpp2IL;
using PolyMod.Managers;
using PolytopiaBackendBase.Game;
using UnityEngine;
using PolytopiaBackendBase.Common;

namespace PolyMod;

/// <summary>
/// A central registry for storing and accessing modded content.
/// </summary>
public static class Registry
{
	/// <summary>
	/// The next available index for automatically assigned IDs.
	/// </summary>
	public static int autoidx = Plugin.AUTOIDX_STARTS_FROM;

	/// <summary>
	/// A dictionary of all loaded sprites, keyed by their name.
	/// </summary>
	public static Dictionary<string, Sprite> sprites = new();

	public static Dictionary<string, KeyValuePair<Texture2D, bool>> textures = new(); // string = texture name, bool, whether texture was processed or not. reviwers, pls tell me how to improve

	/// <summary>
	/// A dictionary of all loaded audio clips, keyed by their name.
	/// </summary>
	public static Dictionary<string, AudioSource> audioClips = new();

	/// <summary>
	/// A dictionary of all loaded mods, keyed by their ID.
	/// </summary>
	public static Dictionary<string, Mod> mods = new();

	/// <summary>
	/// A dictionary of tribe previews, keyed by tribe name.
	/// </summary>
	public static Dictionary<string, TribePreviews.PreviewTile[]> tribePreviews = new();

	/// <summary>
	/// A dictionary of sprite information, keyed by sprite name.
	/// </summary>
	public static Dictionary<string, Visual.SpriteInfo> spriteInfos = new();

	/// <summary>
	/// A dictionary of loaded asset bundles, keyed by their name.
	/// </summary>
	public static Dictionary<string, AssetBundle> assetBundles = new();

	/// <summary>
	/// A list of custom tribes.
	/// </summary>
	public static List<TribeType> customTribes = new();

	/// <summary>
	/// A list of custom skin information.
	/// </summary>
	public static List<Visual.SkinInfo> skinInfo = new();

	/// <summary>
	/// The next available index for climate styles.
	/// </summary>
	public static int climateAutoidx = (int)Enum.GetValues(typeof(TribeType)).Cast<TribeType>().Last();

	/// <summary>
	/// The next available index for game modes.
	/// </summary>
	public static int gameModesAutoidx = Enum.GetValues(typeof(GameMode)).Length;

	public static Dictionary<Mod, Mod.File> filesQueueToBeProcessedAfterGLDMerge = new(); // I dont know whether I should do sum abt it

	public static Sprite? GetSprite(string baseName, TribeType tribe = TribeType.None, SkinType skin = SkinType.Default, int level = -1)
	{
		string tribeText = (tribe == TribeType.None) ? "" : EnumCache<TribeType>.GetName(tribe);
		string skinText = (skin == SkinType.Default) ? "" : EnumCache<SkinType>.GetName(skin);
		return GetSpriteByText(baseName, tribeText, skinText, level);
	}
	/// <summary>
	/// Gets a sprite from the registry, trying to find the best match.
	/// </summary>
	public static Sprite? GetSpriteByText(string baseName, string tribeText = "", string skinText = "", int level = -1)
	{
		Sprite? sprite = null;
		baseName = baseName.ToLower();
		tribeText = tribeText.ToLower();
		skinText = skinText.ToLower();

		List<string> spriteNameVariants = new();

		if (level >= 0)
		{
			string levelText = level.ToString();
			spriteNameVariants.Add(baseName + "_" + levelText);
			spriteNameVariants.Add(baseName + "_" + levelText + "_" + tribeText);
			spriteNameVariants.Add(baseName + "_" + levelText + "_" + skinText);
			spriteNameVariants.Add(baseName + "_" + levelText + "_" + tribeText + "_" + skinText);
		}
		else
		{
			spriteNameVariants.Add(baseName);
			spriteNameVariants.Add(baseName + "_" + tribeText);
			spriteNameVariants.Add(baseName + "_" + skinText);
			spriteNameVariants.Add(baseName + "_" + tribeText + "_" + skinText);
		}

		foreach(string spriteName in spriteNameVariants)
		{
			sprite = sprites.GetOrDefault(spriteName, sprite);
		}

		return sprite;
	}

	/// <summary>
	/// Gets an audio clip from the registry.
	/// </summary>
	/// <param name="name">The name of the audio clip.</param>
	/// <param name="style">The style of the audio clip.</param>
	/// <returns>The requested audio clip, or null if not found.</returns>
	public static AudioClip? GetAudioClip(string name, TribeType tribe = TribeType.None, SkinType skin = SkinType.Default)
	{
		AudioSource? audioSource = null;
		name = name.ToLower();
		string tribeText = (tribe == TribeType.None) ? "" : EnumCache<TribeType>.GetName(tribe);
		string skinText = (skin == SkinType.Default) ? "" : EnumCache<SkinType>.GetName(skin);
		tribeText = tribeText.ToLower();
		skinText = skinText.ToLower();
		audioSource = audioClips.GetOrDefault($"{name}_{tribeText}_{skinText}", audioSource);
		if (audioSource == null) return null;
		return audioSource.clip;
	}
}
