using BepInEx.Logging;
using Newtonsoft.Json.Linq;
using PolyMod.Managers;

namespace PolyMod;

public abstract class PolyScriptModBase
{
    internal abstract void Initialize(string name, ManualLogSource logger);
    public abstract void Load();
    public abstract void UnLoad();
    internal PolyScriptModBase()
    {
    }
}
public abstract class PolyScriptMod<TConfig, TExposedConfig> : PolyScriptModBase where TConfig : class where TExposedConfig : class
{
    internal override void Initialize(string name, ManualLogSource logger)
    {
        ModName = name;
        Config = new Config<TConfig>(name, Config<TConfig>.ConfigTypes.PerMod);
        ExposedConfig = new Config<TExposedConfig>(name, Config<TExposedConfig>.ConfigTypes.Exposed);
        Logger = logger;
    }
    public string ModName { get; private set; }
    protected Config<TConfig> Config { get; private set; } = null!;
    protected Config<TExposedConfig> ExposedConfig { get; private set; } = null!;
    protected ManualLogSource Logger { get; private set; } = null!;
}

public abstract class PolyScriptMod : PolyScriptMod<JObject, JObject>
{
}