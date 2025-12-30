using BepInEx.Logging;
using Newtonsoft.Json.Linq;

namespace PolyMod.Api;

public abstract class PolyScriptBase
{
    /// <summary>
    /// Initializes the PolyScript with the given mod and logger.
    /// </summary>
    /// <param name="mod">The mod instance.</param>
    /// <param name="logger">The logger instance.</param>
    internal abstract void Initialize(Mod mod, ManualLogSource logger);
    
    /// <summary>>
    /// Called when the mod is loaded.
    /// </summary>
    public abstract void Load();

    /// <summary>
    /// Called when the mod is unloaded.
    /// </summary>
    public abstract void Unload();

    internal PolyScriptBase()
    {
    }
}
public abstract class PolyScript<TConfig, TExposedConfig> : PolyScriptBase where TConfig : class where TExposedConfig : class
{
    internal override void Initialize(Mod mod, ManualLogSource logger)
    {
        Mod = mod;
        Config = new Config<TConfig>(mod.id, Config<TConfig>.ConfigTypes.PerMod);
        ExposedConfig = new Config<TExposedConfig>(mod.id, Config<TExposedConfig>.ConfigTypes.Exposed);
        Logger = logger;
    }

    internal byte[]? GetFile(string fileName)
    {
        return Registry.mods[Mod.id].files.FirstOrDefault(f => f.name == fileName)?.bytes;
    }

    public Mod Mod { get; private set; } = null!;
    protected Config<TConfig> Config { get; private set; } = null!;
    protected Config<TExposedConfig> ExposedConfig { get; private set; } = null!;
    protected ManualLogSource Logger { get; private set; } = null!;
}

public abstract class PolyScript : PolyScript<JObject, JObject>
{
}