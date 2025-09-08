using BepInEx.Logging;
using Newtonsoft.Json.Linq;
using PolyMod.Managers;

namespace PolyMod;

public abstract class PolyScriptBase
{
    internal abstract void Initialize(string name, ManualLogSource logger);
    public abstract void Load();
    public abstract void UnLoad();
    internal PolyScriptBase()
    {
    }
}
public abstract class PolyScript<TConfig, TExposedConfig> : PolyScriptBase where TConfig : class where TExposedConfig : class
{
    internal override void Initialize(string name, ManualLogSource logger)
    {
        ModName = name;
        Config = new Config<TConfig>(name, Config<TConfig>.ConfigTypes.PerMod);
        ExposedConfig = new Config<TExposedConfig>(name, Config<TExposedConfig>.ConfigTypes.Exposed);
        Logger = logger;
    }

    public string ModName { get; private set; } = null!;
    protected Config<TConfig> Config { get; private set; } = null!;
    protected Config<TExposedConfig> ExposedConfig { get; private set; } = null!;
    protected ManualLogSource Logger { get; private set; } = null!;
}

public abstract class PolyScript : PolyScript<JObject, JObject>
{
}