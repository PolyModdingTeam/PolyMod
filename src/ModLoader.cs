﻿using Cpp2IL.Core.Extensions;
using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Linq;
using Newtonsoft.Json.Linq;
using Polytopia.Data;
using System.IO.Compression;
using System.Reflection;
using UnityEngine;

namespace PolyMod
{
	internal static class ModLoader
	{
		private static List<JObject> _patches = new();
		private static Dictionary<string, byte[]> _textures = new();
		public static Dictionary<string, Sprite> sprites = new();
		private static Dictionary<string, AudioClip> _audios = new();
		public static Dictionary<string, int> gldDictionary = new ();
		public static int tribesCount = (int)Enum.GetValues(typeof(TribeData.Type)).Cast<TribeData.Type>().Last();
		public static int techCount = (int)Enum.GetValues(typeof(TechData.Type)).Cast<TechData.Type>().Last();
		public static int unitCount = (int)Enum.GetValues(typeof(UnitData.Type)).Cast<UnitData.Type>().Last();
		public static int improvementsCount = (int)Enum.GetValues(typeof(ImprovementData.Type)).Cast<ImprovementData.Type>().Last();
		public static int terrainCount = (int)Enum.GetValues(typeof(Polytopia.Data.TerrainData.Type)).Cast<Polytopia.Data.TerrainData.Type>().Last();
		public static int resourceCount = (int)Enum.GetValues(typeof(ResourceData.Type)).Cast<ResourceData.Type>().Last();
		public static int taskCount = (int)Enum.GetValues(typeof(TaskData.Type)).Cast<TaskData.Type>().Last();
		//public static int skinsCount = Enum.GetValues(typeof(SkinType)).Length;

		[HarmonyPrefix]
		[HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
		private static void GameLogicData_Parse(JObject rootObject)
		{
			//EnumCache<SkinType>.AddMapping("Druid", (SkinType)1000);
			Load(rootObject);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsTribeUnlocked))]
		private static void PurchaseManager_IsTribeUnlocked(ref bool __result, TribeData.Type type)
		{
			__result = (int)type >= ((int)Enum.GetValues(typeof(TribeData.Type)).Cast<TribeData.Type>().Last() + 1) || __result;
		}

		public static void Init()
		{
			Harmony.CreateAndPatchAll(typeof(ModLoader));
			Directory.CreateDirectory(Plugin.MODS_PATH);
			string[] mods = Directory.GetFiles(Plugin.MODS_PATH, "*.polymod").Union(Directory.GetFiles(Plugin.MODS_PATH, "*.polytale")).Union(Directory.GetFiles(Plugin.MODS_PATH, "*.zip")).ToArray();
			foreach (string modname in mods)
			{
				ZipArchive mod = new(File.OpenRead(modname));

				foreach (var entry in mod.Entries)
				{
					string name = entry.ToString();

					if (Path.GetExtension(name) == ".dll")
					{
						PolyscriptLoad(entry.ReadBytes());
					}
					if (Path.GetFileName(name) == "patch.json")
					{
						_patches.Add(JObject.Parse(new StreamReader(entry.Open()).ReadToEnd()));
					}
					if (Path.GetExtension(name) == ".png")
					{
						_textures.Add(name, entry.ReadBytes());
					}
				}
			}
		}

		public static void Load(JObject gameLogicdata)
		{
			GameManager.GetSpriteAtlasManager().cachedSprites.TryAdd("Heads", new());
			foreach (var patch in _patches){
				try
				{
					GameLogicDataPatch(gameLogicdata, patch);
				} catch {}
			}
			foreach (var sprite_ in _textures){
				Vector2 pivot = Path.GetFileNameWithoutExtension(sprite_.Key).Split("_")[0] switch
				{
					"field" => new(0.5f, 0.0f),
					"mountain" => new(0.5f, -0.375f),
					_ => new(0.5f, 0.5f),
				};
				Sprite sprite = Api.BuildSprite(sprite_.Value, pivot);
				GameManager.GetSpriteAtlasManager().cachedSprites["Heads"].Add(Path.GetFileNameWithoutExtension(sprite_.Key), sprite);
				sprites.Add(Path.GetFileNameWithoutExtension(sprite_.Key), sprite);
			}
		}

