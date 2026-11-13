# Prefabs

A prefab is the game object that the game shows for a unit, an improvement or a resource. It is made of **visual parts**: sprites with a position, a rotation and settings for tinting and skins. PolyMod can make new prefabs from JSON files, so that new content gets its own look.

> [!NOTE]
> The prefab format changed completely in PolyMod 2.0. The old format with `type`, `name` and `visualParts` with `gameObjectName`/`baseName`, and the old `"prefab": "<unit>"` field in `unitData`, no longer exist.

## Prefab files

| File name | Content |
| --- | --- |
| `prefabs.json`, `prefabs_*.json` | Any number of prefabs: `{ "units": [...], "improvements": [...], "resources": [...] }` |
| `unitPrefab.json`, `unitPrefab_*.json` | One [unit prefab](#unit-prefabs). |
| `improvementPrefab.json`, `improvementPrefab_*.json` | One [improvement prefab](#improvement-prefabs). |
| `resourcePrefab.json`, `resourcePrefab_*.json` | One [resource prefab](#resource-prefabs). |

PolyMod reads prefab files after it merges the GLD patches, so they can use the ids of new content. Field names in prefab files are not case-sensitive.

> [!TIP]
> Dump the game data to get every vanilla prefab in this format in `DumpedData/Prefabs/prefabs.json`. Copy the prefab that is closest to what you want and change it. See [Debugging and dumping](debugging.md).

## Visual parts

Each prefab has a `visualParts` array. Each visual part is an object:

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| `defaultSpriteName` | string | — | The base name of the sprite of this part. The game looks up the sprite by this name with the tribe, skin and level, as [Textures](textures.md#texture-names) describes. It is also the name of the part's game object. |
| `coordinates` | `[x, y]` | `[0, 0]` | The position of the part, in world units. |
| `rotation` | number | `0` | The rotation around the z axis, in degrees. |
| `tintable` | boolean | `false` | If `true`, the game tints the sprite with the color of the owner. Use it for parts that show the tribe color. The game multiplies the sprite colors by the tribe color, so draw these parts in white: white becomes the exact tribe color, and gray becomes a darker shade of it. Use gray for shading. Other colors mix with the tribe color and do not tint completely. |
| `skinningLogic` | string | `"UseTribe"` | Which skin the sprite uses: `UseTribe`, `UseClimate`, `UseBirthClimate` or `DontChangeSkin`. |
| `improvementSkinningLogic` | string | `"FoundingTribe"` | Improvements only. Which skin the sprite uses: `TileOwner`, `TileClimate`, `FoundingTribe` or `DontChangeSkin`. |
| `levels` | boolean | `false` | Improvements only. If `true`, the sprite changes with the level of the improvement (`base_level` sprite names). |
| `sortingOrderOverride` | integer | calculated | Units only. The sorting order of the part. |
| `outlineSortingOrderOverride` | integer | calculated | Units only. The sorting order of the outline of the part. |

Each part also gets an outline object, named `defaultSpriteName` plus `_Outline`, which shows the [outline sprite](textures.md#outlines).

**Drawing order.** For improvements and resources, the first part in the array is drawn at the back and each next part in front of it. For units, it is the other way around: the *last* part is drawn at the back and the first part is drawn in front. The outlines are drawn behind all parts. Use the sorting order overrides to change the order of unit parts.

## Unit prefabs

| Field | Type | Description |
| --- | --- | --- |
| `skinnedKey` | object | The unit, tribe and skin that this prefab is for: `{ "unitType": "...", "tribeType": "...", "skinType": "..." }`. Use the ids, for example `"tribeType": "none"` for all tribes and `"skinType": "default"` for the default skin. |
| `headPositionMarker` | string | The `defaultSpriteName` of the part that marks the head of the unit. If no part has this name, PolyMod uses the first part. |
| `uiOverride` | boolean | If `true`, this prefab is only used in the UI, in place of the normal prefab of the same key. Default `false`. |
| `visualParts` | array | The [visual parts](#visual-parts). |

```json
{
	"skinnedKey": {
		"unitType": "lancer",
		"tribeType": "none",
		"skinType": "default"
	},
	"headPositionMarker": "lancer_head",
	"visualParts": [
		{
			"defaultSpriteName": "lancer_head",
			"coordinates": [
				0,
				0.35
			]
		},
		{
			"defaultSpriteName": "lancer_body",
			"coordinates": [
				0,
				0.15
			],
			"tintable": true
		},
		{
			"defaultSpriteName": "lancer_legs",
			"coordinates": [
				0,
				0
			]
		}
	]
}
```

### How the game finds a unit prefab

For a unit of type *U*, tribe *T* and skin *S*, PolyMod looks for a prefab in this sequence and uses the first one that exists:

1. During the Christmas season, the Christmas prefab of *U*, if the game has one.
2. The prefab for *U*, *T*, *S*.
3. The prefab for *U* with tribe `none` and skin *S*.
4. The prefab for *U*, *T* with the default skin.
5. The prefab for *U* with tribe `none` and the default skin.
6. As a last resort, the prefab of unit type `none` with tribe `none` and the default skin. PolyMod writes `Couldn't find prefab for type` to the log.

So one prefab with tribe `none` and the default skin covers all tribes and skins. Add more specific prefabs only where the look must differ.

You can also add a prefab for a vanilla unit, tribe and skin to replace the vanilla look.

## Improvement prefabs

| Field | Type | Description |
| --- | --- | --- |
| `improvementType` | string | The id of the improvement. Required: PolyMod skips a prefab without it. |
| `needsOutlines` | boolean | If `true`, the outline objects get a renderer and show the outline sprites. Default `false`. |
| `visualParts` | array | The [visual parts](#visual-parts). |

```json
{
	"improvementType": "mytemple",
	"visualParts": [
		{
			"defaultSpriteName": "mytemple",
			"levels": true
		}
	]
}
```

An improvement prefab replaces the prefab of that improvement for all tribes and skins. The sprites of the parts still change with the tribe and skin, through the [sprite names](textures.md#texture-names).

## Resource prefabs

| Field | Type | Description |
| --- | --- | --- |
| `resourceType` | string | The id of the resource. Required: PolyMod skips a prefab without it. |
| `visualParts` | array | The [visual parts](#visual-parts). |

```json
{
	"resourceType": "gems",
	"visualParts": [
		{
			"defaultSpriteName": "gems"
		}
	]
}
```

## Simple prefabs from the GLD

For an improvement or a resource with only one sprite, you do not need a prefab file. Set the PolyMod field `"prefab": true` in its GLD entry:

```json
{
	"improvementData": {
		"mytemple": {
			"idx": -1,
			"prefab": true,
			"maxLevel": 5
		}
	},
	"resourceData": {
		"gems": {
			"idx": -1,
			"prefab": true
		}
	}
}
```

PolyMod then makes a prefab with one visual part whose `defaultSpriteName` is the id (`mytemple`, `gems`). For an improvement with `maxLevel` above 0, the part has `levels` set, so it uses the level sprites `mytemple_<level>` (for example `mytemple_1`, `mytemple_2`). Add textures with these names.

Do not use `"prefab": true` and a prefab file for the same type. PolyMod makes the simple prefabs after the prefabs from files, so the simple prefab replaces the one from the file.
