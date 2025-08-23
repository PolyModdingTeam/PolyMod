using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using MoonSharp.Interpreter.Serialization.Json;

namespace PolyMod.modApi;

[MoonSharpUserData]
public class LuaConfig : IUserDataType
{
    private readonly string modName;
    private readonly Config<JsonNode>.ConfigTypes configType;
    private readonly Script owner;
    private Table currentConfig;
    public readonly string ConfigPath;
    private IUserDataDescriptor userDataDescriptorImplementation;
    internal LuaConfig(string modName, Config<JsonNode>.ConfigTypes configType, Script owner)
    {
        this.modName = modName;
        this.configType = configType;
        this.owner = owner;

        switch (configType)
        {
            case Config<JsonNode>.ConfigTypes.Exposed:
                ConfigPath = Config<JsonNode>.ExposedConfigPath;
                break;

            case Config<JsonNode>.ConfigTypes.PerMod:
                ConfigPath = Path.Combine(Constants.MODS_PATH, $"{modName}.json");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(configType), configType, "Invalid config type");
        }

        Load();
    }
    private void Load()
    {
        switch (configType)
        {
            case Config<JsonNode>.ConfigTypes.PerMod:
            {
                if (!File.Exists(ConfigPath))
                {
                    return;
                }
                var jsonText = File.ReadAllText(ConfigPath);
                currentConfig = JsonTableConverter.JsonToTable(jsonText, owner);
                break;
            }
            case Config<JsonNode>.ConfigTypes.Exposed:
            {
                if (!File.Exists(ConfigPath))
                {
                    return;
                }
                var jsonText = File.ReadAllText(ConfigPath);
                var configText = JsonNode.Parse(jsonText)![modName]?.ToJsonString() ?? "{}";
                currentConfig = JsonTableConverter.JsonToTable(configText, owner);
                break;  
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void SaveChanges()
    {
        if (currentConfig.Keys.Any()) return;
        switch (configType)
        {
            case Config<JsonNode>.ConfigTypes.PerMod:
            {
                var json = currentConfig.TableToJson();
                File.WriteAllText(ConfigPath, json);
                break;
            }
            case Config<JsonNode>.ConfigTypes.Exposed:
            {
                var json = currentConfig.TableToJson();
                var modsConfigText = File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath) : "{}";
                var modsConfigJson = JsonNode.Parse(modsConfigText)!.AsObject();
                modsConfigJson[modName] = json;
                File.WriteAllText(ConfigPath, modsConfigJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
        
    }

    public DynValue Index(Script script, DynValue index, bool isDirectIndexing)
    {
        if (index.Type != DataType.String)
        {
            return DynValue.Nil;
        }
        if (index.String == "SaveChanges")
        {
            return DynValue.NewCallback(CallbackFunction.FromMethodInfo(script, typeof(LuaConfig).GetMethod("SaveChanges")));
        }
        string key = index.String;
        return DynValue.FromObject(script, currentConfig[key]);
    }
    public bool SetIndex(Script script, DynValue index, DynValue value, bool isDirectIndexing)
    {
        if (index.Type != DataType.String)
        {
            return false;
        }

        string key = index.String;
        currentConfig[key] = value;
        return true;
    }
    public DynValue MetaIndex(Script script, string metaname)
    {
        if (metaname == "__tostring")
            return DynValue.NewString(currentConfig.ToString());
        return DynValue.Nil;
    }
}
