# Texture
A texture is an image file in png format, which is being loaded by PolyMod in order to be used ingame.

PolyMod decides how to load the texture based on filename. Texture's file name consists of `name`, `style` and `level`.
* `name` id of the texture
* `style` tribe or skin id, for which this texture should be set
* `level` level of the texture

### Format
Here is all possible combinations of how you can name the texture file:
* `name__.png` will replace the texture of chosen target for all tribes, skins and possible levels
* `name_style_.png` will replace the texture of chosen target for chosen tribe or skin and for all possible levels
* `name__level.png` will replace the texture of chosen target for all tribes and skins, but for chosen level
* `name_style_level.png` will replace the texture of chosen target for chosen tribe or skin and for chosen level

### Example
You want to replace all lumberhut textures for all tribes.
* We want to replace it for **all** tribes and skins, so we dont specify the style.
* Lumber hut has only one level, which means we dont want to specify it.
  In such case, you should name it as `lumberhut__.png`

# Sprites
Sprites file is a json file which declares advanced settings for how each texture should be transformed into the sprite. Mod sprites is declared in `sprites.json` file.

### Format
* `pixelsPerUnit` _(optional, default `2112`)_ in Unity, a **sprite PPU** is a property that determines how many pixels from a sprite texture correspond to one unit in the Unity game world.

* `pivot` _(optional, default `[0.5, 0.5]`)_ in Unity, a **sprite pivot** is the reference point that determines how a sprite is positioned within the Unity game world. It acts as the point relative to which happen all movements, rotation and scaling of the sprite.

> [!TIP]
> You can find more info in [Unity Documentation](https://docs.unity.com/).

### Example
```json
{
  "lumberhut__": {
    "pixelsPerUnit": 256,
    "pivot": [0.1, 0.5]
  }
}
```

# Prefab
A prefab is a json file in a mod that describes a unit prefab. Prefabs are declared in `prefab_name.json` files (name is your prefab name).

### Format
* `type` int value of the type of a prefab. Here is all current prefab types:
```
Unit,
Improvement,
Resource
```
* `name` name of your prefab
* `visualParts` array of prfab's visual parts
    * `gameObjectName` name of the GameObject of the visual part
    * `baseName` sprite name of the visual part
    * `coordinates` position of the visual part
    * `rotation` _(optional, default `0`)_ rotation of the visual part
    * `scale` scale of the visual part
    * `tintable` _(optional, default `false`)_ is visual part a tint

### Example
```json
{
	"type": 0,
	"name": "Yuukwi",
	"visualParts": [
		{
			"gameObjectName": "Body",
			"baseName": "body",
			"coordinates": [0, 0.2],
			"rotation": 180,
			"scale": [1, 1],
			"tintable": false
		},
		{
			"gameObjectName": "Body_Tint",
			"baseName": "body_tint",
			"coordinates": [0, 0.2],
			"rotation": 90,
			"scale": [1, 1],
			"tintable": true
		}
	]
}
```