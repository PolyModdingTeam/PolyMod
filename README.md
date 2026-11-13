# [PolyMod](https://polymod.dev)

![](https://img.shields.io/github/downloads/PolyModdingTeam/PolyMod/total)
![](https://img.shields.io/codefactor/grade/github/PolyModdingTeam/PolyMod)
![](https://img.shields.io/github/actions/workflow/status/PolyModdingTeam/PolyMod/cicd.yml)

**The mod loader for The Battle of Polytopia.**

PolyMod lets you play Polytopia with new tribes, units, buildings, skins and much more, made by the community. Mods are just folders or archives you drop into the game, and PolyMod takes care of the rest. Your vanilla game, saves and settings stay untouched.

|         | x86 | ARM |
|:-------:|:---:|:---:|
| Windows |  ✅  |  ❌  |
|  Linux  |  ⏺️  |  ❌  |
|  MacOS  |  ❌  |  ❌  |
| Android |  ➖  |  🚧  |
|   IOS   |  ➖  |  ❌  |

## Features

- **Play with mods in a few clicks.** Browse and download mods right in the launcher or on our website, then start the game.
- **Keep your vanilla game safe.** Modded games use their own save data. Start the game without the launcher, and you get plain Polytopia.
- **Everything in one place.** The PolyMod Hub shows your mods, their status and config, and opens with **Ctrl + Tab** from anywhere in the game.
- **Made for modders.** Change the game logic with JSON patches, add your own sprites, translations and prefabs, or write code mods in C#.

## Installation

1. Download the **PolyMod Launcher** from [polymod.dev/download](https://polymod.dev/download/).
2. Install PolyMod with the launcher.
3. Start Polytopia from the launcher.

That's it. The PolyMod button on the start screen opens the hub.

### Installing mods

The easiest way is to download mods directly in the launcher or on [polymod.dev](https://polymod.dev).

You can also install a mod by hand: put it into the `Mods` folder next to the game and restart.

> [!NOTE]
> Modded multiplayer is not supported yet. Weekly challenges and achievements are only available in vanilla Polytopia, so they are turned off while PolyMod is active.

## Making mods

Want to make your own tribe, unit or skin? Everything you need is on the [wiki](https://github.com/PolyModdingTeam/PolyMod/wiki), and the community on [Discord](https://discord.gg/eWPdhWtfVy) is always happy to help.

## Building from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/PolyModdingTeam/PolyMod.git
cd PolyMod
dotnet build
```

Copy `src/bin/IL2CPP/net6.0/PolyMod.dll` into the `BepInEx/plugins` folder of your game. Run `dotnet test` to run the tests.

## License

PolyMod is licensed under [CC BY-NC-ND 4.0](LICENSE).
