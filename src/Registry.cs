using LibCpp2IL;
using PolyMod.Managers;
using Polytopia.Data;
using PolytopiaBackendBase.Game;
using UnityEngine;

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

	/// <summary>
	/// A dictionary of all loaded audio clips, keyed by their name.
	/// </summary>
	public static Dictionary<string, AudioSource> audioClips = new();

	/// <summary>
	/// A dictionary of all loaded mods, keyed by their ID.
	/// </summary>
	internal static Dictionary<string, Mod> mods = new();

	/// <summary>
	/// A dictionary of tribe previews, keyed by tribe name.
	/// </summary>
	public static Dictionary<string, Visual.PreviewTile[]> tribePreviews = new();

	/// <summary>
	/// A dictionary of sprite information, keyed by sprite name.
	/// </summary>
	public static Dictionary<string, Visual.SpriteInfo> spriteInfos = new();

	/// <summary>
	/// A dictionary of prefab names, keyed by their enum value.
	/// </summary>
	public static Dictionary<int, string> prefabNames = new();

	/// <summary>
	/// A dictionary of custom unit prefabs, keyed by their prefab info.
	/// </summary>
	public static Dictionary<Visual.PrefabInfo, Unit> unitPrefabs = new();

	/// <summary>
	/// A dictionary of custom resource prefabs, keyed by their prefab info.
	/// </summary>
	public static Dictionary<Visual.PrefabInfo, Resource> resourcePrefabs = new();

	/// <summary>
	/// A dictionary of custom improvement prefabs, keyed by their prefab info.
	/// </summary>
	public static Dictionary<Visual.PrefabInfo, Building> improvementsPrefabs = new();

	/// <summary>
	/// A dictionary of loaded asset bundles, keyed by their name.
	/// </summary>
	public static Dictionary<string, AssetBundle> assetBundles = new();

	/// <summary>
	/// A list of custom tribes.
	/// </summary>
	public static List<TribeData.Type> customTribes = new();

	/// <summary>
	/// A list of custom skin information.
	/// </summary>
	public static List<Visual.SkinInfo> skinInfo = new();

	/// <summary>
	/// The next available index for climate styles.
	/// </summary>
	public static int climateAutoidx = (int)Enum.GetValues(typeof(TribeData.Type)).Cast<TribeData.Type>().Last();

	/// <summary>
	/// The next available index for game modes.
	/// </summary>
	public static int gameModesAutoidx = Enum.GetValues(typeof(GameMode)).Length;

	public static List<Loc.TermInfo> languageInfo = new();
	/// <summary>
	/// Gets a sprite from the registry, trying to find the best match.
	/// </summary>
	/// <param name="name">The name of the sprite.</param>
	/// <param name="style">The style of the sprite (e.g., tribe or skin).</param>
	/// <param name="level">The level of the sprite (e.g., for cities).</param>
	/// <returns>The requested sprite, or null if not found. The lookup priority is: name_style_level, name__level, name_style_, name__.</returns>
	public static Sprite? GetSprite(string name, string style = "", int level = 0)
	{
		Sprite? sprite = null;
		name = name.ToLower();
		style = style.ToLower();
		// This looks backwards, but GetOrDefault returns the provided default value if the key isn't found.
		// This means `sprite` is updated sequentially, and the last, most-specific match is the one that is kept.
		sprite = sprites.GetOrDefault($"{name}__", sprite);
		sprite = sprites.GetOrDefault($"{name}_{style}_", sprite);
		sprite = sprites.GetOrDefault($"{name}__{level}", sprite);
		sprite = sprites.GetOrDefault($"{name}_{style}_{level}", sprite);
		return sprite;
	}

	/// <summary>
	/// Gets an audio clip from the registry.
	/// </summary>
	/// <param name="name">The name of the audio clip.</param>
	/// <param name="style">The style of the audio clip.</param>
	/// <returns>The requested audio clip, or null if not found.</returns>
	public static AudioClip? GetAudioClip(string name, string style)
	{
		AudioSource? audioSource = null;
		name = name.ToLower();
		style = style.ToLower();
		audioSource = audioClips.GetOrDefault($"{name}_{style}", audioSource);
		if (audioSource == null) return null;
		return audioSource.clip;
	}
}