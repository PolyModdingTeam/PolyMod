using PolyMod;

namespace Tests;

internal static class ModFactory
{
    internal static Mod MakeMod(
        string id,
        Mod.Status status = Mod.Status.Success,
        Version? version = null,
        params Mod.Dependency[] dependencies) =>
        new(new Mod.Manifest(id, null, null, version ?? new Version(1, 0, 0), new[] { "author" }, dependencies),
            status, new());

    internal static Dictionary<string, Mod> Mods(params Mod[] mods) =>
        mods.ToDictionary(mod => mod.id);
}
