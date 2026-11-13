# Config

A mod can let players change values of its [GLD patches](gld-patching.md) without editing the mod. For example, the player can choose how much a new unit costs, or turn a feature off.

## How it works

Every patch file is a [Scriban](https://github.com/scriban/scriban/blob/master/doc/language.md) template. PolyMod renders the template before it reads the patch as JSON. In the template, the function `config` gets a config value:

```
{{ config "key" "default" }}
```

- `key` is the name of the value.
- `default` is the value to use when the player has not set one.

PolyMod keeps the values in `mods.json` in the game folder, in an object with the id of your mod as its key. When a template asks for a key that is not there, PolyMod adds the key with the default value, so after the first start the player finds all your config values in `mods.json`:

```json
{
	"first_mod": {
		"warriorCost": "5",
		"lancers": "true"
	}
}
```

The player edits `mods.json` and restarts the game. The new values apply the next time that PolyMod loads the patch.

> [!NOTE]
> `config` always gives the value as **text**. The text is inserted into the JSON as it is. So `"cost": {{ config "warriorCost" "5" }}` becomes `"cost": 5`, a number, and `"startingUnit": "{{ config "unit" "warrior" }}"` becomes a string because the template puts the quotes around it.

PolyMod only renders a patch as a template if it contains `{{`. A patch without `{{` is read as plain JSON.

## Example: a configurable value

```
{
    "unitData": {
        "warrior": {
            "cost": {{ config "warriorCost" "5" }}
        }
    }
}
```

## Example: turning content on and off

Because `config` gives text, compare it with a text value in conditions. A plain `if config ...` is always true, because any non-empty text is true in Scriban.

This patch adds the `dashbender` unit and makes it the starting unit of Ai-Mo, but only if the config value `dashbenders` is `true`:

```
{{ dashbenders = (config "dashbenders" "true") == "true" }}
{
  "unitData": {
    {{ if dashbenders }}
    "dashbender": {
      "idx": -1,
      "health": 100,
      "defence": 10,
      "movement": 1,
      "range": 1,
      "attack": 0,
      "cost": 15,
      "weapon": "magic",
      "unitAbilities": ["dash", "convert", "stiff", "land"],
      "movementTerrain": ["field", "forest", "mountain"]
    },
    {{ end }}
    "mindbender": {}
  },
  "tribeData": {
    "aimo": {
      "startingUnit": "{{ if dashbenders }}dashbender{{ else }}mindbender{{ end }}"
    }
  }
}
```

The empty `"mindbender": {}` entry keeps the JSON valid when the `if` block is left out: it makes sure that a comma inside the block is always followed by another entry. In a real mod you would also add the unit to a tech.

For all template features, such as variables, loops and functions, see the [Scriban language documentation](https://github.com/scriban/scriban/blob/master/doc/language.md).

## Errors

If the template has a syntax error, PolyMod writes `error during parse of gld patch template` and the details to the log, and sets the status of the mod to *Error*. The rendered text must also be valid JSON; see [Errors](gld-patching.md#errors).

## Other settings files

- `PolyMod.json` holds the settings of PolyMod itself. See [Debugging and dumping](debugging.md#polymodjson).
- PolyMod 2.0 also contains a config API for [PolyScripts](polyscripts.md#configuration), but it is not public yet.
