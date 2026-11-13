# Manifest

The manifest is the `manifest.json` file at the root of a mod. It gives the metadata of the mod: its id, name, version, authors and dependencies. A mod without a valid manifest does not load.

## Format

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | string | yes | The unique id of the mod. It must match `^(?!polytopia$)[a-z_]+$`: only lowercase letters `a`–`z` and underscores, no digits, and not `polytopia`. |
| `name` | string | no | The name that the hub shows. If you leave it out, PolyMod uses the id. |
| `description` | string | no | A description that the hub shows under the mod. |
| `version` | string | yes | The version of the mod, with 2 to 4 numbers: `1.0`, `1.2.3` or `1.2.3.4`. Pre-release suffixes such as `1.0.0-beta` are not allowed. |
| `authors` | array of strings | yes | The authors of the mod. The array must not be empty. |
| `dependencies` | array of objects | no | The mods that this mod needs. See [Dependencies](#dependencies). |
| `client` | boolean | no, default `false` | Set to `true` for a mod that only changes how the game looks or sounds on this computer, for example a texture pack. PolyMod leaves client mods out of the [checksum](#checksums-and-saved-games). |

Field names are case-sensitive: write them exactly as in the table.

> [!WARNING]
> PolyMod does not catch errors while it reads `manifest.json`. A file that is not valid JSON, or a `version` that is not a valid version, can stop PolyMod from loading. Check your manifest with a JSON validator. Trailing commas are not allowed.

## Dependencies

Each item of `dependencies` is an object:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | string | yes | The id of the mod that this mod needs. |
| `min` | string | no | The lowest allowed version, inclusive. |
| `max` | string | no | The highest allowed version, inclusive. |
| `required` | boolean | no, default `true` | If `true`, the mod does not load when the dependency is missing or has a bad version. If `false`, PolyMod only writes a warning to the log. |

A dependency also sets the [load order](mod-structure.md#load-order): your mod always loads after the mods that it depends on, also when the dependency is not required.

> [!TIP]
> There is a special mod with the id `polytopia`. Its version is the version of the game, for example `2.18.0.16751`. Use it to limit your mod to the game versions that it works with.

## Example

```json
{
	"id": "docs_mod",
	"name": "Docs Mod",
	"description": "An example mod for the documentation.",
	"version": "1.2.3",
	"authors": [
		"DocMan",
		"bro0x7"
	],
	"dependencies": [
		{
			"id": "polytopia",
			"min": "2.18.0"
		},
		{
			"id": "another_cool_mod",
			"required": false
		}
	]
}
```

## Validation

PolyMod skips a mod and writes an error to the log when:

- the mod has no `manifest.json` at its root,
- the manifest has no `id`, no `version`, or no `authors` (or an empty `authors` array),
- the `id` does not match the pattern above,
- another mod with the same `id` was already registered.

PolyMod also skips a mod whose id is in the `disabledMods` list of `PolyMod.json`. See [Debugging and dumping](debugging.md#polymodjson).

## Checksums and saved games

PolyMod computes a SHA-256 checksum for each loaded mod from its id, its version, and the name and contents of every file (except `manifest.json`). It combines the checksums of all loaded mods that are not `client` mods into one global checksum.

The global checksum is saved with every new game. When you open a saved game that was made with a different set of mods, PolyMod shows a warning and does not open the game, because the game data would not match. In debug mode, PolyMod does not show this warning.

The hub shows the first 12 characters of each checksum, so you can compare mod sets with other players.

> [!NOTE]
> Modded multiplayer is not supported yet.
