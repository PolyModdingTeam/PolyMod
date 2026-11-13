# Porting a mod from PolyMod 1.x

PolyMod 2.0 changes several file formats. Check this list when you update a mod that was made for PolyMod 1.x and the old wiki.

| Area | PolyMod 1.x | PolyMod 2.0 | Page |
| --- | --- | --- | --- |
| Texture names | `name_style_level.png` with empty parts, for example `lumberhut__.png` | `name.png`, `name_tribe.png`, `name_skin.png`, `name_level_tribe.png`, … without empty parts, for example `lumberhut.png` | [Textures](textures.md) |
| Terrain, resource and improvement textures | Internal game sprite names | The GLD ids also work, for example `field.png` | [Textures](textures.md#base-names-of-terrain-resources-and-improvements) |
| `sprites.json` | `pixelsPerUnit`, `pivot` | Also `spriteOrigin`, `transformToOutline`, `gaussianBlurRadius` | [Sprites](sprites.md) |
| Prefabs | `<name>.json` with `type`, `name` and `visualParts` (`gameObjectName`, `baseName`, `scale`) | `prefabs.json`, `unitPrefab.json`, `improvementPrefab.json`, `resourcePrefab.json` with `skinnedKey` and `visualParts` (`defaultSpriteName`, …) | [Prefabs](prefabs.md) |
| Reusing a vanilla unit look | `"prefab": "mindbender"` in `unitData` | Not supported. Copy the dumped vanilla prefab into a `unitPrefab` file. | [Prefabs](prefabs.md#unit-prefabs) |
| New improvement or resource look | Custom house or animal sprites by default | `"prefab": true` in the GLD entry, or a prefab file | [Prefabs](prefabs.md#simple-prefabs-from-the-gld) |
| Arrays in patches | Before 1.2: always replaced. Since 1.2: `+`/`-` items | `"+value"` adds, `"-value"` removes; plain arrays still replace; plain strings in `skins` add. The old wiki only describes the pre-1.2 rule. | [GLD patching](gld-patching.md#merge-rules) |
| `null` in patches | The old wiki says it removed the value | Sets the value to `null`; the key stays | [GLD patching](gld-patching.md#merge-rules) |
| Config templates | `{% if config "key" true %}` examples | Not supported at this time. Write fixed values in the patch. | — |
| Tribe preview coordinates | Shown in debug mode | Shown with the **Show coordinates** setting, or in the dumped `preview_<tribe>.json` | [Tribe previews](tribe-previews.md#finding-coordinates) |
| Manifest | `id`, `name`, `version`, `authors`, `dependencies` | Also `description` and `client` | [Manifest](manifest.md) |
| Localization files | Only `localization.json` | Also `localization_*.json`, and `language_<code>.json` for new languages | [Localization](localization.md) |
| PolyScripts | `public static void Load(ManualLogSource)` | The same, and `public static void Load()` also works | [PolyScripts](polyscripts.md) |
| Audio | — | Not supported yet | [Mod structure](mod-structure.md#how-polymod-reads-a-mod) |

## GLD changes in the game

The game itself also changed since the old wiki, which was written for Polytopia 2.13 and GLD version 22. Some examples:

- `unitData.*.promotionLimit` no longer exists. The promotion limit is 3, or 2 with the Discipline player ability.
- `diplomacyData.embassyTechPriceMultiplier` no longer exists.
- `tribeData.*.style` and `climate` are no longer read by the game. PolyMod fills them for new tribes.
- `tribeData.*.startingStars` is new.
- There are new enum values, for example the tribe abilities, unit abilities, the `algaespawn` improvement and the `convert` task.

The [GLD reference](../gld/index.md) lists the current fields and values.
