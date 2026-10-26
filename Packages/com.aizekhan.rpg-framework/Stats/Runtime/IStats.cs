namespace RPGFramework.Stats
{
    /// <summary>
    /// Non-generic marker interface for stat containers.
    /// Allows framework code to reference stats without knowing concrete types.
    /// Game layer implements with specific stat properties (CurrentHealth, Mana, etc.).
    /// </summary>
    public interface IStats
    {
        // Marker interface - game layer extends with concrete properties
        // Example game implementation would add:
        // float CurrentHealth { get; }
        // float MaxHealth { get; }
        // float Strength { get; }
        // etc.
    }
}
