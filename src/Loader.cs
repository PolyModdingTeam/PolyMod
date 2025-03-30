using BepInEx.Logging;
using Cpp2IL.Core.Extensions;
using Il2CppSystem.Linq;
using MonoMod.Utils;
using Newtonsoft.Json.Linq;
using PolyMod.Json;
using PolyMod.Managers;
using Polytopia.Data;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PolyMod;
public static class Loader
{
	internal static Dictionary<string, Type> typeMappings = new()
	{
		{ "tribeData", typeof(TribeData.Type) },
		{ "techData", typeof(TechData.Type) },
		{ "unitData", typeof(UnitData.Type) },
		{ "improvementData", typeof(ImprovementData.Type) },
		{ "terrainData", typeof(Polytopia.Data.TerrainData.Type) },
		{ "resourceData", typeof(ResourceData.Type) },
		{ "taskData", typeof(TaskData.Type) },
		{ "tribeAbility", typeof(TribeAbility.Type) },
		{ "unitAbility", typeof(UnitAbility.Type) },
		{ "improvementAbility", typeof(ImprovementAbility.Type) },
		{ "playerAbility", typeof(PlayerAbility.Type) }
	};

	public static void AddPatchDataType(string typeId, Type type)
	{
		if (!typeMappings.ContainsKey(typeId))
			typeMappings.Add(typeId, type);
	}

