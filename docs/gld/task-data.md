# TaskData

`taskData` lists the tasks: goals that a player can complete during a game. Completing a task unlocks an improvement, usually a monument. In this example the monument is only an illustration; check the dumped GLD for which task unlocks which monument.

```json
"wealth": {
  "idx": 4,
  "improvementUnlocks": ["monument4"]
}
```

## Fields

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| `idx` | integer | — | The number of the task. |
| `improvementUnlocks` | improvement id list | `[]` | The improvements that the player can build after completing the task. Tribe `improvementOverrides` do not apply to them. |

The title of a task is the localization key `polyplayer.task.<id>.title`, and its description is `polyplayer.task.<id>.description`.

A task starts when the player gets a tech that has it in its [`taskUnlocks`](tech-data.md#taskunlocks). Some tasks also start by themselves, as the table shows.

> [!NOTE]
> The condition of each task is code in the game, one class per task. A new task id from a patch has no condition, so it can never be completed without a [PolyScript](../docs/polyscripts.md). You can change the `improvementUnlocks` of the vanilla tasks.

## Vanilla tasks

| Id | idx | Completed when | Starts by itself |
| --- | --- | --- | --- |
| `none` | 0 | — | — |
| `pacifist` | 1 | The player does not attack for 5 turns. | no |
| `genius` | 2 | The player owns every tech of their tribe's tree. | no |
| `network` | 3 | 5 cities are connected to the capital. | when a city is connected |
| `wealth` | 4 | The player has 100 stars. | no |
| `killer` | 5 | The player has killed 10 units. | on a kill |
| `metropolis` | 6 | The player has a city of level 5 or more. | when a city levels up or is captured |
| `explorer` | 7 | The player has explored every tile of the map. | no |
| `explorelighthouses` | 8 | The player has explored all 4 corners of the map (the lighthouses). | yes |
| `convert` | 9 | The player has converted 3 units. | no |
