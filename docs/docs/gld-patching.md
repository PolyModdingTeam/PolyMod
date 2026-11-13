# GLD patching

A GLD patch is a JSON file (`patch.json` or `patch_*.json`) that changes the [game logic data](../gld/index.md) (GLD): the stats of units, the tech tree, tribes, buildings and more. PolyMod merges every patch into the GLD before the game reads it.

## The basics

A patch is a small GLD that only contains the values that you want to change. It uses the same sections, ids and field names as the GLD. For example, this patch gives the warrior 100 attack:

```json
{
  "unitData": {
    "warrior": {
      "attack": 1000
    }
  }
}
```

All other values of the warrior, and all other units, stay as they are.

To find the ids and the current values, dump the GLD: see [Debugging and dumping](debugging.md). The [GLD reference](../gld/index.md) explains each field.

## Merge rules

PolyMod merges a patch into the GLD with these rules:

| In the patch | Result |
| --- | --- |
| An object | Merged recursively, field by field. Fields that the patch does not contain stay unchanged. |
| A number, string or boolean | Replaces the value in the GLD. If the field or entry does not exist, PolyMod adds it. |
| `null` | Sets the value in the GLD to `null`. The key stays. |
| An array of plain values | **Replaces** the whole array. |
| An array with `"+value"` items | Adds `value` to the end of the existing array. |
| An array with `"-value"` items | Removes every `value` from the existing array. |

### Changing arrays

Arrays such as `unitAbilities` or `techUnlocks` hold many values. To change only some of them, prefix strings with `+` or `-`:

```json
{
  "unitData": {
    "warrior": {
      "unitAbilities": ["+fortify", "-dash"]
    }
  },
  "techData": {
    "riding": {
      "unitUnlocks": ["+lancer"]
    }
  }
}
```

Rules for arrays:

- If the patch array contains at least one `+` or `-` string, PolyMod applies them to the existing array. It ignores the other plain strings in that patch array, but it adds items that are not strings, such as objects.
- If the patch array contains no `+` or `-` string, it replaces the whole existing array. Use this to set an exact list, or `[]` to clear it.
- `skins` arrays are special: a plain string *adds* the skin. So `"skins": ["myskin"]` adds a skin to a tribe and does not remove the vanilla skins. Use `"-skinname"` to remove a skin.

> [!NOTE]
> The `+`/`-` array syntax exists since PolyMod 1.2. Before 1.2, every array replaced the original array, as the old wiki describes.

### Order of patches

