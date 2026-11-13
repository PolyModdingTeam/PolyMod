# Localization

PolyMod supports two kinds of text files:

- **Localization files** (`localization.json`, `localization_*.json`) add or change terms in the existing game languages. Use them for the names and descriptions of your content.
- **Language files** (`language.json`, `language_*.json`) add a new language to the game, or change many terms of one language.

## Localization files

A localization file is a JSON object. Each key is a term key. Each value is an object that maps a **language name** to the text in that language.

```json
{
  "polymod": {
    "English": "PolyMod",
    "Elyrion": "πȱ∫ỹmȱΔ"
  },
  "unit_names_lancer": {
    "English": "Lancer",
    "Russian": "Копейщик"
  }
}
```

### Term keys

The game uses dots in its term keys, for example `unit.names.warrior`. In a localization file, write the key with underscores:

| You write | PolyMod uses |
| --- | --- |
| `_` (one underscore) | `.` |
| `__` (two underscores) | `_` |

So `unit_names_lancer` becomes `unit.names.lancer`, and `unit_names_cloak__boat` becomes `unit.names.cloak_boat`.

A key that starts with `tribeskins` gets the prefix `TribeSkins/`, which is where the game keeps skin names. For example, `tribeskins_myskin` becomes `TribeSkins/tribeskins.myskin`.

If a term already exists, the new texts replace the old ones. So you can also change vanilla texts.

### Languages

The language names are the names that the game uses for its languages. At the time of writing these are:

`English`, `Portuguese (Brazil)`, `Russian`, `Italian (Italy)`, `French (France)`, `Spanish (Mexico)`, `German (Germany)`, `Japanese`, `Korean`, `Hindi`, `Indonesian`, `Malay`, `Polish`, `Thai`, `Turkish`, `Vietnamese`, `Elyrion`

The column headers of the dumped `localization_*.csv` files show the current list. See [Debugging and dumping](debugging.md).

If a term has no text for a language, PolyMod uses the `English` text for that language. If there is no `English` text either, it uses the term key.

### Keys for game content

The game finds the text of GLD content from its id. For new content, add these keys (shown here with dots; write them with underscores in the file):

| Content | Key | Example id `lancer` |
| --- | --- | --- |
| Tribe name | `tribes.<id>` | `tribes_lancer` |
| Tribe description | `tribes.<id>.info` | `tribes_lancer_info` |
| Unit name | `unit.names.<id>` | `unit_names_lancer` |
| Unit ability name | `unit.abilities.<id>` | `unit_abilities_lancer` |
| Improvement name | `building.names.<id>` | `building_names_lancer` |
| Tech name | `technology.names.<id>` | `technology_names_lancer` |
| Resource name | `resource.names.<id>` | `resource_names_lancer` |
| Terrain name | `terrain.<id>` | `terrain_lancer` |
| Task title | `polyplayer.task.<id>.title` | `polyplayer_task_lancer_title` |
| Task description | `polyplayer.task.<id>.description` | `polyplayer_task_lancer_description` |
| Player ability name | `player.abilities.<id>` | `player_abilities_lancer` |
| Skin name | `TribeSkins/tribeskins.<id>` | `tribeskins_lancer` |
| Skin description | `TribeSkins/tribeskins.<id>.description` | `tribeskins_lancer_description` |

To find the key of any other text, dump the localization and search the `language_en.json` file for the text.

Some game code builds keys with the number of an enum value instead of its name. PolyMod changes a custom number (1000 or more) in a key to the id of the content, so your keys always use the id.

To replace the info text of an improvement, use the PolyMod field [`infoOverride`](../gld/improvement-data.md#polymod-fields).

## Adding a new language

A language file adds a language or changes many terms of one language. The file name gives the **language code**: `language_xx.json` has the code `xx`.

The file is a JSON object that maps term keys to texts. Here the keys use dots, exactly as the game does. The file must contain the key `language`, whose value is the name of the language in the language selection.

```json
{
    "language": "Pirate English",
    "unit.names.warrior": "Deckhand",
    "unit.names.archer": "Musketeer"
}
```

- If no game language has that code, PolyMod adds a new language with that name and code.
- If a game language already has that code, PolyMod changes the terms of that language.
- Terms that are not in the file get no text from PolyMod in a new language. Start from a dumped `language_en.json` and translate it, so that every term has a text.

Each language code can come from only one file. If two files use the same code, PolyMod writes an error to the log for the second one.
