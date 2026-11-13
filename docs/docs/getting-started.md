# Your first mod

This guide makes a small mod that changes the warrior and adds a new unit. It takes a few minutes and shows the files that most mods use.

## 1. Install PolyMod

Install PolyMod with the [PolyMod Launcher](https://polymod.dev/download/) and start the game once from the launcher. PolyMod then makes the `Mods` folder in the game folder.

## 2. Make the mod folder

Make a folder `Mods/first_mod` and add a `manifest.json`:

```json
{
	"id": "first_mod",
	"name": "My First Mod",
	"version": "1.0.0",
	"authors": [
		"YourName"
	]
}
```

The `id` can only contain lowercase letters and underscores. See [Manifest](manifest.md).

## 3. Change a vanilla value

Add a `patch.json` that gives the warrior 3 attack instead of 2:

```json
{
	"unitData": {
		"warrior": {
			"attack": 30
		}
	}
}
```

Unit attack, defence and health are stored as ten times the value that the game shows, so `30` is 3 attack. See [UnitData](../gld/unit-data.md).

Start the game and train a warrior. Its attack is now 3. Open the PolyMod Hub with **Ctrl + Tab** to see that your mod has the status *Success*.

## 4. Add a new unit

Extend `patch.json` to add a new unit and let the Imperius tribe train it with the Riding tech:

```json
{
	"unitData": {
		"warrior": {
			"attack": 30
		},
		"lancer": {
			"idx": -1,
			"health": 100,
			"defence": 10,
			"movement": 2,
			"range": 1,
			"attack": 25,
			"cost": 4,
			"weapon": "sword",
			"unitAbilities": [
				"dash",
				"fortify",
				"land"
			],
			"movementTerrain": [
				"field",
				"forest",
				"mountain"
			]
		}
	},
	"techData": {
		"riding": {
			"unitUnlocks": [
				"+lancer"
			]
		}
	}
}
```

- `"idx": -1` tells PolyMod that `lancer` is new content. PolyMod gives it a free index when the game starts.
- `"+lancer"` adds `lancer` to the existing `unitUnlocks` array of the Riding tech instead of replacing the whole array. See [Merge rules](gld-patching.md#merge-rules).
- All ids are lowercase.

## 5. Give it a name

The game shows a new unit by its localization key, `unit.names.lancer`. Add a `localization.json`:

```json
{
	"unit_names_lancer": {
		"English": "Lancer",
		"German (Germany)": "Lanzenreiter"
	}
}
```

Underscores in the key become dots. See [Localization](localization.md).

## 6. Give it a look

Without a prefab, a new unit uses a fallback look. To build a real look, add a unit prefab and textures. The easiest way is to start from a dumped vanilla prefab:

1. Turn on debug mode in the hub config, open the hub again, and press **Dump**. See [Debugging and dumping](debugging.md).
2. In `DumpedData/Prefabs/prefabs.json`, copy the entry of a unit whose shape you like.
3. Save it as `unitPrefab_lancer.json` in your mod, and change `skinnedKey.unitType` to `lancer`.
4. Change the `defaultSpriteName` of the visual parts to your own names, and add `.png` files with those names.

See [Prefabs](prefabs.md) and [Textures](textures.md).

## 7. Share it

Zip the *contents* of `Mods/first_mod` (so that `manifest.json` is at the root of the archive) and rename the archive to `first_mod.polymod`. Players put that file in their `Mods` folder.

## Next steps

- [GLD patching](gld-patching.md): all merge rules, new tribes, skins and PolyMod-only fields.
- [Game logic data reference](../gld/index.md): every GLD section and field.
- [Config](config.md): let players change values of your patch.
- [PolyScripts](polyscripts.md): change game code with C#.
