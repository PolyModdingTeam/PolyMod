using System.Text.Json;
using System.Text.Json.Nodes;

namespace PolyMod.Api;

/// <summary>
/// The config of a mod. It keeps the config object in memory and reads it from and writes it to a JSON file.
/// </summary>
/// <remarks>
/// <para>
/// To use a config, call <see cref="SetDefaultConfig"/> first. Then use <see cref="Get()"/> or <see cref="Edit"/>.
/// Call <see cref="SaveChanges"/> to write your changes to disk.
/// </para>
/// <para>
/// The class uses <c>System.Text.Json</c> to read and write the config object.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the config object.</typeparam>
// should be public when i get through it
internal class Config<T> where T : class
{
    private static readonly JsonSerializerOptions indentedJsonOptions = new() { WriteIndented = true };
    private T? currentConfig;
    private readonly string modName;
    private readonly ConfigTypes configType;
    private static readonly string ExposedConfigPath = Path.Combine(Plugin.BASE_PATH, "mods.json");
    private readonly string perModConfigPath;
    private T? defaultConfig;
    /// <summary>
    /// Makes a config for a mod and reads the config from disk if the config file exists.
    /// </summary>
    /// <param name="modName">The mod name, normally the mod id. The config uses it as the file name or as the key in <c>mods.json</c>.</param>
    /// <param name="configType">The place on disk for the config.</param>
    // should be public when i get through it
    internal Config(string modName, ConfigTypes configType)
    {
        this.modName = modName;
        this.configType = configType;
        perModConfigPath = Path.Combine(Plugin.MODS_PATH, $"{modName}.json");
        Load();
    }

    /// <summary>
    /// Reads the config from disk into memory.
    /// For <see cref="ConfigTypes.PerMod"/>, the method reads the file <c>Mods/{modName}.json</c>.
    /// For <see cref="ConfigTypes.Exposed"/>, the method reads the value with the mod name as its key in <c>mods.json</c>.
    /// If the file does not exist, the method does not change the config in memory.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The config type is not a known <see cref="ConfigTypes"/> value.</exception>
    internal void Load() // can be called internally if config changes; gui config not implemented yet
    {
        switch (configType)
        {
            case ConfigTypes.PerMod:
                {
                    if (!File.Exists(perModConfigPath))
                    {
                        return;
                    }
                    var jsonText = File.ReadAllText(perModConfigPath);
                    currentConfig = JsonSerializer.Deserialize<T>(jsonText);
                    break;
                }
            case ConfigTypes.Exposed:
                {
                    if (!File.Exists(ExposedConfigPath))
                    {
                        return;
                    }
                    var jsonText = File.ReadAllText(ExposedConfigPath);
                    currentConfig = JsonNode.Parse(jsonText)![modName]?.Deserialize<T>();
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    /// <summary>
    /// Sets the default config. Call this method before you read or edit the config.
    /// If there is no config in memory, the method uses the default config and writes it to disk.
    /// If there is a config in memory, for example a config that the constructor read from disk, the method keeps that config.
    /// </summary>
    /// <param name="defaultValue">The config object to use when there is no config in memory.</param>
    // should be public when i get through it
    internal void SetDefaultConfig(T defaultValue)
    {
        defaultConfig = defaultValue;
        if (currentConfig is not null) return;
        Write(defaultConfig);
        SaveChanges();
    }

    /// <summary>
    /// Replaces the full config object in memory. To change only some values, use <see cref="Edit"/>.
    /// This method does not write to disk. Call <see cref="SaveChanges"/> to write the config to disk.
    /// </summary>
    /// <param name="config">The new config object.</param>
    // should be public when i get through it
    internal void Write(T config)
    {
        currentConfig = config;
    }
    /// <summary>
    /// Gets the config object that is in memory.
    /// </summary>
    /// <returns>The config object. Changes to it stay in memory until you call <see cref="SaveChanges"/>.</returns>
    /// <exception cref="InvalidOperationException">There is no config in memory. This occurs when there is no config on disk and you did not call <see cref="SetDefaultConfig"/>.</exception>
    // should be public when i get through it
    internal T Get()
    {
        return currentConfig ?? throw new InvalidOperationException("Must set default before reading config.");
    }
    /// <summary>
    /// Calls an action on the config object that is in memory, so that the action can change the config.
    /// </summary>
    /// <remarks>This method does not write to disk. Call <see cref="SaveChanges"/> after you edit the config.</remarks>
    /// <param name="editor">The action that changes the config object.</param>
    /// <exception cref="InvalidOperationException">There is no config in memory. This occurs when there is no config on disk and you did not call <see cref="SetDefaultConfig"/>.</exception>
    // should be public when i get through it
    internal void Edit(Action<T> editor)
    {
        editor(currentConfig ?? throw new InvalidOperationException("Must set default before reading config."));
    }
    /// <summary>
    /// Gets one value from the config object that is in memory.
    /// </summary>
    /// <typeparam name="TResult">The type of the value.</typeparam>
    /// <param name="getter">The function that gets the value from the config object.</param>
    /// <returns>The value that <paramref name="getter"/> gives.</returns>
    /// <exception cref="InvalidOperationException">There is no config in memory. This occurs when there is no config on disk and you did not call <see cref="SetDefaultConfig"/>.</exception>
    // should be public when i get through it
    internal TResult Get<TResult>(Func<T, TResult> getter)
    {
        return getter(currentConfig ?? throw new InvalidOperationException("Must set default before reading config."));
    }
    /// <summary>
    /// Writes the config object that is in memory to disk as indented JSON.
    /// For <see cref="ConfigTypes.PerMod"/>, the method replaces the file <c>Mods/{modName}.json</c>.
    /// For <see cref="ConfigTypes.Exposed"/>, the method replaces the value with the mod name as its key in <c>mods.json</c>.
    /// It keeps the values of other mods in <c>mods.json</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The config type is not a known <see cref="ConfigTypes"/> value.</exception>
    // should be public when i get through it
    internal void SaveChanges()
    {
        switch (configType)
        {
            case ConfigTypes.PerMod:
                var perModJson = JsonSerializer.Serialize(currentConfig, indentedJsonOptions);
                File.WriteAllText(perModConfigPath, perModJson);
                break;
            case ConfigTypes.Exposed:
                var modsConfigText = File.ReadAllText(ExposedConfigPath);
                var modsConfigJson = JsonNode.Parse(modsConfigText)!.AsObject();
                modsConfigJson[modName] = JsonSerializer.SerializeToNode(currentConfig!);
                File.WriteAllText(ExposedConfigPath, modsConfigJson.ToJsonString(indentedJsonOptions));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// The places on disk for a config.
    /// </summary>
    // should be public when i get through it
    internal enum ConfigTypes
    {
        /// <summary>The config is in its own file, <c>Mods/{modName}.json</c>.</summary>
        PerMod,
        /// <summary>The config is a value in the shared <c>mods.json</c> file in the game folder, with the mod name as its key.</summary>
        Exposed
    }
}
