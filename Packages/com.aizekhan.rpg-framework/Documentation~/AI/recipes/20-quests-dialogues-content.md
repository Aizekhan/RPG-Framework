# Recipe: quests with stages, dialogues, and content from JSON

Quests and conversations are data. Write them in the inspector (Quest and Dialogue assets) or bring them in as JSON
from a design tool, a spreadsheet export or another program. Do not write a quest state machine, a "which line now"
switch or a JSON parser of your own: they are here.

## Quests with stages

A quest without stages completes when its required objectives are done (as before). With **stages** it moves through
them:

- A stage (`QuestStageData`: `Id`, `Title`, `Description`, `ObjectiveIds`, `Next` links, `Outcome`) is what the player
  does now. Only the objectives the stage names count in it; an objective named by no stage counts in every stage.
- A link without a condition is taken when the stage's required objectives are done (a stage with none passes on at
  once). A link with a **condition** ("golem_defeated") is taken when the game signals it:
  `IQuestStages.Signal(entity, "golem_defeated")` (kit: `world.SignalQuests(player, ...)`, or the dialogue effect
  `SignalQuest`). The game can also move it: `AdvanceStage(entity, quest, stage)` along a link, any stage with
  `force`.
- A stage with `Outcome` **Success** completes the quest (rewards), **Failure** fails it.
- `QuestStageChangedEvent` (from, to, the condition, the outcome) on every move, also into the first stage.
- `Type` is the game's kind of quest (main, side, faction...). `Requirements` (`QuestRequirementData`: kind, id,
  value; kinds `level`, `faction`, `item`, `flag` or the game's own) are answered by the game's `IQuestRequirements`
  (`QuestManager.Requirements`); the action RPG kit answers level, reputation, items in the backpack and dialogue
  variables as flags. `MinimumLevel` is checked the same way. `RepeatCooldownSeconds` (86400 for a daily) on a
  repeatable quest. `WhyNot(entity, quest)` says why it cannot be taken (`QuestRefusal`).
- Saves: the kit saves the stage and the completion times (cooldowns) and sends the stage to co-op friends.

`QuestGraph.Check(quest)` / `Check(quests)` finds: a start or link to a stage that does not exist, stages that cannot
be reached, a stage with no way out that does not end the quest, no reachable success, loops with no exit,
objectives that do not exist, missing or circular prerequisites. The Quest asset's inspector shows them, and so does
`FrameworkValidation`.

## Dialogues

- `DialogueData.Type` (quest, ambient, trader, bark...), `QuestId` and `QuestStageId` (the conversation belongs to that
  stage), `ParticipantIds` (who speaks; "player" and "narrator" always may).
- **Localization:** a line's or answer's `TextKey`. Show `DialogueText.Of(node, localization.Text)`: the key's text in
  the player's language, the written text when there is no key or no entry. `DialogueText.KeyFor(dialogue, node)`
  gives the usual key (`dlg.<dialogue>.<node>`). The kit's dialogue rows (`KitRows.Dialogue`) do this already.
- **Barks:** a dialogue of Line nodes; `DialogueBarks.Pick(dialogue, context, random)` (kit:
  `world.Dialogue.Bark(player, dialogueId)`) gives a line whose condition passes. Nothing starts, no effects.
- **Kit effects** (`DialogueEffectKind`): GiveItem, TakeItem, GiveCurrency, TakeCurrency, AcceptQuest,
  CompleteQuest, SetVariable, GrantSkillPoints, ChangeReputation, **SignalQuest** (a stage condition), **RaiseEvent**
  (`DialogueEventRaisedEvent` with a name and a number for the game's own reaction: start a world event, open a
  door).
- A line with no way on (no `next`, or none whose condition passes) is the last one: going on ends the
  conversation, as an End node does. A one-line conversation is one line. A choice waits for an answer.
- `DialogueGraph.Check(dialogue, questExists, stageExists)` finds: branches that lead nowhere, choices without answers (or with only conditional answers), loops with no exit, nodes that cannot be reached,
  links to nodes that do not exist, empty lines, speakers who are not participants, a quest or stage that does not
  exist. The Dialogue asset's inspector shows the warnings.

