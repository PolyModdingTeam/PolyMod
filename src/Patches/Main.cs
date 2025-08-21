using System.Diagnostics;
using BepInEx.Unity.IL2CPP.Logging;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Polytopia.Data;
using PolytopiaBackendBase.Game;
using UnityEngine;
using Patch = PolyMod.modApi.Patch;

namespace PolyMod.Patches;

internal static class Main
{
	internal const int MAX_TECH_TIER = 100;
	internal static readonly Stopwatch stopwatch = new();
	internal static bool fullyInitialized;
	internal static bool dependencyCycle;
	internal static Dictionary<string, string> embarkNames = new();
	internal static Dictionary<UnitData.Type, UnitData.Type> embarkOverrides = new();
	internal static bool currentlyEmbarking = false;
	internal static Dictionary<string, string> attractsResourceNames = new();
	internal static Dictionary<string, string> attractsTerrainNames = new();
	internal static Dictionary<ImprovementData.Type, ResourceData.Type> attractsResourceOverrides = new();
	internal static Dictionary<ImprovementData.Type, Polytopia.Data.TerrainData.Type> attractsTerrainOverrides = new();
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
	private static void GameLogicData_Parse(GameLogicData __instance, JObject rootObject)
	{ 
		if (!fullyInitialized)
		{
			Load(rootObject);
			foreach (System.Collections.Generic.KeyValuePair<int, string> item in Registry.prefabNames)
			{
				UnitData.Type unitPrefabType = UnitData.Type.Scout;
				string prefabId = item.Value;
				if (Enum.TryParse(prefabId, out UnitData.Type parsedType))
				{
					unitPrefabType = parsedType;
					PrefabManager.units.TryAdd(item.Key, PrefabManager.units[(int)unitPrefabType]);
				}
				else
				{
					KeyValuePair<Visual.PrefabInfo, Unit> prefabInfo = Registry.unitPrefabs.FirstOrDefault(kv => kv.Key.name == prefabId);
					if (!EqualityComparer<Visual.PrefabInfo>.Default.Equals(prefabInfo.Key, default))
					{
						PrefabManager.units.TryAdd(item.Key, prefabInfo.Value);
					}
					else
					{
						PrefabManager.units.TryAdd(item.Key, PrefabManager.units[(int)unitPrefabType]);
					}
				}
			}
			foreach (Visual.SkinInfo skin in Registry.skinInfo)
			{
				if (skin.skinData != null)
					__instance.skinData[(SkinType)skin.idx] = skin.skinData;
			}
			foreach (KeyValuePair<string, string> entry in embarkNames)
			{
				try
				{
					UnitData.Type unit = EnumCache<UnitData.Type>.GetType(entry.Key);
					UnitData.Type newUnit = EnumCache<UnitData.Type>.GetType(entry.Value);
					embarkOverrides[unit] = newUnit;
					Plugin.logger.LogInfo($"Embark unit type for {entry.Key} is now {entry.Value}");
				}
				catch
				{
					Plugin.logger.LogError($"Embark unit type for {entry.Key} is not valid: {entry.Value}");
				}
			}
			foreach (KeyValuePair<string, string> entry in attractsResourceNames)
			{
				try
				{
					ImprovementData.Type improvement = EnumCache<ImprovementData.Type>.GetType(entry.Key);
					ResourceData.Type resource = EnumCache<ResourceData.Type>.GetType(entry.Value);
					attractsResourceOverrides[improvement] = resource;
					Plugin.logger.LogInfo($"Improvement {entry.Key} now attracts {entry.Value}");
				}
				catch
				{
					Plugin.logger.LogError($"Improvement {entry.Key} resource type is not valid: {entry.Value}");
				}
			}
			foreach (KeyValuePair<string, string> entry in attractsTerrainNames)
			{
				try
				{
					ImprovementData.Type improvement = EnumCache<ImprovementData.Type>.GetType(entry.Key);
					Polytopia.Data.TerrainData.Type terrain = EnumCache<Polytopia.Data.TerrainData.Type>.GetType(entry.Value);
					attractsTerrainOverrides[improvement] = terrain;
					Plugin.logger.LogInfo($"Improvement {entry.Key} now attracts on {entry.Value}");
				}
				catch
				{
					Plugin.logger.LogError($"Improvement {entry.Key} terrain type is not valid: {entry.Value}");
				}
			}
			fullyInitialized = true;
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsSkinUnlocked))]
	[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsSkinUnlockedInternal))]
	private static bool PurchaseManager_IsSkinUnlockedInternal(ref bool __result, SkinType skinType)
	{
		__result = (int)skinType >= Constants.AUTOIDX_STARTS_FROM && skinType != SkinType.Test;
		return !__result;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsTribeUnlocked))]
	private static void PurchaseManager_IsTribeUnlocked(ref bool __result, TribeData.Type type)
	{
		__result = (int)type >= Constants.AUTOIDX_STARTS_FROM || __result;
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
		foreach (string stringToIgnore in Constants.LOG_MESSAGES_IGNORE)
		{
			if (logLine.Contains(stringToIgnore))
				return false;
		}
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(GameModeScreen), nameof(GameModeScreen.Init))]
	private static void GameModeScreen_Init(GameModeScreen __instance)
	{
		List<GamemodeButton> list = __instance.buttons.ToList();
		for (int i = 0; i < Loader.gamemodes.Count; i++)
		{
			var item = Loader.gamemodes[i];
			var button = GameObject.Instantiate(__instance.buttons[2]);
			list.Add(button);
			Loader.gamemodes[i] = new Loader.GameModeButtonsInformation(item.gameModeIndex, item.action, __instance.buttons.Length, item.sprite);
		}

		var newArray = list.ToArray();
		for (int i = 0; i < __instance.buttons.Length; i++)
		{
			if (newArray[i] != null) newArray[i].OnClicked = __instance.buttons[i].OnClicked;
		}

		for (int i = 0; i < Loader.gamemodes.Count; i++)
		{
			if (Loader.gamemodes[i].buttonIndex != null)
				newArray[Loader.gamemodes[i].buttonIndex!.Value].OnClicked = Loader.gamemodes[i].action;
		}

		__instance.buttons = newArray;

		for (int i = 0; i < __instance.buttons.Length; i++)
		{
			GamemodeButton button = __instance.buttons[i];
			var newData = button.gamemodeData.ToList();
			foreach (var info in Loader.gamemodes)
			{
				string id = EnumCache<GameMode>.GetName((GameMode)info.gameModeIndex).ToLower();
				newData.Add(new GamemodeButton.GamemodeButtonData()
				{
					gameMode = (GameMode)info.gameModeIndex,
					id = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(id),
					descriptionKey = "gamemode." + id + ".description.button",
					headerKey = "gamemode." + id + ".caps",
					icon = info.sprite
				});
			}
			button.gamemodeData = newData.ToArray();

			for (int j = 0; j < Loader.gamemodes.Count; j++)
			{
				Loader.GameModeButtonsInformation info = Loader.gamemodes[j];

				if (info.buttonIndex == i)
				{
					button.SetGamemode(info.buttonIndex.Value);
				}
			}
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(TechView), nameof(TechView.CreateNode))]
	public static bool TechView_CreateNode(TechView __instance, TechData data, TechItem parentItem, float angle)
	{
		GameLogicData gameLogicData = GameManager.GameState.GameLogicData;
		TribeData tribeData = gameLogicData.GetTribeData(GameManager.LocalPlayer.tribe);
		float baseAngle = 360 / gameLogicData.GetOverride(gameLogicData.GetTechData(TechData.Type.Basic), tribeData).techUnlocks.Count;
		float childAngle = 0f;
		if (parentItem != null)
			childAngle = angle + baseAngle * (data.techUnlocks.Count - 1) / 2f;
		foreach (var techData in data.techUnlocks)
		{
			if (gameLogicData.TryGetData(techData.type, out TechData techData2))
			{
				TechData @override = gameLogicData.GetOverride(techData, tribeData);
				TechItem techItem = __instance.CreateTechItem(@override, parentItem, childAngle);
				__instance.currTechIdx++;
				if (@override.techUnlocks != null && @override.techUnlocks.Count > 0)
					__instance.CreateNode(@override, techItem, childAngle);
				childAngle -= baseAngle;
			}
		}
		Il2CppSystem.Action<TechView> onItemsRefreshed = __instance.OnItemsRefreshed;
		if (onItemsRefreshed == null)
		{
			return false;
		}
		onItemsRefreshed.Invoke(__instance);
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(EmbarkAction), nameof(EmbarkAction.Execute))]
	private static bool EmbarkAction_Execute_Prefix(EmbarkAction __instance, GameState gameState)
	{
		currentlyEmbarking = true;
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.TrainUnit))]
	private static bool ActionUtils_TrainUnit(ref UnitState __result, GameState gameState, PlayerState playerState, TileData tile, ref UnitData unitData)
	{
		if (tile == null)
		{
			return true;
		}
		if (tile.unit == null)
		{
			return true;
		}
		if (currentlyEmbarking)
		{
			if (embarkOverrides.TryGetValue(tile.unit.type, out UnitData.Type newType))
			{
				gameState.GameLogicData.TryGetData(newType, out unitData);
			}
			currentlyEmbarking = false;
		}
		return true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(StartTurnAction), nameof(StartTurnAction.Execute))]
	private static void StartTurnAction_Execute(StartTurnAction __instance, GameState state)
	{
		for (int i = state.ActionStack.Count - 1; i >= 0; i--)
		{
			if (state.ActionStack[i].GetActionType() == ActionType.CreateResource)
			{
				state.ActionStack.RemoveAt(i);
			}
		}
		for (int i = 0; i < state.Map.Tiles.Length; i++)
		{
			TileData tileData = state.Map.Tiles[i];
			if (tileData.owner == __instance.PlayerId && tileData.improvement != null && state.CurrentTurn > 0U)
			{
				ImprovementData improvementData;
				state.GameLogicData.TryGetData(tileData.improvement.type, out improvementData);
				if (improvementData != null)
				{
					if (improvementData.HasAbility(ImprovementAbility.Type.Attract) && tileData.improvement.GetAge(state) % improvementData.growthRate == 0)
					{
						ResourceData.Type resourceType = ResourceData.Type.Game;
						if (attractsResourceOverrides.TryGetValue(tileData.improvement.type, out ResourceData.Type newType))
						{
							resourceType = newType;
						}
						Polytopia.Data.TerrainData.Type targetTerrain = Polytopia.Data.TerrainData.Type.Forest;
						if (attractsTerrainOverrides.TryGetValue(tileData.improvement.type, out Polytopia.Data.TerrainData.Type newTerrain))
						{
							targetTerrain = newTerrain;
						}
						foreach (TileData tileData2 in state.Map.GetArea(tileData.coordinates, 1, true, false))
						{
							if (tileData2.owner == __instance.PlayerId && tileData2.improvement == null && tileData2.resource == null && tileData2.terrain == targetTerrain)
							{
								state.ActionStack.Add(new CreateResourceAction(__instance.PlayerId, resourceType, tileData2.coordinates, CreateResourceAction.CreateReason.Attract));
								break;
							}
						}
					}
				}
			}
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Unit), nameof(Unit.CreateUnit))]
	private static bool Unit_CreateUnit(Unit __instance, UnitData unitData, TribeData.Type tribe, SkinType unitSkin)
	{
		Unit unit = PrefabManager.GetPrefab(unitData.type, tribe, unitSkin);
		if (unit == null) Console.Write("THIS FUCKING SHIT IS NULL WHAT THE FUCK");
		return true;
	}

	internal static void Init()
	{
		stopwatch.Start();
		Harmony.CreateAndPatchAll(typeof(Main));
		Mod.Manifest polytopia = new(
			"polytopia",
			"The Battle of Polytopia",
			null,
			new(Application.version.ToString()),
			new string[] { "Midjiwan AB" },
			Array.Empty<Mod.Dependency>()
		);
		Registry.mods.Add(polytopia.id, new(polytopia, Mod.Status.Success, new()));
		dependencyCycle = Loader.RegisterMods(Registry.mods);
		if (!dependencyCycle) Loader.LoadMods(Registry.mods);
		stopwatch.Stop();
	}

	internal static void Load(JObject gameLogicdata)
	{
		stopwatch.Start();
		if (!dependencyCycle) Loader.PatchGLD(gameLogicdata, Registry.mods);
		stopwatch.Stop();
		Plugin.logger.LogInfo($"Loaded all mods in {stopwatch.ElapsedMilliseconds}ms");
	}
}