When several patches change the same value, the patch that PolyMod merges last wins. Patches merge in the [load order](mod-structure.md#load-order) of the mods. If your patch must come after the patch of another mod, add that mod as a dependency.

## Ids

GLD ids are the lowercase names of the content, such as `warrior`, `imperius` or `lumberhut`. **Always write ids in lowercase**, in keys and in values. The game looks up ids case-sensitively, and an unknown id makes the game fail while it reads the GLD. See [Ids](../gld/index.md#ids).

## Adding new content

To add new content, write an entry with a new id and set its `idx` to `-1`:

```json
{
  "unitData": {
    "lancer": {
      "idx": -1,
      "health": 100,
      "defence": 10,
      "movement": 2,
      "range": 1,
      "attack": 25,
      "cost": 4,
      "weapon": "sword",
      "unitAbilities": ["dash", "fortify", "land"],
      "movementTerrain": ["field", "forest", "mountain"]
    }
  }
}
```

PolyMod gives each new entry an automatic index (autoindex) when the game starts. Autoindexes start at 1000 and are shared by all kinds of content, so indexes of new content never collide with vanilla indexes or with each other.

You can add new entries to every section that has an `idx`: `tribeData`, `techData`, `unitData`, `improvementData`, `terrainData`, `resourceData` and `taskData`, and to the [PolyMod-only sections](#new-abilities-and-weapons).

> [!WARNING]
> Do not set `idx` to a fixed number. PolyMod only maps entries with `"idx": -1`. A fixed number collides with other mods, and the game cannot read an entry that PolyMod did not map. The `allowUnsafeIndexes` setting in `PolyMod.json` lets PolyMod accept a fixed `idx` that is larger than all vanilla values of that type, but it is meant for testing only.

New content is not reachable on its own. Remember to connect it to the game:

- a new **unit** must be in the `unitUnlocks` of a tech, or be the `startingUnit` of a tribe;
- a new **improvement** must be in the `improvementUnlocks` of a tech or a task;
- a new **tech** must be in the `techUnlocks` of another tech, or in the `startingTech` of a tribe;
- a new **resource** must list the terrains that it spawns on, and tribes must have a `resourceModifier` above 0 for it if it should spawn;
- new content needs [localization](localization.md#keys-for-game-content) and a [look](prefabs.md).

### New tribes

A new tribe is an entry in `tribeData` with `"idx": -1`. PolyMod also:

- unlocks every custom tribe and skin for free,
- fills in the legacy `style` and `climate` values of the tribe, so do not set them,
- lets you change its preview map with the [`preview` field](tribe-previews.md).

```json
{
  "tribeData": {
    "testingtribe": {
      "idx": -1,
      "color": 15713321,
      "language": "az,bar,bryn,dum,girt,hall,kar,khaz,kol,kruk,lok,rudh,ruf,und,vorn,zak",
      "startingTech": ["basic", "mining"],
      "startingResource": ["metal"],
      "startingUnit": "warrior",
      "skins": ["testingskin"],
      "priceTier": 0,
      "category": 1,
      "bonus": 0,
      "preview": [
        { "x": 0, "y": 0, "terrainType": "mountain", "unitType": "swordsman", "improvementType": "mine" }
      ]
    }
  }
}
```

The tribe needs a name and description in [localization](localization.md#keys-for-game-content) (`tribes_testingtribe` and `tribes_testingtribe_info`), and textures for its buildings, units and terrain. Without tribe-specific textures, the game uses the generic sprites. See [TribeData](../gld/tribe-data.md).

### New skins

When a tribe lists a skin id that does not exist in `skins`, PolyMod creates that skin. Skin ids can use any case in `skins`; PolyMod changes them to lowercase.

```json
{
  "tribeData": {
    "imperius": {
      "skins": ["goldenimperius"]
    }
  },
  "skinData": {
    "goldenimperius": {
      "color": 16766720,
      "language": "au,rum,gol,den"
    }
  }
}
```

The `skinData` entry is optional. It gives the skin its own color and city name syllables. See [SkinData](../gld/skin-data.md).

If the tribe has the `flooded` tribe ability, PolyMod also creates a tile effect `<skin>_flood` for each new skin of that tribe, so that tiles flooded by that skin use the sprites `field_flooded_<skin>` and the matching shorelines.

Give the skin a name with the localization keys `tribeskins_<skin>` and `tribeskins_<skin>_description`.

### New abilities and weapons

PolyMod reads some GLD sections that the game itself does not have. They only create new enum values with a name, so that other GLD entries can refer to them, and [PolyScripts](polyscripts.md) can give them a behavior:

| Section | Creates a new | Used in |
| --- | --- | --- |
| `unitAbility` | unit ability | `unitData.*.unitAbilities` |
| `improvementAbility` | improvement ability | `improvementData.*.improvementAbilities` |
| `tribeAbility` | tribe ability | `tribeData.*.tribeAbilities` |
| `playerAbility` | player ability | `techData.*.abilityUnlocks` |
| `weaponData` | weapon | `unitData.*.weapon` |

```json
{
  "unitAbility": {
    "berserk": { "idx": -1 }
  },
  "unitData": {
    "lancer": {
      "unitAbilities": ["+berserk"]
    }
  }
}
```

A new ability has no effect in the game until a PolyScript checks for it, for example with `EnumCache<UnitAbility.Type>.GetType("berserk")` and a Harmony patch.

## PolyMod-only fields

PolyMod reads some extra fields in GLD entries. The game ignores them.

| Section | Field | Description |
| --- | --- | --- |
| `tribeData` | `preview` | Changes the tribe preview map. See [Tribe previews](tribe-previews.md). |
| `tribeData` | `skins` | Accepts new skin ids, see [New skins](#new-skins). |
| `unitData` | `weapon` | Accepts a weapon id such as `"sword"` in place of a number. |
| `unitData` | `embarksTo` | The id of the unit that this unit turns into when it embarks, in place of the default boat. |
| `improvementData` | `prefab` | `true` makes a [simple prefab](prefabs.md#simple-prefabs-from-the-gld). |
| `improvementData` | `attractsResource` | For an improvement with the `attract` ability: the id of the resource that it spawns. Default `game`. |
| `improvementData` | `attractsToTerrain` | For an improvement with the `attract` ability: the id of the terrain that the resource spawns on. Default `forest`. |
| `improvementData` | `infoOverride` | A localization key (underscores allowed, as in localization files) whose text replaces the info text of the improvement. |
| `resourceData` | `prefab` | `true` makes a [simple prefab](prefabs.md#simple-prefabs-from-the-gld). |

The [GLD reference](../gld/index.md) explains each field on the page of its section.

## Making values configurable

A patch can read values that the player can change, with `{{ config "key" "default" }}`. See [Config](config.md).

## Errors

- **Invalid JSON.** PolyMod does not catch JSON syntax errors in patch files. An invalid patch can stop the game data from loading. Check every patch with a JSON validator before you test it.
- **Template errors.** If a patch has an error in its [config template](config.md), PolyMod writes the error to the log and sets the status of the mod to *Error*.
- **Unknown ids.** A wrong or uppercase id makes the game fail while it reads the GLD. The log shows `Missing enum value for string <id>`.

See [Debugging and dumping](debugging.md) for how to read the log.
