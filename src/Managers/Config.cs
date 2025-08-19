using System.Text.Json;
using System.Text.Json.Nodes;

namespace PolyMod.Managers;

/// <summary>
/// Allows mods to save config.
/// </summary>
public class Config<T> where T : class
{
    private T? currentConfig;
    private readonly string modName;
    private readonly ConfigTypes configType;
    private static readonly string ExposedConfigPath = Path.Combine(Plugin.BASE_PATH, "mods.json");
    private readonly string perModConfigPath;
    private T? defaultConfig;
    public Config(string modName, ConfigTypes configType)
    {
        this.modName = modName;
        this.configType = configType;
        perModConfigPath = Path.Combine(Plugin.MODS_PATH, $"{modName}.json");
        Load();
    }

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
    /// Sets the default if the config does not exist yet. Always call this before reading from the config.
    /// </summary>
    public void SetDefaultConfig(T defaultValue)
    {
        defaultConfig = defaultValue;
        if (currentConfig is not null) return;
        Write(defaultConfig);
        SaveChanges();
    }
    
    /// <summary>
    /// Writes the **entire** config. Usage not recommended, use Edit() instead
    /// </summary>
    public void Write(T config)
    {
        currentConfig = config;
    }
    /// <summary>
    /// Gets the config. Should only be called after setting a default.
    /// </summary>
    public T Get()
    {
        return currentConfig ?? throw new InvalidOperationException("Must set default before reading config.");
    }
    /// <summary>
    /// Edits the config. Should only be called after setting a default.
    /// </summary>
    /// <remarks>Call SaveChanges after editing</remarks>
    public void Edit(Action<T> editor)
    {
        editor(currentConfig ?? throw new InvalidOperationException("Must set default before reading config."));
    }
    /// <summary>
    /// Gets part of the config. Should only be called after setting a default
    /// </summary>
    public TResult Get<TResult>(Func<T, TResult> getter)
    {
        return getter(currentConfig ?? throw new InvalidOperationException("Must set default before reading config."));
    }
    /// <summary>
    /// Writes the config to disk
    /// </summary>
    public void SaveChanges()
    {
        switch (configType)
        {
            case ConfigTypes.PerMod:
                var perModJson = JsonSerializer.Serialize(currentConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(perModConfigPath, perModJson);
                break;
            case ConfigTypes.Exposed:
                var modsConfigText = File.ReadAllText(ExposedConfigPath);
                var modsConfigJson = JsonNode.Parse(modsConfigText)!.AsObject();
                modsConfigJson[modName] = JsonSerializer.SerializeToNode(currentConfig!);
                File.WriteAllText(ExposedConfigPath, modsConfigJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public enum ConfigTypes
    {
        PerMod,
        Exposed
    }
}