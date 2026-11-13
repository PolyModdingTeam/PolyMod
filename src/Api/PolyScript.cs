using BepInEx.Logging;
// using Newtonsoft.Json.Linq;

namespace PolyMod.Api;

/// <summary>
/// The base class of all mod scripts. Do not use this class directly.
/// To make a mod script, make a subclass of <see cref="PolyScript"/>.
/// </summary>
/// <remarks>
/// When PolyMod loads a mod assembly, it finds the first type in the assembly that is a subclass of this class.
/// It makes an instance of that type, calls <see cref="Initialize"/>, and then calls <see cref="Load"/>.
/// </remarks>
// should be public when i get through it
internal abstract class PolyScriptBase
{
    /// <summary>
    /// Gives the mod and the logger to the script. PolyMod calls this method before <see cref="Load"/>.
    /// </summary>
    /// <param name="mod">The mod that the script belongs to.</param>
    /// <param name="logger">The log source of the mod.</param>
    internal abstract void Initialize(Mod mod, ManualLogSource logger);

    /// <summary>
    /// Starts the mod script. PolyMod calls this method one time, after it loads the mod assembly.
    /// Put the code that starts your mod here, for example the code that applies your Harmony patches.
    /// </summary>
    // should be public when i get through it
    internal abstract void Load();

    /// <summary>
    /// Stops the mod script. PolyMod does not call this method at this time.
    /// </summary>
    // should be public when i get through it
    internal abstract void Unload();

    /// <summary>
    /// Makes a script. The constructor is internal, so only classes in PolyMod can be direct subclasses.
    /// </summary>
    internal PolyScriptBase()
    {
    }
}
/// <summary>
/// The base class for a mod script.
/// </summary>
/// <remarks>
/// Make a subclass and override <see cref="PolyScriptBase.Load"/>.
/// Before PolyMod calls <see cref="PolyScriptBase.Load"/>, it sets the <see cref="Mod"/> and <see cref="Logger"/> properties.
/// </remarks>
// should be public when i get through it
// internal abstract class PolyScript<TConfig, TExposedConfig> : PolyScriptBase where TConfig : class where TExposedConfig : class
internal abstract class PolyScript : PolyScriptBase
{
    /// <summary>
    /// Sets the <see cref="Mod"/> and <see cref="Logger"/> properties.
    /// </summary>
    /// <param name="mod">The mod that the script belongs to.</param>
    /// <param name="logger">The log source of the mod.</param>
    internal override void Initialize(Mod mod, ManualLogSource logger)
    {
        Mod = mod;
        // Config = new Config<TConfig>(mod.id, Config<TConfig>.ConfigTypes.PerMod);
        // ExposedConfig = new Config<TExposedConfig>(mod.id, Config<TExposedConfig>.ConfigTypes.Exposed);
        Logger = logger;
    }

    /// <summary>
    /// Gets the contents of a file of the mod.
    /// </summary>
    /// <param name="fileName">The name of the file in the mod. The name must match exactly.</param>
    /// <returns>The bytes of the file, or null if the mod does not have a file with that name.</returns>
    internal byte[]? GetFile(string fileName)
    {
        return Registry.mods[Mod.id].files.FirstOrDefault(f => f.name == fileName)?.bytes;
    }

    /// <summary>
    /// Does nothing. Override this method to stop your mod. PolyMod does not call this method at this time.
    /// </summary>
    // should be public when i get through it
    internal override void Unload() { }

    /// <summary>
    /// Gets the mod that the script belongs to.
    /// </summary>
    // should be public when i get through it
    internal Mod Mod { get; private set; } = null!;
    // /// <summary>
    // /// Gets the per-mod config. PolyMod keeps it in the file <c>Mods/{mod id}.json</c>.
    // /// Call <see cref="PolyMod.Api.Config{T}.SetDefaultConfig"/> before you read it.
    // /// </summary>
    // protected Config<TConfig> Config { get; private set; } = null!;
    // /// <summary>
    // /// Gets the exposed config. PolyMod keeps it in the shared <c>mods.json</c> file, with the mod id as its key.
    // /// Call <see cref="PolyMod.Api.Config{T}.SetDefaultConfig"/> before you read it.
    // /// </summary>
    // protected Config<TExposedConfig> ExposedConfig { get; private set; } = null!;
    /// <summary>
    /// Gets the log source of the mod. Use it to write messages to the BepInEx log.
    /// </summary>
    protected ManualLogSource Logger { get; private set; } = null!;
}

// /// <summary>
// /// The base class for a mod script that uses <see cref="JObject"/> for the per-mod config and the exposed config.
// /// Make a subclass and override <see cref="PolyScriptBase.Load"/>.
// /// </summary>
// // should be public when i get through it
// internal abstract class PolyScript : PolyScript<JObject, JObject>
// {
// }
