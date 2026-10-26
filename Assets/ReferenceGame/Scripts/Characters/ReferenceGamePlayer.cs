using UnityEngine;
using RPGFramework.Characters;
using ReferenceGame.Stats;

namespace ReferenceGame.Characters
{
    /// <summary>
    /// Reference Game player implementation using Framework's CharacterBase.
    /// Demonstrates how to extend Framework for game-specific functionality.
    /// </summary>
    public class ReferenceGamePlayer : CharacterBase
    {
        [Header("Player Configuration")]
        [SerializeField] private string playerName = "Hero";
        [SerializeField] private int startingLevel = 1;

        private PlayerStats playerStats;

        protected override void Awake()
        {
            base.Awake();
            InitializePlayer();
        }

        private void InitializePlayer()
        {
            // Initialize game-specific stats
            playerStats = new PlayerStats(startingLevel);

            // Set Framework properties
            stats = playerStats;
            inventory = null; // Will be initialized when needed
            progression = null; // Will be initialized when needed
            level = startingLevel;
            isAlive = true;
        }

        // Framework interface implementations
        public override string Name => playerName;
        public override string CharacterClass => "Warrior"; // Default class
        public override bool IsPlayer => true;

        // Game-specific methods
        public PlayerStats PlayerStats => playerStats;

        public void TakeDamage(float amount)
        {
            if (!isAlive) return;

            playerStats.TakeDamage(amount);

            if (!playerStats.IsAlive)
            {
                isAlive = false;
                OnPlayerDeath();
            }
        }

        public void Heal(float amount)
        {
            playerStats.Heal(amount);
        }

        private void OnPlayerDeath()
        {
            Debug.Log($"{Name} has been defeated!");
            // Trigger death effects, respawn logic, etc.
        }

        // Debug visualization
        private void OnGUI()
        {
            if (playerStats == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"<b>{Name}</b> - Level {level}", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            GUILayout.Label($"Health: {playerStats.CurrentHealth:F0}/{playerStats.MaxHealth:F0}");
            GUILayout.Label($"Mana: {playerStats.CurrentMana:F0}/{playerStats.MaxMana:F0}");
            GUILayout.Label($"");
            GUILayout.Label($"Strength: {playerStats.Strength}");
            GUILayout.Label($"Agility: {playerStats.Agility}");
            GUILayout.Label($"Intelligence: {playerStats.Intelligence}");
            GUILayout.Label($"Vitality: {playerStats.Vitality}");
            GUILayout.EndArea();
        }
    }
}
