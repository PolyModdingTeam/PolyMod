using LibCpp2IL;
using PolyMod.Patches;
using Polytopia.Data;
using PolytopiaBackendBase.Game;
using UnityEngine;

namespace PolyMod;

/// <summary>
/// Provides central registries for custom content managed by PolyMod.
/// </summary>
public static class Registry
{
	internal static int autoidx = Constants.AUTOIDX_STARTS_FROM;

	/// <summary>The registry for custom sprites.</summary>
	internal static Dictionary<string, Sprite> sprites = new();

	/// <summary>The registry for custom audio clips.</summary>
	internal static Dictionary<string, AudioSource> audioClips = new();

	/// <summary>The registry for loaded mods.</summary>
	public static Dictionary<string, Mod> mods = new();

	/// <summary>The registry for custom tribe previews.</summary>
	internal static Dictionary<string, Visual.PreviewTile[]> tribePreviews = new();

	/// <summary>The registry for custom sprite information.</summary>
	internal static Dictionary<string, Visual.SpriteInfo> spriteInfos = new();

	/// <summary>The registry for custom prefab names.</summary>
	internal static Dictionary<int, string> prefabNames = new();

	/// <summary>The registry for custom unit prefabs.</summary>
	internal static Dictionary<Visual.PrefabInfo, Unit> unitPrefabs = new();

	/// <summary>The registry for custom resource prefabs.</summary>
	internal static Dictionary<Visual.PrefabInfo, Resource> resourcePrefabs = new();

	/// <summary>The registry for custom improvement prefabs.</summary>
	internal static Dictionary<Visual.PrefabInfo, Building> improvementsPrefabs = new();

	/// <summary>The registry for loaded asset bundles.</summary>
	public static Dictionary<string, AssetBundle> assetBundles = new();

	/// <summary>The registry for custom tribes.</summary>
	public static List<TribeData.Type> customTribes = new();

	/// <summary>The registry for custom skin information.</summary>
	internal static List<Visual.SkinInfo> skinInfo = new();

	internal static int climateAutoidx = (int)Enum.GetValues(typeof(TribeData.Type)).Cast<TribeData.Type>().Last();
	internal static int gameModesAutoidx = Enum.GetValues(typeof(GameMode)).Length;

	/// <summary>
	/// Retrieves a sprite from the registry based on its name, style, and level.
	/// The method searches for multiple key formats to find the best match.
	/// </summary>
	/// <param name="name">The base name of the sprite.</param>
	/// <param name="style">The style variant of the sprite (e.g., tribe-specific).</param>
	/// <param name="level">The level variant of the sprite (e.g., for a city).</param>
	/// <returns>The matching <see cref="Sprite"/>, or <c>null</c> if no sprite is found.</returns>
	public static Sprite? GetSprite(string name, string style = "", int level = 0)
	{
		Sprite? sprite = null;
		name = name.ToLower();
		style = style.ToLower();
		sprite = sprites.GetOrDefault($"{name}__", sprite);
		sprite = sprites.GetOrDefault($"{name}_{style}_", sprite);
		sprite = sprites.GetOrDefault($"{name}__{level}", sprite);
		sprite = sprites.GetOrDefault($"{name}_{style}_{level}", sprite);
		return sprite;
	}

	/// <summary>
	/// Retrieves an audio clip from the registry.
	/// </summary>
	/// <param name="name">The name of the audio clip.</param>
	/// <param name="style">The style of the audio clip.</param>
	/// <returns>The matching <see cref="AudioClip"/>, or <c>null</c> if not found.</returns>
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
