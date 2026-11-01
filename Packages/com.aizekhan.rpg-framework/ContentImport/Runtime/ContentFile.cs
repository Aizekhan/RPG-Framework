using System;
using System.Collections.Generic;

namespace RPGFramework.ContentImport
{
    // The JSON shape of a content file (rpgframework.content/1). Field names are the JSON keys; every key is optional
    // except the ids. Enum-like values are strings, read without regard to case. See recipe 20.

    [Serializable]
    public sealed class ContentFile
    {
        /// <summary><see cref="ContentImporter.Schema"/>.</summary>
        public string schema;
        public List<QuestJson> quests = new List<QuestJson>();
        public List<DialogueJson> dialogues = new List<DialogueJson>();
    }

    [Serializable]
    public sealed class QuestJson
    {
        public string id;
        public string title;
        public string description;

        /// <summary>The game's kind of quest: main, side, faction... (free text).</summary>
        public string type;

        public int minimumLevel;
        public bool repeatable;
        public double cooldownSeconds;
        public List<string> prerequisites = new List<string>();
        public List<RequirementJson> requirements = new List<RequirementJson>();
        public List<ObjectiveJson> objectives = new List<ObjectiveJson>();
        public List<RewardJson> rewards = new List<RewardJson>();

        /// <summary>The first stage; empty = the first in <see cref="stages"/>.</summary>
        public string start;

        public List<StageJson> stages = new List<StageJson>();
    }

    [Serializable]
    public sealed class RequirementJson
    {
        /// <summary>level, faction, item, flag or the game's own.</summary>
        public string kind;

        public string id;
        public int value = 1;
    }

    [Serializable]
    public sealed class ObjectiveJson
    {
        public string id;

        /// <summary>kill, collect, interact, explore, escort, custom.</summary>
        public string type;

        public string target;
        public int count = 1;
        public string description;
        public bool optional;
    }

    [Serializable]
    public sealed class RewardJson
    {
        /// <summary>experience, currency, item, unlock, custom.</summary>
        public string type;

        public string id;
        public int amount = 1;
    }

    [Serializable]
    public sealed class StageJson
    {
        public string id;
        public string title;
        public string description;

        /// <summary>Ids of the quest's objectives that count in this stage.</summary>
        public List<string> objectives = new List<string>();

        public List<LinkJson> next = new List<LinkJson>();

        /// <summary>none, success, failure.</summary>
        public string outcome;
    }

    [Serializable]
    public sealed class LinkJson
    {
        public string to;

        /// <summary>A name the game signals; empty = when the stage's objectives are done.</summary>
        public string condition;
    }

    [Serializable]
    public sealed class DialogueJson
    {
        public string id;
        public string title;
        public string description;

        /// <summary>The game's kind of dialogue: quest, ambient, trader, bark... (free text).</summary>
        public string type;

        public string quest;
        public string stage;
        public List<string> participants = new List<string>();
        public string start;
        public List<NodeJson> nodes = new List<NodeJson>();
    }

    [Serializable]
    public sealed class NodeJson
    {
        public string id;

        /// <summary>line, choice, branch, end; empty = choice when it has choices, else line.</summary>
        public string type;

        public string speaker;
        public string text;

        /// <summary>The localization key of the text.</summary>
        public string key;

        public List<string> next = new List<string>();
        public ConditionJson condition;
        public List<ChoiceJson> choices = new List<ChoiceJson>();
        public List<EffectJson> effects = new List<EffectJson>();
    }

    [Serializable]
    public sealed class ChoiceJson
    {
        /// <summary>Empty = "c1", "c2"... in order.</summary>
        public string id;

        public string text;
        public string key;
        public string to;
        public ConditionJson condition;
        public List<EffectJson> effects = new List<EffectJson>();
    }

    [Serializable]
    public sealed class ConditionJson
    {
        /// <summary>quest (completed), stat, item, variable, custom (or the ConditionType names).</summary>
        public string type;

        public string id;
        public int value = 1;
    }

    [Serializable]
    public sealed class EffectJson
    {
        /// <summary>The game's effect kind (in the action RPG kit a DialogueEffectKind name: giveItem, signalQuest...).</summary>
        public string kind;

        public string target;
        public int amount = 1;
    }
}
