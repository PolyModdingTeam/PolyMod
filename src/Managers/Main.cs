using BepInEx.Unity.IL2CPP.Logging;
using HarmonyLib;
using Il2CppSystem.Linq;
using Newtonsoft.Json.Linq;
using Polytopia.Data;
using PolytopiaBackendBase.Game;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using UnityEngine;
using PolytopiaBackendBase.Common;
using DG.Tweening.Core;

namespace PolyMod.Managers;

/// <summary>
/// The main manager for PolyMod, responsible for initializing the mod and patching game logic.
/// </summary>
public static class Main
{
	internal static bool dependencyCycle;

	/// <summary>
	/// The maximum tier for technology, used to extend the tech tree.
	/// </summary>
	internal const int MAX_TECH_TIER = 100;

	/// <summary>
	/// A stopwatch to measure the time taken to load mods.
	/// </summary>
	internal static readonly Stopwatch stopwatch = new();

	/// <summary>
	/// Whether the mod has been fully initialized.
	/// </summary>
	internal static bool fullyInitialized;
	
	/// <summary>
	/// A dictionary mapping unit IDs to the IDs of the units they embark into.
	/// </summary>
	internal static Dictionary<string, string> embarkNames = new();

	/// <summary>
	/// A dictionary mapping unit types to the types of the units they embark into.
	/// </summary>
	internal static Dictionary<UnitData.Type, UnitData.Type> embarkOverrides = new();

	/// <summary>
	/// Whether an embark action is currently being executed.
	/// </summary>
	internal static bool currentlyEmbarking = false;

	/// <summary>
	/// A dictionary mapping improvement IDs to the IDs of the resources they attract.
	/// </summary>
	internal static Dictionary<string, string> attractsResourceNames = new();

	/// <summary>
	/// A dictionary mapping improvement IDs to the IDs of the terrain types they attract resources on.
	/// </summary>
	internal static Dictionary<string, string> attractsTerrainNames = new();

	/// <summary>
	/// A dictionary mapping improvement types to the types of the resources they attract.
	/// </summary>
	internal static Dictionary<ImprovementData.Type, ResourceData.Type> attractsResourceOverrides = new();

	/// <summary>
	/// A dictionary mapping improvement types to the types of the terrain they attract resources on.
	/// </summary>
	internal static Dictionary<ImprovementData.Type, Polytopia.Data.TerrainData.Type> attractsTerrainOverrides = new();

	/// <summary>
	/// Patches the game logic data parsing to load PolyMod content.
	/// </summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
	private static void GameLogicData_AddGameLogicPlaceholders(GameLogicData __instance, ref JObject rootObject)
	{
		if (!fullyInitialized)
		{
			Load(__instance, rootObject);
			fullyInitialized = true;
		}
	}

