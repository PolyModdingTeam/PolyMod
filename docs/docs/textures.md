# Textures

A texture is a `.png` file in a mod. PolyMod loads each texture and makes a sprite from it. The game then uses the sprite in place of the vanilla sprite with the same name, or a prefab uses it as a visual part.

## Texture names

The sprite name is the file name without `.png`, in lowercase. The folder does not matter: `sprites/units/Lancer.png` gives the sprite name `lancer`.

> [!WARNING]
> Two textures with the same file name overwrite each other, also when they are in different folders or different mods. The texture that PolyMod loads last wins.

When the game looks for a sprite, it gives a **base name**, and optionally a tribe, a skin and a level. PolyMod tries these sprite names, from the least to the most specific, and uses the most specific one that exists:

| Sprite name | Used for |
| --- | --- |
| `base` | all tribes and skins |
| `base_tribe` | one tribe, all its skins |
| `base_skin` | one skin |
| `base_tribe_skin` | one skin of one tribe |

For sprites that have levels, the level comes directly after the base name: `base_level`, `base_level_tribe`, `base_level_skin`, `base_level_tribe_skin`.

`tribe` and `skin` are the [ids](../gld/index.md#ids) of the tribe and skin, for example `imperius` or `baerion`. The default skin has no skin part.

These are the same rules that the game uses for its own sprite names, so the names of the dumped vanilla sprites show the base names that you can use.

### Base names of terrain, resources and improvements

For terrain, resources and improvements, PolyMod also accepts the [id](../gld/index.md#ids) of the type as the base name, in place of the internal sprite name of the game. For example, `field.png` replaces the field tile for all tribes, `forest_imperius.png` replaces the Imperius forest, and `lumberhut.png` replaces the lumber hut. A flooded field has the base name `field_flooded`.

New terrains, resources and improvements always use their id as the base name.

### Outlines

Units and some other objects have an outline sprite. Its name is the sprite name followed by `_outline`, for example `lancer_body_outline` for the sprite `lancer_body`. To make an outline from an existing texture, use [`transformToOutline`](sprites.md#making-outlines) in `sprites.json`.

## Examples

| File | Effect |
| --- | --- |
| `lumberhut.png` | Replaces the lumber hut of all tribes and skins. |
| `lumberhut_cymanti.png` | Replaces the lumber hut only for Cymanti. |
| `city_3.png` | Replaces the level 3 city for all tribes. |
| `city_3_bardur_baerion.png` | Replaces the level 3 city of Bardur with the Baerion skin. |
| `mightyleader_imperius.png` | Replaces the image at the top of the Mighty Leader popup for Imperius. |

## How the sprite is made

PolyMod makes the sprite with these default settings:

| Setting | Default |
| --- | --- |
| Pixels per unit | `2112` |
| Pivot | `[0.5, 0]` for names that start with `field`, `[0.5, -0.375]` for names that start with `mountain`, and `[0.5, 0.5]` for all other names |

Use a [`sprites.json` file](sprites.md) to change these settings for a sprite.

PolyMod also fixes common problems of exported images: if an image looks like it has premultiplied alpha, PolyMod changes it back to straight alpha, and it fills the color of fully transparent edge pixels so that no dark seams show when the game scales the sprite.

## Getting the vanilla sprites

Dump the game data to get every loaded sprite as a `.png`, together with its pixels per unit and pivot in `sprites_<atlas>.json`. See [Debugging and dumping](debugging.md). Use these files as templates: an image with the same size and pivot as the vanilla sprite fits in the same place.
