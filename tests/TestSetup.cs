using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using PolyMod;

namespace Tests;

internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Init()
    {
        var root = AppContext.BaseDirectory;
        var prop = typeof(BepInEx.Paths).GetProperty(
            nameof(BepInEx.Paths.BepInExRootPath),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        try
        {
            prop?.SetValue(null, root);
        }
        catch (ArgumentException)
        {
            var field = typeof(BepInEx.Paths)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(f => f.Name.Contains("BepInExRootPath", StringComparison.Ordinal));
            field?.SetValue(null, root);
        }
        Plugin.logger ??= new ManualLogSource("tests");
    }
}
