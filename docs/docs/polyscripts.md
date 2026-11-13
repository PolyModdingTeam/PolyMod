# PolyScripts

A PolyScript is a compiled .NET class library (a `.dll` file) in a mod. It lets a mod change the code of the game at runtime, which JSON files cannot do: new ability behavior, new UI, new rules.

PolyScripts use [HarmonyX](https://github.com/BepInEx/HarmonyX) to patch methods of the game without changing the game files. The game runs on IL2CPP, so the game types come from the interop assemblies that BepInEx generates.

> [!TIP]
> The official [PolyScript template](https://github.com/PolyModdingTeam/PolyScriptTemplate) gives you a ready project.

## Entry point

PolyMod loads every `.dll` file of a mod. In each assembly, it calls every **public static** method named `Load` that has one of these signatures, on every type:

```cs
public static void Load(ManualLogSource logger)
public static void Load()
```

The `ManualLogSource` writes to the BepInEx log with the prefix `[PolyMod] [<mod id>]`. Use it to see what your script does.

```cs
using BepInEx.Logging;
using HarmonyLib;

namespace MyCoolMod;

public static class MyCoolScript
{
    internal static ManualLogSource? Logger;

    public static void Load(ManualLogSource logger)
    {
        Logger = logger;
        Harmony.CreateAndPatchAll(typeof(MyCoolScript));
        logger.LogMessage("PolyScript loaded!");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
    private static void GameManager_Update()
    {
        // Your code here.
    }
}
```

### When `Load` runs

PolyMod calls `Load` very early: when BepInEx loads PolyMod, before the game shows its splash screen and before it reads the game logic data. At that time most game objects do not exist yet. Use `Load` to apply Harmony patches and to register [GLD extensions](#extending-the-gld), and do the real work in your patches.

The mods load in [load order](mod-structure.md#load-order), so the scripts of your dependencies are already loaded.

### Errors

If a `Load` method throws an exception, PolyMod writes the message to the log and sets the status of the mod to *Error*. PolyMod then does not load the patches, localization, textures and prefabs of that mod.

> [!WARNING]
> PolyMod calls *every* public method named `Load` with these signatures, in *every* `.dll` of the mod. Do not use the name `Load` for other public methods with these signatures, also not for instance methods: PolyMod would try to call them too. If your mod contains library `.dll` files, they are loaded the same way.

## Building a PolyScript

1. Make a .NET 6 class library project. You need the [.NET SDK](https://dotnet.microsoft.com/download) 6 or newer.
2. Add the package sources and the PolyMod package. The PolyMod package also brings the game and BepInEx references.

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net6.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <RestoreAdditionalProjectSources>
            https://api.nuget.org/v3/index.json;
            https://nuget.bepinex.dev/v3/index.json;
            https://nuget.samboy.dev/v3/index.json;
            https://polymod.dev/nuget/v3/index.json;
        </RestoreAdditionalProjectSources>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="PolyMod" Version="2.0.0-pre" />
    </ItemGroup>
</Project>
```

   Use the PolyMod version that your players have. A floating version such as `*` does not pick pre-release versions such as `2.0.0-pre`.

3. Build the project:

   ```bash
   dotnet build
   ```

4. Copy the `.dll` from `bin/Debug/net6.0/` into your mod folder. It can be anywhere in the mod, but next to `manifest.json` is common.
5. Start the game and check the log for your message.

Do not copy the PolyMod, BepInEx or game assemblies into your mod. They are already loaded.

## Useful PolyMod API

PolyScripts can use the public API of PolyMod. Some useful members:

| Member | Use |
| --- | --- |
| `PolyMod.Registry.GetSprite(name, tribe, skin, level)` | Gets a mod sprite with the [texture name rules](textures.md#texture-names). |
| `PolyMod.Registry.GetSpriteByText(...)` | The same with tribe, skin and level as text. |
| `PolyMod.Loader.AddPatchDataType(section, enumType)` | Registers a new GLD section whose entries become values of an enum. |
| `PolyMod.Loader.AddTypeHandler(enumType, handler)` | Runs code for each GLD entry of a section. |
| `PolyMod.Managers.Visual.BuildSprite(bytes, pivot, pixelsPerUnit)` | Makes a sprite from PNG bytes. |
| `PolyMod.Managers.Visual.ShowSetWidth(popup, width)` | Shows a legacy popup with a custom width. |
| `PolyMod.Managers.Prefabs.unitInfos`, `improvementInfos`, `resourceInfos` | The prefab descriptions that PolyMod will make. |
| `PolyMod.Plugin.MODS_PATH`, `BASE_PATH`, `DUMPED_DATA_PATH` | Paths of the `Mods` folder, the game folder and the dump folder. |
| `PolyMod.Json.JsonMerger.Merge(original, patch)` | Merges JSON with the [GLD merge rules](gld-patching.md#merge-rules). |

See the [API reference](xref:PolyMod) for all members.

### Extending the GLD

The game turns ids into enum values with `EnumCache<T>`. PolyMod adds new values for GLD entries with `"idx": -1`. Your script can use the same system for its own content:

```cs
public static void Load(ManualLogSource logger)
{
    // Entries of the GLD section "unitAbility" become UnitAbility.Type values.
    // PolyMod registers this section already; this shows the API for your own sections.
    Loader.AddPatchDataType("unitAbility", typeof(UnitAbility.Type));

    // Runs for each entry of the section. The bool is true in the first pass,
    // just after PolyMod created the enum value of a new entry, and false in the
    // second pass, which runs for every entry.
    Loader.AddTypeHandler(typeof(UnitAbility.Type), (entry, isNewEntry) =>
    {
        logger.LogInfo($"Unit ability entry: {entry.Path}");
    });
}
```

To check for a new ability in a Harmony patch, get its enum value by its id:

```cs
UnitAbility.Type berserk = EnumCache<UnitAbility.Type>.GetType("berserk");
if (unitData.HasAbility(berserk)) { /* ... */ }
```

`AddTypeHandler` only works for a type that has a section registered with `AddPatchDataType` (or by PolyMod). Register both in `Load`, because PolyMod reads the GLD after all scripts are loaded.

## Configuration

PolyMod 2.0 contains a class-based script API: a `PolyScript` base class with typed per-mod and shared config objects (`Mods/<mod id>.json` and `mods.json`). In the current build (`2.0.0-pre`) these classes are internal, so mods cannot use them yet. Until they are public, use static `Load` methods, and use [GLD config templates](config.md) for values that players can change.
