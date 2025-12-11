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
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UnityEngine;

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
		{ "tribeData", new TypeMapping(typeof(TribeData.Type)) },
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
		{ "skinData", new TypeMapping(typeof(SkinType), false) }
	};

	/// <summary>
	/// List of custom game modes to be added.
	/// </summary>
	internal static List<GameModeButtonsInformation> gamemodes = new();

	/// <summary>
	/// Handlers for processing specific data types during mod loading.
	/// </summary>
	internal static readonly Dictionary<Type, Action<JObject, bool>> typeHandlers = new()
	{
		[typeof(TribeData.Type)] = new((token, duringEnumCacheCreation) =>
		{
			if (duringEnumCacheCreation)
			{
				Registry.customTribes.Add((TribeData.Type)Registry.autoidx);
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
						string skinValue = skin.ToString();
						if (!Enum.TryParse<SkinType>(skinValue, ignoreCase: true, out _))
						{
							EnumCache<SkinType>.AddMapping(skinValue.ToLowerInvariant(), (SkinType)Registry.autoidx);
							EnumCache<SkinType>.AddMapping(skinValue.ToLowerInvariant(), (SkinType)Registry.autoidx);
							Registry.skinInfo.Add(new Visual.SkinInfo(Registry.autoidx, skinValue));
							Plugin.logger.LogInfo("Created mapping for skinType with id " + skinValue + " and index " + Registry.autoidx);
							Registry.autoidx++;
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
					Visual.PreviewTile[] preview = JsonSerializer.Deserialize<Visual.PreviewTile[]>(token["preview"].ToString())!;
					Registry.tribePreviews[Util.GetJTokenName(token)] = preview;
				}
			}
		}),

		[typeof(UnitData.Type)] = new((token, duringEnumCacheCreation) =>
		{
			if (!duringEnumCacheCreation)
			{
				if (token["prefab"] != null)
				{
					Registry.prefabNames.Add((int)(UnitData.Type)(int)token["idx"], CultureInfo.CurrentCulture.TextInfo.ToTitleCase(token["prefab"]!.ToString()));
				}
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
		}),

		[typeof(ImprovementData.Type)] = new((token, duringEnumCacheCreation) =>
		{
			if (duringEnumCacheCreation)
			{
				ImprovementData.Type improvementPrefabType = ImprovementData.Type.CustomsHouse;
				if (token["prefab"] != null)
				{
					string prefabId = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(token["prefab"]!.ToString());
					if (Enum.TryParse(prefabId, out ImprovementData.Type parsedType))
						improvementPrefabType = parsedType;
				}
				PrefabManager.improvements.TryAdd((ImprovementData.Type)Registry.autoidx, PrefabManager.improvements[improvementPrefabType]);
			}
			else
			{
				if (token["attractsResource"] != null)
				{
					string improvementId = Util.GetJTokenName(token);
					string attractsId = token["attractsResource"].ToString();
					Main.attractsResourceNames[improvementId] = attractsId;
				}
				if (token["attractsToTerrain"] != null)
				{
					string improvementId = Util.GetJTokenName(token);
					string attractsId = token["attractsToTerrain"].ToString();
					Main.attractsTerrainNames[improvementId] = attractsId;
				}
			}
		}),

		[typeof(ResourceData.Type)] = new((token, duringEnumCacheCreation) =>
		{
			if (duringEnumCacheCreation)
			{
				ResourceData.Type resourcePrefabType = ResourceData.Type.Game;
				if (token["prefab"] != null)
				{
					string prefabId = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(token["prefab"]!.ToString());
					if (Enum.TryParse(prefabId, out ResourceData.Type parsedType))
						resourcePrefabType = parsedType;
				}
				PrefabManager.resources.TryAdd((ResourceData.Type)Registry.autoidx, PrefabManager.resources[resourcePrefabType]);
			}
		}),
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
	/// <param name="type">The C# type corresponding to the identifier.</param>
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
				if (Path.GetFileName(file.name) == "sprites.json")
				{
					LoadSpriteInfoFile(mod, file);
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
				modInstance.Initialize(mod.id, BepInEx.Logging.Logger.CreateLogSource($"PolyMod] [{mod.id}"));
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

	/// <summary>
	/// Loads a sprite file from a mod.
	/// </summary>
	/// <param name="mod">The mod the sprite file belongs to.</param>
	/// <param name="file">The sprite file to load.</param>
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

	/// <summary>
	/// Updates a sprite with new information.
	/// </summary>
	/// <param name="name">The name of the sprite to update.</param>
	public static void UpdateSprite(string name)
	{
		if (Registry.spriteInfos.ContainsKey(name) && Registry.sprites.ContainsKey(name))
		{
			Visual.SpriteInfo spriteData = Registry.spriteInfos[name];
			Sprite sprite = Visual.BuildSpriteWithTexture(
				Registry.sprites[name].texture,
				spriteData.pivot,
				spriteData.pixelsPerUnit
			);
			GameManager.GetSpriteAtlasManager().cachedSprites["Heads"][name] = sprite;
			Registry.sprites[name] = sprite;
		}
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
		// AudioSource audioSource = new GameObject().AddComponent<AudioSource>();
		// GameObject.DontDestroyOnLoad(audioSource);
		// audioSource.clip = Managers.Audio.BuildAudioClip(file.bytes);
		// Registry.audioClips.Add(Path.GetFileNameWithoutExtension(file.name), audioSource);
		// TODO: issue #71
	}

	/// <summary>
	/// Loads a prefab info file from a mod and creates a new unit prefab.
	/// </summary>
	/// <param name="mod">The mod the prefab info file belongs to.</param>
	/// <param name="file">The prefab info file to load.</param>
	public static void LoadPrefabInfoFile(Mod mod, Mod.File file)
	{
		try
		{
			var prefab = JsonSerializer.Deserialize<Visual.PrefabInfo>(file.bytes, new JsonSerializerOptions
			{
				Converters = { new Vector2Json() },
				PropertyNameCaseInsensitive = true,
			});
			if (prefab == null || prefab.type != Visual.PrefabType.Unit || prefab.visualParts.Count == 0)
				return;

			var baseUnit = PrefabManager.GetPrefab(UnitData.Type.Warrior, SkinType.Default);
			if (baseUnit == null)
				return;

			var unitInstance = GameObject.Instantiate(baseUnit);
			if (unitInstance == null)
				return;

			var spriteContainer = unitInstance.transform.GetChild(0);
			var material = ClearExistingPartsAndExtractMaterial(spriteContainer);

			var visualParts = ApplyVisualParts(prefab, spriteContainer, material);

			Transform? headPositionMarker = null;
			foreach (var vp in visualParts)
			{
				if (vp.skinPart.gameObject.name == prefab.headPositionMarker)
				{
					headPositionMarker = vp.skinPart.gameObject.transform;
					break;
				}
			}

			unitInstance.unitVisuals.headPositionMarker = headPositionMarker ?? visualParts[0].skinPart.transform;

			var svr = unitInstance.GetComponent<UnitVisuals>();
			svr.visualParts = visualParts.ToArray();

			GameObject.DontDestroyOnLoad(unitInstance.gameObject);
			Registry.unitPrefabs.Add(prefab, unitInstance.GetComponent<Unit>());

			Plugin.logger.LogInfo($"Registered prefab info from {mod.id} mod");
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on loading prefab info from {mod.id} mod: {e.StackTrace}");
		}
	}

	/// <summary>
	/// Clears existing visual parts from a prefab and extracts the material.
	/// </summary>
	/// <param name="spriteContainer">The transform containing the sprite parts.</param>
	/// <returns>The material of the original sprite parts.</returns>
	private static Material? ClearExistingPartsAndExtractMaterial(Transform spriteContainer)
	{
		Material? material = null;
		for (int i = 0; i < spriteContainer.childCount; i++)
		{
			var child = spriteContainer.GetChild(i);
			if (child.gameObject.name == "Head")
			{
				var renderer = child.GetComponent<SpriteRenderer>();
				if (renderer != null)
					material = renderer.material;
			}
			GameObject.Destroy(child.gameObject);
		}
		return material;
	}

	/// <summary>
	/// Applies new visual parts to a prefab.
	/// </summary>
	/// <param name="partInfos">A list of visual part information.</param>
	/// <param name="spriteContainer">The transform to add the parts to.</param>
	/// <param name="material">The material to use for the parts.</param>
	/// <returns>A list of the created visual parts.</returns>
	private static List<UnitVisuals.UnitVisualPart> ApplyVisualParts(
		Visual.PrefabInfo prefab,
		Transform spriteContainer,
		Material? material)
	{
		if (prefab.visualParts.Count == 0)
			return new List<UnitVisuals.UnitVisualPart>();

		List<UnitVisuals.UnitVisualPart> parts = new();

		foreach (var info in prefab.visualParts)
		{
			parts.Add(CreateVisualPart(info, spriteContainer, material));
		}

		return parts;
	}

	/// <summary>
	/// Creates a single visual part for a prefab.
	/// </summary>
	/// <param name="info">The information for the visual part.</param>
	/// <param name="parent">The parent transform for the part.</param>
	/// <param name="material">The material to use for the part.</param>
	/// <returns>The created visual part.</returns>
	private static UnitVisuals.UnitVisualPart CreateVisualPart(
		Visual.VisualPartInfo info,
		Transform parent,
		Material? material)
	{
		var visualPartObj = new GameObject(info.gameObjectName);
		visualPartObj.transform.SetParent(parent);
		visualPartObj.transform.position = info.coordinates;
		visualPartObj.transform.localScale = info.scale;
		visualPartObj.transform.rotation = Quaternion.Euler(0f, 0f, info.rotation);

		var outlineObj = new GameObject("Outline");
		outlineObj.transform.SetParent(visualPartObj.transform);
		outlineObj.transform.position = info.coordinates;
		outlineObj.transform.localScale = info.scale;
		outlineObj.transform.rotation = Quaternion.Euler(0f, 0f, info.rotation);

		var visualPart = new UnitVisuals.UnitVisualPart
		{
			DefaultSpriteName = info.baseName,
			skinPart = visualPartObj,
			outline = outlineObj,
			tintable = info.tintable
		};

		var renderer = visualPartObj.AddComponent<SpriteRenderer>();
		renderer.material = material;
		renderer.sortingLayerName = "Units";
		renderer.sortingOrder = info.tintable ? 0 : 1;

		visualPart.renderer = new UnitVisuals.UnitRendererRef { spriteRenderer = renderer };

		var outlineRenderer = outlineObj.AddComponent<SpriteRenderer>();
		outlineRenderer.material = material;
		outlineRenderer.sortingLayerName = "Units";
		outlineRenderer.sortingOrder = -1;

		visualPart.outlineRenderer = new UnitVisuals.UnitRendererRef { spriteRenderer = outlineRenderer };

		return visualPart;
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
			ProcessPrefabs();
			ProcessEmbarkOverrides();
			ProcessAttractOverrides();
		}
		catch (Exception e)
		{
			Plugin.logger.LogError($"Error on processing modified game logic data : {e.StackTrace}");
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

			if (typeHandlers.TryGetValue(targetType, out var handler))
			{
				handler(token, true);
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
					if (typeHandlers.TryGetValue(typeMapping.type, out var handler))
					{
						handler(token, false);
					}
				}
			}
		}
	}

	/// <summary>
	/// Processes the prefab registry and populates the PrefabManager with custom prefabs.
	/// </summary>
	internal static void ProcessPrefabs()
	{
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
