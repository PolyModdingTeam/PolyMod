# TechData

`techData` lists the technologies: the tech tree, what each tech unlocks, and how much it costs.

The example uses the basic tech, the root of the tree (values from game logic data version 28):

```json
"basic": {
	"techUnlocks": [
		"organization",
		"climbing",
		"fishing",
		"hunting",
		"riding"
	],
	"unitUnlocks": [
		"giant",
		"bunny",
		"scout",
		"transportship",
		"warrior",
		"babydragon",
		"firedragon",
		"dagger",
		"larva"
	],
	"improvementUnlocks": [
		"city",
		"ruin"
	],
	"movementUnlocks": {
		"field": 1,
		"forest": 2,
		"ice": 1,
		"wetland": 2
	},
	"cost": 0,
	"idx": 0
}
```

## Fields

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| [`idx`](#idx) | integer | — | The number of the tech. `-1` for a new tech. |
| [`cost`](#cost) | integer | `0` | The cost tier of the tech. |
| [`techUnlocks`](#techunlocks) | tech id list | `[]` | The techs that come after this tech in the tree. |
| [`unitUnlocks`](#unitunlocks) | unit id list | `[]` | The units that the tech lets cities train. |
| [`improvementUnlocks`](#improvementunlocks) | improvement id list | `[]` | The improvements that the tech lets the player build. |
| [`abilityUnlocks`](#abilityunlocks) | player ability id list | `[]` | The player abilities that the tech gives. |
| [`taskUnlocks`](#taskunlocks) | task id list | `[]` | The tasks that the tech starts. |
| [`movementUnlocks`](#movementunlocks) | terrain id map | `{}` | The terrains that units can enter. |
| [`defenceBonusUnlocks`](#defencebonusunlocks) | terrain id map | `{}` | The terrains that give a defence bonus. |

The name of a tech is the localization key `technology.names.<id>`.

## idx

The number of the tech in the `TechData.Type` enum. Use `-1` for a new tech.

## cost

The cost tier. The star price of the tech is:

```
price = ceil((4 + cost × cities) × multiplier)
```

where `cities` is the number of cities of the player, and `multiplier` is 2/3 with the `literacy` player ability and 1 without it. A tech with `cost` 0 is free and does not count for the score or for the Genius task. Vanilla techs have the tiers 1, 2 and 3.

## techUnlocks

The techs that come after this tech. The tech tree of a tribe starts at `basic` and follows `techUnlocks`, with the tribe's [`techOverrides`](tribe-data.md#overrides) applied to each tech.

A player can research a tech if they own the tech before it. In current games, a player can also research a tech if they own one of the techs after it.

A new tech must be in the `techUnlocks` of another tech (or in a tribe's `startingTech`), or no one can reach it. The tech tree screen spreads the branches of `basic` evenly around the circle, so PolyMod supports more than 5 branches.

## unitUnlocks

The units that cities can train once the player owns the tech, after the tribe's [`unitOverrides`](tribe-data.md#overrides). [Hidden](unit-data.md#hidden) units are not trainable, but a tech still makes them available as [upgrades](unit-data.md#upgradesfrom). A `unique` unit is not trainable while the player has one.

## improvementUnlocks

The improvements that the player can build once they own the tech, after the tribe's [`improvementOverrides`](tribe-data.md#overrides). [Hidden](improvement-data.md#hidden) improvements are left out.

## abilityUnlocks

The [player abilities](#player-abilities) that the player gets with the tech.

## taskUnlocks

The [tasks](task-data.md) that start when the player gets the tech. Some tasks also start by themselves; see [TaskData](task-data.md#vanilla-tasks).

## movementUnlocks

The terrains that the units of the player can enter once they own the tech. Only the keys matter; the game does not read the numbers. Unlocking `ocean` also unlocks `water`.

```json
"movementUnlocks": {
	"mountain": 1
}
```

A unit with a non-empty [`movementTerrain`](unit-data.md#movementterrain) can only use the unlocked terrains that are also in its list (or all terrains of its list with `navigate`).

## defenceBonusUnlocks

The terrains on which the units of the player get a defence bonus. Any number above 1 gives the same bonus of 1.5×; the exact number does not matter.

```json
"defenceBonusUnlocks": {
	"water": 15,
	"ocean": 15
}
```

## Player abilities

Player abilities are rules that a player gets from a tech, through `abilityUnlocks`.

| Id | Number | Effect |
| --- | --- | --- |
| `disband` | 0 | Units that have not moved or attacked can be disbanded for half their cost. |
| `destroy` | 1 | Improvements can be destroyed. |
| `literacy` | 2 | Techs cost 1/3 less. |
| `glide` | 3 | Units without `skate` move slightly cheaper on ice. |
| `decompose` | 5 | Improvements can be decomposed: at the end of the turn they are removed and their cost is paid back. |
| `peacetreaty` | 6 | The player can offer peace treaties. |
| `embassy` | 7 | The player can build embassies with players who own their capital. |
| `capitalvision` | 8 | The player sees the capitals of the players that they know. |
| `dividend` | 9 | No effect in game code. |
| `discipline` | 10 | Units are promoted after 2 kills instead of 3. |
| `pontoon` | 11 | Improvements that need `field` can be built on water (except road, temple, canal and grow forest). |
| `treehouse` | 12 | Improvements that need `field` can be built on forest. |
| `bubble` | 13 | Units with `amphibious` get a bubble (+1 movement) on flooded tiles. |
| `microbes` | 14 | Fungi can reach one more level. |

New player abilities can be added with the PolyMod section `playerAbility`; they need a [PolyScript](../docs/polyscripts.md) to do something. The name of a player ability is the localization key `player.abilities.<id>`.

## Vanilla techs

| Id | idx | | Id | idx | | Id | idx |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `basic` | 0 | | `mathematics` | 17 | | `shock` | 35 |
| `riding` | 1 | | `archery` | 18 | | `recycling` | 36 |
| `freespirit` | 2 | | `spiritualism` | 19 | | `hydrology` | 37 |
| `chivalry` | 3 | | `climbing` | 20 | | `diplomacy` | 38 |
| `roads` | 4 | | `meditation` | 21 | | `aquaculture` | 39 |
| `trade` | 5 | | `philosophy` | 22 | | `fishingsimple` | 40 |
| `organization` | 6 | | `mining` | 23 | | `sledding` | 41 |
| `shields` | 7 | | `smithery` | 24 | | `icefishing` | 42 |
| `farming` | 8 | | `freediving` | 25 | | `pescetism` | 43 |
| `construction` | 9 | | `spearing` | 26 | | `ramming` | 44 |
| `fishing` | 10 | | `riding2` | 27 | | `aquabase` | 45 |
| `whaling` | 11 | | `forestmagic` | 28 | | `marinelife` | 46 |
| `aquatism` | 12 | | `watermagic` | 29 | | `aquafishing` | 47 |
| `sailing` | 13 | | `frostwork` | 30 | | `waterways` | 48 |
| `navigation` | 14 | | `polarwarfare` | 31 | | `rituals` | 49 |
| `hunting` | 15 | | `polarism` | 32 | | `mysticism` | 50 |
| `forestry` | 16 | | `oceanology` | 33 | |  |  |

There is no tech with the number 34. Some techs are tribe versions of other techs, used through [`techOverrides`](tribe-data.md#overrides), for example `shock` for Cymanti in place of `chivalry`.
