# Tribe previews

The tribe selection screen shows a small preview map for each tribe. PolyMod lets a mod change the tiles of that map with the PolyMod-only field `preview` in a [tribe entry](../gld/tribe-data.md).

```json
{
	"tribeData": {
		"testingtribe": {
			"preview": [
				{
					"x": 0,
					"y": 0,
					"terrainType": "mountain",
					"unitType": "swordsman",
					"improvementType": "mine"
				},
				{
					"x": 1,
					"y": 0,
					"resourceType": "none"
				}
			]
		}
	}
}
```

## Preview tile fields

`preview` is an array of tile changes. Each tile change has these fields (field names are case-sensitive):

| Field | Type | Description |
| --- | --- | --- |
| `x` | integer | The x coordinate of the tile. Required. |
| `y` | integer | The y coordinate of the tile. Required. |
| `terrainType` | terrain id | The new terrain of the tile. Leave it out to keep the terrain. |
| `resourceType` | resource id | The new resource of the tile. Leave it out to keep the resource, or use `"none"` to remove it. |
| `unitType` | unit id | The new unit on the tile, owned by the tribe. Leave it out to keep the unit, or use `"none"` to remove it. |
| `improvementType` | improvement id | The new improvement on the tile, at level 0. Leave it out to keep the improvement, or use `"none"` to remove it. |
| `tileEffects` | array of tile effect ids | The new [tile effects](../gld/effects.md#tile-effects), for example `["flooded"]`. If the array is not empty, it replaces all effects of the tile. Leave it out to keep the effects. |

PolyMod skips a tile change without `x` and `y`, or with coordinates outside the map.

## Which map is changed

- For a vanilla tribe, PolyMod changes a copy of that tribe's own preview map.
- For a new tribe, which has no preview map, PolyMod starts from the game's fallback preview map and adds the changed copy as the preview of the tribe.

## Finding coordinates

There are two ways to find the coordinates of the preview tiles:

- Turn on **Show coordinates** in the PolyMod Hub config (or set `showCoordinates` in `PolyMod.json`). The tribe selection screen then shows a zoomed-out preview, and every tile shows its coordinates.
- Dump the game data. PolyMod writes `DumpedData/preview_<tribe>.json` for each vanilla tribe with the terrain, resource, unit, improvement and effects of every tile, in the same format as `preview`. See [Debugging and dumping](debugging.md).
