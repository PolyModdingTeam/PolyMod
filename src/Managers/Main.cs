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
	internal const int MAX_TECH_TIER = 100;
	internal static readonly Stopwatch stopwatch = new();
	internal static bool fullyInitialized;
	internal static bool dependencyCycle;
	internal static Dictionary<UnitData.Type, UnitData.Type> embarkUnitTypes = new()
	{
		{ UnitData.Type.Cloak, UnitData.Type.Cloak_Boat },
		{ UnitData.Type.Dagger, UnitData.Type.Pirate },
		{ UnitData.Type.Giant, UnitData.Type.Juggernaut },
	};


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

	[HarmonyPrefix]
	[HarmonyPatch(typeof(EmbarkAction), nameof(EmbarkAction.ExecuteDefault))]
	private static bool EmbarkAction_ExecuteDefault(EmbarkAction __instance, GameState gameState)
	{
		PlayerState playerState;
		if (gameState.TryGetPlayer(__instance.PlayerId, out playerState))
		{
			TileData tile = gameState.Map.GetTile(__instance.Coordinates);
			UnitState unit = tile.unit;
			UnitData.Type type = UnitData.Type.Transportship;
			if (embarkUnitTypes.TryGetValue(unit.type, out UnitData.Type newType))
			{
				type = newType;
			}
			UnitData unitData;
			gameState.GameLogicData.TryGetData(type, out unitData);
			UnitState unitState = ActionUtils.TrainUnit(gameState, playerState, tile, unitData);
			if (!unitState.HasAbility(UnitAbility.Type.Protect, gameState))
			{
				unitState.health = unit.health;
			}
			unitState.home = unit.home;
			unitState.direction = unit.direction;
			unitState.flipped = unit.flipped;
			unitState.passengerUnit = unit;
			unitState.effects = unit.effects;
			unitState.attacked = true;
			unitState.moved = true;
			// if (unitState.HasAbility(UnitAbility.Type.Stomp, gameState))
			// {
			// 	ActionUtils.StompAttack(gameState, unitState, __instance.Coordinates);
			// }
		}
		return false;
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
					Loader.LoadGameLogicDataPatch(mod, gameLogicdata, JObject.Parse(new StreamReader(new MemoryStream(file.bytes)).ReadToEnd()), embarkUnitTypes);
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
		TechItem.techTierFirebaseId.Clear();
		for (int i = 0; i <= MAX_TECH_TIER; i++)
		{
			TechItem.techTierFirebaseId.Add($"tech_research_{i}");
		}
		stopwatch.Stop();
		Plugin.logger.LogInfo($"Loaded all mods in {stopwatch.ElapsedMilliseconds}ms");
	}
}
