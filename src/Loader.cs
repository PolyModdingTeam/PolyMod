using System.Globalization;
using BepInEx.Logging;
using Cpp2IL.Core.Extensions;
using Il2CppSystem.Linq;
using MonoMod.Utils;
using Newtonsoft.Json.Linq;
using PolyMod.Json;
using PolyMod.Managers;
using Polytopia.Data;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using UnityEngine;
using PolytopiaBackendBase.Common;

namespace PolyMod;

/// <summary>
/// The methods that register mods, sort them, load their files and apply their GLD data.
/// </summary>
public static class Loader
{
    private static readonly JsonSerializerOptions manifestJsonOptions = new()
    {
        Converters = { new VersionJson() },
    };

    private static readonly JsonSerializerOptions spriteInfoJsonOptions = new()
    {
        Converters = { new Vector2Json() },
    };

    private static readonly JsonSerializerOptions prefabJsonOptions = new()
    {
        Converters = { new Vector2Json() },
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// The C# type that PolyMod uses for one GLD data category.
    /// </summary>
    /// <param name="type">The C# type of the data category. To make <c>EnumCache</c> mappings, this type must be an enum.</param>
    /// <param name="shouldCreateCache">If true, <see cref="CreateMappings"/> makes <c>EnumCache</c> mappings for new entries of the category. If false, it only calls the type handlers.</param>
    internal record TypeMapping(Type type, bool shouldCreateCache = true);

    /// <summary>
    /// The type mappings of the GLD data categories, keyed by the category name in the GLD, for example <c>unitData</c>.
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
    /// The type handlers that <see cref="CreateMappings"/> calls for each GLD entry, keyed by the C# type of the data category.
    /// </summary>
    /// <remarks>
    /// Each handler gets the JSON object of the entry and a flag.
    /// The flag is true when <see cref="CreateMappings"/> calls the handler just after it made a new <c>EnumCache</c> mapping for the entry.
    /// The flag is false in the second pass, in which <see cref="CreateMappings"/> calls the handler for all entries of the category.
    /// <list type="bullet">
    /// <item><description><see cref="TribeType"/>: in the first pass, adds the tribe to <see cref="Registry.customTribes"/> and gives it a new style and climate. In the second pass, makes <c>EnumCache</c> mappings for new skins, makes a flood tile effect for each new skin of a tribe with the <c>Flooded</c> ability, changes the skin ids to indexes, and keeps the tribe preview.</description></item>
    /// <item><description><c>UnitData.Type</c>: in the second pass, adds the <c>embarksTo</c> value to <c>Main.embarkNames</c> and changes the <c>weapon</c> name to its index.</description></item>
    /// <item><description><c>ImprovementData.Type</c>: in the second pass, adds the improvement to the prefabs to make if <c>prefab</c> is true. It also adds the <c>attractsResource</c>, <c>attractsToTerrain</c> and <c>infoOverride</c> values to the related dictionaries.</description></item>
    /// <item><description><c>ResourceData.Type</c>: in the second pass, adds the resource to the prefabs to make if <c>prefab</c> is true.</description></item>
    /// <item><description><c>SkinData</c>: changes the name of the entry to lowercase.</description></item>
    /// </list>
    /// </remarks>
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
                                modifiedSkins.Add(skin.idx.ToString(CultureInfo.InvariantCulture));
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
                prop.Replace(new JProperty(prop.Name.ToLowerInvariant(), prop.Value));
            })
        },
    };

    /// <summary>
    /// Adds a type mapping for a GLD data category. <see cref="CreateMappings"/> makes <c>EnumCache</c> mappings for new entries of this category.
    /// If <see cref="typeMappings"/> already contains the category name, the method does nothing.
    /// </summary>
    /// <param name="typeId">The name of the data category in the GLD, for example <c>unitData</c>.</param>
    /// <param name="type">The C# enum type of the data category.</param>
    public static void AddPatchDataType(string typeId, Type type)
    {
        if (!typeMappings.ContainsKey(typeId))
            typeMappings.Add(typeId, new TypeMapping(type));
    }

    /// <summary>
    /// Adds a type mapping for a GLD data category.
    /// If <see cref="typeMappings"/> already contains the category name, the method does nothing.
    /// </summary>
    /// <param name="typeId">The name of the data category in the GLD, for example <c>unitData</c>.</param>
    /// <param name="type">The C# type of the data category.</param>
    /// <param name="shouldCreateCache">If true, <see cref="CreateMappings"/> makes <c>EnumCache</c> mappings for new entries of the category. If false, it only calls the type handlers.</param>
    public static void AddPatchDataType(string typeId, Type type, bool shouldCreateCache)
    {
        if (!typeMappings.ContainsKey(typeId))
            typeMappings.Add(typeId, new TypeMapping(type, shouldCreateCache));
    }

    /// <summary>
    /// Adds a type handler for a C# type to <see cref="typeHandlers"/>.
    /// A type mapping for the type must exist in <see cref="typeMappings"/>.
    /// If no type mapping exists, the method writes a warning to the log and does not add the handler.
    /// </summary>
    /// <param name="type">The C# type of the data category.</param>
    /// <param name="handler">The handler to add. It gets the JSON object of a GLD entry and a flag. The flag is true when <see cref="CreateMappings"/> just made a new <c>EnumCache</c> mapping for the entry.</param>
    public static void AddTypeHandler(Type type, Action<JObject, bool> handler)
    {
        string typeString = type.ToString();
        if (!typeMappings.ContainsValue(new TypeMapping(type, true)) && !typeMappings.ContainsValue(new TypeMapping(type, false)))
        {
            Plugin.logger.LogWarning($"Tried adding TypeHandler for type: {typeString} with missing TypeMapping. Please, add TypeMapping first.");
            return;
        }
        if (!typeHandlers.ContainsKey(type))
            typeHandlers[type] = new();

        typeHandlers[type].Add(handler);

        Plugin.logger.LogInfo($"Added TypeHandler for type: {typeString}.");
    }

    /// <summary>
    /// Finds the mods in <see cref="Plugin.MODS_PATH"/> and adds them to a dictionary.
    /// </summary>
    /// <remarks>
    /// The method makes the mods directory if it does not exist.
    /// A mod can be a directory, a <c>.polymod</c> file or a <c>.zip</c> file.
    /// The method reads all files of the mod and gets the manifest from <c>manifest.json</c> at the root.
    /// It does not add a mod if the manifest is missing, not valid, or has an id that the dictionary already contains.
    /// A manifest is not valid if it has no id, no version or no authors.
    /// A manifest is also not valid if the id is <c>polytopia</c> or contains characters other than lowercase letters and underscores.
    /// The method also does not add a mod that the configuration disables.
    /// For each mod that it does not add, the method writes an error to the log.
    /// The method gives each added mod the status <see cref="Mod.Status.Success"/> and then calls <see cref="CheckDependencies"/>.
    /// </remarks>
    /// <param name="mods">The dictionary to which the method adds the mods, keyed by the mod id.</param>
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
                Plugin.logger.LogInfo($"Mod {manifest.id} is disabled, skipping");
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

    /// <summary>
    /// Adds one file of a mod to the file list, or reads it as the manifest.
    /// If the file is <c>manifest.json</c> at the root of the mod, the method parses it into <paramref name="manifest"/> and does not add it to the list.
    /// </summary>
    /// <param name="fullName">The path of the file, relative to the root of the mod.</param>
    /// <param name="bytes">The contents of the file.</param>
    /// <param name="files">The file list of the mod.</param>
    /// <param name="manifest">The manifest of the mod. The method sets it when the file is <c>manifest.json</c>.</param>
    private static void ProcessModFile(string fullName, byte[] bytes, List<Mod.File> files, ref Mod.Manifest? manifest)
    {
        if (fullName == "manifest.json")
        {
            manifest = JsonSerializer.Deserialize<Mod.Manifest>(
                bytes,
                manifestJsonOptions
            );
            return;
        }
        files.Add(new(fullName, bytes));
    }

    /// <summary>
    /// Sorts the registered mods and loads their assemblies, sprite info files and language files.
    /// </summary>
    /// <remarks>
    /// The method uses <see cref="Registry.mods"/>.
    /// First, it sorts the mods with <see cref="SortMods"/>. If there is a dependency cycle, the method stops.
    /// For each mod with the status <see cref="Mod.Status.Success"/>, it loads the <c>.dll</c> files, the sprite info files and the language files.
    /// A sprite info file has the name <c>sprites.json</c> or <c>sprites_*.json</c>.
    /// A language file has the name <c>language.json</c> or <c>language_*.json</c>.
    /// At the end, it calls <c>Compatibility.ComputeChecksums</c>.
    /// </remarks>
    /// <param name="dependencyCycle">Set to true if the mods have a dependency cycle, false otherwise.</param>
    internal static void LoadMods(out bool dependencyCycle)
    {
        dependencyCycle = !SortMods(Registry.mods);
        if (dependencyCycle) return;

        foreach (var (id, mod) in Registry.mods)
        {
            if (mod.status != Mod.Status.Success) continue;
            foreach (var file in mod.files)
            {
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
        }
        Compatibility.ComputeChecksums(Registry.mods);
    }

    /// <summary>
    /// Checks the dependencies of each mod in a dictionary.
    /// </summary>
    /// <remarks>
    /// A dependency fails if the dictionary does not contain it, or if its version is out of the permitted range.
    /// For a required dependency that fails, the method writes an error to the log.
    /// It also sets the status of the mod to <see cref="Mod.Status.DependenciesUnsatisfied"/>.
    /// For an optional dependency that fails, the method only writes a warning to the log.
    /// </remarks>
    /// <param name="mods">The mods to check, keyed by the mod id.</param>
    internal static void CheckDependencies(Dictionary<string, Mod> mods)
    {
        foreach (var (id, mod) in mods)
        {
            foreach (var dependency in mod.dependencies ?? Array.Empty<Mod.Dependency>())
            {
                string? message = null;
                if (!mods.TryGetValue(dependency.id, out Mod? dependencyMod))
                {
                    message = $"Dependency {dependency.id} not found";
                }
                else
                {
                    Version version = dependencyMod.version;
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
    /// Sorts the mods in a dictionary so that each mod comes after its dependencies.
    /// </summary>
    /// <remarks>
    /// The method uses a topological sort on the mods with the status <see cref="Mod.Status.Success"/>.
    /// It ignores dependencies that are not in the dictionary or that do not have the status <see cref="Mod.Status.Success"/>.
    /// If the sort succeeds, the method replaces the contents of the dictionary.
    /// The sorted mods come first, then the mods with a different status.
    /// If there is a dependency cycle, the method does not change the dictionary.
    /// </remarks>
    /// <param name="mods">The mods to sort, keyed by the mod id.</param>
    /// <returns>True if the sort succeeds, false if the mods have a dependency cycle.</returns>
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
    /// Loads a <c>.dll</c> file of a mod as an assembly and starts its code.
    /// </summary>
    /// <remarks>
    /// If the assembly has a subclass of <see cref="Api.PolyScriptBase"/>, the method makes an instance of the first one.
    /// It calls <c>Initialize</c> and then <c>Load</c> on that instance, and does nothing more.
    /// If the assembly has no such subclass, the method calls each static <c>Load(ManualLogSource)</c> method and each static <c>Load()</c> method in the assembly.
    /// If a static <c>Load</c> method or the constructor of the subclass throws an exception, the method writes an error to the log.
    /// It also sets the status of the mod to <see cref="Mod.Status.Error"/>.
    /// The method catches only exceptions of the type <see cref="TargetInvocationException"/>.
    /// For this reason, it does not catch an exception from <c>Initialize</c> or from <c>Load</c> on a <see cref="Api.PolyScriptBase"/> instance.
    /// </remarks>
    /// <param name="mod">The mod that contains the file.</param>
    /// <param name="file">The <c>.dll</c> file to load.</param>
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
                var modInstance = (Api.PolyScriptBase)Activator.CreateInstance(modType)!;
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
    /// Loads a localization file of a mod and adds its terms to the game localization.
    /// </summary>
    /// <remarks>
    /// The file is a JSON object. Each key is a term name, and each value is an object that maps language names to texts.
    /// The method gives the data to <c>Loc.BuildAndLoadLocalization</c>.
    /// If an error occurs, the method writes the error to the log.
    /// </remarks>
    /// <param name="mod">The mod that contains the file.</param>
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
    /// Loads a language file of a mod and adds it to <c>Loc.languagesToAdd</c>.
    /// </summary>
    /// <remarks>
    /// The file is a JSON object that maps term names to texts.
    /// If an error occurs, the method writes the error to the log.
    /// An error occurs, for example, when a different file already added the same language code.
    /// </remarks>
    /// <param name="mod">The mod that contains the file.</param>
    /// <param name="file">The language file to load.</param>
    /// <param name="languageName">The language code from the file name. It is the text after <c>language_</c>, or an empty string for <c>language.json</c>.</param>
    public static void LoadLanguageFile(Mod mod, Mod.File file, string languageName)
    {
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(file.bytes);
            if (dict != null)
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
    /// Loads a PNG file of a mod as a texture and adds it to <see cref="Registry.textures"/>.
    /// The key is the lowercase file name without the extension.
    /// The method does not make a sprite. <c>Visual.CreateSprites</c> makes the sprites later.
    /// </summary>
    /// <param name="mod">The mod that contains the file. The method does not use this parameter.</param>
    /// <param name="file">The PNG file to load.</param>
    public static void LoadTextureFile(Mod mod, Mod.File file)
    {
        string name = Path.GetFileNameWithoutExtension(file.name).ToLowerInvariant();
        Texture2D texture2D = Visual.BuildTexture(file.bytes);
        Registry.textures[name] = texture2D;
    }

    /// <summary>
    /// Loads a sprite info file of a mod and adds its sprite infos to <see cref="Registry.spriteInfos"/>.
    /// </summary>
    /// <remarks>
    /// The method changes each sprite name and each sprite origin name to lowercase.
    /// A sprite info replaces an existing sprite info with the same name.
    /// If an error occurs, the method writes the error to the log.
    /// </remarks>
    /// <param name="mod">The mod that contains the file.</param>
    /// <param name="file">The sprite info file to load.</param>
    /// <returns>The sprite infos from the file, keyed by the sprite name as the file gives it, or null if an error occurs.</returns>
    public static Dictionary<string, Visual.SpriteInfo>? LoadSpriteInfoFile(Mod mod, Mod.File file)
    {
        try
        {
            var deserialized = JsonSerializer.Deserialize<Dictionary<string, Visual.SpriteInfo>>(
                file.bytes,
                spriteInfoJsonOptions
            );

            if (deserialized != null)
            {
                foreach (var kvp in deserialized)
                {
                    if (kvp.Value.spriteOrigin?.name != null)
                        kvp.Value.spriteOrigin.name = kvp.Value.spriteOrigin.name.ToLowerInvariant();
                    Registry.spriteInfos[kvp.Key.ToLowerInvariant()] = kvp.Value;
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
    /// Loads an audio file of a mod and adds it to <see cref="Registry.audioClips"/>.
    /// </summary>
    /// <remarks>
    /// The method uses <c>Audio.BuildAudioClip</c> to make the audio clip.
    /// It puts the clip on a new audio source that stays loaded when the scene changes.
    /// The key is the lowercase file name without the extension.
    /// If an error occurs, the method writes the error to the log.
    /// </remarks>
    /// <param name="mod">The mod that contains the file.</param>
    /// <param name="file">The audio file to load.</param>
    public static void LoadAudioFile(Mod mod, Mod.File file)
    {
        string name = Path.GetFileNameWithoutExtension(file.name).ToLowerInvariant();
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

    /// <summary>
    /// Loads a prefabs file of a mod.
    /// The method adds its unit, improvement and resource prefab infos to the lists in <c>Prefabs</c>.
    /// If the file contains JSON null, the method does nothing.
    /// If an error occurs, the method writes the error to the log.
    /// </summary>
    /// <param name="mod">The mod that contains the file.</param>
    /// <param name="file">The prefabs file to load.</param>
    public static void LoadPrefabsFile(Mod mod, Mod.File file)
    {
        try
        {
            var prefab = JsonSerializer.Deserialize<PrefabsInfo>(file.bytes, prefabJsonOptions);
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

    /// <summary>
    /// Loads a unit prefab file of a mod and adds its unit prefab info to <c>Prefabs.unitInfos</c>.
    /// If the file contains JSON null, the method does nothing.
    /// If an error occurs, the method writes the error to the log.
    /// </summary>
    /// <param name="mod">The mod that contains the file.</param>
    /// <param name="file">The unit prefab file to load.</param>
    public static void LoadUnitPrefabFile(Mod mod, Mod.File file)
    {
        try
        {
            var unitPrefab = JsonSerializer.Deserialize<UnitPrefabInfo>(file.bytes, prefabJsonOptions);
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

    /// <summary>
    /// Loads an improvement prefab file of a mod and adds its improvement prefab info to <c>Prefabs.improvementInfos</c>.
    /// If the file contains JSON null, the method does nothing.
    /// If an error occurs, the method writes the error to the log.
    /// </summary>
    /// <param name="mod">The mod that contains the file.</param>
    /// <param name="file">The improvement prefab file to load.</param>
    public static void LoadImprovementPrefabFile(Mod mod, Mod.File file)
    {
        try
        {
            var improvementPrefab = JsonSerializer.Deserialize<ImprovementPrefabInfo>(file.bytes, prefabJsonOptions);
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

    /// <summary>
    /// Loads a resource prefab file of a mod and adds its resource prefab info to <c>Prefabs.resourceInfos</c>.
    /// If the file contains JSON null, the method does nothing.
    /// If an error occurs, the method writes the error to the log.
    /// </summary>
    /// <param name="mod">The mod that contains the file.</param>
    /// <param name="file">The resource prefab file to load.</param>
    public static void LoadResourcePrefabFile(Mod mod, Mod.File file)
    {
        try
        {
            var resourcePrefab = JsonSerializer.Deserialize<ResourcePrefabInfo>(file.bytes, prefabJsonOptions);
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
    /// Merges a GLD patch of a mod into the GLD.
    /// The method uses <see cref="JsonMerger.Merge"/>, which changes <paramref name="gld"/> in place.
    /// If an error occurs, the method writes the error to the log and sets the status of the mod to <see cref="Mod.Status.Error"/>.
    /// </summary>
    /// <param name="mod">The mod that contains the patch.</param>
    /// <param name="gld">The GLD as a JSON object. The method changes this object.</param>
    /// <param name="patch">The patch to merge.</param>
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
    /// Loads a <c>.bundle</c> file of a mod as an asset bundle and adds it to <see cref="Registry.assetBundles"/>.
    /// The key is the file name without the extension.
    /// </summary>
    /// <param name="mod">The mod that contains the file. The method does not use this parameter.</param>
    /// <param name="file">The asset bundle file to load.</param>
    public static void LoadAssetBundle(Mod mod, Mod.File file)
    {
        Registry.assetBundles.Add(
            Path.GetFileNameWithoutExtension(file.name),
            AssetBundle.LoadFromMemory(file.bytes)
        );
    }

    /// <summary>
    /// Uses the merged GLD to make enum mappings and to fill the PolyMod overrides.
    /// The method calls <see cref="CreateMappings"/>, <see cref="ProcessCustomTribes"/>, <see cref="ProcessEmbarkOverrides"/> and <see cref="ProcessAttractOverrides"/> in sequence.
    /// If an error occurs, the method writes the error to the log.
    /// </summary>
    /// <param name="gameLogicData">The game logic data. The method does not use this parameter.</param>
    /// <param name="rootObject">The root JSON object of the merged GLD.</param>
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

    /// <summary>
    /// Adds each tribe in <see cref="Registry.customTribes"/> to <c>GameLogicData.legacyTribeTypesOrder</c>, if the list does not contain it.
    /// </summary>
    internal static void ProcessCustomTribes()
    {
        foreach (var tribe in Registry.customTribes)
        {
            if (!GameLogicData.legacyTribeTypesOrder.Contains(tribe))
            {
                GameLogicData.legacyTribeTypesOrder.Add(tribe);
            }
        }
    }

    /// <summary>
    /// Makes <c>EnumCache</c> mappings for new GLD entries and calls the type handlers.
    /// </summary>
    /// <remarks>
    /// The method does two passes over all entries of all data categories in the GLD.
    /// In the first pass, it uses only entries that have an <c>idx</c>, in categories with <c>shouldCreateCache</c> set to true.
    /// If the type of the category is not an enum, the method writes a warning to the log and ignores the entry.
    /// If <c>idx</c> is -1, the method sets <c>idx</c> to the next value of <see cref="Registry.autoidx"/>.
    /// If <c>allowUnsafeIndexes</c> is true, the method also uses an entry with an <c>idx</c> larger than all existing enum values.
    /// The method ignores all other entries in the first pass.
    /// For each entry that it uses, it calls <c>EnumCache.AddMapping</c> two times. The first call adds the name, and the second call adds the type.
    /// Then it calls the type handlers of the category with the flag set to true.
    /// In the second pass, the method calls the type handlers for all entries of categories in <see cref="typeMappings"/>, with the flag set to false.
    /// </remarks>
    /// <param name="rootObject">The root JSON object of the merged GLD.</param>
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

            if ((int)token["idx"] == -1)
            {
                token["idx"] = Registry.autoidx;
                Registry.autoidx++;
            }
            else if (Plugin.config.allowUnsafeIndexes)
            {
                Array values = Enum.GetValues(targetType);

                var maxValue = values.Cast<int>().Max();

                if (maxValue >= (int)token["idx"])
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
                foreach (var handler in handlers)
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
                        foreach (var handler in handlers)
                        {
                            handler(token, false);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Loads the prefab files in <see cref="Registry.filesQueueToBeProcessedAfterGLDMerge"/>.
    /// </summary>
    /// <remarks>
    /// The method uses the file name to select the load method.
    /// It calls <see cref="LoadPrefabsFile"/> for <c>prefabs.json</c>, <see cref="LoadUnitPrefabFile"/> for <c>unitPrefab.json</c>,
    /// <see cref="LoadImprovementPrefabFile"/> for <c>improvementPrefab.json</c> and <see cref="LoadResourcePrefabFile"/> for <c>resourcePrefab.json</c>.
    /// Each of these names can also have a suffix that starts with an underscore, for example <c>unitPrefab_knight.json</c>.
    /// The method ignores other files.
    /// If an error occurs, the method writes the error to the log and stops.
    /// </remarks>
    internal static void ProcessFilesQueue()
    {
        try
        {
            foreach ((Mod mod, Mod.File file) in Registry.filesQueueToBeProcessedAfterGLDMerge)
            {
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
    /// Changes the unit ids in <c>Main.embarkNames</c> to unit types and adds them to <c>Main.embarkOverrides</c>.
    /// Each entry maps a unit to the unit that it becomes when it embarks.
    /// If a unit id is not valid, the method writes an error to the log and continues with the next entry.
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
    /// Changes the ids in <c>Main.attractsResourceNames</c> and <c>Main.attractsTerrainNames</c> to enum values.
    /// The method adds the results to <c>Main.attractsResourceOverrides</c> and <c>Main.attractsTerrainOverrides</c>.
    /// Each entry maps an improvement to the resource that it attracts, or to the terrain on which it attracts resources.
    /// If an id is not valid, the method writes an error to the log and continues with the next entry.
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
