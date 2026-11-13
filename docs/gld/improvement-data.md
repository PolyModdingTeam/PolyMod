# ImprovementData

`improvementData` lists everything that can be built or done on a tile: buildings such as the farm or the temple, and tile actions such as clearing a forest or fishing. In the game code, improvements are also called buildings.

The example uses the windmill (values from game logic data version 28):

```json
"windmill": {
	"cost": 5,
	"adjacencyImprovements": [
		{
			"improvement": "farm"
		},
		{
			"improvement": "aquafarm"
		}
	],
	"adjacencyRequirements": [
		{
			"improvement": "farm"
		},
		{
			"improvement": "aquafarm"
		}
	],
	"improvementAbilities": [
		"limited"
	],
	"maxLevel": 8,
	"terrainRequirements": [
		{
			"terrain": "field"
		}
	],
	"idx": 6,
	"growthRewards": [
		{
			"score": 0,
			"population": 1
		}
	]
}
```

## Fields

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| [`idx`](#idx) | integer | — | The number of the improvement. `-1` for a new improvement. |
| [`hidden`](#hidden) | boolean | `false` | Players cannot build it. |
| [`cost`](#cost) | integer | `0` | The star cost to build it. |
| [`work`](#work) | integer | `0` | Star income per turn per level. |
| [`borderSize`](#bordersize) | integer | `0` | The border size of the improvement state. |
| [`maxLevel`](#maxlevel) | integer | `0` | The highest level. `0` means no cap. |
| [`improvementAbilities`](#improvement-abilities) | improvement ability id list | `[]` | Special rules. |
| [`creates`](#creates) | array of objects | `[]` | Terrain, resources, units or effects that building it creates. |
| [`rewards`](#rewards) | array of objects | `[]` | Population, stars and score for building it. |
| [`terrainRequirements`](#terrainrequirements) | array of objects | `[]` | The terrain and resource that the tile needs. |
| [`adjacencyRequirements`](#adjacencyrequirements) | array of objects | `[]` | What a neighbor tile must (or must not) have. |
| [`adjacencyImprovements`](#adjacencyimprovements) | array of objects | `[]` | Neighbors that give the improvement levels. |
| [`routes`](#routes-and-range) | terrain id list | `[]` | Terrains for route connections. |
| [`range`](#routes-and-range) | integer | `0` | Distance of route connections. |
| [`growthRate`](#growthrate) | integer | `0` | Turns per level (`patina`) or per spawn (`attract`). |
| [`growthRewards`](#growthrewards) | array of objects | `[]` | Population and score for each level-up. |
| [`prefab`](#polymod-fields) | boolean | `false` | **PolyMod.** Makes a simple prefab. |
| [`attractsResource`](#polymod-fields) | resource id | `game` | **PolyMod.** The resource that `attract` spawns. |
| [`attractsToTerrain`](#polymod-fields) | terrain id | `forest` | **PolyMod.** The terrain that `attract` spawns on. |
| [`infoOverride`](#polymod-fields) | localization key | — | **PolyMod.** Replaces the info text. |

The name of an improvement is the localization key `building.names.<id>`.

## idx

The number of the improvement in the `ImprovementData.Type` enum. Use `-1` for a new improvement.

## hidden

If `true`, players cannot build the improvement, it does not show in the tech tree, and the AI ignores it. The game can still place it by code; for example, the `algae` and `algaespawn` improvements are hidden and placed by abilities. A hidden improvement does not count as an unlocked `embark` improvement.

## cost

The stars that it costs to build the improvement. When a player decomposes an improvement, they get the cost back.

## work

The **star income per turn** of the improvement, not its build cost. Each turn the owner gets `work × level` stars, or twice that with the `doubled` ability next to a port.

A new improvement starts at level 0 and then gives nothing. `work` only matters if something raises the level: [`adjacencyImprovements`](#adjacencyimprovements), the `patina` ability, or the hard-coded rules of the ice bank and the lighthouse. Cities ignore `work` and use their own income rules.

## borderSize

Copied into the improvement on the map. The game only reads border sizes of cities, and it creates cities with a fixed border size. No effect was found for other improvements.

## maxLevel

The highest level that the improvement can reach through adjacency or `patina`. `0` means no cap. The fungi get +1 maximum level with the `microbes` player ability. For PolyMod's [simple prefabs](../docs/prefabs.md#simple-prefabs-from-the-gld), a `maxLevel` above 0 makes the sprite change with the level.

## creates

What the improvement creates on its tile when it is built. Each entry is an object with one or more of these fields:

| Field | Type | Effect |
| --- | --- | --- |
| `terrain` | terrain id | Changes the terrain of the tile and removes its resource. For example, clearing a forest creates `field`. |
| `resource` | resource id | Creates the resource, if the tile has no resource that the player can see. |
| `unit` | unit id | Trains the unit for free on the tile. If a unit stands there, it is pushed away. |
| `effect` | [tile effect](effects.md#tile-effects) id | Adds the effect to the tile. |

```json
"creates": [
	{
		"terrain": "field"
	},
	{
		"resource": "crop"
	}
]
```

The improvement cannot be built if the tile already has the resource or the effect that it creates.

## rewards

What the player gets when the improvement is built. The values of all entries are added up.

| Field | Type | Effect |
| --- | --- | --- |
| `population` | integer | Population for the city that the tile belongs to. A tile outside any city gets nothing. |
| `currency` | integer | Stars, paid at once. |
| `score` | integer | Score for the player. It is lost for improvements that do not stay on the tile (roads, `consumed` and `discrete` improvements). |

```json
"rewards": [
	{
		"population": 2
	}
]
```

When the improvement is destroyed, its score and the population that it gave (including from levels) are taken away again.

> [!WARNING]
> Do not use negative `population` or `currency` rewards. The game treats them as huge positive numbers.

## terrainRequirements

The tiles that the improvement can be built on. Each entry is an object with a `terrain` and/or a `resource` (ids):

```json
"terrainRequirements": [
	{
		"terrain": "field",
		"resource": "crop"
	}
]
```

The game collects all terrains and all resources of all entries. The tile must have **one of the terrains** (if any are listed) **and one of the resources** (if any are listed). A terrain and a resource in the same entry are *not* paired: `[{ "terrain": "field", "resource": "crop" }, { "terrain": "forest" }]` means "field or forest, with crop". An empty list allows every tile.

Special matches:

- `wetland` also matches flooded or algae tiles that are not forest.
- `mangrove` also matches a flooded forest.
- With the `pontoon` player ability, `field` also matches water, except for the road, temple, canal and grow forest.
- With the `treehouse` player ability, `field` also matches forest.

When the improvement is destroyed, the required resource is put back on the tile.

## adjacencyRequirements

What the 8 neighbor tiles must have. Each entry is an object with one of these fields:

| Field | Type | Requirement |
| --- | --- | --- |
| `improvement` | improvement id | A neighbor of the same owner has this improvement. |
| `terrain` | terrain id | A neighbor of the same owner has this terrain. |
| `resource` | resource id | A neighbor of the same owner has this resource. |
| `notImprovement` | improvement id | No neighbor, of any owner, has this improvement. |

```json
"adjacencyRequirements": [
	{
		"improvement": "farm"
	}
]
```

`improvement`, `terrain` and `resource` entries are alternatives: one match is enough.

> [!WARNING]
> Two limits of the game code:
> - The check stops at the first positive match, so a `notImprovement` entry together with positive entries is not always enforced. Use `notImprovement` alone.
> - A `resource` entry seems to make the game throw an error when a neighbor of the same owner has no resource. This was found by reading the code and not tested. Avoid `resource` adjacency requirements.

## adjacencyImprovements

Neighbors that give the improvement its level. If this list is not empty, the level of the improvement is computed from the neighbor tiles of the same owner. Each entry is an object with one of these fields:

| Field | Type | Adds to the level |
| --- | --- | --- |
| `improvement` | improvement id | The level of each neighbor with this improvement (at least 1). |
| `resources` | resource id | 1 for each neighbor with this resource and no improvement. Note the plural field name. |

```json
"adjacencyImprovements": [
	{
		"improvement": "farm"
	}
]
```

The abilities `simple` (each neighbor counts 1) and `picky` (each type counts once) change the count. The level is capped by `maxLevel` and recomputed when neighbors change and at the start of each turn. This is how the windmill, sawmill, forge and market work.

## routes and range

An improvement with a non-empty `routes` list opens **routes**, like the port. Two route openers of a player connect if they are within the `range` of the first one, over a path of the terrains that both list and that the player can move on. The tiles of the path get a route: units following it move at road speed, and the route connects cities. With the `network` ability, the improvement also connects to the player's cities within `range`.

```json
"routes": [
	"water",
	"ocean"
],
"range": 5
```

With `range` 0 nothing connects. Routes are not roads; road building is hard-coded to the `road` improvement.

## growthRate

The number of turns per level for the `patina` ability, and the number of turns between spawns for the `attract` ability. It must be above 0 if the improvement has either ability; `0` makes the game divide by zero.

## growthRewards

What each level-up of the improvement gives, whatever caused it. Each entry has `population` (for the city of the tile) and `score`.

```json
"growthRewards": [
	{
		"population": 1
	}
]
```

When the level goes down, the population is taken away again, but not the score.

## Improvement abilities

| Id | Number | Effect |
| --- | --- | --- |
| `none` | 0 | No effect. |
| `unique` | 1 | Each player can build it only once per game, also if it is destroyed. |
| `limited` | 2 | Only one per city. The check only looks at the city tiles up to 2 tiles from the city. This covers all vanilla cities, but if a mod lets city borders grow past 2 tiles, the outer tiles are not checked. |
| `expand` | 3 | No effect found in the game code. |
| `consumed` | 4 | An action, not a building: nothing stays on the tile. It removes the resource of the tile (except for the canal). |
| `patina` | 5 | Gains a level every `growthRate` turns, up to `maxLevel`. |
| `heal` | 7 | At the end of the owner's turn, heals the friendly unit on the tile and the units next to it. |
| `attract` | 10 | Every `growthRate` turns, spawns a resource on a free tile next to it. With PolyMod, the resource and terrain are set by [`attractsResource` and `attractsToTerrain`](#polymod-fields). |
| `bridge` | 13 | Land units can stand on and cross the tile, and units can leave boats there. |
| `poison` | 14 | Poisons units that enter the tile, except flying units and tribes with `poisonresist`. |
| `network` | 15 | Also connects routes to cities. See [routes](#routes-and-range). |
| `embark` | 16 | Land units that move onto it on water or wetland board a boat. See [UnitData](unit-data.md#hard-coded-unit-behavior). |
| `freelance` | 17 | Can be built on tiles that the player does not own (on another player's tile only with `manual`). |
| `manual` | 18 | Built by a unit standing on the tile, from the unit's actions. The unit's turn ends. |
| `slow` | 19 | Units that enter the tile must stop, unless they follow a road. |
| `picky` | 21 | For the adjacency level, each type of neighbor counts once. |
| `doubled` | 22 | Double `work` income if a port of the same owner is next to it. |
| `flood` | 23 | Floods its tile. Cannot be built on a flooded tile. |
| `discrete` | 24 | Can be built on a tile that already has an improvement, which stays. |
| `fill` | 25 | Can only be built on flooded or algae tiles, and removes the flood and algae. |
| `simple` | 27 | For the adjacency level, each neighbor counts 1, whatever its level. |

New improvement abilities can be added with the PolyMod section `improvementAbility`; they need a [PolyScript](../docs/polyscripts.md) to do something. The ability texts in the building info use the localization keys `building.ability.<id>`.

## PolyMod fields

| Field | Type | Description |
| --- | --- | --- |
| `prefab` | boolean | `true` makes a [simple prefab](../docs/prefabs.md#simple-prefabs-from-the-gld) whose sprite is the id of the improvement. |
| `attractsResource` | resource id | For the `attract` ability: the resource that is spawned. Default `game`. |
| `attractsToTerrain` | terrain id | For the `attract` ability: the terrain that the resource is spawned on. Default `forest`. |
| `infoOverride` | localization key | The key of a text that replaces the info text of the improvement. Write it with underscores as in [localization files](../docs/localization.md#term-keys), for example `my_building_info`. |

## Vanilla improvements

| Id | idx | | Id | idx | | Id | idx |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `none` | 0 | | `watertemple` | 19 | | `algae` | 38 |
| `city` | 1 | | `mountaintemple` | 20 | | `mycelium` | 39 |
| `ruin` | 2 | | `mine` | 21 | | `burnspores` | 40 |
| `road` | 3 | | `forge` | 22 | | `clathrus` | 41 |
| `customshouse` | 4 | | `monument1` | 23 | | `hiddensanctuary` | 42 |
| `farm` | 5 | | `monument2` | 24 | | `harvestspores` | 43 |
| `windmill` | 6 | | `monument3` | 25 | | `nullbuilding` | 44 |
| `fishing` | 7 | | `monument4` | 26 | | `cultivate` | 45 |
| `port` | 8 | | `monument5` | 27 | | `starfishing` | 46 |
| `hunting` | 9 | | `monument6` | 28 | | `lighthouse` | 47 |
| `clearforest` | 10 | | `monument7` | 29 | | `bridge` | 48 |
| `burnforest` | 11 | | `enchantanimal` | 30 | | `aquafarm` | 49 |
| `lumberhut` | 12 | | `enchantwhale` | 31 | | `market` | 50 |
| `sawmill` | 13 | | `sanctuary` | 32 | | `atoll` | 51 |
| `growforest` | 14 | | `outpost` | 33 | | `canal` | 52 |
| `harvestfruit` | 15 | | `icebank` | 34 | | `fertilize` | 53 |
| `whalehunting` | 16 | | `icetemple` | 35 | | `landfill` | 54 |
| `temple` | 17 | | `polarisclimate` | 36 | | `algaespawn` | 55 |
| `foresttemple` | 18 | | `fungi` | 37 | |  |  |

## Hard-coded improvement behavior

These improvements have rules in the game code. Copying their fields to a new improvement does not copy these rules:

- **city**: income, levels, borders, capture and roads. Created by the game, cannot be built or destroyed.
- **ruin**: placed by map generation, examined by units, cannot be destroyed.
- **lighthouse**: placed at the map corners; its level grows when players find it. Cannot be destroyed.
- **road**: sets a road on the tile instead of placing an improvement, and can be built over an improvement.
- **bridge**: needs a valid direction across water, and keeps its direction in its level.
- **port**: the only improvement that triggers `doubled`.
- **icebank**: its level is the number of ice tiles divided by 20.
- **fungi**: +1 maximum level with `microbes`; placed by map generation.
- **algae**, **algaespawn**: placed by abilities, not by players.
- **temples** and **monument1**–**monument7**: counted in player statistics by their type.
- **road**, **temple**, **canal**, **growforest**: cannot use the `pontoon` rule.

The **market**, **sawmill**, **windmill**, **forge**, **customshouse**, **outpost** and **sanctuary** have no special rules: their behavior comes only from their GLD data.

## Removing an improvement from a tribe

A tribe can lose an improvement with an `improvementOverrides` entry whose value is `"none"`, as Cymanti do with `"bridge": "none"`. The improvement then disappears from that tribe's build menu. Some game code does not expect this case, so test it well. See [Overrides](tribe-data.md#overrides).
