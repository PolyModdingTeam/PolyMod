# TribeData

`tribeData` lists the tribes: their color, city names, start, abilities, skins and the tribe-specific versions of techs, units and improvements.

The example uses Cymanti (values from an older game version):

```json
"cymanti": {
  "idx": 17,
  "color": 12778752,
  "language": "cy,zi,phy,dio,bio,tic,sy,ca,ly,um,sis,ci,hex,pod,ea,rhi,the,exo,neo,ner,seg,ho,lo,chy,ia,ce,li,co,ta,my,aph,ich,zo,di,bo",
  "startingStars": 5,
  "bonus": 0,
  "startingUnit": "shaman",
  "category": 2,
  "priceTier": 2,
  "terrainModifier": { "mountain": 1.2 },
  "resourceModifier": { "spores": 0.2, "crop": 0 },
  "startingTech": ["basic", "farming"],
  "startingResource": ["spores"],
  "tribeAbilities": ["poisonresist"],
  "techOverrides": {
    "construction": "recycling",
    "chivalry": "shock",
    "aquaculture": "hydrology",
    "sailing": "pescetism",
    "fishing": "fishingsimple",
    "navigation": "oceanology"
  },
  "unitOverrides": {
    "rider": "hexapod",
    "giant": "centipede",
    "knight": "doomux",
    "archer": "phychi",
    "catapult": "exida",
    "defender": "kiton",
    "mindbender": "shaman"
  },
  "improvementOverrides": {
    "market": "clathrus",
    "farm": "fungi",
    "road": "mycelium",
    "bridge": "none"
  }
}
```

