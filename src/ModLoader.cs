using Cpp2IL.Core.Extensions;
using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Linq;
using LibCpp2IL;
using Newtonsoft.Json.Linq;
using PolyMod.Json;
using Polytopia.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Unity.VectorGraphics.External.LibTessDotNet;
using UnityEngine;

namespace PolyMod
{
	public static class ModLoader
	{
		public class Mod
		{
			public record Dependency(string id, Version min, Version max, bool required = true);
			public record Manifest(string id, Version version, string[] authors, Dependency[] dependencies);
			public record File(string name, byte[] bytes);
			public enum Status { SUCCESS, ERROR };

			public Version version;
			public string[] authors;
			public Dependency[] dependencies;
			public Status status;
			public List<File> files;

			public Mod(Manifest manifest, Status status, List<File> files)
			{
				version = manifest.version;
				authors = manifest.authors;
				dependencies = manifest.dependencies;
				this.status = status;
				this.files = files;
			}

			public string GetPrettyStatus()
			{
				return status switch
				{
					Status.SUCCESS => "loaded successfully",
					Status.ERROR => "had loading error",
					_ => throw new InvalidOperationException(),
				};
			}
		}

		public class PreviewTile
		{
			public int? x = null;
			public int? y = null;
			[JsonConverter(typeof(EnumCacheJson<Polytopia.Data.TerrainData.Type>))]
			public Polytopia.Data.TerrainData.Type terrainType = Polytopia.Data.TerrainData.Type.Ocean;
			[JsonConverter(typeof(EnumCacheJson<ResourceData.Type>))]
			public ResourceData.Type resourceType = ResourceData.Type.None;
			[JsonConverter(typeof(EnumCacheJson<UnitData.Type>))]
			public UnitData.Type unitType = UnitData.Type.None;
			[JsonConverter(typeof(EnumCacheJson<ImprovementData.Type>))]
			public ImprovementData.Type improvementType = ImprovementData.Type.None;
		}

		private static readonly Stopwatch stopwatch = new();
		public static int autoidx = Plugin.AUTOIDX_STARTS_FROM;
		public static Dictionary<string, Sprite> sprites = new();
		public static Dictionary<string, AudioSource> audioClips = new();
		public static Dictionary<string, Mod> mods = new();
		public static Dictionary<int, int> climateToTribeData = new();
		public static Dictionary<string, List<PreviewTile>> tribePreviews = new();
		public static int climateAutoidx = (int)Enum.GetValues(typeof(TribeData.Type)).Cast<TribeData.Type>().Last();
		public static bool shouldInitializeSprites = true;


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

