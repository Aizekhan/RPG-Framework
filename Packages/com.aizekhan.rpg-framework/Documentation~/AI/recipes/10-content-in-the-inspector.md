# Recipe: add content in the inspector

Content is assets, not code. Change the assets; the game and its tests pick them up.

| Task | Do |
|---|---|
| New enemy | Game Setup > **New Enemy** (or Create > RPG Framework > Character, then add it to Enemies). Set id (Kill quests target it), stats, speed, reach, attack rate, experience, loot table. |
| Real model | Drag the prefab into the Character's **Prefab** (pivot at the feet, facing +Z). Controls, AI, strike and health bar are added in play mode; add `PlayerController` / `EnemyBrain` / `StrikeAnimator` / `HealthBarView` to the prefab to tune them. Animator: set the strike's trigger name. |
| New item | Game Setup > **New Item**. Loot tables and quest rewards refer to it by its item id. |
| Loot | Game Setup > **New Loot Table**; set it on a Character's **Loot**. Character loot tables are included automatically. |
| Quest | Game Setup > **New Starting Quest** (a Kill objective on the first enemy). Other quests go in **Other Quests** and are given by your code (`IQuestManager.AcceptQuest`). |
| Waves | Game Setup > Waves: delays, first size, growth, cap; untick **Enabled** to spawn enemies from your own code (`ActorWorld.SpawnEnemy`). |
| Same run every time | Game Setup > **Seed** (0 = random). Tests use a seed for repeatable loot and waves. |

The box at the top of the Game Setup inspector lists every problem (unknown item in a loot table, quest targeting a
missing character, empty slots). The host refuses to start with problems and logs each one; the scene test fails too.

Rules the assets cannot express go in code (recipes 02 and 03): subscribe to `ActorSpawnedEvent`, `ActorDamagedEvent`,
`WaveStartedEvent`, `DeathEvent`, `QuestCompletedEvent`, `LevelUpEvent` and act through `ActorWorld`.
