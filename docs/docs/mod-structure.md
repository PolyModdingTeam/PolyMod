# Mod structure

A mod is a folder or an archive in the `Mods` folder of the game. The `Mods` folder is next to the `BepInEx` folder, in the game folder. PolyMod makes it the first time that it starts.

```
The Battle of Polytopia/
├── BepInEx/
├── Mods/
│   ├── my_mod/              ← a mod as a folder
│   │   ├── manifest.json
│   │   ├── patch.json
│   │   └── ...
│   └── other_mod.polymod    ← a mod as an archive
├── PolyMod.json             ← PolyMod settings
└── mods.json                ← config values of mods
```

## Mod types

PolyMod accepts three kinds of mod container:

| Container | Use it for |
| --- | --- |
| Folder | Development. You can edit the files and restart the game. |
| `.polymod` file | Distribution. It is a ZIP archive with a different extension. |
| `.zip` file | Also accepted. Prefer `.polymod`, so that players do not confuse the mod with a normal archive and unpack it. |

> [!IMPORTANT]
> In an archive, `manifest.json` must be at the root of the archive, not in a subfolder. Zip the *contents* of your mod folder, not the folder itself. If PolyMod does not find `manifest.json` at the root, it writes `Mod manifest not found` to the log and skips the mod.

## How PolyMod reads a mod

PolyMod reads **all** files of a mod, in all subfolders. It then decides what to do with each file from its **file name** (not its folder), so you can put files in subfolders such as `sprites/` or `patches/` to keep your mod tidy. The only exception is `manifest.json`, which must be at the root.

| File name | What PolyMod does | When | Page |
| --- | --- | --- | --- |
| `manifest.json` (root only) | Reads the mod metadata. | At game start | [Manifest](manifest.md) |
| `*.dll` | Loads the assembly and runs its `Load` methods. | At game start | [PolyScripts](polyscripts.md) |
| `sprites.json`, `sprites_*.json` | Reads sprite settings. | At game start | [Sprites](sprites.md) |
| `language.json`, `language_*.json` | Adds or changes a language. | At game start | [Localization](localization.md#adding-a-new-language) |
| `localization.json`, `localization_*.json` | Adds localization terms. | When the GLD loads | [Localization](localization.md) |
| `patch.json`, `patch_*.json` | Merges the patch into the GLD. | When the GLD loads | [GLD patching](gld-patching.md) |
| `prefabs.json`, `prefabs_*.json` | Reads unit, improvement and resource prefabs. | After the GLD merge | [Prefabs](prefabs.md) |
| `unitPrefab.json`, `unitPrefab_*.json` | Reads one unit prefab. | After the GLD merge | [Prefabs](prefabs.md) |
| `improvementPrefab.json`, `improvementPrefab_*.json` | Reads one improvement prefab. | After the GLD merge | [Prefabs](prefabs.md) |
| `resourcePrefab.json`, `resourcePrefab_*.json` | Reads one resource prefab. | After the GLD merge | [Prefabs](prefabs.md) |
| `*.png` | Loads a texture. | When the GLD loads | [Textures](textures.md) |
| `*.bundle` | Loads a Unity asset bundle. | When the GLD loads | [Asset bundles](#asset-bundles) |

All other files are ignored, but they still count for the [mod checksum](manifest.md#checksums-and-saved-games).

The name patterns are case-sensitive: `unitPrefab.json` works, `unitprefab.json` does not. The `_*` part can be any text, so you can split content into several files, for example `patch_units.json` and `patch_tribes.json`, or `localization_en.json`.

> [!NOTE]
> Audio files are not loaded in PolyMod 2.0. The audio manager exists, but it is a stub, and `.wav` loading is turned off in the loader.

## Load order

PolyMod loads the mods in this sequence:

1. It registers every mod in `Mods` and checks the manifests. It skips a mod with a missing or invalid manifest, a duplicate id, or an id in `disabledMods` of `PolyMod.json`.
2. It checks the [dependencies](manifest.md#dependencies). A mod with a missing required dependency gets the status *dependencies unsatisfied* and does not load.
3. It sorts the mods so that each mod comes after the mods that it depends on. If the dependencies make a cycle, PolyMod loads no mod content and shows a popup that only lets you exit the game.
4. In that order, it loads the `.dll`, `sprites` and `language` files of each mod.
5. When the game loads its game logic data, PolyMod goes through the mods again in the same order. It loads localization, merges patches and loads textures and asset bundles. Then it creates the enum values for new content, loads the prefab files, makes sprites and makes prefabs.

Only the dependencies give a fixed order. If your patch must apply after the patch of another mod, add that mod as a dependency, with `"required": false` if your mod also works without it.

Within one mod, files are processed in the order in which PolyMod reads them from the folder or archive. Do not rely on that order between two patch files that change the same value.

## Mod status

The PolyMod Hub (**Ctrl + Tab**, or the PolyMod button on the start screen) shows every mod with its status:

| Status | Meaning |
| --- | --- |
| Success | The mod loaded. |
| Error | An error occurred while PolyMod loaded the mod, for example a PolyScript threw an exception or a patch template failed. PolyMod stops loading the files of that mod. |
| Dependencies unsatisfied | A required dependency is missing or has a version outside the allowed range. |

The details of each error are in the BepInEx log, `BepInEx/LogOutput.log`. See [Debugging and dumping](debugging.md).

## Asset bundles

PolyMod loads every `.bundle` file as a Unity `AssetBundle` and keeps it in `Registry.assetBundles`, keyed by the file name without the extension. PolyMod itself does not use the assets in a bundle. Use them from a [PolyScript](polyscripts.md).
