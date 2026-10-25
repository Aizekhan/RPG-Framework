namespace RPGFramework.Progression
{
    /// <summary>
    /// Represents something that can be unlocked at a certain level.
    /// Can be abilities, features, items, or any game content.
    /// </summary>
    public struct UnlockData
    {
        /// <summary>Unique unlock identifier</summary>
        public string UnlockId;

        /// <summary>Display name</summary>
        public string Name;

        /// <summary>Description</summary>
        public string Description;

        /// <summary>Level required to unlock</summary>
        public int RequiredLevel;

        /// <summary>Unlock type (Ability, Feature, Item, etc.)</summary>
        public string UnlockType;

        /// <summary>Additional data for unlock (JSON, parameters, etc.)</summary>
        public string AdditionalData;

        public UnlockData(string unlockId, string name, int requiredLevel, string unlockType = "Feature")
        {
            UnlockId = unlockId;
            Name = name;
            Description = string.Empty;
            RequiredLevel = requiredLevel;
            UnlockType = unlockType;
            AdditionalData = string.Empty;
        }
    }
}
