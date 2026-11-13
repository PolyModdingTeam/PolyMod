# Game logic data

The game logic data (GLD) is a JSON file in the game assets. It holds most of the rules of the game: the stats of every unit, the tech tree, the tribes, the buildings and their requirements, and more. Mods change it with [GLD patches](../docs/gld-patching.md).

Each GLD has a version. Each game version uses one GLD version. This reference describes **GLD version 28**. To see the vanilla values of your game version, [dump the game data](../docs/debugging.md#dumping-game-data) and open the `GameLogicData` file with the highest number.

## Sections

The GLD is one JSON object with these sections:

| Section | Content | Keyed by | Page |
| --- | --- | --- | --- |
| `tribeData` | Tribes | tribe id | [TribeData](tribe-data.md) |
| `techData` | Technologies | tech id | [TechData](tech-data.md) |
| `unitData` | Units | unit id | [UnitData](unit-data.md) |
| `improvementData` | Buildings and tile actions | improvement id | [ImprovementData](improvement-data.md) |
| `terrainData` | Terrain types | terrain id | [TerrainData](terrain-data.md) |
| `resourceData` | Resources | resource id | [ResourceData](resource-data.md) |
| `taskData` | Tasks that unlock monuments | task id | [TaskData](task-data.md) |
| `skinData` | Colors and city names of skins | skin id | [SkinData](skin-data.md) |
| `diplomacyData` | Embassy values | — (one object) | [DiplomacyData](diplomacy-data.md) |

PolyMod also reads the sections `unitAbility`, `improvementAbility`, `tribeAbility`, `playerAbility` and `weaponData`, which the game does not have. They create new ability and weapon ids. See [New abilities and weapons](../docs/gld-patching.md#new-abilities-and-weapons).

Unit effects and tile effects are not in the GLD, but some GLD fields refer to them. See [Effects](effects.md).

## An entry

Each section except `diplomacyData` is an object of entries. The key of an entry is its id. The value is an object with the fields of that section:

```json
{
	"unitData": {
		"warrior": {
			"health": 100,
			"defence": 20,
			"movement": 1,
			"range": 1,
			"attack": 20,
			"cost": 2,
			"unitAbilities": [
				"dash",
				"fortify",
				"land"
			],
			"weapon": 1,
			"promotionLimit": 3,
			"idx": 2
		}
	}
}
```

The main example on each page uses the values from game logic data version 28. The smaller examples only show the format. Check the dumped GLD for the current values.

## Ids

An id is the name of a value of a game enum, in lowercase. For example, the enum value `UnitData.Type.BabyDragon` has the id `babydragon`, and `TribeType.Aimo` has the id `aimo`. The game uses ids in two places:

- as the **keys** of entries, for example `"babydragon": { ... }`;
- as **values** that refer to other content, for example `"startingUnit": "warrior"` or `"techUnlocks": ["riding"]`.

> [!IMPORTANT]
> The game looks up ids case-sensitively, and all ids are lowercase. An id with an uppercase letter, or an id that does not exist, makes the game fail while it reads the GLD (`Missing enum value for string <id>` in the log).

Each reference page lists the vanilla ids of its section. For ids that are only names of enum values (abilities, effects), the ids are on the page that uses them.

### Fields that refer to content

Fields can refer to other content in several ways. The reference pages give the type of each field with these names:

| Type | JSON | Example |
| --- | --- | --- |
| *id* | A string id. | `"startingUnit": "warrior"` |
| *id list* | An array of string ids. | `"startingTech": ["basic", "farming"]` |
| *id map* | An object with ids as keys and numbers as values. | `"terrainModifier": { "mountain": 1.2 }` |
| *id→id map* | An object with ids as keys and ids as values. | `"unitOverrides": { "rider": "hexapod" }` |
| *enum number* | A number, or the C# name of the enum value as a string. | `"category": 1` |

## idx

Every entry in `tribeData`, `techData`, `unitData`, `improvementData`, `terrainData`, `resourceData` and `taskData` has an integer `idx`. It is the number of the enum value of the entry, so the vanilla `idx` values match the numbers in the enum tables on each page.

For **new** content, set `"idx": -1`. PolyMod then gives the entry a free number from 1000 up. See [Adding new content](../docs/gld-patching.md#adding-new-content).

## Overrides

Several tribes have their own versions of vanilla content. For example, Cymanti have the `hexapod` in place of the `rider`. The tribe fields `techOverrides`, `unitOverrides` and `improvementOverrides` map the vanilla id to the tribe's version. The tech tree, the training menu and the build menu then show the override in place of the original. See [TribeData](tribe-data.md#overrides).

## PolyMod-only fields

PolyMod reads some extra fields in the GLD that the game ignores, such as `preview` for tribes and `embarksTo` for units. They are marked **PolyMod** on the reference pages. The full list is in [GLD patching](../docs/gld-patching.md#polymod-only-fields).
