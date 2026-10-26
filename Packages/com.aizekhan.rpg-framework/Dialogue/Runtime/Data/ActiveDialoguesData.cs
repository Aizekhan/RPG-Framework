using System;
using System.Collections.Generic;
using RPGFramework.ECS;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// ECS component holding all dialogue progress for an entity.
    /// Supports multiple concurrent dialogues per entity.
    /// </summary>
    [Serializable]
    public struct ActiveDialoguesData : IComponent
    {
        /// <summary>
        /// Dictionary of active dialogues indexed by dialogue ID.
        /// </summary>
        public Dictionary<string, DialogueProgressData> ActiveDialogues;

        /// <summary>
        /// List of dialogue IDs that have been completed by this entity.
        /// </summary>
        public List<string> CompletedDialogueIds;

        /// <summary>
        /// Total number of dialogues completed by this entity.
        /// </summary>
        public int TotalDialoguesCompleted;
    }
}
