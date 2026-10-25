namespace RPGFramework.DynamicEvents.Runtime
{
    /// <summary>
    /// Difficulty levels for dynamic events.
    /// </summary>
    public enum EventDifficulty
    {
        /// <summary>
        /// Very easy, can be soloed by low-level players.
        /// </summary>
        Trivial,

        /// <summary>
        /// Easy, requires few players or a single skilled player.
        /// </summary>
        Easy,

        /// <summary>
        /// Normal difficulty, requires moderate group participation.
        /// </summary>
        Normal,

        /// <summary>
        /// Hard difficulty, requires coordinated group effort.
        /// </summary>
        Hard,

        /// <summary>
        /// Epic difficulty, requires large group or raid.
        /// </summary>
        Epic,

        /// <summary>
        /// Legendary difficulty, extremely challenging even for large groups.
        /// </summary>
        Legendary
    }
}
