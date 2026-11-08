using PolyMod;

namespace Tests;

public class CheckDependenciesTests
{
    [Fact]
    public void MissingRequiredDependencyMarksModUnsatisfied()
    {
        var mod = ModFactory.MakeMod("b", dependencies: new Mod.Dependency("ghost", null, null));
        Loader.CheckDependencies(ModFactory.Mods(ModFactory.MakeMod("a"), mod));
        Assert.Equal(Mod.Status.DependenciesUnsatisfied, mod.status);
    }

    [Fact]
    public void VersionBelowMinimumMarksModUnsatisfied()
    {
        var dep = new Mod.Dependency("a", min: new Version(2, 0, 0), max: null);
        var mod = ModFactory.MakeMod("b", dependencies: dep);
        Loader.CheckDependencies(ModFactory.Mods(ModFactory.MakeMod("a"), mod));
        Assert.Equal(Mod.Status.DependenciesUnsatisfied, mod.status);
    }

    [Fact]
    public void VersionAboveMaximumMarksModUnsatisfied()
    {
        var dep = new Mod.Dependency("a", min: null, max: new Version(1, 0, 0));
        var mod = ModFactory.MakeMod("b", dependencies: dep);
        Loader.CheckDependencies(ModFactory.Mods(ModFactory.MakeMod("a", version: new Version(2, 0, 0)), mod));
        Assert.Equal(Mod.Status.DependenciesUnsatisfied, mod.status);
    }

    [Fact]
    public void VersionEqualToBoundsIsAllowed()
    {
        var dep = new Mod.Dependency("a", min: new Version(2, 0, 0), max: new Version(2, 0, 0));
        var mod = ModFactory.MakeMod("b", dependencies: dep);
        Loader.CheckDependencies(ModFactory.Mods(ModFactory.MakeMod("a", version: new Version(2, 0, 0)), mod));
        Assert.Equal(Mod.Status.Success, mod.status);
    }

    [Fact]
    public void SatisfiedDependencyKeepsModSuccessful()
    {
        var dep = new Mod.Dependency("a", min: new Version(1, 0, 0), max: new Version(2, 0, 0));
        var mod = ModFactory.MakeMod("b", dependencies: dep);
        Loader.CheckDependencies(ModFactory.Mods(ModFactory.MakeMod("a"), mod));
        Assert.Equal(Mod.Status.Success, mod.status);
    }

    [Fact]
    public void MissingOptionalDependencyOnlyWarns()
    {
        var dep = new Mod.Dependency("ghost", null, null, required: false);
        var mod = ModFactory.MakeMod("b", dependencies: dep);
        Loader.CheckDependencies(ModFactory.Mods(mod));
        Assert.Equal(Mod.Status.Success, mod.status);
    }
}
