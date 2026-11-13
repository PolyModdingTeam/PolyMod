# Introduction

PolyMod is the mod loader for The Battle of Polytopia. It runs as a [BepInEx](https://github.com/BepInEx/BepInEx) plugin inside the game, finds the mods in the `Mods` folder, and loads their content: game logic changes, sprites, translations, prefabs and code.

This documentation is for **PolyMod 2.0** (`2.0.0-pre`). It was written against Polytopia `2.18.0.16751`, the version that PolyMod 2.0 is built for, and against game logic data (GLD) version 28.

> [!NOTE]
> These pages replace the old GitHub wiki, which described the PolyMod 1.x API. A lot of the formats changed in 2.0, for example the texture names, the prefab files and the GLD patch merge rules. If you are porting a 1.x mod, read [Porting a mod from PolyMod 1.x](porting.md).

## What a mod can do

| You want to… | Use | Page |
| --- | --- | --- |
| Describe your mod (id, version, authors, dependencies) | `manifest.json` | [Manifest](manifest.md) |
| Change units, tribes, techs, buildings and other game rules | `patch.json` | [GLD patching](gld-patching.md), [Game logic data](../gld/index.md) |
| Let players configure values of your patch | `{{ config }}` templates | [Config](config.md) |
| Add names and descriptions for new content, or translate it | `localization.json` | [Localization](localization.md) |
| Add a completely new language | `language_<code>.json` | [Localization](localization.md#adding-a-new-language) |
| Replace or add images | `.png` files | [Textures](textures.md) |
| Set the pivot, scale, outline or blur of images | `sprites.json` | [Sprites](sprites.md) |
| Build the look of a new unit, building or resource | prefab files | [Prefabs](prefabs.md) |
| Change the tribe preview map | `preview` in a tribe | [Tribe previews](tribe-previews.md) |
| Change game code | `.dll` PolyScripts | [PolyScripts](polyscripts.md) |

## Where to start

1. Read [Mod structure](mod-structure.md) to see how a mod is laid out and how PolyMod finds its files.
2. Follow [Your first mod](getting-started.md) to make a working mod in a few minutes.
3. Turn on debug mode and dump the game data, as [Debugging and dumping](debugging.md) explains. The dumped `GameLogicData` file shows every vanilla value that you can change.
4. Use the [Game logic data reference](../gld/index.md) to look up what each GLD field does.

## Getting help

The community on [Discord](https://discord.gg/eWPdhWtfVy) is happy to help with questions about modding. You can also look at the [PolyMod source code](https://github.com/PolyModdingTeam/PolyMod) and the generated [API reference](xref:PolyMod).
