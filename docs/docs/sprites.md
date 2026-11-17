# Sprites

A sprites file (`sprites.json` or `sprites_*.json`) gives settings for how PolyMod makes sprites. With it you can:

- change the pivot and the pixels per unit of a sprite,
- make several sprites from one texture,
- make a sprite from a vanilla game sprite,
- turn a sprite into an outline, with an optional blur.

A texture that has no entry in a sprites file still becomes a sprite, with the [default settings](textures.md#how-the-sprite-is-made).

## Format

The file is a JSON object. Each key is a **sprite name** (PolyMod changes it to lowercase). Each value is an object with these fields:

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| `pixelsPerUnit` | number | The value of the origin sprite info, if it has one; else `2112` for a texture and the original value for a game sprite | How many texture pixels make one unit in the game world. A larger value makes the sprite smaller. |
| `pivot` | `[x, y]` | The pivot of the origin sprite info, if it has one; else the [preset pivot](textures.md#how-the-sprite-is-made) for a texture and the original pivot for a game sprite | The point that the sprite is positioned, rotated and scaled around, as a fraction of the sprite size. `[0, 0]` is the bottom-left corner and `[1, 1]` is the top-right corner. Values outside 0–1 are allowed. |
| `spriteOrigin` | object | none | Where the image comes from. See [Sprite origin](#sprite-origin). Without it, PolyMod uses the texture with the same name as the sprite. |
| `outline` | object | none | Changes the image into an outline. See [Making outlines](#making-outlines). |

Field names are case-sensitive.

> [!TIP]
> Unity documents pixels per unit and pivots in its [Sprite documentation](https://docs.unity3d.com/Manual/sprite/sprite-editor/use-editor.html).

### Sprite origin

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| `name` | string | — | The name of the source image. |
| `originType` | `"Outer"` or `"Inner"` | `"Outer"` | `Outer`: the source is a texture of a mod, by its [texture name](textures.md#texture-names). `Inner`: the source is a sprite of the game, by its sprite name. |

PolyMod makes `Outer` sprites when the game data loads, and `Inner` sprites when the start screen opens, because the game sprites are not loaded before that.

If the source texture or game sprite does not exist, PolyMod skips the entry. For a missing `Inner` sprite, it writes a warning to the log.

## Examples

Change the pivot and size of a texture:

```json
{
	"lumberhut": {
		"pixelsPerUnit": 1056,
		"pivot": [
			0.5,
			0.1
		]
	}
}
```

Use one texture for two sprites with different pivots. The texture `lancer.png` also stays available as the sprite `lancer`:

```json
{
	"lancer_head": {
		"spriteOrigin": {
			"name": "lancer"
		},
		"pivot": [
			0.5,
			0.2
		]
	}
}
```

Reuse a vanilla sprite under a new name, for example for a new improvement:

```json
{
	"mytemple": {
		"spriteOrigin": {
			"name": "temple",
			"originType": "Inner"
		}
	}
}
```

### Making outlines

Units have an outline sprite next to each body sprite, named like the sprite plus `_outline`. Instead of drawing it, let PolyMod make it from the same texture with an `outline` object:

```json
{
	"lancer_body_outline": {
		"spriteOrigin": {
			"name": "lancer_body"
		},
		"outline": {
			"thickness": 8,
			"blur": 16
		}
	}
}
```

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| `thickness` | integer | `8` | How far the silhouette grows around the visible pixels of the shape, in pixels. |
| `color` | text or `null` | `"#FFFFFF"` | The color of the silhouette, as `#RRGGBB` or `#RRGGBBAA`. Keep white for outlines that the game tints. With `null`, PolyMod does not fill a color: the image keeps its own pixels, and only the blur changes it. `thickness` is then not used. |
| `blur` | number | `0` | The radius of a Gaussian blur, in pixels. `0` gives a hard edge. |

PolyMod makes the texture larger by the thickness and by the reach of the blur, so the blur is never cut at the edge of the texture. The pixels per unit stay the same, so the outline does not change size in the world. PolyMod also moves the pivot by the added pixels: the outline gets the `pivot` and `pixelsPerUnit` of its source sprite info, so it lines up with the source without extra settings.
