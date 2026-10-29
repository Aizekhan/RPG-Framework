using System;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// How much a hit deals and a heal restores. Swap it in <see cref="TurnBasedSettings.Formula"/> for the game's
    /// own maths. Use only the random given, so a fixed seed replays the same battle.
    /// </summary>
    public interface IBattleFormula
    {
        /// <param name="power">Multiplier of the scaling stat (1 for a plain attack).</param>
        /// <param name="scalesWith">Attack for weapon hits, Magic for spells.</param>
        HitResult Hit(Combatant attacker, Combatant target, float power, CombatStat scalesWith, Random random);

        /// <returns>Health restored (before it is capped at the target's maximum).</returns>
        int Heal(Combatant healer, Combatant target, float power, CombatStat scalesWith, Random random);
    }

    public readonly struct HitResult
    {
        public HitResult(bool missed, int damage, bool critical = false)
        {
            Missed = missed;
            Damage = missed ? 0 : damage;
            Critical = !missed && critical;
        }

        public bool Missed { get; }
        public int Damage { get; }

        /// <summary>
        /// A critical hit: it already deals the extra damage.
        /// </summary>
        public bool Critical { get; }

        public static HitResult Miss => new HitResult(true, 0);
    }

    /// <summary>
    /// The default maths, deterministic for a seed:
    /// a weapon hit (scales with Attack) first misses with <see cref="MissChance"/>; spells never miss.
    /// Damage = stat x power - target Defense x (<see cref="DefenseWeight"/> for weapons,
    /// <see cref="MagicDefenseWeight"/> for spells), then x (1 ± <see cref="Variance"/>), halved
    /// (<see cref="DefendMultiplier"/>) when the target defends, rounded, at least 1. A hit that lands is critical
    /// with <see cref="CriticalChance"/> plus the attacker's own (<see cref="CombatantData.CriticalChance"/>) and
    /// deals <see cref="CriticalMultiplier"/> times as much (no roll is made while both chances are 0).
    /// Healing = stat x power x (1 ± Variance), rounded, at least 1.
    /// </summary>
    public sealed class DefaultBattleFormula : IBattleFormula
    {
        public float MissChance { get; set; } = 0.05f;
        public float Variance { get; set; } = 0.1f;
        public float DefenseWeight { get; set; } = 0.5f;
        public float MagicDefenseWeight { get; set; } = 0.25f;
        public float DefendMultiplier { get; set; } = 0.5f;
        public float CriticalChance { get; set; }
        public float CriticalMultiplier { get; set; } = 1.5f;

        public HitResult Hit(Combatant attacker, Combatant target, float power, CombatStat scalesWith, Random random)
        {
            bool weapon = scalesWith != CombatStat.Magic;
            if (weapon && MissChance > 0f && random.NextDouble() < MissChance)
                return HitResult.Miss;

            float raw = attacker.Stat(scalesWith) * power - target.Defense * (weapon ? DefenseWeight : MagicDefenseWeight);
            raw *= Spread(random);
            float chance = CriticalChance + (attacker.Data?.CriticalChance ?? 0f);
            bool critical = chance > 0f && random.NextDouble() < chance;
            if (critical)
                raw *= CriticalMultiplier;
            if (target.IsDefending)
                raw *= DefendMultiplier;
            return new HitResult(false, Math.Max(1, Combatant.Round(raw)), critical);
        }

        public int Heal(Combatant healer, Combatant target, float power, CombatStat scalesWith, Random random)
        {
            float raw = healer.Stat(scalesWith) * power * Spread(random);
            return Math.Max(1, Combatant.Round(raw));
        }

        private float Spread(Random random) =>
            Variance > 0f ? 1f + (float)(random.NextDouble() * 2.0 - 1.0) * Variance : 1f;
    }
}
