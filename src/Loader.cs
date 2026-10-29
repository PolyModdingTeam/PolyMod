using BepInEx.Logging;
using Cpp2IL.Core.Extensions;
using Il2CppSystem.Linq;
using MonoMod.Utils;
using Newtonsoft.Json.Linq;
using PolyMod.Json;
using PolyMod.Managers;
using Polytopia.Data;
using PolytopiaBackendBase.Game;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UnityEngine;
using PolytopiaBackendBase.Common;

namespace PolyMod;

/// <summary>
/// Handles loading of mods and their assets.
/// </summary>
public static class Loader
{
	internal record TypeMapping(Type type, bool shouldCreateCache = true);

	/// <summary>
	/// Mappings from JSON data types to their corresponding C# types.
	/// </summary>
	internal static Dictionary<string, TypeMapping> typeMappings = new()
	{
		{ "tribeData", new TypeMapping(typeof(TribeType)) },
		{ "techData", new TypeMapping(typeof(TechData.Type)) },
		{ "unitData", new TypeMapping(typeof(UnitData.Type)) },
		{ "improvementData", new TypeMapping(typeof(ImprovementData.Type)) },
		{ "terrainData", new TypeMapping(typeof(Polytopia.Data.TerrainData.Type)) },
		{ "resourceData", new TypeMapping(typeof(ResourceData.Type)) },
		{ "taskData", new TypeMapping(typeof(TaskData.Type)) },
		{ "tribeAbility", new TypeMapping(typeof(TribeAbility.Type)) },
		{ "unitAbility", new TypeMapping(typeof(UnitAbility.Type)) },
		{ "improvementAbility", new TypeMapping(typeof(ImprovementAbility.Type)) },
		{ "playerAbility", new TypeMapping(typeof(PlayerAbility.Type)) },
		{ "weaponData", new TypeMapping(typeof(UnitData.WeaponEnum)) },
		{ "skinData", new TypeMapping(typeof(SkinData), false) }
	};

	/// <summary>
	/// List of custom game modes to be added.
	/// </summary>
	internal static List<GameModeButtonsInformation> gamemodes = new();

