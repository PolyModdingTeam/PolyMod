using BepInEx.Unity.IL2CPP.Logging;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Polytopia.Data;
using PolytopiaBackendBase.Game;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PolyMod.Managers;
public static class Main
{
	internal const int MAX_TECH_TIER = 100;
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
	public static bool TechView_CreateNode(TechView __instance, TechData data, TechItem parentItem, float angle) {
		float baseAngle = 360 / GameManager.GameState.GameLogicData.GetTechData(TechData.Type.Basic).techUnlocks.Count;
		float childAngle = 0f;
		if (parentItem != null)
		{
			childAngle = angle + baseAngle * (data.techUnlocks.Count - 1) / 2f;
		}
		GameLogicData gameLogicData = GameManager.GameState.GameLogicData;
		TribeData tribeData = gameLogicData.GetTribeData(GameManager.LocalPlayer.tribe);
		foreach (TechData techData in data.techUnlocks)
		{
			if (gameLogicData.TryGetData(techData.type, out TechData techData2))
			{
				TechData @override = GameManager.GameState.GameLogicData.GetOverride(techData, tribeData);
				TechItem techItem = __instance.CreateTechItem(@override, parentItem, childAngle);
				__instance.currTechIdx++;
				if (@override.techUnlocks != null && @override.techUnlocks.Count > 0)
				{
					__instance.CreateNode(@override, techItem, childAngle);
				}
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
		Loader.LoadMods(Registry.mods);
		dependencyCycle = !Loader.SortMods(Registry.mods);
		if (dependencyCycle) return;

		StringBuilder checksumString = new();
		foreach (var (id, mod) in Registry.mods)
		{
			if (mod.status != Mod.Status.Success) continue;
			foreach (var file in mod.files)
			{
				checksumString.Append(JsonSerializer.Serialize(file));
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
				checksumString.Append(id);
				checksumString.Append(mod.version.ToString());
			}
		}
		Compatibility.HashSignatures(checksumString);

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
				if (Path.GetFileName(file.name) == "localization.json")
				{
					Loader.LoadLocalizationFile(mod, file);
					continue;
				}
				if (Regex.IsMatch(Path.GetFileName(file.name), @"^patch(_.*)?\.json$"))
				{
					Loader.LoadGameLogicDataPatch(
						mod,
						gameLogicdata,
						JObject.Parse(new StreamReader(new MemoryStream(file.bytes)).ReadToEnd())
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
		stopwatch.Stop();
		Plugin.logger.LogInfo($"Loaded all mods in {stopwatch.ElapsedMilliseconds}ms");
	}
}
