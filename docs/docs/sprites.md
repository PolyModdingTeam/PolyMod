# Sprites

A sprites file (`sprites.json` or `sprites_*.json`) gives settings for how PolyMod makes sprites. With it you can:

- change the pivot and the pixels per unit of a sprite,
- make several sprites from one texture,
- make a sprite from a vanilla game sprite,
- turn a sprite into an outline, or blur it.

A texture that has no entry in a sprites file still becomes a sprite, with the [default settings](textures.md#how-the-sprite-is-made).

## Format

The file is a JSON object. Each key is a **sprite name** (PolyMod changes it to lowercase). Each value is an object with these fields:

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| `pixelsPerUnit` | number | `2112` for a texture; the original value for a game sprite | How many texture pixels make one unit in the game world. A larger value makes the sprite smaller. |
| `pivot` | `[x, y]` | The [preset pivot](textures.md#how-the-sprite-is-made) for a texture; the original pivot for a game sprite | The point that the sprite is positioned, rotated and scaled around, as a fraction of the sprite size. `[0, 0]` is the bottom-left corner and `[1, 1]` is the top-right corner. Values outside 0–1 are allowed. |
| `spriteOrigin` | object | none | Where the image comes from. See [Sprite origin](#sprite-origin). Without it, PolyMod uses the texture with the same name as the sprite. |
| `transformToOutline` | boolean | `false` | Changes the image into a white silhouette with an 8 pixel border. See [Making outlines](#making-outlines). |
| `gaussianBlurRadius` | number | none | Blurs the image with this radius, in pixels. |

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

Units have an outline sprite next to each body sprite, named like the sprite plus `_outline`. Instead of drawing it, let PolyMod make it from the same texture:

```json
{
	"lancer_body_outline": {
		"spriteOrigin": {
			"name": "lancer_body"
		},
		"transformToOutline": true
	}
}
```

The outline is larger than the source by 8 pixels on each side. Set its `pivot` if it must line up with a sprite that does not use the center pivot.