	/// <summary>
	/// Handlers for processing specific data types during mod loading.
	/// </summary>
	internal static readonly Dictionary<Type, List<Action<JObject, bool>>> typeHandlers = new()
	{
		[typeof(TribeType)] = new List<Action<JObject, bool>>() {
			new((token, duringEnumCacheCreation) =>
			{
				if (duringEnumCacheCreation)
				{
					Registry.customTribes.Add((TribeType)(int)token["idx"]);
					token["style"] = Registry.climateAutoidx;
					token["climate"] = Registry.climateAutoidx;
					Registry.climateAutoidx++;
				}
				else
				{
					if (token["skins"] != null)
					{
						JArray skins = token["skins"].Cast<JArray>();
						List<JToken> skinValues = skins._values.ToArray().ToList();
						foreach (var skin in skinValues)
						{
							string skinId = skin.ToString();
							if (!Enum.TryParse<SkinType>(skinId, ignoreCase: true, out _))
							{
								SkinType skinValue = (SkinType)Registry.autoidx;
								EnumCache<SkinType>.AddMapping(skinId.ToLowerInvariant(), skinValue);
								EnumCache<SkinType>.AddMapping(skinId.ToLowerInvariant(), skinValue);
								Registry.skinInfo.Add(new Visual.SkinInfo(Registry.autoidx, skinId, null));
								Plugin.logger.LogInfo("Created mapping for skinType with id " + skinId + " and index " + Registry.autoidx);
								Registry.autoidx++;
								if(token["tribeAbilities"] != null)
								{
									JArray tribeAbilities = token["tribeAbilities"].Cast<JArray>();
									List<JToken> tribeAbilitiesValues = tribeAbilities._values.ToArray().ToList();

									foreach(JToken ability in tribeAbilitiesValues)
									{
										string abilityValue = ability.ToString();

										if (Enum.TryParse<TribeAbility.Type>(
												abilityValue, ignoreCase: true,
												out TribeAbility.Type tribeAbilityType
											) && tribeAbilityType == TribeAbility.Type.Flooded)
										{
											string tileEffectId = skinId.ToLowerInvariant() + "_flood";
											EnumCache<TileData.EffectType>.AddMapping(tileEffectId, (TileData.EffectType)Registry.autoidx);
											EnumCache<TileData.EffectType>.AddMapping(tileEffectId, (TileData.EffectType)Registry.autoidx);
                                            Visual.customFloodingSkins.Add((TileData.EffectType)Registry.autoidx, skinValue);
											Plugin.logger.LogInfo("Created mapping for tileEffect with id " + tileEffectId + " and index " + Registry.autoidx);
											Registry.autoidx++;
										}
									}
								}
							}
						}
						Il2CppSystem.Collections.Generic.List<JToken> modifiedSkins = skins._values;
						foreach (var skin in Registry.skinInfo)
						{
							if (modifiedSkins.Contains(skin.id))
							{
								modifiedSkins.Remove(skin.id);
								modifiedSkins.Add(skin.idx.ToString());
							}
						}
						JArray newSkins = new JArray();
						foreach (var item in modifiedSkins)
						{
							newSkins.Add(item);
						}
						token["skins"] = newSkins;
					}
					if (token["preview"] != null)
					{
						TribePreviews.PreviewTile[] preview = JsonSerializer.Deserialize<TribePreviews.PreviewTile[]>(token["preview"].ToString())!;
						Registry.tribePreviews[Util.GetJTokenName(token)] = preview;
					}
				}
			})
		},
		[typeof(UnitData.Type)] = new List<Action<JObject, bool>>() {
			new((token, duringEnumCacheCreation) =>
			{
				if(!duringEnumCacheCreation)
				{
					if (token["embarksTo"] != null)
					{
						string unitId = Util.GetJTokenName(token);
						string embarkUnitId = token["embarksTo"].ToString();
						Main.embarkNames[unitId] = embarkUnitId;
					}
					if (token["weapon"] != null)
					{
						string weaponString = token["weapon"].ToString();
						if (EnumCache<UnitData.WeaponEnum>.TryGetType(weaponString, out UnitData.WeaponEnum type))
						{
							token["weapon"] = (int)type;
						}
					}
				}
			})
		},

		[typeof(ImprovementData.Type)] = new List<Action<JObject, bool>>() {
		new((token, duringEnumCacheCreation) =>
		{
			if (duringEnumCacheCreation)
				return;

			if (token["prefab"] != null && (bool)token["prefab"])
			{
				bool levels = false;
				if(token["maxLevel"] != null && (int)token["maxLevel"] > 0)
				{
					levels = true;
				}
				Prefabs.improvementPrefabInfosToCreate[(ImprovementData.Type)(int)token["idx"]] = levels;
			}
			string improvementId = Util.GetJTokenName(token);
			if (token["attractsResource"] != null)
			{
				string attractsId = token["attractsResource"].ToString();
				Main.attractsResourceNames[improvementId] = attractsId;
			}
			if (token["attractsToTerrain"] != null)
			{
				string attractsId = token["attractsToTerrain"].ToString();
				Main.attractsTerrainNames[improvementId] = attractsId;
			}
			if(token["infoOverride"] != null)
			{
				Loc.buildingsInfoOverrides[improvementId] = Loc.ReplaceDashesWithDots(token["infoOverride"].ToString());
			}
		})
		},

		[typeof(ResourceData.Type)] = new List<Action<JObject, bool>>() {
			new((token, duringEnumCacheCreation) =>
			{
				if (duringEnumCacheCreation)
					return;

				if (token["prefab"] != null && (bool)token["prefab"])
				{
					Prefabs.resourcePrefabsInfosToCreate.Add((ResourceData.Type)(int)token["idx"]);
				}
			})
		},

		[typeof(SkinData)] = new List<Action<JObject, bool>>() {
			new((token, duringEnumCacheCreation) =>
			{
				var prop = token.Parent.Cast<JProperty>();
				prop.Replace(new JProperty(prop.Name.ToLower(), prop.Value));
			})
		},
	};

