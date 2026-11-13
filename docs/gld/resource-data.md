# ResourceData

`resourceData` lists the resources, such as fruit, game or metal, and the terrains that they spawn on.

```json
"crop": {
	"resourceTerrainRequirements": [
		"field"
	],
	"idx": 2
}
```

## Fields

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| `idx` | integer | — | The number of the resource. `-1` for a new resource. |
| [`resourceTerrainRequirements`](#resourceterrainrequirements) | terrain id list | `[]` | The terrains that the resource can spawn on. |
| [`prefab`](#prefab) | boolean | `false` | **PolyMod.** Makes a simple prefab. |

The name of a resource is the localization key `resource.names.<id>`.

## resourceTerrainRequirements

The terrains that map generation can place the resource on. An empty list allows every terrain. The list also decides which terrain the game creates for a [starting resource](tribe-data.md#startingresource) when no fitting tile is next to the capital.

How often a resource spawns is set per tribe, with the tribe's [`resourceModifier`](tribe-data.md#resourcemodifier). A new resource has a base chance of 0, so it only spawns for tribes that give it a `resourceModifier` value.

What players can do with a resource comes from improvements: an improvement that requires the resource in its [`terrainRequirements`](improvement-data.md#terrainrequirements) can be built on it, for example `hunting` on `game`.

## prefab

**PolyMod.** `true` makes a [simple prefab](../docs/prefabs.md#simple-prefabs-from-the-gld) whose sprite is the id of the resource. Without a prefab, a new resource has no look of its own.

## Vanilla resources

| Id | idx | | Id | idx | | Id | idx |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `none` | 0 | | `whale` | 4 | | `starfish` | 8 |
| `game` | 1 | | `metal` | 5 | | `aquacrop` | 9 |
| `crop` | 2 | | `fruit` | 6 | |  |  |
| `fish` | 3 | | `spores` | 7 | |  |  |

`starfish` is not placed by the normal resource generation.
