# Minor Data Structures

This page documents the smaller data structures found in the game's data file, including terrain, resources, tasks, skins, and diplomacy settings.

---

## Terrain Data (`terrainData`)

Defines all possible terrain types in the game. Each terrain is an object within the `terrainData` section, keyed by its name. To add a new terrain, simply add a new entry with a unique name and `idx`.

### Syntax

```json
"terrainData": {
  "water": {
    "idx": 1
  },
  "field": {
    "idx": 3
  }
}
```

### Properties

| Property | Type    | Description                                                                                                                                                             | Example Value |
| :------- | :------ | :---------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :------------ |
| `idx`    | Integer | A unique integer index for the terrain type. When adding a new entry, it is recommended to set this value to **-1** to ensure uniqueness and prevent conflicts with game updates. | `"idx": 1`    |

---

## Resource Data (`resourceData`)

Defines all possible resource types that can spawn on the map.

### Syntax

```json
"resourceData": {
  "game": {
    "resourceTerrainRequirements": [
      "forest"
    ],
    "idx": 1
  }
}
```

### Properties

| Property                        | Type             | Description                                                                                                                                                               | Example Value                  |
| :------------------------------ | :--------------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | :----------------------------- |
| `resourceTerrainRequirements` | Array of Strings | An array of terrain IDs where this resource can naturally spawn.                                                                                                            | `["forest"]`                   |
| `idx`                           | Integer          | A unique integer index for the resource type. When adding a new entry, it is recommended to set this value to **-1** to ensure uniqueness and prevent conflicts with game updates. | `"idx": 1`                     |

---

## Task Data (`taskData`)

Defines special in-game achievements or "tasks" that unlock unique monuments upon completion.

### Syntax

```json
"taskData": {
  "pacifist": {
    "improvementUnlocks": [
      "monument1"
    ],
    "idx": 1
  }
}
```

### Properties

| Property               | Type             | Description                                                                                                                                                            | Example Value         |
| :--------------------- | :--------------- | :--------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :-------------------- |
| `improvementUnlocks` | Array of Strings | An array of improvement IDs (usually a monument) unlocked when this task is completed.                                                                                 | `["monument1"]`       |
| `idx`                  | Integer          | A unique integer index for the task. When adding a new entry, it is recommended to set this value to **-1** to ensure uniqueness and prevent conflicts with game updates. | `"idx": 1`            |

---

## Skin Data (`skinData`)

Defines alternate appearances (skins) for tribes. A tribe must list the skin's key in its `skins` array to use it.

### Syntax

```json
"skinData": {
  "swamp" : {
    "color" : 6786096,
    "language": "bub,ly,sq,ee,to,ad"
  }
}
```

### Properties

| Property   | Type    | Description                                                                         | Example Value                        |
| :--------- | :------ | :---------------------------------------------------------------------------------- | :----------------------------------- |
| `color`    | Integer | An integer representing the skin's primary color, overriding the tribe's default.   | `"color": 6786096`                   |
| `language` | String  | A comma-separated string of syllables, overriding the tribe's default language.     | `"language": "bub,ly,sq,ee,to,ad"` |

---

## Diplomacy Data (`diplomacyData`)

Contains key-value pairs that define the global game mechanics for diplomacy, such as embassies.

### Syntax

```json
"diplomacyData": {
  "embassyCost" : 5,
  "embassyIncome" : 2,
  "embassyMaxLevel" : 3,
  "embassyUpgradeCost" : 20
}
```

### Properties

| Property               | Type    | Description                                                       | Example Value          |
| :--------------------- | :------ | :--------------------------------------------------------
