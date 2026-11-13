# DiplomacyData

`diplomacyData` holds the values of embassies. Unlike the other sections, it is one object, not a list of entries, and it is the same for all tribes.

```json
"diplomacyData": {
  "embassyCost": 5,
  "embassyIncome": 2,
  "embasyGrowthTime": 3,
  "embassyMaxLevel": 3,
  "embassyUpgradeCost": 20,
  "dividendIncome": 2
}
```

## Fields

| Field | Type | Description |
| --- | --- | --- |
| `embassyCost` | integer | The star cost of an embassy, but only in games made with game version 113 or older. In newer games the cost is `4 + 2 × (players that already have an embassy with this player)`, and this field is ignored. |
| `embassyIncome` | integer | The star income of an embassy: `embassyIncome × embassy level`, doubled when the two players are at peace. It is paid to the capital. |
| `embasyGrowthTime` | integer | Not read by the game; left over from the development of diplomacy. (The spelling with one *s* is the real field name.) |
| `embassyMaxLevel` | integer | Not read by the game; left over from the development of diplomacy. |
| `embassyUpgradeCost` | integer | Not read by the game; left over from the development of diplomacy. |
| `dividendIncome` | integer | Not read by the game. (The `dividend` [player ability](tech-data.md#player-abilities) has no effect either.) |

The field `embassyTechPriceMultiplier`, which older GLD versions had, no longer exists.
