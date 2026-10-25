using System.Collections.Generic;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// Concrete implementation of save metadata.
    /// </summary>
    public class SaveMetadata : ISaveMetadata
    {
        public long Timestamp { get; set; }
        public string GameVersion { get; set; }
        public string CharacterName { get; set; }
        public float PlaytimeSeconds { get; set; }
        public string LocationName { get; set; }
        public int CharacterLevel { get; set; }
        public Dictionary<string, string> CustomData { get; set; }

        public SaveMetadata()
        {
            Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            GameVersion = "1.0.0";
            CharacterName = string.Empty;
            PlaytimeSeconds = 0f;
            LocationName = string.Empty;
            CharacterLevel = 1;
            CustomData = new Dictionary<string, string>();
        }

        public SaveMetadata(
            string gameVersion,
            string characterName,
            float playtimeSeconds,
            string locationName,
            int characterLevel)
        {
            Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            GameVersion = gameVersion;
            CharacterName = characterName;
            PlaytimeSeconds = playtimeSeconds;
            LocationName = locationName;
            CharacterLevel = characterLevel;
            CustomData = new Dictionary<string, string>();
        }
    }
}
