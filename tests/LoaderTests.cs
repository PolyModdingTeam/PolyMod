using PolyMod;

namespace Tests;

public class LoaderTests
{
    private static string[] Ids(Dictionary<string, Mod> mods) =>
        mods.Select(kv => kv.Key).ToArray();

    [Fact]
    public void DependencyLoadsBeforeDependent()
    {
        var mods = ModFactory.Mods(
            ModFactory.MakeMod("b", dependencies: new Mod.Dependency("a", null, null)),
            ModFactory.MakeMod("a"));
        Assert.True(Loader.SortMods(mods));
        Assert.Equal(new[] { "a", "b" }, Ids(mods));
    }

    [Fact]
    public void TransitiveChainSortsDeepestFirst()
    {
        var mods = ModFactory.Mods(
            ModFactory.MakeMod("c", dependencies: new Mod.Dependency("b", null, null)),
            ModFactory.MakeMod("b", dependencies: new Mod.Dependency("a", null, null)),
            ModFactory.MakeMod("a"));
        Assert.True(Loader.SortMods(mods));
        Assert.Equal(new[] { "a", "b", "c" }, Ids(mods));
    }

    [Fact]
    public void DependencyCycleReturnsFalseAndLeavesDictionaryUntouched()
    {
        var mods = ModFactory.Mods(
            ModFactory.MakeMod("a", dependencies: new Mod.Dependency("b", null, null)),
            ModFactory.MakeMod("b", dependencies: new Mod.Dependency("a", null, null)));
        Assert.False(Loader.SortMods(mods));
        Assert.Equal(new[] { "a", "b" }, Ids(mods));
    }

    [Fact]
    public void DependencyOnMissingModDoesNotBlockSorting()
    {
        var mods = ModFactory.Mods(
            ModFactory.MakeMod("b", dependencies: new Mod.Dependency("ghost", null, null)),
            ModFactory.MakeMod("a"));
        Assert.True(Loader.SortMods(mods));
        Assert.Equal(2, mods.Count);
    }

    [Fact]
    public void FailedModsSinkToEnd()
    {
        var mods = ModFactory.Mods(
            ModFactory.MakeMod("bad", Mod.Status.Error),
            ModFactory.MakeMod("good"));
        Assert.True(Loader.SortMods(mods));
        Assert.Equal(new[] { "good", "bad" }, Ids(mods));
    }
}
