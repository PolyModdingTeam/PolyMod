using LibCpp2IL;
using PolyMod.Managers;
using Polytopia.Data;
using UnityEngine;

namespace PolyMod;
public static class Registry
{
	public static int autoidx = Plugin.AUTOIDX_STARTS_FROM;
	public static Dictionary<string, Sprite> sprites = new();
	public static Dictionary<string, AudioSource> audioClips = new();
	internal static Dictionary<string, Mod> mods = new();
	public static Dictionary<string, Visual.PreviewTile[]> tribePreviews = new();
	public static Dictionary<string, Visual.SpriteInfo> spriteInfos = new();
	public static List<TribeData.Type> customTribes = new();
	public static List<Visual.SkinInfo> skinInfo = new();
	public static int climateAutoidx = (int)Enum.GetValues(typeof(TribeData.Type)).Cast<TribeData.Type>().Last();

	public static Sprite? GetSprite(string name, string style = "", int level = 0, bool skipStyle = false)
	{
		Sprite? sprite = null;
		name = name.ToLower();
		style = style.ToLower();
		if (skipStyle)
		{
			sprite = sprites.GetOrDefault($"{name}__", sprite);
			sprite = sprites.GetOrDefault($"{name}_", sprite);
			sprite = sprites.GetOrDefault($"{name}__{level}", sprite);
			sprite = sprites.GetOrDefault($"{name}_{level}", sprite);
		}
		else
		{
			sprite = sprites.GetOrDefault($"{name}__", sprite);
			sprite = sprites.GetOrDefault($"{name}_{style}_", sprite);
			sprite = sprites.GetOrDefault($"{name}__{level}", sprite);
			sprite = sprites.GetOrDefault($"{name}_{style}_{level}", sprite);
		}
		return sprite;
	}

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