## Content from JSON (`rpgframework.content/1`)

```json
{
  "schema": "rpgframework.content/1",
  "quests": [{
    "id": "lost_hammer", "title": "The Lost Hammer", "type": "main", "description": "...",
    "minimumLevel": 0, "repeatable": false, "cooldownSeconds": 0,
    "prerequisites": ["other_quest"],
    "requirements": [{ "kind": "flag", "id": "met_lyra", "value": 1 }],
    "objectives": [{ "id": "reach", "type": "explore", "target": "forge1", "count": 1, "description": "", "optional": false }],
    "rewards": [{ "type": "experience", "amount": 200 }, { "type": "item", "id": "old_hammer", "amount": 1 }],
    "start": "s1",
    "stages": [
      { "id": "s1", "title": "Into the forest", "objectives": ["reach"], "next": [{ "to": "s2" }] },
      { "id": "s2", "next": [{ "to": "s3", "condition": "golem_defeated" }, { "to": "s4", "condition": "player_died_3x" }] },
      { "id": "s3", "outcome": "success" },
      { "id": "s4", "outcome": "failure" }
    ]
  }],
  "dialogues": [{
    "id": "lyra_edge", "title": "Lyra at the forest edge", "type": "quest",
    "quest": "lost_hammer", "stage": "s1", "participants": ["lyra"], "start": "n1",
    "nodes": [
      { "id": "n1", "speaker": "lyra", "text": "Seen a hammer?", "key": "dlg.lyra_edge.n1",
        "choices": [
          { "id": "help", "text": "I can find it.", "to": "n2" },
          { "id": "no", "text": "Not my problem.", "to": "n3", "effects": [{ "kind": "setVariable", "target": "lyra_refused", "amount": 1 }] }
        ] },
      { "id": "n2", "type": "end", "speaker": "lyra", "text": "Be careful.", "effects": [{ "kind": "acceptQuest", "target": "lost_hammer" }] },
      { "id": "n3", "type": "end", "speaker": "player", "text": "Not my problem.",
        "condition": { "type": "variable", "id": "grumpy", "value": 0 } }
    ]
  }]
}
```

- Every key is optional except the ids. Names are read without regard to case. Objective types: kill, collect,
  interact, explore, escort, custom. Reward types: experience, currency, item, unlock, custom. Outcomes: none,
  success, failure. Node types: line, choice, branch, end (empty: choice when it has choices, else line). Condition
  types: quest (completed), stat, item, variable (or flag), custom. Effect kinds: the game's (the kit's
  `DialogueEffectKind` names). An answer without an id is `c1`, `c2`... in order.
- A node goes on through `next` (the first whose condition passes) or its `choices`; an `end` node closes the
  conversation and can still say its text, and so does a line with no `next` (from v0.32.1). A conversation cannot
  start at an `end` node (it would close before its line is shown): a one-line conversation is a line.
- Ids are the contract between the file and the game: keep them when the text changes, so saves and effects still
  match.

Bring it in:

- **Code:** `var package = ContentImporter.Read(json, knownQuest)` gives `Quests`, `Dialogues`, `Effects` and
  `Problems` (form errors and the graph checks; `HasErrors`). It changes nothing. `ContentImporter.Write(quests,
  dialogues, effects)` writes the same JSON back.
- **Action RPG kit:** list the JSON files in the Game Setup's **Content Files** (and quest ids in **Starting Quest
  Ids**), or in code `content.Add(package)`; a file with errors is left out and named by **Validate**. Effects of an
  unknown kind are reported and left out.
- **Assets:** **RPG Framework > Tools > Import Content (JSON)...** makes Quest and Dialogue assets in
  `Assets/Content` (an id already in the project updates its asset). Batch mode:
  `-executeMethod RPGFramework.ContentImport.Editor.ContentAssetImport.ImportFromCommandLine -contentFile <file>
  [-contentFolder <folder>]` (exit code 0 imported, 1 the file has errors and nothing was written, 2 failed). Dialogue
  assets do not hold effects; a kit game keeps them by listing the file in the Game Setup.
