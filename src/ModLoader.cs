using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Logging;
using Cpp2IL.Core.Extensions;
using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Linq;
using LibCpp2IL;
using Newtonsoft.Json.Linq;
using PolyMod.Json;
using Polytopia.Data;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PolyMod
{
	public static class ModLoader
	{
		public class Mod
		{
			public record Dependency(string id, Version min, Version max, bool required = true);
			public record Manifest(string id, string? name, Version version, string[] authors, Dependency[]? dependencies, bool client = false);
			public record File(string name, byte[] bytes);
			public enum Status
			{
				SUCCESS,
				ERROR,
				DEPENDENCIES_UNSATISFIED,
			}

			public string? name;
			public Version version;
			public string[] authors;
			public Dependency[]? dependencies;
			public bool client;
			public Status status;
			public List<File> files;

			public Mod(Manifest manifest, Status status, List<File> files)
			{
				name = manifest.name ?? manifest.id;
				version = manifest.version;
				authors = manifest.authors;
				dependencies = manifest.dependencies;
				client = manifest.client;
				this.status = status;
				this.files = files;
			}
		}

		public class PreviewTile
		{
			[JsonInclude]
			public int? x = null;
			[JsonInclude]
			public int? y = null;
			[JsonInclude]
			[JsonConverter(typeof(EnumCacheJson<Polytopia.Data.TerrainData.Type>))]
			public Polytopia.Data.TerrainData.Type terrainType = Polytopia.Data.TerrainData.Type.Ocean;
			[JsonInclude]
			[JsonConverter(typeof(EnumCacheJson<ResourceData.Type>))]
			public ResourceData.Type resourceType = ResourceData.Type.None;
			[JsonInclude]
			[JsonConverter(typeof(EnumCacheJson<UnitData.Type>))]
			public UnitData.Type unitType = UnitData.Type.None;
			[JsonInclude]
			[JsonConverter(typeof(EnumCacheJson<ImprovementData.Type>))]
			public ImprovementData.Type improvementType = ImprovementData.Type.None;
		}

		public record DataSprite(float? pixelsPerUnit, Vector2? pivot, bool? alphaIsTransparency);

		public static int autoidx = Plugin.AUTOIDX_STARTS_FROM;
		public static Dictionary<string, Sprite> sprites = new();
		public static Dictionary<string, AudioSource> audioClips = new();
		public static Dictionary<string, Mod> mods = new();
		public static Dictionary<string, PreviewTile[]> tribePreviews = new();
		public static Dictionary<string, DataSprite> spriteDatas = new();
		private static readonly Stopwatch stopwatch = new();
		private static int maxTechTier = TechItem.techTierFirebaseId.Count - 1;
		private static string signature = string.Empty;
		private static string looseSignature = string.Empty;
		private static bool sawIncompatibilityWarning;
		private static List<TribeData.Type> customTribes = new();
		private static int climateAutoidx = (int)Enum.GetValues(typeof(TribeData.Type)).Cast<TribeData.Type>().Last();
		private static bool shouldInitializeSprites = true;


		[HarmonyPrefix]
		[HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
		private static void GameLogicData_Parse(JObject rootObject)
		{
			Load(rootObject);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsTribeUnlocked))]
		private static void PurchaseManager_IsTribeUnlocked(ref bool __result, TribeData.Type type)
		{
			__result = (int)type >= Plugin.AUTOIDX_STARTS_FROM || __result;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsSkinUnlocked))]
		private static void PurchaseManager_IsSkinUnlocked(ref bool __result, SkinType skinType)
		{
			__result = ((int)skinType >= Plugin.AUTOIDX_STARTS_FROM && (int)skinType != 2000) || __result;
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
		[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.SetAmbienceClimate))]
		private static void AudioManager_SetAmbienceClimatePrefix(ref int climate)
		{
			if (climate > 16)
			{
				climate = 1;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(SelectTribePopup), nameof(SelectTribePopup.SetDescription))]
		private static void SetDescription(SelectTribePopup __instance)
		{
			if ((int)__instance.SkinType >= Plugin.AUTOIDX_STARTS_FROM)
			{
				__instance.Description = Localization.Get(__instance.SkinType.GetLocalizationDescriptionKey()) + "\n\n" + Localization.GetSkinned(__instance.SkinType, __instance.tribeData.description2, new Il2CppSystem.Object[]
				{
					__instance.tribeName,
					Localization.Get(__instance.startTechSid, Array.Empty<Il2CppSystem.Object>())
				});
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(IL2CPPUnityLogSource), nameof(IL2CPPUnityLogSource.UnityLogCallback))]
		private static bool IL2CPPUnityLogSource_UnityLogCallback(string logLine, string exception, LogType type)
		{
			if (logLine.Contains("Failed to find atlas") && type == LogType.Warning) return false;
			if (logLine.Contains("Could not find sprite") && type == LogType.Warning) return false;
			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(GameInfoPopup), nameof(GameInfoPopup.OnMainButtonClicked))]
		private static bool GameInfoPopup_OnMainButtonClicked(GameInfoPopup __instance, int id, BaseEventData eventData)
		{
			return CheckSignatures(__instance.OnMainButtonClicked, id, eventData, __instance.gameId);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(StartScreen), nameof(StartScreen.OnResumeButtonClick))]
		private static bool StartScreen_OnResumeButtonClick(StartScreen __instance, int id, BaseEventData eventData)
		{
			return CheckSignatures(__instance.OnResumeButtonClick, id, eventData, ClientBase.GetSinglePlayerSessions()[0]);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(ClientBase), nameof(ClientBase.DeletePassAndPlayGame))]
		private static void ClientBase_DeletePassAndPlayGame(string gameId)
		{
			File.Delete(Path.Combine(Application.persistentDataPath, $"{gameId}.signatures"));
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(ClientBase), nameof(ClientBase.DeleteSinglePlayerGames))]
		private static void ClientBase_DeleteSinglePlayerGames()
		{
			foreach (var gameId in ClientBase.GetSinglePlayerSessions())
			{
				File.Delete(Path.Combine(Application.persistentDataPath, $"{gameId}.signatures"));
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(ClientBase), nameof(ClientBase.CreateSession), typeof(GameSettings), typeof(Il2CppSystem.Guid))]
		private static void ClientBase_CreateSession(GameSettings settings, Il2CppSystem.Guid gameId)
		{
			File.WriteAllLinesAsync(
				Path.Combine(Application.persistentDataPath, $"{gameId}.signatures"),
				new string[] { looseSignature, signature }
			);
		}

		private static bool CheckSignatures(Action<int, BaseEventData> action, int id, BaseEventData eventData, Il2CppSystem.Guid gameId)
		{
			if (sawIncompatibilityWarning)
			{
				sawIncompatibilityWarning = false;
				return true;
			}

			string[] signatures = { string.Empty, string.Empty };
			try
			{
				signatures = File.ReadAllLines(Path.Combine(Application.persistentDataPath, $"{gameId}.signatures"));
			}
			catch { }
			if (signatures[0] == string.Empty && signatures[1] == string.Empty) return true;
			if (Plugin.config.debug) return true;
			if (looseSignature != signatures[0])
			{
				PopupManager.GetBasicPopup(new(
					Localization.Get("polymod.signature.mismatch"),
					Localization.Get("polymod.signature.incompatible"),
					new(new PopupBase.PopupButtonData[] {
						new("OK")
					}))
				).Show();
				return false;
			}
			if (signature != signatures[1])
			{
				PopupManager.GetBasicPopup(new(
					Localization.Get("polymod.signature.mismatch"),
					Localization.Get("polymod.signature.maybe.incompatible"),
					new(new PopupBase.PopupButtonData[] {
						new(
							"OK",
							callback: (UIButtonBase.ButtonAction)((int buttonId, BaseEventData baseEventData) => {
								sawIncompatibilityWarning = true;
								action(id, eventData);
							})
						)
					}))
				).Show();
				return false;
			}
			return true;
		}

		internal static void Init()
		{
			stopwatch.Start();
			Harmony.CreateAndPatchAll(typeof(ModLoader));

			Directory.CreateDirectory(Plugin.MODS_PATH);
			string[] modContainers = Directory.GetDirectories(Plugin.MODS_PATH)
				.Union(Directory.GetFiles(Plugin.MODS_PATH, "*.polymod"))
				.Union(Directory.GetFiles(Plugin.MODS_PATH, "*.zip"))
				.ToArray();
			foreach (var modContainer in modContainers)
			{
				Mod.Manifest? manifest = null;
				List<Mod.File> files = new();

				if (Directory.Exists(modContainer))
				{
					foreach (var file in Directory.GetFiles(modContainer))
					{
						if (Path.GetFileName(file) == "manifest.json")
						{
							manifest = JsonSerializer.Deserialize<Mod.Manifest>(
								File.ReadAllBytes(file),
								new JsonSerializerOptions()
								{
									Converters = { new VersionJson() },
								}
							);
							continue;
						}
						files.Add(new(Path.GetFileName(file), File.ReadAllBytes(file)));
					}
				}
				else
				{
					foreach (var entry in new ZipArchive(File.OpenRead(modContainer)).Entries)
					{
						if (entry.FullName == "manifest.json")
						{
							manifest = JsonSerializer.Deserialize<Mod.Manifest>(
								entry.ReadBytes(),
								new JsonSerializerOptions()
								{
									Converters = { new VersionJson() },
								}
							);
							continue;
						}
						files.Add(new(entry.FullName, entry.ReadBytes()));
					}
				}

				if (manifest != null
					&& manifest.id != null
					&& Regex.IsMatch(manifest.id, @"^(?!polytopia$)[a-z_]+$")
					&& manifest.version != null
					&& manifest.authors != null
					&& manifest.authors.Length != 0
				)
				{
					if (mods.ContainsKey(manifest.id))
					{
						Plugin.logger.LogError($"Mod {manifest.id} already exists");
						continue;
					}
					mods.Add(manifest.id, new(
						manifest,
						Mod.Status.SUCCESS,
						files
					));
					Plugin.logger.LogInfo($"Registered mod {manifest.id}");
				}
				else
				{
					Plugin.logger.LogError("An invalid mod manifest was found or not found at all");
				}
			}

			StringBuilder looseSignatureString = new();
			StringBuilder signatureString = new();
			foreach (var (id, mod) in mods)
			{
				if (!mod.client && id != "polytopia")
				{
					looseSignatureString.Append(id);
					looseSignatureString.Append(mod.version.Major);

					signatureString.Append(id);
					signatureString.Append(mod.version.ToString());
				}
				foreach (var dependency in mod.dependencies ?? Array.Empty<Mod.Dependency>())
				{
					string? message = null;
					if (!mods.ContainsKey(dependency.id))
					{
						message = $"Dependency {dependency.id} not found";
					}
					else
					{
						Version version = mods[dependency.id].version;
						if (
							(dependency.min != null && version < dependency.min)
							||
							(dependency.max != null && version > dependency.max)
						)
						{
							message = $"Need dependency {dependency.id} version {dependency.min} - {dependency.max} found {version}";
						}
					}
					if (message != null)
					{
						if (dependency.required)
						{
							Plugin.logger.LogError(message);
							mod.status = Mod.Status.DEPENDENCIES_UNSATISFIED;
						}
						else
						{
							Plugin.logger.LogWarning(message);
						}
					}
				}
				if (mod.status != Mod.Status.SUCCESS) continue;
				foreach (var file in mod.files)
				{
					if (Path.GetExtension(file.name) == ".dll")
					{
						try
						{
							Assembly assembly = Assembly.Load(file.bytes);
							foreach (Type type in assembly.GetTypes())
							{
								MethodInfo? loadWithLogger = type.GetMethod("Load", new Type[] { typeof(ManualLogSource) });
								if (loadWithLogger != null)
								{
									loadWithLogger.Invoke(null, new object[]
									{
										BepInEx.Logging.Logger.CreateLogSource($"PolyMod] [{id}")
									});
									Plugin.logger.LogInfo($"Invoked Load method with logger from {type.FullName} from {id} mod");
								}
								MethodInfo? load = type.GetMethod("Load", Array.Empty<Type>());
								if (load != null)
								{
									load.Invoke(null, null);
									Plugin.logger.LogInfo($"Invoked Load method from {type.FullName} from {id} mod");
								}
							}
						}
						catch (TargetInvocationException exception)
						{
							if (exception.InnerException != null)
							{
								Plugin.logger.LogError($"Error on loading assembly from {id} mod: {exception.InnerException.Message}");
								mod.status = Mod.Status.ERROR;
							}
						}
					}
					if (Path.GetFileName(file.name) == "sprites.json")
					{
						try
						{
							spriteDatas = spriteDatas
								.Concat(JsonSerializer.Deserialize<Dictionary<string, DataSprite>>(
									file.bytes,
									new JsonSerializerOptions()
									{
										Converters = { new Vector2Json() },
									}
								)!)
								.ToDictionary(e => e.Key, e => e.Value);
							Plugin.logger.LogInfo($"Registried sprite data from {id} mod");
						}
						catch (Exception e)
						{
							Plugin.logger.LogError($"Error on loading sprite data from {id} mod: {e.Message}");
						}
					}
				}
			}
			looseSignature = Plugin.Hash(looseSignatureString);
			signature = Plugin.Hash(signatureString);

			stopwatch.Stop();
		}

		internal static void Load(JObject gameLogicdata)
		{
			stopwatch.Start();
			GameManager.GetSpriteAtlasManager().cachedSprites.TryAdd("Heads", new());
			Mod.Manifest polytopia = new(
				"polytopia",
				"The Battle of Polytopia",
				new(VersionManager.SemanticVersion.ToString()),
				new string[] { "Midjiwan AB" },
				Array.Empty<Mod.Dependency>()
			);
			mods.Add(polytopia.id, new(polytopia, Mod.Status.SUCCESS, new()));
			LocalizationPatch(JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
				Plugin.GetResource("localization.json")
			)!);

			foreach (var (id, mod) in mods)
			{
				if (mod.status != Mod.Status.SUCCESS) continue;
				foreach (var file in mod.files)
				{
					if (Path.GetFileName(file.name) == "patch.json")
					{
						try
						{
							GameLogicDataPatch(gameLogicdata, JObject.Parse(new StreamReader(new MemoryStream(file.bytes)).ReadToEnd()));
							Plugin.logger.LogInfo($"Registried patch from {id} mod");
						}
						catch (Exception e)
						{
							Plugin.logger.LogError($"Error on loading patch from {id} mod: {e.Message}");
							mod.status = Mod.Status.ERROR;
						}
					}
					if (Path.GetFileName(file.name) == "localization.json")
					{
						try
						{
							LocalizationPatch(JsonSerializer
								.Deserialize<Dictionary<string, Dictionary<string, string>>>(file.bytes)!);
							Plugin.logger.LogInfo($"Registried localization from {id} mod");
						}
						catch (Exception e)
						{
							Plugin.logger.LogError($"Error on loading locatization from {id} mod: {e.Message}");
						}
					}
					if (Path.GetExtension(file.name) == ".png" && shouldInitializeSprites)
					{
						string name = Path.GetFileNameWithoutExtension(file.name);
						Vector2 pivot = name.Split("_")[0] switch
						{
							"field" => new(0.5f, 0.0f),
							"mountain" => new(0.5f, -0.375f),
							_ => new(0.5f, 0.5f),
						};
						float pixelsPerUnit = 2112f;
						if (spriteDatas.ContainsKey(name))
						{
							DataSprite spriteData = spriteDatas[name];
							pivot = spriteData.pivot ?? pivot;
							pixelsPerUnit = spriteData.pixelsPerUnit ?? pixelsPerUnit;
						}
						Sprite sprite = SpritesLoader.BuildSprite(file.bytes, pivot, pixelsPerUnit);
						GameManager.GetSpriteAtlasManager().cachedSprites["Heads"].Add(name, sprite);
						sprites.Add(name, sprite);
					}
					if (Path.GetExtension(file.name) == ".wav")
					{
						AudioSource audioSource = new GameObject().AddComponent<AudioSource>();
						GameObject.DontDestroyOnLoad(audioSource);
						audioSource.clip = AudioClipLoader.BuildAudioClip(file.bytes);
						audioClips.Add(Path.GetFileNameWithoutExtension(file.name), audioSource);
					}
				}
			}

			TechItem.techTierFirebaseId.Clear();
			for (int i = 0; i <= maxTechTier; i++)
			{
				TechItem.techTierFirebaseId.Add($"tech_research_{i}");
			}
			shouldInitializeSprites = false;
			stopwatch.Stop();
			Plugin.logger.LogInfo($"Loaded all mods in {stopwatch.ElapsedMilliseconds}ms");
		}

		private static void GameLogicDataPatch(JObject gld, JObject patch)
		{
			foreach (JToken jtoken in patch.SelectTokens("$.tribeData.*").ToArray())
			{
				JObject token = jtoken.Cast<JObject>();

				if (token["skins"] != null)
				{
					JArray skins = token["skins"].Cast<JArray>();
					Dictionary<string, int> skinsToReplace = new();

					foreach (var skin in skins._values)
					{
						string skinValue = skin.ToString();

						if (!Enum.TryParse<SkinType>(skinValue, out _))
						{
							EnumCache<SkinType>.AddMapping(skinValue, (SkinType)autoidx);
							skinsToReplace[skinValue] = autoidx;
							Plugin.logger.LogInfo("Created mapping for skinType with id " + skinValue + " and index " + autoidx);
							autoidx++;
						}
					}

					foreach (var entry in skinsToReplace)
					{
						if (skins._values.Contains(entry.Key))
						{
							skins._values.Remove(entry.Key);
							skins._values.Add(entry.Value);
						}
					}

					JToken originalSkins = gld.SelectToken(skins.Path, false);
					if (originalSkins != null)
					{
						skins.Merge(originalSkins);
					}
				}
			}

			foreach (JToken jtoken in patch.SelectTokens("$.*.*").ToArray())
			{
				JObject token = jtoken.Cast<JObject>();

				if (token["idx"] != null && (int)token["idx"] == -1)
				{
					string id = GetJTokenName(token);
					string dataType = GetJTokenName(token, 2);
					token["idx"] = autoidx;
					switch (dataType)
					{
						case "tribeData":
							EnumCache<TribeData.Type>.AddMapping(id, (TribeData.Type)autoidx);
							EnumCache<TribeData.Type>.AddMapping(id, (TribeData.Type)autoidx);
							customTribes.Add((TribeData.Type)autoidx);
							token["style"] = climateAutoidx;
							token["climate"] = climateAutoidx;
							climateAutoidx++;
							break;
						case "techData":
							int cost = (int)token["cost"];
							if (cost > maxTechTier) maxTechTier = cost;
							EnumCache<TechData.Type>.AddMapping(id, (TechData.Type)autoidx);
							EnumCache<TechData.Type>.AddMapping(id, (TechData.Type)autoidx);
							break;
						case "unitData":
							EnumCache<UnitData.Type>.AddMapping(id, (UnitData.Type)autoidx);
							EnumCache<UnitData.Type>.AddMapping(id, (UnitData.Type)autoidx);
							UnitData.Type unitPrefabType = UnitData.Type.Scout;
							if (token["prefab"] != null)
							{
								TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
								string prefabId = textInfo.ToTitleCase(token["prefab"].ToString());
								if (Enum.TryParse(prefabId, out UnitData.Type parsedType))
								{
									unitPrefabType = parsedType;
								}
							}
							PrefabManager.units.TryAdd((int)(UnitData.Type)autoidx, PrefabManager.units[(int)unitPrefabType]);
							break;
						case "improvementData":
							EnumCache<ImprovementData.Type>.AddMapping(id, (ImprovementData.Type)autoidx);
							EnumCache<ImprovementData.Type>.AddMapping(id, (ImprovementData.Type)autoidx);
							ImprovementData.Type improvementPrefabType = ImprovementData.Type.CustomsHouse;
							if (token["prefab"] != null)
							{
								TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
								string prefabId = textInfo.ToTitleCase(token["prefab"].ToString());
								if (Enum.TryParse(prefabId, out ImprovementData.Type parsedType))
								{
									improvementPrefabType = parsedType;
								}
							}
							PrefabManager.improvements.TryAdd((ImprovementData.Type)autoidx, PrefabManager.improvements[improvementPrefabType]);
							break;
						case "terrainData":
							EnumCache<Polytopia.Data.TerrainData.Type>.AddMapping(id, (Polytopia.Data.TerrainData.Type)autoidx);
							EnumCache<Polytopia.Data.TerrainData.Type>.AddMapping(id, (Polytopia.Data.TerrainData.Type)autoidx);
							break;
						case "resourceData":
							EnumCache<ResourceData.Type>.AddMapping(id, (ResourceData.Type)autoidx);
							EnumCache<ResourceData.Type>.AddMapping(id, (ResourceData.Type)autoidx);
							ResourceData.Type resourcePrefabType = ResourceData.Type.Game;
							if (token["prefab"] != null)
							{
								TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
								string prefabId = textInfo.ToTitleCase(token["prefab"].ToString());
								if (Enum.TryParse(prefabId, out ResourceData.Type parsedType))
								{
									resourcePrefabType = parsedType;
								}
							}
							PrefabManager.resources.TryAdd((ResourceData.Type)autoidx, PrefabManager.resources[resourcePrefabType]);
							break;
						case "taskData":
							EnumCache<TaskData.Type>.AddMapping(id, (TaskData.Type)autoidx);
							EnumCache<TaskData.Type>.AddMapping(id, (TaskData.Type)autoidx);
							break;
						default:
							continue;
					}
					Plugin.logger.LogInfo("Created mapping for " + dataType + " with id " + id + " and index " + autoidx);
					autoidx++;
				}
			}
			foreach (JToken jtoken in patch.SelectTokens("$.tribeData.*").ToArray())
			{
				JObject token = jtoken.Cast<JObject>();

				if (token["preview"] != null)
				{
					PreviewTile[] preview = JsonSerializer.Deserialize<PreviewTile[]>(token["preview"].ToString())!;
					tribePreviews[GetJTokenName(token)] = preview;
				}
			}
			gld.Merge(patch, new() { MergeArrayHandling = MergeArrayHandling.Replace, MergeNullValueHandling = MergeNullValueHandling.Merge });
		}

		private static void LocalizationPatch(Dictionary<string, Dictionary<string, string>> localization)
		{
			foreach (var (key, data) in localization)
			{
				string name = key.Replace("_", ".");
				if (name.StartsWith("tribeskins")) name = "TribeSkins/" + name;
				TermData term = LocalizationManager.Sources[0].AddTerm(name);
				List<string> strings = new();
				foreach (string language in LocalizationManager.GetAllLanguages(false))
				{
					strings.Add(data.GetOrDefault(language, data.GetOrDefault("English", term.Term))!);
				}
				term.Languages = new Il2CppStringArray(strings.ToArray());
			}
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

		public static string GetJTokenName(JToken token, int n = 1)
		{
			return token.Path.Split('.')[^n];
		}
	}
}
