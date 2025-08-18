using Newtonsoft.Json.Linq;
using PolyMod.Managers;

namespace PolyMod;

public abstract class PolyScriptMod<TConfig, TExposedConfig> where TConfig : class where TExposedConfig : class
{
    internal void Initialize(string name)
    {
        ModName = name;
        Config = new Config<TConfig>(name, Config<TConfig>.ConfigTypes.PerMod);
        ExposedConfig = new Config<TExposedConfig>(name, Config<TExposedConfig>.ConfigTypes.Exposed);
    }
    public string ModName { get; private set; }
    protected Config<TConfig> Config { get; private set; } = null!;
    protected Config<TExposedConfig> ExposedConfig { get; private set; } = null!;
    protected virtual JObject DefaultConfig => new JObject();
    public abstract void OnLoad();
}

public abstract class PolyScriptMod : PolyScriptMod<JObject, JObject>
{
}