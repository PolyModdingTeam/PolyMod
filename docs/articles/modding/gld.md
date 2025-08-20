# Gld Patching

## Rules
The rules that PolyMod uses to merge patches into the gld are:
- If an array exists in both the patch and the gld, the array will be **completely replaced**, it will not be merged. This can be useful to remove items from an array.
- If a value is `null` in the patch, it will be removed from the gld.

## The basics
The `patch.json` file is essentially a mini GLD that includes only the values you want to change and nothing else. If you want to make a mod that just changes a `warrior`'s attack, for example…
```json
{
  "unitData": {
    "warrior": {
      "attack": 1000
    }
  }
}
```

As you can see, this uses the exact same keys as the GLD file, but excludes everything that's unnecessary for the mod.

## Adding new content
Well, adding new content is as simple as editing existing content! You just have to pretend that something exists, and then it will exist: you need to set its `idx` to `-1` for it to work. This will make PolyMod automatically assign an `idx` when the game starts (autoindex).
> [!WARNING]  
> Manually setting `idx` to something other than `-1` breaks mod compatibility!

Additionally, make sure that all the internal names you use are in lowercase. PolyMod will crash otherwise.
Here's part of an example gld patch which adds new tribe:
```json
{
  "tribeData": {
    "testingtribe": {
      "color":15713321,
      "language":"az,bar,bryn,dûm,girt,hall,kar,khâz,kol,kruk,lok,rûdh,ruf,und,vorn,zak",
      "startingTech":[
        "basic",
        "mining"
      ],
      "startingResource":[
        "metal"
      ],
      "skins":[
        "Testingskin"
      ],
      "priceTier":0,
      "category":2,
      "bonus":0,
      "startingUnit":"warrior",
      "idx":-1,
      "preview": [
        {
          "x": 0,
          "y": 0,
          "terrainType": "mountain",
          "unitType": "swordsman",
          "improvementType ": "mine"
        }
      ]
    }
  }
}
```

## Custom Tribe Preview
As you could have noticed, our testingtribe has a field `preview` which does not exist in GLD. This field was added in order to modify tribe previews if needed.
```json
{
  "preview": [
    {
      "x": 0,
      "y": 0,
      "terrainType": "mountain",
      "unitType": "swordsman",
      "improvementType ": "mine"
    }
  ]
}
```
Each tile of preview consists of:
* `x` X coordinate of the tile in the preview
* `y` Y coordinate of the tile in the preview
* `terrainType` terrain of the original tile will be replaced with the one you choose here
* `resourceType` resource of the original tile will be replaced with the one you choose here
* `unitType` unit of the original tile will be replaced with the one you choose here
* `improvementType` improvement of the original tile will be replaced with the one you choose here

Based on that, our chosen preview tile will have `mountain`, `swordsman` and `mine`.
You can see all tiles and their coordinates in Tribe Preview by enabling PolyMod debug mode in `PolyMod.json`

## Custom Skins
Also, our tribe has a non-existing skin. By writing such, PolyMod will create a skin automatically:
```json
{
  "skins": [
    "Testingskin"
  ]
}
```

## Prefabs
Let's look at this patch which adds new unit:
```json
{
  "unitData": {
    "dashbender": {
      "health": 100,
      "defence": 10,
      "movement": 1,
      "range": 1,
      "attack": 0,
      "cost": 15,
      "unitAbilities": [
      "dash",
      "convert",
      "stiff",
      "land"
      ],
      "weapon": 4,
      "promotionLimit": 3,
      "idx": -1,
      "prefab": "mindbender"
    }
  }
}
```

By default, when creating a new unit, improvement or resource PolyMod will set basic sprites for them, such as:

* New **Unit** have explorer's sprites by default
* New **Improvement**s have custom house's sprites by default
* New **Resource**s have animal's sprites by default

If you want to change it to another already existing type, you can do just what we did for `dashbender`:
```json
{
  "prefab": "mindbender"
}
```
That sets `mindbender`'s sprites as our base sprites for `dashbender`.

## Config
Say that, in your mod, you created a new unit, but you aren't a balancing expert and thus you want the user to be able to configure how expensive it is. Before, you would need polyscript to do this. However, in polymod 1.2 there is a new feature that allows you to have configurable options in gld patches!
Say that, for example, you wanted to change the warrior's cost to whatever the user wants.
you can use `{{ config key defaultValue}}`.
```
{
    "unitData" : {
        "warrior" : {
            "cost" : {{ config "warriorCost" 5 }}
        }
    }
}
```
but what if you want to disable or enable a unit based on config? For that, you need to do more advanced templating. Here is an example that gives ai-mo a dashbender if dashbenders are enabled, otherwise a mindbender. In reality you will also need to modify tech etc.
```
{
  "unitData": {
  {% if config "dashbenders" true %}
    "dashbender": {
      "health": 100,
      "defence": 10,
      "movement": 1,
      "range": 1,
      "attack": 0,
      "cost": 15,
      "unitAbilities": [
      "dash",
      "convert",
      "stiff",
      "land"
      ],
      "weapon": 4,
      "promotionLimit": 3,
      "idx": -1,
      "prefab": "mindbender"
    }
  }
  {% aimo-starting = "dashbender" %}
  {% else %}
  {% aimo-starting = "mindbender" %}
  {% end %}
  "tribeData":{
    "ai-mo":{
        "startingUnit": "{{ aimo-starting }}"
     }
   } 
}
```
For a full list of templates, see [Scriban docs](https://github.com/scriban/scriban/blob/master/doc/language.md).