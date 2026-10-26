using UnityEngine;
using ReferenceGame.Characters;

namespace ReferenceGame
{
    /// <summary>
    /// Main game manager for Reference Game.
    /// Demonstrates Framework initialization and game setup.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Player Setup")]
        [SerializeField] private ReferenceGamePlayer player;

        [Header("Game State")]
        [SerializeField] private bool initializeOnStart = true;

        private void Start()
        {
            if (initializeOnStart)
            {
                InitializeGame();
            }
        }

        private void InitializeGame()
        {
            Debug.Log("=== Reference Game Initializing ===");

            // Find player if not assigned
            if (player == null)
            {
                player = FindFirstObjectByType<ReferenceGamePlayer>();
            }

            if (player != null)
            {
                Debug.Log($"Player '{player.Name}' initialized at Level {player.Level}");
                Debug.Log($"Stats: HP={player.PlayerStats.CurrentHealth}/{player.PlayerStats.MaxHealth}, " +
                         $"MP={player.PlayerStats.CurrentMana}/{player.PlayerStats.MaxMana}");
                Debug.Log($"Attributes: STR={player.PlayerStats.Strength}, " +
                         $"AGI={player.PlayerStats.Agility}, " +
                         $"INT={player.PlayerStats.Intelligence}, " +
                         $"VIT={player.PlayerStats.Vitality}");
            }
            else
            {
                Debug.LogWarning("No player found in scene!");
            }

            Debug.Log("=== Reference Game Ready ===");
            Debug.Log("Controls: WASD to move, Space to test damage");
        }

        private void Update()
        {
            // Test damage/heal with Space key
            if (Input.GetKeyDown(KeyCode.Space) && player != null)
            {
                TestCombat();
            }

            // Test heal with H key
            if (Input.GetKeyDown(KeyCode.H) && player != null)
            {
                player.Heal(20f);
                Debug.Log($"Healed! Current HP: {player.PlayerStats.CurrentHealth}/{player.PlayerStats.MaxHealth}");
            }
        }

        private void TestCombat()
        {
            float damage = 15f;
            Debug.Log($"Testing combat system - dealing {damage} damage");

            float healthBefore = player.PlayerStats.CurrentHealth;
            player.TakeDamage(damage);
            float healthAfter = player.PlayerStats.CurrentHealth;

            Debug.Log($"HP: {healthBefore:F0} -> {healthAfter:F0}");

            if (!player.IsAlive)
            {
                Debug.Log("<color=red>Player defeated!</color>");
            }
        }

        private void OnGUI()
        {
            // Instructions
            GUILayout.BeginArea(new Rect(Screen.width - 310, 10, 300, 150));
            GUILayout.Label("<b>Reference Game - Controls</b>", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Label("WASD - Move");
            GUILayout.Label("Space - Test Damage (15 HP)");
            GUILayout.Label("H - Heal (20 HP)");
            GUILayout.Label("");
            GUILayout.Label("<color=yellow>Framework Integration: ✓</color>");
            GUILayout.EndArea();
        }
    }
}