	/// <summary>
	/// Represents information for a custom game mode button.
	/// </summary>
	/// <param name="gameModeIndex">The index of the game mode.</param>
	/// <param name="action">The action to perform when the button is clicked.</param>
	/// <param name="buttonIndex">The index of the button in the UI.</param>
	/// <param name="sprite">The sprite for the button.</param>
	/// <param name="spriteName">The name of the sprite for the button.</param>
	public record GameModeButtonsInformation(int gameModeIndex, UIButtonBase.ButtonAction action, int? buttonIndex, Sprite? sprite, string? spriteName);

	/// <summary>
	/// Adds a new game mode button.
	/// </summary>
	/// <param name="id">The unique identifier for the game mode.</param>
	/// <param name="action">The action to perform when the button is clicked.</param>
	/// <param name="shouldShowInMenu">The boolean which decides if GameMode should appear in the menu.</param>
	/// <param name="sprite">The sprite for the button.</param>
	/// <param name="spriteName">The name of the sprite for the button.</param>
	public static void AddGameMode(string id, UIButtonBase.ButtonAction action, bool shouldShowInMenu = true, Sprite? sprite = null, string? spriteName = null)
	{
		EnumCache<GameMode>.AddMapping(id, (GameMode)Registry.gameModesAutoidx);
		EnumCache<GameMode>.AddMapping(id, (GameMode)Registry.gameModesAutoidx);
		if(shouldShowInMenu)
			gamemodes.Add(new GameModeButtonsInformation(Registry.gameModesAutoidx, action, null, sprite, spriteName));
		Registry.gameModesAutoidx++;
	}

	/// <summary>
	/// Adds a new data type for patching.
	/// </summary>
	/// <param name="typeId">The identifier for the data type in JSON.</param>
	/// <param name="type">"The C# type corresponding to the identifier.</param>
	public static void AddPatchDataType(string typeId, Type type)
	{
		if (!typeMappings.ContainsKey(typeId))
			typeMappings.Add(typeId, new TypeMapping(type));
	}

	public static void AddPatchDataType(string typeId, Type type, bool shouldCreateCache)
	{
		if (!typeMappings.ContainsKey(typeId))
			typeMappings.Add(typeId, new TypeMapping(type, shouldCreateCache));
	}

	public static void AddTypeHandler(Type type, Action<JObject, bool> handler)
	{
		string typeString = type.ToString();
		if(!typeMappings.ContainsValue(new TypeMapping(type, true)) && !typeMappings.ContainsValue(new TypeMapping(type, false)))
		{
			Plugin.logger.LogWarning($"Tried adding TypeHandler for type: {typeString} with missing TypeMapping. Please, add TypeMapping first.");
			return;
		}
		if(!typeHandlers.ContainsKey(type))
			typeHandlers[type] = new();
		
		typeHandlers[type].Add(handler);

		Plugin.logger.LogInfo($"Added TypeHandler for type: {typeString}.");
	}

	/// <summary>
	/// Loads all mods from the mods directory.
	/// </summary>
	/// <param name="mods">A dictionary to populate with the loaded mods.</param>
	internal static void RegisterMods(Dictionary<string, Mod> mods)
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

