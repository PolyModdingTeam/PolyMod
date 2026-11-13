# Effects

Effects are states of tiles and units. They are not GLD sections, but GLD fields and abilities refer to them.

## Tile effects

Tile effects are used in the [`creates`](improvement-data.md#creates) field of improvements and in the [`tileEffects`](../docs/tribe-previews.md#preview-tile-fields) of tribe previews.

| Id | Number | Created by | Effect |
| --- | --- | --- | --- |
| `none` | 0 | — | No effect. |
| `flooded` | 1 | The `flood` improvement ability, the `autoflood` and `drench` unit abilities, the `flooded` tribe ability, `creates`. | The tile counts as wetland: it changes movement, matches `wetland` and `mangrove` requirements, allows `fill` improvements and gives amphibious units a bubble. Removed by `fill` improvements and by some captures. |
| `swamped` | 2 | Added with `flooded` by players with the `swamp` skin. | Only visual. |
| `tentacle` | 3 | Units with the `tentacles` ability, on the tiles around them. | An enemy that enters the tile is attacked. |
| `algae` | 4 | The `algae` and `algaespawn` improvements, the `algae` unit ability, exploding units. | The tile counts as wetland; land units must stop there. Removed by `fill` improvements. |
| `foam` | 5 | Added with `algae` by players with the `cute` skin. | Only visual. |

PolyMod adds a flood effect `<skin>_flood` for each new skin of a tribe with the `flooded` ability. See [New skins](../docs/gld-patching.md#new-skins).

## Unit effects

Unit effects come from unit abilities and actions. No GLD field sets them directly, but they explain several [unit abilities](unit-data.md#unit-abilities).

| Id | Number | Effect |
| --- | --- | --- |
| `frozen` | 0 | The unit cannot move or attack and does not strike back. Removed at the end of its owner's turn. |
| `poisoned` | 1 | −1 movement (at least 1) and half the defence bonus. Healing cures the poison instead of giving health. Spores spawn where a poisoned unit dies. |
| `boosted` | 2 | Old version of the boost (only in old saved games): +0.5 attack and +1 movement. |
| `invisible` | 3 | Enemies cannot see the unit, and it ignores zones of control. Removed when the unit attacks, captures, builds or examines ruins. |
| `bubble` | 4 | +1 movement. Removed when the unit is hit, or when an amphibious unit moves onto dry land. |
| `petrified` | 5 | The look of `frozen` for the `magma` skin. |
| `swift` | 6 | +1 movement, from `boost` and `swarm`. Removed when the unit is hit. |
| `doubleready` | 7 | The unit can attack a second time this turn (`doubleattack`). |
| `charmed` | 8 | The look of `poisoned` for the `cute` skin. |
