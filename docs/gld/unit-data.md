# UnitData

`unitData` lists the units: their stats, abilities, movement and upgrades.

The example uses the baby dragon (values from an older game version):

```json
"babydragon": {
  "idx": 18,
  "health": 150,
  "defence": 30,
  "movement": 2,
  "range": 1,
  "attack": 30,
  "cost": 10,
  "hidden": true,
  "upgradesFrom": "dragonegg",
  "unitAbilities": ["grow", "dash", "fly", "escape", "scout", "static"],
  "weapon": 7,
  "growthRate": 3
}
```

## Fields

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| [`idx`](#idx) | integer | — | The number of the unit. `-1` for a new unit. |
| [`hidden`](#hidden) | boolean | `false` | The unit cannot be trained. |
| [`cost`](#cost) | integer | `0` | The star cost to train or upgrade into the unit. |
| [`health`](#health-defence-and-attack) | integer | `0` | Maximum health, ×10. |
| [`defence`](#health-defence-and-attack) | integer | `0` | Defence, ×10. |
| [`attack`](#health-defence-and-attack) | integer | `0` | Attack, ×10. |
| [`movement`](#movement) | integer | `0` | Tiles per turn. |
| [`range`](#range) | integer | `0` | Attack range in tiles. |
| [`weapon`](#weapon) | weapon number or id | `0` | The attack animation. |
| [`unitAbilities`](#unitabilities) | unit ability id list | `[]` | The abilities of the unit. |
| [`movementTerrain`](#movementterrain) | terrain id list | `[]` | The terrains that the unit can enter. |
| [`upgradesFrom`](#upgradesfrom) | unit id | none | The unit that can be upgraded into this unit. |
| [`promotesTo`](#promotesto) | unit id | none | The unit that this unit turns into when it is promoted. |
| [`growthRate`](#growthrate) | integer | `0` | Turns until a unit with `grow` grows. |
| [`embarksTo`](#embarksto) | unit id | — | **PolyMod.** The boat that this unit becomes when it embarks. |

The name of a unit is the localization key `unit.names.<id>`.

The old field `promotionLimit` no longer exists; the game ignores it. A unit is promoted after 3 kills, or after 2 kills if the player has the `discipline` [player ability](tech-data.md#player-abilities). A unit can be promoted only once.

## idx

The number of the unit in the `UnitData.Type` enum. Use `-1` for a new unit.

## hidden

If `true`, the unit is not in the training menu of cities and cannot be trained. A hidden unit can still:

- be the target of a paid upgrade (see [`upgradesFrom`](#upgradesfrom)),
- appear through growth, promotion, rewards or abilities.

A hidden unit gets no experience from kills and can never be promoted.

## cost

The stars that it costs to train the unit, and to upgrade another unit into it. The unit is also worth `cost × 5` score. Disbanding a unit gives back half of its cost, rounded down. For a boat, the cost of its passenger counts too.

## health, defence and attack

These values are stored as **ten times** the value that the game shows: `"health": 100` is 10 HP, `"defence": 20` is 2 defence, `"attack": 25` is 2.5 attack.

- **health** is the maximum health. A new unit starts with full health. A promoted (veteran) unit has 5 more maximum HP.
- **defence** is multiplied by a defence bonus: 1.5× on a terrain where the player has a [defence bonus](tech-data.md#defencebonusunlocks), 1.5× for a `fortify` unit in its own city and 4× in a city with a city wall. A poisoned unit gets only half the bonus.
- **attack** is the attack strength. A unit with `0` attack can only target something if it has `convert` or `infiltrate`.

The damage of an attack is `round(attackForce / (attackForce + defenceForce) × attack × 4.5)`, where each force is the stat multiplied by the current health share of the unit. The defender strikes back with its base defence, unless the defender is `stiff` or frozen, the attacker has `convert`, `surprise` or `freeze`, or the attacker is out of the defender's range.

## movement

The number of tiles that the unit can move per turn. A road step costs half. Some terrain and improvements stop a unit when it enters them. The [unit effects](effects.md#unit-effects) `swift` and `bubble` each give +1 movement, and `poisoned` gives −1 (at least 1).

## range

The attack range in tiles, counting diagonal steps as 1. A unit with range 1 moves onto the tile of a unit that it kills, if it can. The `splash` ability only works with a range above 1.

## weapon

The look of the attack: the animation, the projectile and the sound. It has almost no effect on game rules. In the GLD it is a number; in a patch PolyMod also accepts the id.

| Id | Number | | Id | Number |
| --- | --- | --- | --- | --- |
| `none` | 0 | | `trident` | 9 |
| `club` | 1 | | `icearrow` | 10 |
| `sword` | 2 | | `poison` | 11 |
| `arrow` | 3 | | `sting` | 12 |
| `magic` | 4 | | `dagger` | 13 |
| `gun` | 5 | | `burn` | 14 |
| `rock` | 6 | | `laser` | 15 |
| `claw` | 7 | | `pierce` | 16 |
| `fireblow` | 8 | | `water` | 17 |

`arrow`, `icearrow`, `fireblow`, `rock`, `sting`, `gun`, `poison`, `pierce`, `trident`, `water` and `burn` are projectiles. New weapon ids can be added with the PolyMod section `weaponData`, but a new weapon has no animation of its own.

## unitAbilities

The abilities of the unit, by unit ability id. See [Unit abilities](#unit-abilities).

## movementTerrain

The terrains that the unit can move on, by [terrain id](terrain-data.md#vanilla-terrains).

- **Empty**: the unit can enter every terrain that the techs of the player unlock (see [`movementUnlocks`](tech-data.md#movementunlocks)), and water if it has `swim`.
- **Not empty**: the unit can only enter these terrains, and only after the player unlocked them with a tech, unless the unit has `navigate`. The terrain `wetland` in this list also matches flooded and algae tiles.

A city can only train the unit if at least one tile next to the city matches.

The list, together with `swim` and `carry`, also decides the movement class of the unit:

| Abilities and list | Class | Behavior |
| --- | --- | --- |
| no `swim` | land unit | Stops when it enters an algae tile. Embarks on water at an `embark` improvement (port). |
| `swim`, empty list, no `carry` | amphibious | Moves on water and land, but stops after the first land tile that is not flooded. |
| `swim` with a list, or `carry` | water-bound | Stops when it enters a flooded tile. |

## upgradesFrom

The id of another unit. It has two uses:

1. **Paid upgrades.** If unit *X* has `"upgradesFrom": "Y"`, a unit *Y* can be upgraded into *X* for *X*'s cost, once a tech unlocks *X* (hidden units are allowed). This is how boats become ships. Upgrades are only offered in your own or an allied territory. Units with `grow` cannot be upgraded.
2. **Growth.** A unit with `grow` grows into the first unit in `unitData` whose `upgradesFrom` is that unit. For example, the baby dragon has `"upgradesFrom": "dragonegg"`, so the dragon egg grows into it. Growth ignores techs and tribe overrides.

When a unit upgrades or grows, it becomes a new unit: experience and promotion are reset.

## promotesTo

If set, the unit turns into this unit for free when it is promoted, instead of becoming a veteran.

## growthRate

For units with `grow`: the number of turns after which the unit grows, at the end of its owner's turn. `0` makes it grow at the first end of turn. When a growing unit leaves a boat, it grows if it is at least 3 turns old, whatever the growth rate.

## embarksTo

**PolyMod.** The id of the boat that this unit becomes when it embarks. Without it, the game picks the boat: `transportship`, `cloak_boat` for units with `hide`, `pirate` for the dagger and `juggernaut` for the giant.

## Unit abilities

| Id | Number | Effect |
| --- | --- | --- |
| `none` | 0 | No effect. |
| `dash` | 1 | Can attack after it moves. |
| `escape` | 2 | Can move after it attacks. |
| `scout` | 3 | Sees 2 tiles far instead of 1. |
| `sneak` | 4 | Ignores enemy zones of control: enemies next to it do not stop it. |
| `hide` | 5 | Becomes invisible when it moves, can move through enemy units, and ignores zones of control. Embarks into a `cloak_boat`. |
| `persist` | 7 | Can attack again after a kill, if another target is in range. |
| `convert` | 8 | A hit that does not kill converts the target to the attacker's side. The target does not strike back. Works with 0 attack. |
| `heal` | 9 | Can heal friendly units next to it by 4 HP, or cure their poison, instead of moving. |
| `swim` | 10 | Moves on water without a port and never embarks. See [movementTerrain](#movementterrain). |
| `carry` | 11 | A boat: it carries a passenger unit and unloads it on land or in a city. Its health is the passenger's, unless it has `protect`. A city that trains a boat puts a warrior in it. |
| `grow` | 12 | Grows into another unit after `growthRate` turns. See [upgradesFrom](#upgradesfrom). |
| `fly` | 13 | Can enter every terrain, at the same cost, without a port; it is not hurt by poison improvements. |
| `splash` | 14 | With a range above 1, units next to the target take half damage (or are poisoned, with `poison`). |
| `navigate` | 16 | Can enter the terrains of its `movementTerrain` without a tech. |
| `crush` | 17 | No effect in game logic; only a text exists. |
| `freeze` | 18 | Its attack freezes the target. The target does not strike back. |
| `freezearea` | 19 | Can freeze the area around it instead of moving: water becomes ice and enemy units are frozen. |
| `autofreeze` | 20 | Freezes the area around it after each move. |
| `skate` | 21 | Moves fast on ice, but every other tile stops it. |
| `fortify` | 22 | Gets the higher defence bonus in its own cities. See [defence](#health-defence-and-attack). |
| `creep` | 23 | Every tile except mountains costs normal movement: forest, flood and algae do not slow it. Gets no road bonus. |
| `boost` | 24 | Can give friendly units next to it +1 movement (`swift`) instead of moving. |
| `independent` | 25 | Belongs to no city, so it uses no city unit slot. |
| `poison` | 26 | A hit that does not kill poisons the target. |
| `eat` | 27 | When it kills a unit, a `segment` unit grows at the end of its body. |
| `unique` | 29 | The player can only have one of it at a time. |
| `explode` | 30 | Can explode: it dies, spawns spores and deals half damage and poison to enemy units next to it. |
| `surprise` | 31 | Its targets never strike back. |
| `infiltrate` | 35 | Attacks only enemy cities next to it. It dies, robs the city and spawns daggers (or pirates on water) there. |
| `detect` | 36 | No effect in game logic; only a text exists. |
| `stiff` | 38 | Never strikes back. |
| `protect` | 39 | A boat with its own health, instead of the passenger's health. |
| `stomp` | 40 | Deals half damage to all enemy units next to it after each move. |
| `autoheal` | 41 | Heals friendly units next to it after each move. |
| `static` | 42 | Gets no experience and can never be promoted. |
| `tentacles` | 44 | Attacks with its defence. Hits all units next to it when it is trained or moves, and hits enemies that move next to it. |
| `autoflood` | 45 | Floods the tile that it moves to. |
| `drench` | 46 | Can target any tile that can be flooded, also an empty one, and floods it. |
| `amphibious` | 47 | Gets a bubble (+1 movement) on flooded tiles, if the player has the `bubble` ability, and can clear algae. Also the movement label in the unit info. |
| `water` | 48 | The movement label in the unit info. |
| `land` | 49 | The movement label in the unit info. |
| `algae` | 51 | Spreads algae on the water tiles along its path. |
| `consumed` | 52 | Dies when it attacks. |
| `swarm` | 53 | Like `boost`. |
| `doubleattack` | 54 | Can attack twice per turn. |

New unit abilities can be added with the PolyMod section `unitAbility`; they need a [PolyScript](../docs/polyscripts.md) to do something. The name of an ability is the localization key `unit.abilities.<id>`.

## Vanilla units

| Id | idx | | Id | idx | | Id | idx |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `none` | 0 | | `tridention` | 21 | | `scoutship` | 42 |
| `scout` | 1 | | `mooni` | 22 | | `transportship` | 43 |
| `warrior` | 2 | | `battlesled` | 23 | | `rammership` | 44 |
| `rider` | 3 | | `icefortress` | 24 | | `juggernaut` | 45 |
| `knight` | 4 | | `icearcher` | 25 | | `mermaidwarrior` | 46 |
| `defender` | 5 | | `crab` | 26 | | `mermaidarcher` | 47 |
| `ship` | 6 | | `gaami` | 27 | | `mermaidswordsman` | 48 |
| `battleship` | 7 | | `hexapod` | 28 | | `mermaiddefender` | 49 |
| `catapult` | 8 | | `doomux` | 29 | | `mermaidcloak` | 50 |
| `archer` | 9 | | `phychi` | 30 | | `mermaiddagger` | 51 |
| `mindbender` | 10 | | `kiton` | 31 | | `jelly` | 52 |
| `swordsman` | 11 | | `exida` | 32 | | `shark` | 53 |
| `giant` | 12 | | `centipede` | 33 | | `siren` | 54 |
| `bunny` | 13 | | `segment` | 34 | | `aquapult` | 55 |
| `boat` | 14 | | `raychi` | 35 | | `boomchi` | 56 |
| `polytaur` | 15 | | `shaman` | 36 | | `island` | 57 |
| `navalon` | 16 | | `dagger` | 37 | | `ciru` | 58 |
| `dragonegg` | 17 | | `cloak` | 38 | | `mantis` | 59 |
| `babydragon` | 18 | | `cloak_boat` | 39 | | `bugegg` | 60 |
| `firedragon` | 19 | | `pirate` | 40 | | `moth` | 61 |
| `amphibian` | 20 | | `bombership` | 41 | | `larva` | 62 |

Some of these ids are unused or only used in special modes. Check the dumped GLD for the units that the game uses.

## Hard-coded unit behavior

Some behavior is tied to specific units in the game code, so copying the abilities of such a unit to a new unit does not copy it:

- The boats that units embark into (see [`embarksTo`](#embarksto)), and the warrior that a trained boat carries.
- `eat` always spawns a `segment`; when a unit with a follower dies, the follower becomes a `centipede`.
- `infiltrate` spawns `dagger` (or `pirate` on water, and in some cases `bugegg`); the `moth` always uses the poison variant.
- The `bunny` destroys the improvement that it steps on. The `polytaur` remembers the climate where it was born.
- The `giant` is the super unit of the city reward. Ruins can give a `swordsman`, `giant`, `navalon`, `battleship`, `bombership` or `rammership`.
- Tribe overrides apply to training, tech unlocks and spawns, but not to embark boats, the warrior in a trained boat, or growth.
