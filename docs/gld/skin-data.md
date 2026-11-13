# SkinData

`skinData` gives skins their own tribe color and city names. A tribe lists its skins in its [`skins`](tribe-data.md#skins) field; `skinData` only adds values that differ from the tribe.

```json
"skinData": {
	"swamp": {
		"color": 6786096,
		"language": "bub,ly,sq,ee,to,ad,bo,swa,bu,oa,squ,am,goo,pho,am,oct"
	}
}
```

## Fields

| Field | Type | Description |
| --- | --- | --- |
| `color` | integer | The color of the skin, as an RGB number: `red × 65536 + green × 256 + blue`. For example, `6786096` is `0x678C30`. If it is `0` or missing, the skin uses the [tribe color](tribe-data.md#color). |
| `language` | string | The city name syllables of the skin, separated by commas. If it is empty or missing, the skin uses the [tribe language](tribe-data.md#language). |

The key is the id of the skin. PolyMod changes `skinData` keys to lowercase.

## Vanilla skins

| Id | Number | Note |
| --- | --- | --- |
| `none` | -1 | No skin. |
| `default` | 0 | The default look of every tribe. |
| `ranger` | 1 |  |
| `ninja` | 2 |  |
| `baerion` | 3 |  |
| `scholar` | 5 |  |
| `mercenary` | 7 |  |
| `sfinx` | 8 |  |
| `skeleton` | 9 |  |
| `arty` | 10 |  |
| `pirate` | 11 |  |
| `aibo` | 12 |  |
| `urkaz` | 13 |  |
| `ikarus` | 14 |  |
| `darkelf` | 15 |  |
| `swamp` | 17 |  |
| `magma` | 18 |  |
| `cute` | 19 |  |
| `test` | 2000 | A test skin, not for play. |

Which tribe has which skin is set by the `skins` field of each tribe in the GLD. Numbers 4, 6 and 16 are not used.

## New skins

A new skin is made by listing a new id in the `skins` of a tribe. See [New skins](../docs/gld-patching.md#new-skins).
