using BepInEx.Unity.IL2CPP.Logging;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Polytopia.Data;
using PolytopiaBackendBase.Game;
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

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameModeScreen), nameof(GameModeScreen.Init))]
    private static void GameModeScreen_Init(GameModeScreen __instance) // TODO: refactor
    {
		List<GamemodeButton> list = __instance.buttons.ToList();
		for (int i = 0; i < Loader.gamemodes.Count; i++)
		{
			Loader.GameModeButtonsInformation item = Loader.gamemodes[i];
			GamemodeButton prefab = __instance.buttons[2];
			GamemodeButton button = UnityEngine.GameObject.Instantiate(prefab);
			int buttonIndex = __instance.buttons.Length;
			list.Add(button);
			Loader.gamemodes[i] = new Loader.GameModeButtonsInformation(item.gameModeIndex, item.action, buttonIndex, item.sprite);
		};
		Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GamemodeButton> newArray = list.ToArray();
		for (int i = 0; i < __instance.buttons.Length; i++)
		{
			if(newArray[i] != null)
			{
				newArray[i].OnClicked = __instance.buttons[i].OnClicked;
			}
		}

		for (int i = 0; i < Loader.gamemodes.Count; i++)
		{
			Loader.GameModeButtonsInformation item = Loader.gamemodes[i];
			if(item.buttonIndex != null)
			{
				newArray[item.buttonIndex.Value].OnClicked = item.action;
			}
		}
		__instance.buttons = newArray;

		for (int i = 0; i < __instance.buttons.Length; i++)
		{
			GamemodeButton item = __instance.buttons[i];
			List<GamemodeButton.GamemodeButtonData> newData = item.gamemodeData.ToList();
			for (int j = 0; j < Loader.gamemodes.Count; j++)
			{
				Loader.GameModeButtonsInformation info = Loader.gamemodes[j];
				string id = EnumCache<GameMode>.GetName((GameMode)info.gameModeIndex).ToLower();
				newData.Add(new GamemodeButton.GamemodeButtonData() { gameMode = (GameMode)info.gameModeIndex, id = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(id), descriptionKey = "gamemode." + id + ".description.button", headerKey = "gamemode." + id + ".caps", icon = info.sprite});
			}
			item.gamemodeData = newData.ToArray();
			for (int j = 0; j < Loader.gamemodes.Count; j++)
			{
				Loader.GameModeButtonsInformation info = Loader.gamemodes[j];

				if(info.buttonIndex == i)
				{
					item.SetGamemode(info.buttonIndex.Value);
				}
			}
		}
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
				switch (Path.GetFileName(file.name))
				{
					case "patch.json":
						Loader.LoadGameLogicDataPatch(
							mod, 
							gameLogicdata, 
							JObject.Parse(new StreamReader(new MemoryStream(file.bytes)).ReadToEnd())
						);
						break;
					case "localization.json":
						Loader.LoadLocalizationFile(mod, file);
						break;
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

		stopwatch.Stop();
		Plugin.logger.LogInfo($"Loaded all mods in {stopwatch.ElapsedMilliseconds}ms");
	}
}
