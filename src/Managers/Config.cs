using System.Text.Json;
using System.Text.Json.Nodes;

namespace PolyMod.Managers;

/// <summary>
/// allows mods to save config.
/// </summary>
public class Config<T> where T : class
{
    private T? _currentConfig;
    private readonly string _modName;
    private readonly ConfigTypes _configType;
    private static readonly string ExposedConfigPath = Path.Combine(Plugin.BASE_PATH, "mods.json");
    private readonly string _perModConfigPath;
    private T? _defaultConfig;
    public Config(string modName, ConfigTypes configType)
    {
        _modName = modName;
        _configType = configType;
        _perModConfigPath = Path.Combine(Plugin.MODS_PATH, $"{modName}.json");
        Load();
    }

    internal void Load() // can be called internally if config changes; gui config not implemented yet
    {
        switch (_configType)
        {
            case ConfigTypes.PerMod:
            {
                if (!File.Exists(_perModConfigPath))
                {
                    return;
                }
                var jsonText = File.ReadAllText(_perModConfigPath);
                _currentConfig = JsonSerializer.Deserialize<T>(jsonText);
                break;
            }
            case ConfigTypes.Exposed:
            {
                if (!File.Exists(ExposedConfigPath))
                {
                    return;
                }
                var jsonText = File.ReadAllText(ExposedConfigPath);
                _currentConfig = JsonNode.Parse(jsonText)![_modName]!.Deserialize<T>();
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
        _defaultConfig = defaultValue;
        if (_currentConfig is not null) return;
        Write(_defaultConfig);
        SaveChanges();
    }
    
    /// <summary>
    /// Writes the **entire** config. Usage not recommended, use Edit() instead
    /// </summary>
    public void Write(T config)
    {
        _currentConfig = config;
    }
    /// <summary>
    /// Gets the config. Should only be called after setting a default.
    /// </summary>
    public T Get()
    {
        return _currentConfig ?? throw new InvalidOperationException("Must set default before reading config.");
    }
    /// <summary>
    /// edits the config. Should only be called after setting a default.
    /// </summary>
    /// <remarks>Call SaveChanges after editing</remarks>
    public void Edit(Action<T> editor)
    {
        editor(_currentConfig ?? throw new InvalidOperationException("Must set default before reading config."));
    }
    /// <summary>
    /// Gets part of the config. Should only be called after setting a default
    /// </summary>
    public TResult Get<TResult>(Func<T, TResult> getter)
    {
        return getter(_currentConfig ?? throw new InvalidOperationException("Must set default before reading config."));
    }
    /// <summary>
    /// writes the config to disk
    /// </summary>
    public void SaveChanges()
    {
        switch (_configType)
        {
            case ConfigTypes.PerMod:
                var perModJson = JsonSerializer.Serialize(_currentConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_perModConfigPath, perModJson);
                break;
            case ConfigTypes.Exposed:
                var modsConfigText = File.ReadAllText(ExposedConfigPath);
                var modsConfigJson = JsonNode.Parse(modsConfigText)!.AsObject();
                modsConfigJson[_modName] = JsonSerializer.SerializeToNode(_currentConfig!);
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