## Fields

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| [`idx`](#idx) | integer | — | The number of the tribe. `-1` for a new tribe. |
| [`color`](#color) | integer | `0` | The tribe color. |
| [`language`](#language) | string | — | The syllables for city names. |
| [`startingStars`](#startingstars) | integer | `5` | The stars at the start of a game. |
| [`bonus`](#bonus) | enum number | `0` | A special bonus at the start of a game. |
| [`startingUnit`](#startingunit) | unit id | — | The unit in the capital at the start. |
| [`category`](#category) | enum number | `0` | Where the tribe shows in tribe selection. |
| [`priceTier`](#pricetier) | enum number | `0` | The store price tier of the tribe. |
| [`skins`](#skins) | list of skin names | `[]` | The skins of the tribe. |
| [`terrainModifier`](#terrainmodifier) | terrain id map | `{}` | Multipliers for terrain generation. |
| [`resourceModifier`](#resourcemodifier) | resource id map | `{}` | Multipliers for resource generation. |
| [`startingTech`](#startingtech) | tech id list | `[]` | The techs that the tribe has at the start. |
| [`startingResource`](#startingresource) | resource id list | `[]` | Resources placed near the capital at the start. |
| [`tribeAbilities`](#tribeabilities) | tribe ability id list | `[]` | Special rules of the tribe. |
| [`techOverrides`](#overrides) | tech id→id map | `{}` | Tribe versions of techs. |
| [`unitOverrides`](#overrides) | unit id→id map | `{}` | Tribe versions of units. |
| [`improvementOverrides`](#overrides) | improvement id→id map | `{}` | Tribe versions of improvements. |
| [`preview`](#preview) | array | — | **PolyMod.** Changes to the tribe preview map. |

The name of a tribe is the localization key `tribes.<id>`, and its description is `tribes.<id>.info`.

## Vanilla tribes

| Id | idx | | Id | idx |
| --- | --- | --- | --- | --- |
| `none` | 0 | | `luxidoor` | 9 |
| `nature` | 1 | | `oumaji` | 10 |
| `aimo` | 2 | | `quetzali` | 11 |
| `aquarion` | 3 | | `vengir` | 12 |
| `bardur` | 4 | | `xinxi` | 13 |
| `elyrion` | 5 | | `yadakk` | 14 |
| `hoodrick` | 6 | | `zebasi` | 15 |
| `imperius` | 7 | | `polaris` | 16 |
| `kickoo` | 8 | | `cymanti` | 17 |

`none` and `nature` are not playable tribes.

## idx

The number of the tribe in the `TribeType` enum. Use `-1` for a new tribe; see [Adding new content](../docs/gld-patching.md#new-tribes).

## color

The tribe color as an RGB number: `red × 65536 + green × 256 + blue`. For example, `12778752` is `0xC2FD00`. A [skin](skin-data.md) with a color above 0 replaces it. If two players in a game would have the same color, the game gives one of them the unused color of another tribe.

## language

The syllables that the game combines into names, separated by commas. The game uses them for city names and for the names of AI players. It joins random syllables until the name has about 3 to 6 characters, and capitalizes the first letter (except for Elyrion). A [skin](skin-data.md) can have its own syllables.

## startingStars

The stars that the player has at the start of a game. Default `5`. (In the tutorial, the game uses 0.)

## bonus

A special rule at the start of a game. The value is a number, or the name as a string:

| Value | Name | Effect |
| --- | --- | --- |
| 0 | `None` | No bonus. |
| 1 | `CityLevel3` | The capital starts at level 3. Luxidoor uses it. |
| 2 | `Spores` | Places a `fungi` improvement on the first field tile next to the capital. Not used by vanilla tribes. |

## startingUnit

The id of the unit that the game puts in the capital at the start of a game. It can move and attack in the first turn. The game uses this unit as it is: `unitOverrides` do not apply to it.

## category

Where the tribe shows in the tribe selection. A number, or the name as a string:

| Value | Name | Meaning |
| --- | --- | --- |
| 0 | `Hidden` | Not shown in the tribe selection. |
| 1 | `Human` | Shown with the regular tribes. |
| 2 | `Special` | Shown with the special tribes. |

The category only changes the tribe selection screen. Random tribes and AI players can still be any tribe, also a `Hidden` one.

## priceTier

The store price tier: `0` (`Tier0`) to `3` (`Tier3`). The game reads this field, but no game code uses it. PolyMod unlocks all custom tribes anyway.

## skins

The skins that players can choose for this tribe, by skin name, for example `["Baerion"]`. Vanilla skin names are not case-sensitive here. A name that is not a vanilla skin creates a new skin; see [New skins](../docs/gld-patching.md#new-skins). The skin ids are listed in [SkinData](skin-data.md#vanilla-skins).

In a patch, a plain string in `skins` **adds** the skin, and `"-name"` removes it. See [Merge rules](../docs/gld-patching.md#merge-rules).

## terrainModifier

Multipliers for the terrain that map generation makes on the land of this tribe. The keys are [terrain ids](terrain-data.md#vanilla-terrains) and the values are numbers: `1` is normal, `1.2` is 20% more, `0` is none.

Map generation divides the land of a tribe between `field`, `forest` and `mountain` in proportion to their weights (base weights 0.5, 0.4 and 0.15). Only these three keys have a visible effect. `water` is only used when its value is above 1, and `ocean`, `ice`, `wetland` and `mangrove` have no effect found in the map generator.

## resourceModifier

Chances for the resources that map generation places on the land of this tribe. The keys are [resource ids](resource-data.md#vanilla-resources) and the values are numbers.

- For a resource with a base chance above 0, the value is a **multiplier**: `1` is normal, `0` is none. The base chances are 0.5 for `crop`, `fish`, `fruit` and `game`, 0.8 for `metal` and 0.3 for `aquacrop`.
- For a resource with a base chance of 0 (`whale` and `spores`), the value **is** the chance. So `"spores": 0.2` gives spores a chance of 0.2.
- `starfish` is not placed by map generation, whatever the value.
- New resources have a base chance of 0, so their value is their chance.

A resource can only be placed on the terrains in its [`resourceTerrainRequirements`](resource-data.md#resourceterrainrequirements).

## startingTech

The techs that the tribe gets for free at the start of a game. Every vanilla tribe has `basic`, which is the root of the tech tree.

## startingResource

Resources that the game guarantees on the 8 tiles around the capital. Each entry gives one tile, so list a resource twice to get two. Resources that are already there count. If no tile has a fitting terrain, the game changes the terrain of a tile to one that the resource needs.

If the list is empty, the game uses the resource that the first improvement of the starting techs needs.

## tribeAbilities

Special rules of the tribe, by tribe ability id:

| Id | Number | Effect |
| --- | --- | --- |
| `none` | 0 | No effect. |
| `rainbowvision` | 1 | Only visual: ruins show a rainbow fire for the player. Elyrion. |
| `alienclimate` | 2 | Map generation: the land of the tribe does not get the usual climate growth. For Polaris, water next to the capital becomes ice. |
| `spread` | 3 | When the tribe captures a city or its borders grow, the climate of the city area changes to the tribe's climate (for Polaris: the tiles freeze). Other tribes change these tiles back. |
| `freeze` | 4 | No effect in game logic; only an achievement checks it. The freezing of Polaris is tied to the tribe, not to this ability. |
| `poisonresist` | 5 | Units are not poisoned by improvements with the `poison` ability. Cymanti. |
| `flooded` | 6 | Map generation floods the field and forest tiles of the tribe; captured cities are flooded; ruins on water become cities for the tribe. Aquarion. |

Some Polaris behavior is tied to the tribe `polaris` itself, not to its abilities. New tribe abilities can be added with the PolyMod section `tribeAbility`; see [New abilities and weapons](../docs/gld-patching.md#new-abilities-and-weapons).

## Overrides

`techOverrides`, `unitOverrides` and `improvementOverrides` replace vanilla content with the tribe's own version. Each key is the id of the original content, and each value is the id of the tribe version:

```json
"unitOverrides": { "rider": "hexapod" }
```

The game applies overrides when it builds the tech tree of the tribe, lists the units that it can train, and lists the improvements that it can build. So where other tribes get a `rider`, this tribe gets a `hexapod`. Overrides do **not** apply to the `startingUnit`, to the improvements of tasks (monuments), to embark boats, or to unit growth.

To take an improvement away from a tribe, use `"none"` as the value, as Cymanti do with `"bridge": "none"`. The improvement then disappears from that tribe's build menu. This only works well for improvements:

- for units, `"none"` seems to crash the training list (found by reading the code, not tested);
- for techs, `"none"` is not a valid id and makes the game fail while it reads the GLD.

## preview

**PolyMod.** Changes the tiles of the preview map in tribe selection. See [Tribe previews](../docs/tribe-previews.md).

## style and climate

Older GLD versions had the fields `style` and `climate`. The game no longer reads them. PolyMod sets them for new tribes; you do not need to.