			// Load mod from directory or zip archive
			if (Directory.Exists(modContainer))
			{
				foreach (var file in Directory.GetFiles(modContainer, "*", SearchOption.AllDirectories))
				{
					ProcessModFile(
						Path.GetRelativePath(modContainer, file),
						File.ReadAllBytes(file),
						files,
						ref manifest
					);
				}
			}
			else
			{
				foreach (var entry in new ZipArchive(File.OpenRead(modContainer)).Entries)
				{
					ProcessModFile(
						entry.FullName,
						entry.ReadBytes(),
						files,
						ref manifest
					);
				}
			}
			#region ValidateManifest()
			if (manifest == null)
			{
				Plugin.logger.LogError($"Mod manifest not found in {modContainer}");
				continue;
			}
			if (manifest.id == null)
			{
				Plugin.logger.LogError($"Mod id not found in {modContainer}");
				continue;
			}
			if (!Regex.IsMatch(manifest.id, @"^(?!polytopia$)[a-z_]+$"))
			{
				Plugin.logger.LogError($"Mod id {manifest.id} is invalid in {modContainer}");
				continue;
			}
			if (Plugin.config.disabledMods.Contains(manifest.id))
			{
				Plugin.logger.LogError($"Mod {manifest.id} is disabled, skipping");
				continue;
			}
			if (manifest.version == null)
			{
				Plugin.logger.LogError($"Mod version not found in {modContainer}");
				continue;
			}
			if (manifest.authors == null || manifest.authors.Length == 0)
			{
				Plugin.logger.LogError($"Mod authors not found in {modContainer}");
				continue;
			}
			if (mods.ContainsKey(manifest.id))
			{
				Plugin.logger.LogError($"Mod {manifest.id} already exists");
				continue;
			}
			#endregion
			mods.Add(manifest.id, new(
				manifest,
				Mod.Status.Success,
				files
			));
			Plugin.logger.LogInfo($"Registered mod {manifest.id}");
		}

		CheckDependencies(mods);
	}

	private static void ProcessModFile(string fullName, byte[] bytes, List<Mod.File> files, ref Mod.Manifest? manifest)
	{
		if (fullName == "manifest.json")
		{
			manifest = JsonSerializer.Deserialize<Mod.Manifest>(
				bytes,
				new JsonSerializerOptions()
				{
					Converters = { new VersionJson() },
				}
			);
			return;
		}
		files.Add(new(fullName, bytes));
	}

	internal static void LoadMods(Dictionary<string, Mod> mods, out bool dependencyCycle)
	{
		dependencyCycle = !SortMods(Registry.mods);
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
					LoadAssemblyFile(mod, file);
				}
				Match spritesMatch = Regex.Match(Path.GetFileName(file.name), @"^sprites(?:_(.*))?\.json$");
				if (spritesMatch.Success)
				{
					LoadSpriteInfoFile(mod, file);
				}
				Match languageMatch = Regex.Match(Path.GetFileName(file.name), @"^language(?:_(.*))?\.json$");
				if (languageMatch.Success)
				{
					string languageName = languageMatch.Groups[1].Value;
					LoadLanguageFile(
						mod,
						file,
						languageName
					);
				}
			}
			if (!mod.client && id != "polytopia")
			{
				checksumString.Append(id);
				checksumString.Append(mod.version.ToString());
			}
		}
		Compatibility.HashSignatures(checksumString);
	}
	private static void CheckDependencies(Dictionary<string, Mod> mods)
	{
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

	/// <summary>
	/// Sorts mods based on their dependencies using a topological sort.
	/// </summary>
	/// <param name="mods">The dictionary of mods to sort.</param>
	/// <returns>True if the mods could be sorted (no circular dependencies), false otherwise.</returns>
	private static bool SortMods(Dictionary<string, Mod> mods)
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
				if (!graph.ContainsKey(dependency.id)) continue;
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

	/// <summary>
	/// Loads an assembly file from a mod.
	/// </summary>
	/// <param name="mod">The mod the assembly belongs to.</param>
	/// <param name="file">The assembly file to load.</param>
	public static void LoadAssemblyFile(Mod mod, Mod.File file)
	{
		try
		{
			Assembly assembly = Assembly.Load(file.bytes);
			if (assembly
				    .GetTypes()
				    .FirstOrDefault(t => t.IsSubclassOf(typeof(Api.PolyScriptBase)))
			    is { } modType)
			{
				var modInstance = (Api.PolyScriptBase) Activator.CreateInstance(modType)!;
				modInstance.Initialize(mod, BepInEx.Logging.Logger.CreateLogSource($"PolyMod] [{mod.id}"));
				modInstance.Load();
				return;
			}
			foreach (Type type in assembly.GetTypes())
			{
				MethodInfo? loadWithLogger = type.GetMethod("Load", new Type[] { typeof(ManualLogSource) });
				if (loadWithLogger != null)
				{
					loadWithLogger.Invoke(null, new object[]
					{
						BepInEx.Logging.Logger.CreateLogSource($"PolyMod] [{mod.id}")
					});
					Plugin.logger.LogInfo($"Invoked Load method with logger from {type.FullName} from {mod.id} mod");
				}
				MethodInfo? load = type.GetMethod("Load", Array.Empty<Type>());
				if (load != null)
				{
					load.Invoke(null, null);
					Plugin.logger.LogInfo($"Invoked Load method from {type.FullName} from {mod.id} mod");
				}
			}
		}
		catch (TargetInvocationException exception)
		{
			if (exception.InnerException != null)
			{
				Plugin.logger.LogError($"Error on loading assembly from {mod.id} mod: {exception.InnerException.Message}");
				mod.status = Mod.Status.Error;
			}
		}
	}

	/// <summary>
	/// Loads a localization file from a mod.
	/// </summary>
	/// <param name="mod">The mod the localization file belongs to.</param>
	/// <param name="file">The localization file to load.</param>
	public static void LoadLocalizationFile(Mod mod, Mod.File file)
	{
		try
		{
			Loc.BuildAndLoadLocalization(JsonSerializer
				.Deserialize<Dictionary<string, Dictionary<string, string>>>(file.bytes)!);
			Plugin.logger.LogInfo($"Registered localization from {mod.id} mod");
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on loading locatization from {mod.id} mod: {e.StackTrace}");
		}
	}

	public static void LoadLanguageFile(Mod mod, Mod.File file, string languageName)
	{
		try
		{
			var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(file.bytes);
			if(dict != null)
			{
				Loc.languagesToAdd.Add(languageName, dict);
				Plugin.logger.LogInfo($"Registered language from {mod.id} mod");
			}
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on loading language file from {mod.id} mod: {e.StackTrace}");
		}
	}

	/// <summary>
	/// Loads a sprite file from a mod.
	/// </summary>
	/// <param name="mod">The mod the sprite file belongs to.</param>
	/// <param name="file">The sprite file to load.</param>
	public static void LoadTextureFile(Mod mod, Mod.File file)
	{
		string name = Path.GetFileNameWithoutExtension(file.name);
		Texture2D texture2D = Visual.BuildTexture(file.bytes);
		Registry.textures[name] = new KeyValuePair<Texture2D, bool>(texture2D, false);
	}

	/// <summary>
	/// Loads a sprite info file from a mod.
	/// </summary>
	/// <param name="mod">The mod the sprite info file belongs to.</param>
	/// <param name="file">The sprite info file to load.</param>
	/// <returns>A dictionary of sprite information, or null if an error occurred.</returns>
	public static Dictionary<string, Visual.SpriteInfo>? LoadSpriteInfoFile(Mod mod, Mod.File file)
	{
		try
		{
			var deserialized = JsonSerializer.Deserialize<Dictionary<string, Visual.SpriteInfo>>(
				file.bytes,
				new JsonSerializerOptions()
				{
					Converters = { new Vector2Json() },
				}
			);

			if (deserialized != null)
			{
				foreach (var kvp in deserialized)
				{
					Registry.spriteInfos[kvp.Key] = kvp.Value;
				}
			}

			Plugin.logger.LogInfo($"Registered sprite data from {mod.id} mod");
			return deserialized;
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on loading sprite data from {mod.id} mod: {e.StackTrace}");
			return null;
		}
	}

	/// <summary>
	/// Loads an audio file from a mod.
	/// </summary>
	/// <param name="mod">The mod the audio file belongs to.</param>
	/// <param name="file">The audio file to load.</param>
	public static void LoadAudioFile(Mod mod, Mod.File file)
	{
		string name = Path.GetFileNameWithoutExtension(file.name).ToLower();
		try
		{
			AudioClip clip = Managers.Audio.BuildAudioClip(file.bytes, name);
			AudioSource audioSource = new GameObject($"PolyModAudio_{name}").AddComponent<AudioSource>();
			UnityEngine.Object.DontDestroyOnLoad(audioSource.gameObject);
			audioSource.clip = clip;
			Registry.audioClips[name] = audioSource;

			Plugin.logger.LogInfo(
				$"Registered audio {name} file from {mod.id} mod"
					+ $" ({clip.channels}ch {clip.frequency}Hz {clip.length:F2}s)."
			);
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on loading audio {name} file from {mod.id} mod: {e.Message}");
		}
	}

	public static void LoadPrefabsFile(Mod mod, Mod.File file)
	{
		try
		{
			var prefab = JsonSerializer.Deserialize<PrefabsInfo>(file.bytes, new JsonSerializerOptions
			{
				Converters = { new Vector2Json() },
				PropertyNameCaseInsensitive = true,
			});
			if (prefab == null)
				return;

			Prefabs.unitInfos.AddRange(prefab.units);
			Prefabs.improvementInfos.AddRange(prefab.improvements);
			Prefabs.resourceInfos.AddRange(prefab.resources);

			Plugin.logger.LogInfo($"Registered prefabs {Path.GetFileNameWithoutExtension(file.name)} file from {mod.id} mod.");
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on loading prefabs {Path.GetFileNameWithoutExtension(file.name)} file from {mod.id} mod: {e.StackTrace}.");
		}
	}

	public static void LoadUnitPrefabFile(Mod mod, Mod.File file)
	{
		try
		{
			var unitPrefab = JsonSerializer.Deserialize<UnitPrefabInfo>(file.bytes, new JsonSerializerOptions
			{
				Converters = { new Vector2Json() },
				PropertyNameCaseInsensitive = true,
			});
			if (unitPrefab == null)
				return;

			Prefabs.unitInfos.Add(unitPrefab);

			Plugin.logger.LogInfo($"Registered unit prefab {Path.GetFileNameWithoutExtension(file.name)} file from {mod.id} mod.");
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on loading unit prefab {Path.GetFileNameWithoutExtension(file.name)} file from {mod.id} mod: {e.StackTrace}.");
		}
	}

	public static void LoadImprovementPrefabFile(Mod mod, Mod.File file)
	{
		try
		{
			var improvementPrefab = JsonSerializer.Deserialize<ImprovementPrefabInfo>(file.bytes, new JsonSerializerOptions
			{
				Converters = { new Vector2Json() },
				PropertyNameCaseInsensitive = true,
			});
			if (improvementPrefab == null)
				return;

			Prefabs.improvementInfos.Add(improvementPrefab);

			Plugin.logger.LogInfo($"Registered improvement prefab {Path.GetFileNameWithoutExtension(file.name)} file from {mod.id} mod.");
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on loading improvement prefab {Path.GetFileNameWithoutExtension(file.name)} file from {mod.id} mod: {e.StackTrace}.");
		}
	}

	public static void LoadResourcePrefabFile(Mod mod, Mod.File file)
	{
		try
		{
			var resourcePrefab = JsonSerializer.Deserialize<ResourcePrefabInfo>(file.bytes, new JsonSerializerOptions
			{
				Converters = { new Vector2Json() },
				PropertyNameCaseInsensitive = true,
			});
			if (resourcePrefab == null)
				return;

			Prefabs.resourceInfos.Add(resourcePrefab);

			Plugin.logger.LogInfo($"Registered resource prefab {Path.GetFileNameWithoutExtension(file.name)} file from {mod.id} mod.");
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on loading resource prefab {Path.GetFileNameWithoutExtension(file.name)} file from {mod.id} mod: {e.StackTrace}.");
		}
	}

	/// <summary>
	/// Loads and applies a game logic data patch from a mod.
	/// </summary>
	/// <param name="mod">The mod the patch belongs to.</param>
	/// <param name="gld">The original game logic data.</param>
	/// <param name="patch">The patch to apply.</param>
	public static void LoadGameLogicDataPatch(Mod mod, JObject gld, JObject patch)
	{
		try
		{
			// Merge the patch into the game logic data
			gld = JsonMerger.Merge(gld, patch);
			Plugin.logger.LogInfo($"Registered patch from {mod.id} mod");
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on loading patch from {mod.id} mod: {e.StackTrace}");
			mod.status = Mod.Status.Error;
		}
	}

	/// <summary>
	/// Loads an asset bundle from a mod.
	/// </summary>
	/// <param name="mod">The mod the asset bundle belongs to.</param>
	/// <param name="file">The asset bundle file to load.</param>
	public static void LoadAssetBundle(Mod mod, Mod.File file)
	{
		Registry.assetBundles.Add(
			Path.GetFileNameWithoutExtension(file.name),
			AssetBundle.LoadFromMemory(file.bytes)
		);
	}

	/// <summary>
	/// Processes the merged game logic data after all mods have been loaded and patched.
	/// </summary>
	/// <param name="gameLogicData">The game logic data object to populate.</param>
	/// <param name="rootObject">The root JObject of the merged game logic data.</param>
	internal static void ProcessGameLogicData(GameLogicData gameLogicData, JObject rootObject)
	{
		try
		{
			CreateMappings(rootObject);
			ProcessCustomTribes();
			ProcessEmbarkOverrides();
			ProcessAttractOverrides();
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on processing modified game logic data : {e.StackTrace}");
		}
	}

	internal static void ProcessCustomTribes()
	{
		foreach (var tribe in Registry.customTribes)
		{
			if(!GameLogicData.legacyTribeTypesOrder.Contains(tribe))
			{
				GameLogicData.legacyTribeTypesOrder.Add(tribe);
			}
		}
	}

	/// <summary>
	/// Creates EnumCache mappings for custom enum values and invokes type handlers.
	/// </summary>
	/// <param name="rootObject"></param>
	internal static void CreateMappings(JObject rootObject)
	{
		foreach (JToken jtoken in rootObject.SelectTokens("$.*.*").ToArray())
		{
			JObject? token = jtoken.TryCast<JObject>();
			if (token == null)
				continue;

			string dataType = Util.GetJTokenName(token, 2);
			if (!typeMappings.TryGetValue(dataType, out TypeMapping? typeMapping))
				continue;

			if (token["idx"] == null || !typeMapping.shouldCreateCache)
				continue;

			Type targetType = typeMapping.type;
			if (!targetType.IsEnum)
			{
				Plugin.logger.LogWarning($"Type {targetType.FullName} is not an enum, skipping!");
				continue;
			}

			string id = Util.GetJTokenName(token);

			if((int)token["idx"] == -1)
			{
				token["idx"] = Registry.autoidx;
				Registry.autoidx++;
			}
			else if(Plugin.config.allowUnsafeIndexes)
			{
				Array values = Enum.GetValues(targetType);

				var maxValue = values.Cast<int>().Max();

				if(maxValue >= (int)token["idx"])
				{
					continue;
				}
			}
			else
			{
				continue;
			}

			MethodInfo? methodInfo = typeof(EnumCache<>).MakeGenericType(targetType).GetMethod("AddMapping");
			if (methodInfo == null)
			{
				Plugin.logger.LogWarning($"Missing AddMapping method for {targetType.FullName}");
				continue;
			}

			methodInfo.Invoke(null, new object[] { id, (int)token["idx"] });
			methodInfo.Invoke(null, new object[] { id, (int)token["idx"] });

			if (typeHandlers.TryGetValue(targetType, out var handlers))
			{
				foreach(var handler in handlers)
				{
					handler(token, true);
				}
			}
			Plugin.logger.LogInfo("Created mapping for " + targetType.ToString() + " with id " + id + " and index " + (int)token["idx"]);
		}
		foreach (JToken jtoken in rootObject.SelectTokens("$.*.*").ToArray())
		{
			JObject? token = jtoken.TryCast<JObject>();
			if (token != null)
			{
				string dataType = Util.GetJTokenName(token, 2);
				if (typeMappings.TryGetValue(dataType, out TypeMapping? typeMapping))
				{
					if (typeHandlers.TryGetValue(typeMapping.type, out var handlers))
					{
						foreach(var handler in handlers)
						{
							handler(token, false);
						}
					}
				}
			}
		}
	}

	/// <summary>
	/// Processes the prefab registry and populates the PrefabManager with custom prefabs.
	/// </summary>
	internal static void ProcessFilesQueue()
	{
		try
		{
			foreach (KeyValuePair<Mod, Mod.File> keyValuePair in Registry.filesQueueToBeProcessedAfterGLDMerge)
			{
				Mod mod = keyValuePair.Key;
				Mod.File file = keyValuePair.Value;
				Match prefabsMatch = Regex.Match(Path.GetFileName(file.name), @"^prefabs(?:_(.*))?\.json$");
				if (prefabsMatch.Success)
				{
					LoadPrefabsFile(mod, file);
					continue;
				}

				Match unitPrefabMatch = Regex.Match(Path.GetFileName(file.name), @"^unitPrefab(?:_(.*))?\.json$");
				if (unitPrefabMatch.Success)
				{
					LoadUnitPrefabFile(mod, file);
					continue;
				}

				Match improvementPrefabMatch = Regex.Match(Path.GetFileName(file.name), @"^improvementPrefab(?:_(.*))?\.json$");
				if (improvementPrefabMatch.Success)
				{
					LoadImprovementPrefabFile(mod, file);
					continue;
				}

				Match resourcePrefabMatch = Regex.Match(Path.GetFileName(file.name), @"^resourcePrefab(?:_(.*))?\.json$");
				if (resourcePrefabMatch.Success)
				{
					LoadResourcePrefabFile(mod, file);
					continue;
				}

			}
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on processing post gld merge files queue : {e.StackTrace}");
		}
	}

	/// <summary>
	/// Processes embark overrides by mapping original embark unit types to configured overrides.
	/// </summary>
	internal static void ProcessEmbarkOverrides()
	{
		foreach (KeyValuePair<string, string> entry in Main.embarkNames)
		{
			try
			{
				UnitData.Type unit = EnumCache<UnitData.Type>.GetType(entry.Key);
				UnitData.Type newUnit = EnumCache<UnitData.Type>.GetType(entry.Value);
				Main.embarkOverrides[unit] = newUnit;
				Plugin.logger.LogInfo($"Embark unit type for {entry.Key} is now {entry.Value}");
			}
			catch
			{
				Plugin.logger.LogError($"Embark unit type for {entry.Key} is not valid: {entry.Value}");
			}
		}
	}

	/// <summary>
	/// Processes attract overrides by mapping improvements to resources and terrain types based on configured overrides.
	/// </summary>
	internal static void ProcessAttractOverrides()
	{
		foreach (KeyValuePair<string, string> entry in Main.attractsResourceNames)
		{
			try
			{
				ImprovementData.Type improvement = EnumCache<ImprovementData.Type>.GetType(entry.Key);
				ResourceData.Type resource = EnumCache<ResourceData.Type>.GetType(entry.Value);
				Main.attractsResourceOverrides[improvement] = resource;
				Plugin.logger.LogInfo($"Improvement {entry.Key} now attracts {entry.Value}");
			}
			catch
			{
				Plugin.logger.LogError($"Improvement {entry.Key} resource type is not valid: {entry.Value}");
			}
		}
		foreach (KeyValuePair<string, string> entry in Main.attractsTerrainNames)
		{
			try
			{
				ImprovementData.Type improvement = EnumCache<ImprovementData.Type>.GetType(entry.Key);
				Polytopia.Data.TerrainData.Type terrain = EnumCache<Polytopia.Data.TerrainData.Type>.GetType(entry.Value);
				Main.attractsTerrainOverrides[improvement] = terrain;
				Plugin.logger.LogInfo($"Improvement {entry.Key} now attracts on {entry.Value}");
			}
			catch
			{
				Plugin.logger.LogError($"Improvement {entry.Key} terrain type is not valid: {entry.Value}");
			}
		}
	}
}