	internal static void LoadMods(Dictionary<string, Mod> mods)
	{
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
					Mod.Status.Success,
					files
				));
				Plugin.logger.LogInfo($"Registered mod {manifest.id}");
			}
			else
			{
				Plugin.logger.LogError("An invalid mod manifest was found or not found at all");
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
						mod.status = Mod.Status.DependenciesUnsatisfied;
					}
					else
					{
						Plugin.logger.LogWarning(message);
					}
				}
			}
		}
	}

	internal static bool SortMods(Dictionary<string, Mod> mods)
	{
		Stopwatch s = new();
		Dictionary<string, List<string>> graph = new();
		Dictionary<string, int> inDegree = new();
		Dictionary<string, Mod> successfulMods = new();
		Dictionary<string, Mod> unsuccessfulMods = new();
		foreach (var (id, mod) in mods)
		{
			if (mod.status == Mod.Status.Success) successfulMods.Add(id, mod);
			else unsuccessfulMods.Add(id, mod);
		}
		foreach (var (id, _) in successfulMods)
		{
			graph[id] = new();
			inDegree[id] = 0;
		}
		foreach (var (id, mod) in successfulMods)
		{
			foreach (var dependency in mod.dependencies ?? Array.Empty<Mod.Dependency>())
			{
				graph[dependency.id].Add(id);
				inDegree[id]++;
			}
		}
		Queue<string> queue = new();
		foreach (var (id, _) in successfulMods)
		{
			if (inDegree[id] == 0)
			{
				queue.Enqueue(id);
			}
		}
		Dictionary<string, Mod> sorted = new();
		while (queue.Count > 0)
		{
			var id = queue.Dequeue();
			var mod = successfulMods[id];
			sorted.Add(id, mod);
			foreach (var neighbor in graph[id])
			{
				inDegree[neighbor]--;
				if (inDegree[neighbor] == 0)
				{
					queue.Enqueue(neighbor);
				}
			}
		}
		if (sorted.Count != successfulMods.Count)
		{
			return false;
		}
		mods.Clear();
		mods.AddRange(sorted);
		mods.AddRange(unsuccessfulMods);

		return true;
	}

	public static void LoadAssemblyFile(Mod mod, Mod.File file)
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
						BepInEx.Logging.Logger.CreateLogSource($"PolyMod] [{mod.name}")
					});
					Plugin.logger.LogInfo($"Invoked Load method with logger from {type.FullName} from {mod.name} mod");
				}
				MethodInfo? load = type.GetMethod("Load", Array.Empty<Type>());
				if (load != null)
				{
					load.Invoke(null, null);
					Plugin.logger.LogInfo($"Invoked Load method from {type.FullName} from {mod.name} mod");
				}
			}
		}
		catch (TargetInvocationException exception)
		{
			if (exception.InnerException != null)
			{
				Plugin.logger.LogError($"Error on loading assembly from {mod.name} mod: {exception.InnerException.Message}");
				mod.status = Mod.Status.Error;
			}
		}
	}

	public static void LoadLocalizationFile(Mod mod, Mod.File file)
	{
		try
		{
			Loc.BuildAndLoadLocalization(JsonSerializer
				.Deserialize<Dictionary<string, Dictionary<string, string>>>(file.bytes)!);
			Plugin.logger.LogInfo($"Registried localization from {mod.name} mod");
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on loading locatization from {mod.name} mod: {e.Message}");
		}
	}

	public static void LoadSpriteFile(Mod mod, Mod.File file)
	{
		string name = Path.GetFileNameWithoutExtension(file.name);
		Vector2 pivot = name.Split("_")[0] switch
		{
			"field" => new(0.5f, 0.0f),
			"mountain" => new(0.5f, -0.375f),
			_ => new(0.5f, 0.5f),
		};
		float pixelsPerUnit = 2112f;
		if (Registry.spriteInfos.ContainsKey(name))
		{
			Visual.SpriteInfo spriteData = Registry.spriteInfos[name];
			pivot = spriteData.pivot ?? pivot;
			pixelsPerUnit = spriteData.pixelsPerUnit ?? pixelsPerUnit;
		}
		Sprite sprite = Visual.BuildSprite(file.bytes, pivot, pixelsPerUnit);
		GameManager.GetSpriteAtlasManager().cachedSprites.TryAdd("Heads", new());
		GameManager.GetSpriteAtlasManager().cachedSprites["Heads"].Add(name, sprite);
		Registry.sprites.Add(name, sprite);
	}

	public static void LoadSpriteInfoFile(Mod mod, Mod.File file)
	{
		try
		{
			Registry.spriteInfos = Registry.spriteInfos
				.Concat(JsonSerializer.Deserialize<Dictionary<string, Visual.SpriteInfo>>(
					file.bytes,
					new JsonSerializerOptions()
					{
						Converters = { new Vector2Json() },
					}
				)!)
				.ToDictionary(e => e.Key, e => e.Value);
			Plugin.logger.LogInfo($"Registried sprite data from {mod.name} mod");
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on loading sprite data from {mod.name} mod: {e.Message}");
		}
	}

	public static void LoadAudioFile(Mod mod, Mod.File file)
	{
		AudioSource audioSource = new GameObject().AddComponent<AudioSource>();
		GameObject.DontDestroyOnLoad(audioSource);
		audioSource.clip = Managers.Audio.BuildAudioClip(file.bytes);
		Registry.audioClips.Add(Path.GetFileNameWithoutExtension(file.name), audioSource);
	}

	public static void LoadGameLogicDataPatch(Mod mod, JObject gld, JObject patch)
	{
		try
		{
			HandleSkins(gld, patch);
			foreach (JToken jtoken in patch.SelectTokens("$.*.*").ToArray())
			{
				JObject? token = jtoken.TryCast<JObject>();
				if (token != null)
				{
					if (token["idx"] != null && (int)token["idx"] == -1)
					{
						string id = Util.GetJTokenName(token);
						string dataType = Util.GetJTokenName(token, 2);
						token["idx"] = Registry.autoidx;
						if (typeMappings.TryGetValue(dataType, out Type? targetType))
						{
							MethodInfo? methodInfo = typeof(EnumCache<>).MakeGenericType(targetType).GetMethod("AddMapping");
							if (methodInfo != null)
							{
								methodInfo.Invoke(null, new object[] { id, Registry.autoidx });
								methodInfo.Invoke(null, new object[] { id, Registry.autoidx });
								if (targetType == typeof(TribeData.Type))
								{
									Registry.customTribes.Add((TribeData.Type)Registry.autoidx);
									token["style"] = Registry.climateAutoidx;
									token["climate"] = Registry.climateAutoidx;
									Registry.climateAutoidx++;
								}
								else if (targetType == typeof(UnitData.Type))
								{
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
									PrefabManager.units.TryAdd((int)(UnitData.Type)Registry.autoidx, PrefabManager.units[(int)unitPrefabType]);
								}
								else if (targetType == typeof(ImprovementData.Type))
								{
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
									PrefabManager.improvements.TryAdd((ImprovementData.Type)Registry.autoidx, PrefabManager.improvements[improvementPrefabType]);
								}
								else if (targetType == typeof(ResourceData.Type))
								{
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
									PrefabManager.resources.TryAdd((ResourceData.Type)Registry.autoidx, PrefabManager.resources[resourcePrefabType]);
								}
								Plugin.logger.LogInfo("Created mapping for " + targetType.ToString() + " with id " + id + " and index " + Registry.autoidx);
								Registry.autoidx++;
							}
						}
					}
				}
			}
			foreach (JToken jtoken in patch.SelectTokens("$.tribeData.*").ToArray())
			{
				JObject token = jtoken.Cast<JObject>();

				if (token["preview"] != null)
				{
					Visual.PreviewTile[] preview = JsonSerializer.Deserialize<Visual.PreviewTile[]>(token["preview"].ToString())!;
					Registry.tribePreviews[Util.GetJTokenName(token)] = preview;
				}
			}
			gld.Merge(patch, new() { MergeArrayHandling = MergeArrayHandling.Replace, MergeNullValueHandling = MergeNullValueHandling.Merge });
			Plugin.logger.LogInfo($"Registried patch from {mod.name} mod");
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on loading patch from {mod.name} mod: {e.Message}");
			mod.status = Mod.Status.Error;
		}
	}

	public static void HandleSkins(JObject gld, JObject patch)
	{
		foreach (JToken jtoken in patch.SelectTokens("$.tribeData.*").ToArray())
		{
			JObject token = jtoken.Cast<JObject>();

			if (token["skins"] != null)
			{
				JArray skins = token["skins"].Cast<JArray>();
				List<string> skinsToRemove = new();
				List<JToken> skinValues = skins._values.ToArray().ToList();
				foreach (var skin in skinValues)
				{
					string skinValue = skin.ToString();
					if (skinValue.StartsWith('-') && Enum.TryParse<SkinType>(skinValue.Substring(1), out _))
					{
						skinsToRemove.Add(skinValue.Substring(1));
					}
					else if (!Enum.TryParse<SkinType>(skinValue, out _))
					{
						EnumCache<SkinType>.AddMapping(skinValue.ToLowerInvariant(), (SkinType)Registry.autoidx);
						EnumCache<SkinType>.AddMapping(skinValue.ToLowerInvariant(), (SkinType)Registry.autoidx);
						Registry.skinInfo.Add(new Visual.SkinInfo(Registry.autoidx, skinValue, null));
						Plugin.logger.LogInfo("Created mapping for skinType with id " + skinValue + " and index " + Registry.autoidx);
						Registry.autoidx++;
					}
				}
				foreach (var skin in Registry.skinInfo)
				{
					if (skins._values.Contains(skin.id))
					{
						skins._values.Remove(skin.id);
						skins._values.Add(skin.idx);
					}
				}
				JToken originalSkins = gld.SelectToken(skins.Path, false);
				if (originalSkins != null)
				{
					skins.Merge(originalSkins);
					foreach (var skin in skinsToRemove)
					{
						skins._values.Remove(skin);
						skins._values.Remove("-" + skin);
					}
				}
			}
		}
		foreach (JToken jtoken in patch.SelectTokens("$.skinData.*").ToArray())
		{
			JObject token = jtoken.Cast<JObject>();
			string id = Util.GetJTokenName(token);
			int index = Registry.skinInfo.FindIndex(t => t.id == id);
			if (Registry.skinInfo.ElementAtOrDefault(index) != null)
			{
				SkinData skinData = new();
				if (token["color"] != null)
				{
					skinData.color = (int)token["color"];
				}
				if (token["language"] != null)
				{
					skinData.language = token["language"].ToString();
				}
				Registry.skinInfo[index] = new Visual.SkinInfo(Registry.skinInfo[index].idx, Registry.skinInfo[index].id, skinData);
			}
		}
		patch.Remove("skinData");
	}
}