		private static void PolyscriptLoad(byte[] polyscriptData)
		{
			try{
				Assembly assembly = Assembly.Load(polyscriptData);
				foreach (Type type in assembly.GetTypes())
				{
					type.GetMethod("Load")?.Invoke(null, null);
				}
			}
			catch(Exception exception){
				Plugin.logger.LogInfo(exception.Message);
			}
		}
		private static void GameLogicDataPatch(JObject gld, JObject patch)
		{
			//Console.Write(skinsCount);
			try
			{
				foreach (JToken jtoken in patch.SelectTokens("$.localizationData.*").ToArray())
				{
					JObject token = jtoken.Cast<JObject>();
					TermData term = LocalizationManager.Sources[0].AddTerm(Api.GetJTokenName(token).Replace('_', '.'));

					List<string> strings = new List<string>();
					Il2CppSystem.Collections.Generic.List<string> availableLanguages = LocalizationManager.GetAllLanguages();

					foreach (string language in availableLanguages)
					{
						if (token.TryGetValue(language, out JToken localizedString))
						{
							strings.Add((string)localizedString);
						}
						else
						{
							strings.Add(term.Term);
						}
					}
					term.Languages = new Il2CppStringArray(strings.ToArray());
				}

				patch.Remove("localizationData");

				foreach (JToken jtoken in patch.SelectTokens("$.*.*").ToArray())
				{
					JObject token = jtoken.Cast<JObject>();

					if (token["idx"] != null && (int)token["idx"] == -1)
					{
						string id = Api.GetJTokenName(token);
						string dataType = Api.GetJTokenName(token, 2);
						Plugin.logger.LogInfo("Loading object of " + dataType + " with id: " + id);
						switch (dataType)
						{
							case "tribeData":
								++tribesCount;
								token["idx"] = tribesCount;
								gldDictionary[id] = tribesCount;
								EnumCache<TribeData.Type>.AddMapping(id, (TribeData.Type)tribesCount);
								break;
							case "techData":
								++techCount;
								token["idx"] = techCount;
								gldDictionary[id] = techCount;
								EnumCache<TechData.Type>.AddMapping(id, (TechData.Type)techCount);
								break;
							case "unitData":
								++unitCount;
								token["idx"] = unitCount;
								gldDictionary[id] = unitCount;
								EnumCache<UnitData.Type>.AddMapping(id, (UnitData.Type)unitCount);
								PrefabManager.units.TryAdd((int)(UnitData.Type)unitCount, PrefabManager.units[(int)UnitData.Type.Scout]);
								break;
							case "improvementData":
								++improvementsCount;
								token["idx"] = improvementsCount;
								gldDictionary[id] = improvementsCount;
								EnumCache<ImprovementData.Type>.AddMapping(id, (ImprovementData.Type)improvementsCount);
								PrefabManager.improvements.TryAdd((ImprovementData.Type)improvementsCount, PrefabManager.improvements[ImprovementData.Type.CustomsHouse]);
								break;
							case "terrainData":
								++terrainCount;
								token["idx"] = terrainCount;
								gldDictionary[id] = terrainCount;
								EnumCache<Polytopia.Data.TerrainData.Type>.AddMapping(id, (Polytopia.Data.TerrainData.Type)terrainCount);
								break;
							case "resourceData":
								++resourceCount;
								token["idx"] = resourceCount;
								gldDictionary[id] = resourceCount;
								EnumCache<ResourceData.Type>.AddMapping(id, (ResourceData.Type)resourceCount);
								PrefabManager.resources.TryAdd((ResourceData.Type)resourceCount, PrefabManager.resources[ResourceData.Type.Game]);
								break;
							case "taskData":
								++taskCount;
								token["idx"] = taskCount;
								gldDictionary[id] = taskCount;
								EnumCache<TaskData.Type>.AddMapping(id, (TaskData.Type)taskCount);
								break;
						}
					}
				}

				gld.Merge(patch, Plugin.GLD_MERGE_SETTINGS);
			}
			catch (Exception ex)
			{
				Plugin.logger.LogError(ex.Message);
			}
		}
	}
}