	/// <summary>
	/// Patches the purchase manager to unlock custom skins.
	/// </summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsSkinUnlocked))]
	[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsSkinUnlockedInternal))]
	private static bool PurchaseManager_IsSkinUnlockedInternal(ref bool __result, SkinType skinType)
	{
		__result = (int)skinType >= Plugin.AUTOIDX_STARTS_FROM && skinType != SkinType.Test;
		return !__result;
	}

	/// <summary>
	/// Patches the purchase manager to unlock custom tribes.
	/// </summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsTribeUnlocked))]
	private static void PurchaseManager_IsTribeUnlocked(ref bool __result, TribeType type)
	{
		__result = (int)type >= Plugin.AUTOIDX_STARTS_FROM || __result;
	}

	/// <summary>
	/// Patches the steam purchase manager to unlock custom content.
	/// </summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(SteamPlatformPurchaseManager), nameof(SteamPlatformPurchaseManager.IsProductUnlocked))]
	private static bool SteamPlatformPurchaseManager_IsProductUnlocked(ref bool __result, IAPProduct iapProduct)
	{
		__result = iapProduct == null;
		return iapProduct != null;
	}

	/// <summary>
	/// Patches the purchase manager to add custom tribes to the list of unlocked tribes.
	/// </summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.GetUnlockedTribes))]
	private static void PurchaseManager_GetUnlockedTribes(
		ref Il2CppSystem.Collections.Generic.List<TribeType> __result,
		bool forceUpdate = false
	)
	{
		foreach (var tribe in Registry.customTribes) __result.Add(tribe);
	}

	/// <summary>
	/// Patches the Unity log callback to filter out spammy messages.
	/// </summary>
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

	/// <summary>
	/// Patches the game mode screen to add custom game modes.
	/// </summary>
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
			Sprite? sprite = item.sprite;
			if (item.sprite == null && item.spriteName != null)
			{
				sprite = Registry.GetSprite(item.spriteName);
			}
			Loader.gamemodes[i] = new Loader.GameModeButtonsInformation(item.gameModeIndex, item.action, __instance.buttons.Length, sprite, item.spriteName);
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

	/// <summary>
	/// Patches the tech view to correctly display the tech tree with custom technologies.
	/// </summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(TechView), nameof(TechView.CreateNode))]
	public static bool TechView_CreateNode(TechView __instance, TechData data, TechItem parentItem, float angle)
	{
		// This patch is a reimplementation of the original CreateNode method to fix layout issues with custom techs.
		// It recalculates the angles for each node to ensure they are displayed correctly.
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

	/// <summary>
	/// Patches the embark action to set a flag indicating that an embark is in progress.
	/// </summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(EmbarkAction), nameof(EmbarkAction.Execute))]
	private static bool EmbarkAction_Execute_Prefix(EmbarkAction __instance, GameState gameState)
	{
		currentlyEmbarking = true;
		return true;
	}

	/// <summary>
	/// Patches the unit training method to handle custom embark units.
	/// </summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.TrainUnit))]
	private static bool ActionUtils_TrainUnit(ref UnitState __result, GameState gameState, PlayerState playerState, TileData tile, ref UnitData unitData)
	{
		if (tile == null || tile.unit == null)
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

	/// <summary>
	/// Patches the start turn action to handle the 'Attract' ability for custom improvements.
	/// </summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(StartTurnAction), nameof(StartTurnAction.Execute))]
	private static void StartTurnAction_Execute(StartTurnAction __instance, GameState state)
	{
		// Clear any existing CreateResource actions to prevent duplicates.
		for (int i = state.ActionStack.Count - 1; i >= 0; i--)
		{
			if (state.ActionStack[i].GetActionType() == ActionType.CreateResource)
			{
				state.ActionStack.RemoveAt(i);
			}
		}

		// Iterate through all tiles owned by the current player.
		for (int i = 0; i < state.Map.Tiles.Length; i++)
		{
			TileData tileData = state.Map.Tiles[i];
			if (tileData.owner == __instance.PlayerId && tileData.improvement != null && state.CurrentTurn > 0U)
			{
				ImprovementData improvementData;
				state.GameLogicData.TryGetData(tileData.improvement.type, out improvementData);
				if (improvementData != null)
				{
					// If the improvement has the 'Attract' ability and is ready to spawn a resource.
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

						// Find a valid tile to spawn the resource on.
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

	/// <summary>
	/// Patches the unit creation method to handle custom unit prefabs.
	/// </summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(Unit), nameof(Unit.CreateUnit))]
	private static bool Unit_CreateUnit(Unit __instance, UnitData unitData, TribeType tribe, SkinType unitSkin)
	{
		Unit unit = PrefabManager.GetPrefab(unitData.type, tribe, unitSkin);
		if (unit == null) Console.Write("THIS FUCKING SHIT IS NULL WHAT THE FUCK");
		return true;
	}

	/// <summary>
	/// Initializes the Main manager.
	/// </summary>
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
		Loader.RegisterMods(Registry.mods);
		Loader.LoadMods(Registry.mods, out var cycle);
		Util.CacheReversedSpriteDataNames();
		dependencyCycle = cycle;
		stopwatch.Stop();
	}

	/// <summary>
	/// Loads all mod content.
	/// </summary>
	/// <param name="gameLogicdata">The game logic data to patch.</param>
	/// <param name="json">The JSON object representing the game logic data.</param>
	internal static void Load(GameLogicData gameLogicData, JObject json)
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
				if (Path.GetFileName(file.name) == "localization.json")
				{
					Loader.LoadLocalizationFile(mod, file);
					continue;
				}
				if (Regex.IsMatch(Path.GetFileName(file.name), @"^patch(_.*)?\.json$"))
				{
					var patchText = new StreamReader(new MemoryStream(file.bytes)).ReadToEnd();
					var template = new Api.GldConfigTemplate(patchText, mod.id);
					var text = template.Render();
					if (text is null)
					{
						mod.status = Mod.Status.Error;
						continue;
					}
					Loader.LoadGameLogicDataPatch(
						mod,
						json,
						JObject.Parse(text)
					);
					continue;
				}
				if (Regex.IsMatch(Path.GetFileName(file.name), @"^prefab(_.*)?\.json$"))
				{
					Loader.LoadPrefabInfoFile(
						mod,
						file
					);
					continue;
				}

				switch (Path.GetExtension(file.name))
				{
					case ".png":
						Loader.LoadSpriteFile(mod, file);
						break;
					case ".wav":
						Loader.LoadAudioFile(mod, file);
						break;
					case ".bundle":
						Loader.LoadAssetBundle(mod, file);
						break;
				}
			}
		}
		TechItem.techTierFirebaseId.Clear();
		for (int i = 0; i <= MAX_TECH_TIER; i++)
		{
			TechItem.techTierFirebaseId.Add($"tech_research_{i}");
		}
		Loader.ProcessGameLogicData(gameLogicData, json);
		stopwatch.Stop();
		Plugin.logger.LogInfo($"Loaded all mods in {stopwatch.ElapsedMilliseconds}ms");
	}
}
