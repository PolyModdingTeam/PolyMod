# Debugging and dumping

## The PolyMod Hub

The PolyMod Hub shows all mods with their version, authors, status and checksum, and the global checksum. Open it with the PolyMod button on the start screen, or with **Ctrl + Tab** anywhere in the game.

The **Config** button of the hub opens the PolyMod settings:

| Setting | Effect |
| --- | --- |
| Debug | Shows the BepInEx console window with the live log, adds the **Dump** button to the hub, and turns off the warning for saved games with a different mod set. |
| Show coordinates | Shows the coordinates of every tile in games and in the [tribe previews](tribe-previews.md#finding-coordinates). |

## The log

PolyMod, BepInEx and your [PolyScripts](polyscripts.md) write to the BepInEx log, `BepInEx/LogOutput.log` in the game folder. In debug mode, the console window shows the same log while the game runs.

Search the log for the id of your mod. PolyMod writes a line for each file that it registers, for example `Registered patch from first_mod mod`, and an error with details for each problem.

## PolyMod.json

`PolyMod.json` in the game folder holds the PolyMod settings. PolyMod makes it with the default values on the first start.

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| `debug` | boolean | `false` | Debug mode, as above. |
| `showCoordinates` | boolean | `false` | Shows tile coordinates, as above. |
| `allowUnsafeIndexes` | boolean | `false` | Lets PolyMod accept a fixed `idx` for new GLD content, if it is larger than all vanilla values of that type. For testing only; see [Adding new content](gld-patching.md#adding-new-content). |
| `overrideDeviceId` | string | `""` | Not used by PolyMod at this time. |
| `disabledMods` | array of strings | `[]` | Ids of mods that PolyMod does not load. |

```json
{"debug":true,"showCoordinates":false,"allowUnsafeIndexes":false,"overrideDeviceId":"","disabledMods":["first_mod"]}
```

Close the game before you edit the file.

## Dumping game data

In debug mode, the hub has a **Dump** button. It writes the data of the running game to the `DumpedData` folder in the game folder, and shows a popup with a button that opens the folder.

| Path | Content |
| --- | --- |
| `Logic/GameLogicData<n>.json` | The vanilla GLD of every GLD version from 1 to the current one. The file with the highest number is the current one. |
| `Logic/AvatarData<n>.json` | The avatar data of every version. |
| `Localization/language_<code>.json` | All terms of each language. Use one as the start for a [new language](localization.md#adding-a-new-language). |
| `Localization/localization_<category>.csv` | All terms of each localization category, with one column per language. |
| `Sprites/<atlas>/<sprite>.png` | Every sprite of every loaded atlas. |
| `Sprites/<atlas>/sprites_<atlas>.json` | The pixels per unit and the pivot of each sprite, in the [sprites file format](sprites.md). |
| `Prefabs/prefabs.json` | Every vanilla unit, improvement and resource prefab, in the [prefab file format](prefabs.md). |
| `preview_<tribe>.json` | The tiles of the preview map of each tribe, in the [preview format](tribe-previews.md). |

> [!TIP]
> The dumped `GameLogicData` file is the best reference for vanilla values. Use it together with the [GLD reference](../gld/index.md), which explains the fields.

The dump contains the *vanilla* GLD, without the patches of your mods. The sprite dump contains only the atlases that are loaded at the time, so dump while a game is open to get the in-game sprites.

## Common problems

| Problem | Likely cause |
| --- | --- |
| The mod is not in the hub. | `manifest.json` is missing, not at the root, or invalid; the id has uppercase letters or digits; or the mod is in `disabledMods`. The log says why. |
| The game data does not load after you add a patch. | The patch is not valid JSON, or it uses an unknown or uppercase id. |
| The mod has the status *Error*. | A PolyScript threw an exception, or a config template has an error. The log has the details. |
| A new unit has the wrong look. | It has no [unit prefab](prefabs.md#unit-prefabs), or the `skinnedKey` does not match. |
| A texture does not replace the vanilla sprite. | The file name does not match the sprite name. Compare it with the dumped sprite names, see [Textures](textures.md). |
| A saved game does not open. | The mod set changed since the game was saved. See [Checksums](manifest.md#checksums-and-saved-games). |
