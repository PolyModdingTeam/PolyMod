using BepInEx.Unity.IL2CPP.Logging;
using HarmonyLib;
using Il2CppSystem.Linq;
using LibCpp2IL;
using Newtonsoft.Json.Linq;
using Polytopia.Data;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using UnityEngine;

namespace PolyMod.Managers
{
	public static class Main
	{
		public record SpriteInfo(float? pixelsPerUnit, Vector2? pivot);

		public static int autoidx = Plugin.AUTOIDX_STARTS_FROM;
		public static Dictionary<string, Sprite> sprites = new();
		public static Dictionary<string, AudioSource> audioClips = new();
		public static Dictionary<string, Mod> mods = new();
		public static Dictionary<string, Visual.PreviewTile[]> tribePreviews = new();
		public static Dictionary<string, SpriteInfo> spriteInfos = new();
		internal static readonly Stopwatch stopwatch = new();
		internal static List<TribeData.Type> customTribes = new();
		internal static List<Tuple<int, string, SkinData?>> skinInfo = new();
		internal static int climateAutoidx = (int)Enum.GetValues(typeof(TribeData.Type)).Cast<TribeData.Type>().Last();
		internal static bool fullyInitialized;
		internal static bool dependencyCycle;



		[HarmonyPrefix]
		[HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
		private static void GameLogicData_Parse(GameLogicData __instance, JObject rootObject)
		{
			if (!fullyInitialized)
			{
				Load(rootObject);
				foreach (Tuple<int, string, SkinData?> skin in skinInfo)
				{
					if (skin.Item3 != null)
						__instance.skinData[(SkinType)skin.Item1] = skin.Item3;
				}
				fullyInitialized = true;
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsSkinUnlocked))]
		[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsSkinUnlockedInternal))]
		private static bool PurchaseManager_IsSkinUnlockedInternal(ref bool __result, SkinType skinType)
		{
			__result = (int)skinType >= Plugin.AUTOIDX_STARTS_FROM && skinType != SkinType.Test;
			return !__result;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsTribeUnlocked))]
		private static void PurchaseManager_IsTribeUnlocked(ref bool __result, TribeData.Type type)
		{
			__result = (int)type >= Plugin.AUTOIDX_STARTS_FROM || __result;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.GetUnlockedTribes))]
		private static void PurchaseManager_GetUnlockedTribes(
			ref Il2CppSystem.Collections.Generic.List<TribeData.Type> __result,
			bool forceUpdate = false
		)
		{
			foreach (var tribe in customTribes) __result.Add(tribe);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(IL2CPPUnityLogSource), nameof(IL2CPPUnityLogSource.UnityLogCallback))]
		private static bool IL2CPPUnityLogSource_UnityLogCallback(string logLine, string exception, LogType type)
		{
			return !(type == LogType.Warning && (logLine.Contains("Failed to find atlas") || logLine.Contains("Could not find sprite") || logLine.Contains("Couldn't find prefab for type")));
		}

		internal static void Init()
		{
			stopwatch.Start();
			Harmony.CreateAndPatchAll(typeof(Main));
			Loader.LoadMods(mods);
			dependencyCycle = !Loader.SortMods(mods);
			if (dependencyCycle) return;

			StringBuilder looseSignatureString = new();
			StringBuilder signatureString = new();
			foreach (var (id, mod) in mods)
			{
				if (mod.status != Mod.Status.Success) continue;
				foreach (var file in mod.files)
				{
					if (Path.GetExtension(file.name) == ".dll")
					{
						Loader.LoadAssemblyFile(mod, file);
					}
					if (Path.GetFileName(file.name) == "sprites.json")
					{
						Loader.LoadSpriteInfoFile(mod, file);
					}
				}
				if (!mod.client && id != "polytopia")
				{
					looseSignatureString.Append(id);
					looseSignatureString.Append(mod.version.Major);

					signatureString.Append(id);
					signatureString.Append(mod.version.ToString());
				}
			}
			Compatibility.HashSignatures(looseSignatureString, signatureString);

			stopwatch.Stop();
		}

		internal static void Load(JObject gameLogicdata)
		{
			stopwatch.Start();
			Mod.Manifest polytopia = new(
				"polytopia",
				"The Battle of Polytopia",
				new(VersionManager.SemanticVersion.ToString()),
				new string[] { "Midjiwan AB" },
				Array.Empty<Mod.Dependency>()
			);
			mods.Add(polytopia.id, new(polytopia, Mod.Status.Success, new()));
			Loc.BuildAndLoadLocalization(
				JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
					Plugin.GetResource("localization.json")
				)!
			);
			if (dependencyCycle) return;

			foreach (var (id, mod) in mods)
			{
				if (mod.status != Mod.Status.Success) continue;
				foreach (var file in mod.files)
				{
					if (Path.GetFileName(file.name) == "patch.json")
					{
						Loader.LoadGameLogicDataPatch(mod, gameLogicdata, JObject.Parse(new StreamReader(new MemoryStream(file.bytes)).ReadToEnd()));
					}
					if (Path.GetFileName(file.name) == "localization.json")
					{
						Loader.LoadLocalizationFile(mod, file);
					}
					if (Path.GetExtension(file.name) == ".png")
					{
						Loader.LoadSpriteFile(mod, file);
					}
					if (Path.GetExtension(file.name) == ".wav")
					{
						Loader.LoadAudioFile(mod, file);
					}
				}
			}

			stopwatch.Stop();
			Plugin.logger.LogInfo($"Loaded all mods in {stopwatch.ElapsedMilliseconds}ms");
		}

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
}
