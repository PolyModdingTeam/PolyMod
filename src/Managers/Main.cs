using BepInEx.Unity.IL2CPP.Logging;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Polytopia.Data;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using UnityEngine;

namespace PolyMod.Managers;
public static class Main
{

	internal static readonly Stopwatch stopwatch = new();
	internal static bool fullyInitialized;
	internal static bool dependencyCycle;



	[HarmonyPrefix]
	[HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
	private static void GameLogicData_Parse(GameLogicData __instance, JObject rootObject)
	{
		if (!fullyInitialized)
		{
			Load(rootObject);
			foreach (Visual.SkinInfo skin in Registry.skinInfo)
			{
				if (skin.skinData != null)
					__instance.skinData[(SkinType)skin.idx] = skin.skinData;
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
		foreach (var tribe in Registry.customTribes) __result.Add(tribe);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(IL2CPPUnityLogSource), nameof(IL2CPPUnityLogSource.UnityLogCallback))]
	private static bool IL2CPPUnityLogSource_UnityLogCallback(string logLine, string exception, LogType type)
	{
		foreach (string stringToIgnore in Plugin.LOG_MESSAGES_IGNORE)
		{
			if (logLine.Contains(stringToIgnore))
				return false;
		}
		return true;
	}

	internal static void Init()
	{
		stopwatch.Start();
		Harmony.CreateAndPatchAll(typeof(Main));
		Mod.Manifest polytopia = new(
			"polytopia",
			"The Battle of Polytopia",
			new(Application.version.ToString()),
			new string[] { "Midjiwan AB" },
			Array.Empty<Mod.Dependency>()
		);
		Registry.mods.Add(polytopia.id, new(polytopia, Mod.Status.Success, new()));
		Loader.LoadMods(Registry.mods);
		dependencyCycle = !Loader.SortMods(Registry.mods);
		if (dependencyCycle) return;

		StringBuilder looseSignatureString = new();
		StringBuilder signatureString = new();
		foreach (var (id, mod) in Registry.mods)
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
		Loc.BuildAndLoadLocalization(
			JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
				Plugin.GetResource("localization.json")
			)!
		);
		if (dependencyCycle) return;

		foreach (var (id, mod) in Registry.mods)
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
}