		internal static void Init()
		{
			stopwatch.Start();
			Harmony.CreateAndPatchAll(typeof(ModLoader));

			Directory.CreateDirectory(Plugin.MODS_PATH);
			string[] modFiles = Directory.GetDirectories(Plugin.MODS_PATH)
				.Union(Directory.GetFiles(Plugin.MODS_PATH, "*.polymod"))
				.Union(Directory.GetFiles(Plugin.MODS_PATH, "*.zip"))
				.ToArray();
			foreach (var modFile in modFiles)
			{
				Mod.Manifest? manifest = null;
				List<Mod.File> files = new();

				if (Directory.Exists(modFile))
				{
					foreach (var file in Directory.GetFiles(modFile))
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
					foreach (var entry in new ZipArchive(File.OpenRead(modFile)).Entries)
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
					&& Regex.IsMatch(manifest.id, @"^[a-zA-Z_]+$")
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
					Plugin.logger.LogError("Error on registering mod");
				}
			}

			foreach (var (id, mod) in mods)
			{
				foreach (var dependency in mod.dependencies ?? Array.Empty<Mod.Dependency>())
				{
					string? message = null;
					if (!mods.ContainsKey(dependency.id))
					{
						message = $"Dependency {dependency.id} not found";
					}
					Version version = mods[dependency.id].version;
					if (
						(dependency.min != null && version < dependency.min)
						||
						(dependency.max != null && version > dependency.max)
					)
					{
						message = $"Need dependency {dependency.id} version {dependency.min} - {dependency.max} found {version}";
					}
					if (message != null)
					{
						if (dependency.required)
						{
							Plugin.logger.LogError(message);
							mod.status = Mod.Status.ERROR;
						}
						Plugin.logger.LogWarning(message);
					}
				}
				foreach (var file in mod.files)
				{
					if (Path.GetExtension(file.name) == ".dll")
					{
						try
						{
							Assembly assembly = Assembly.Load(file.bytes);
							foreach (Type type in assembly.GetTypes())
							{
								MethodInfo? method = type.GetMethod("Load");
								if (method != null)
								{
									method.Invoke(null, null);
									Plugin.logger.LogInfo($"Invoked Load method from {assembly.GetName().Name} assembly from {id} mod");
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
				}
			}

			stopwatch.Stop();
		}

		internal static void Load(JObject gameLogicdata)
		{
			stopwatch.Start();
			GameManager.GetSpriteAtlasManager().cachedSprites.TryAdd("Heads", new());

			foreach (var (id, mod) in mods)
			{
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
							foreach(var (key, data) in JsonSerializer
								.Deserialize<Dictionary<string, Dictionary<string, string>>>(file.bytes)!
							)
							{
								string name = key.Replace("_", ".");
								if (name.StartsWith("tribeskins")) name = "TribeSkins/" + name;
								TermData term = LocalizationManager.Sources[0].AddTerm(name);
								List<string> strings = new();
								foreach (string language in LocalizationManager.GetAllLanguages())
								{
									if (data.TryGetValue(language, out string? localized))
									{
										strings.Add(localized);
									}
									else
									{
										strings.Add(term.Term);
									}
								}
								term.Languages = new Il2CppStringArray(strings.ToArray());
							}
							Plugin.logger.LogInfo($"Registried localization from {id} mod");
						}
						catch (Exception e)
						{
							Plugin.logger.LogError($"Found invalid localization in {id} mod: {e.Message}");
						}
					}
					if (Path.GetExtension(file.name) == ".png" && shouldInitializeSprites)
					{
						Vector2 pivot = Path.GetFileNameWithoutExtension(file.name).Split("_")[0] switch
						{
							"field" => new(0.5f, 0.0f),
							"mountain" => new(0.5f, -0.375f),
							_ => new(0.5f, 0.5f),
						};
						float pixelsPerUnit = Path.GetFileNameWithoutExtension(file.name).Split("_")[0] switch
						{
							"field" => 256f,
							"forest" => 280f,
							"mountain" => 240f,
							"game" => 512f,
							"fruit" => 256f,
							"house" => 300f,
							_ => 2048f,
						};
						Sprite sprite = SpritesLoader.BuildSprite(file.bytes, pivot, pixelsPerUnit);
						GameManager.GetSpriteAtlasManager().cachedSprites["Heads"].Add(Path.GetFileNameWithoutExtension(file.name), sprite);
						sprites.Add(Path.GetFileNameWithoutExtension(file.name), sprite);
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
							climateToTribeData[climateAutoidx++] = autoidx;
							break;
						case "techData":
							EnumCache<TechData.Type>.AddMapping(id, (TechData.Type)autoidx);
							break;
						case "unitData":
							EnumCache<UnitData.Type>.AddMapping(id, (UnitData.Type)autoidx);
							UnitData.Type unitPrefabType = UnitData.Type.Scout;
							if(token["prefab"] != null)
							{
								TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
								string prefabId = textInfo.ToTitleCase(token["prefab"].ToString());
								if(Enum.TryParse(prefabId, out UnitData.Type parsedType))
								{
									unitPrefabType = parsedType;
								}
							}
							PrefabManager.units.TryAdd((int)(UnitData.Type)autoidx, PrefabManager.units[(int)unitPrefabType]);
							break;
						case "improvementData":
							EnumCache<ImprovementData.Type>.AddMapping(id, (ImprovementData.Type)autoidx);
							ImprovementData.Type improvementPrefabType = ImprovementData.Type.CustomsHouse;
							if(token["prefab"] != null)
							{
								TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
								string prefabId = textInfo.ToTitleCase(token["prefab"].ToString());
								if(Enum.TryParse(prefabId, out ImprovementData.Type parsedType))
								{
									improvementPrefabType = parsedType;
								}
							}
							PrefabManager.improvements.TryAdd((ImprovementData.Type)autoidx, PrefabManager.improvements[improvementPrefabType]);
							break;
						case "terrainData":
							EnumCache<Polytopia.Data.TerrainData.Type>.AddMapping(id, (Polytopia.Data.TerrainData.Type)autoidx);
							break;
						case "resourceData":
							EnumCache<ResourceData.Type>.AddMapping(id, (ResourceData.Type)autoidx);
							ResourceData.Type resourcePrefabType = ResourceData.Type.Game;
							if(token["prefab"] != null)
							{
								TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
								string prefabId = textInfo.ToTitleCase(token["prefab"].ToString());
								if(Enum.TryParse(prefabId, out ResourceData.Type parsedType))
								{
									resourcePrefabType = parsedType;
								}
							}
							PrefabManager.resources.TryAdd((ResourceData.Type)autoidx, PrefabManager.resources[resourcePrefabType]);
							break;
						case "taskData":
							EnumCache<TaskData.Type>.AddMapping(id, (TaskData.Type)autoidx);
							break;
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
					List<PreviewTile>? preview = JsonSerializer.Deserialize<List<PreviewTile>>(token["preview"].ToString());

					if (preview != null)
					{
						tribePreviews[GetJTokenName(token)] = preview;
					}
				}
			}
			gld.Merge(patch, new() { MergeArrayHandling = MergeArrayHandling.Replace, MergeNullValueHandling = MergeNullValueHandling.Merge });
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
