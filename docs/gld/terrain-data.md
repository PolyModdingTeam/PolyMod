# TerrainData

`terrainData` lists the terrain types. A terrain entry only has an index; the rules for a terrain are in other sections, for example the movement and defence unlocks of [techs](tech-data.md#movementunlocks), the `movementTerrain` of [units](unit-data.md#movementterrain) and the requirements of [improvements](improvement-data.md#terrainrequirements).

```json
"terrainData": {
  "field": { "idx": 3 },
  "mountain": { "idx": 4 }
}
```

## Fields

| Field | Type | Description |
| --- | --- | --- |
| `idx` | integer | The number of the terrain type. `-1` for new terrain. |

The name of a terrain is the localization key `terrain.<id>`.

## Vanilla terrains

| Id | idx |
| --- | --- |
| `none` | 0 |
| `water` | 1 |
| `ocean` | 2 |
| `field` | 3 |
| `mountain` | 4 |
| `forest` | 5 |
| `ice` | 6 |
| `wetland` | 7 |
| `mangrove` | 8 |

> [!NOTE]
> Much terrain behavior is hard-coded in the game, for example that units on `water` and `ocean` are boats, or how `ice` renders. A new terrain only gets the behavior that the GLD fields give it. It uses its id as its [sprite name](../docs/textures.md#base-names-of-terrain-resources-and-improvements).
