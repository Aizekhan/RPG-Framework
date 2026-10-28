namespace RPGFramework.Combat
{
    /// <summary>
    /// What a hit is besides its numbers: who struck whom, game-defined tags (a hit zone, a damage kind, "from
    /// behind") as a bit mask, what dealt it (an ability, a weapon, an effect; 0 = the attacker's plain strike) and
    /// whether it is damage from an effect (poison, burning) rather than a strike, whether it is a critical hit and
    /// what multiplies it (a crit, a power strike, rage, a combo). The default is a plain strike.
    /// </summary>
    public readonly struct AttackContext
    {
        public AttackContext(int tags, int sourceId = 0, bool fromEffect = false)
            : this(-1, -1, tags, sourceId, fromEffect)
        {
        }

        private AttackContext(int attackerId, int targetId, int tags, int sourceId, bool fromEffect,
            bool isCritical = false, float multiplierAboveOne = 0f)
        {
            AttackerIdOrNone = attackerId + 1;
            TargetIdOrNone = targetId + 1;
            Tags = tags;
            SourceId = sourceId;
            FromEffect = fromEffect;
            IsCritical = isCritical;
            MultiplierAboveOne = multiplierAboveOne;
        }

        // Stored less one, so that default(AttackContext) multiplies by 1
        private float MultiplierAboveOne { get; }

        // Stored shifted by one, so that default(AttackContext) means "nobody" (-1) rather than entity 0
        private int AttackerIdOrNone { get; }
        private int TargetIdOrNone { get; }

        /// <summary>
        /// The entity that dealt the hit; -1 when nobody did (a trap, the world) or not filled in yet.
        /// </summary>
        public int AttackerId => AttackerIdOrNone - 1;

        /// <summary>
        /// The entity that takes the hit; -1 when not filled in yet.
        /// </summary>
        public int TargetId => TargetIdOrNone - 1;

        /// <summary>
        /// Game-defined bits: hit zones, damage kinds, situations. The framework never reads them.
        /// </summary>
        public int Tags { get; }

        /// <summary>
        /// What dealt the hit, as the game numbers it (an ability, a weapon, an effect); 0 = a plain strike.
        /// </summary>
        public int SourceId { get; }

        /// <summary>
        /// Damage from an effect (damage over time, an aura), not a strike.
        /// </summary>
        public bool FromEffect { get; }

        /// <summary>
        /// A critical hit (the game rolled it; <see cref="AsCritical"/>).
        /// </summary>
        public bool IsCritical { get; }

        /// <summary>
        /// What the hit's damage is multiplied by before the calculator sees it: the product of
        /// <see cref="AsCritical"/> and every <see cref="WithMultiplier"/> (1 = as it is).
        /// </summary>
        public float Multiplier => 1f + MultiplierAboveOne;

        /// <summary>
        /// Any of these tag bits is set.
        /// </summary>
        public bool HasAny(int tags) => (Tags & tags) != 0;

        /// <summary>
        /// All of these tag bits are set.
        /// </summary>
        public bool HasAll(int tags) => (Tags & tags) == tags;

        /// <summary>
        /// Damage from an effect with this source id.
        /// </summary>
        public static AttackContext Effect(int sourceId, int tags = 0) => new AttackContext(tags, sourceId, fromEffect: true);

        /// <summary>
        /// The same hit between these two (the rules fill this in before calculating damage).
        /// </summary>
        public AttackContext Between(int attackerId, int targetId) =>
            new AttackContext(attackerId, targetId, Tags, SourceId, FromEffect, IsCritical, MultiplierAboveOne);

        /// <summary>
        /// The same hit with these tags instead.
        /// </summary>
        public AttackContext WithTags(int tags) =>
            new AttackContext(AttackerId, TargetId, tags, SourceId, FromEffect, IsCritical, MultiplierAboveOne);

        /// <summary>
        /// The same hit, critical: its damage times <paramref name="multiplier"/> (on top of any other multiplier).
        /// </summary>
        public AttackContext AsCritical(float multiplier) =>
            new AttackContext(AttackerId, TargetId, Tags, SourceId, FromEffect, true, Times(multiplier));

        /// <summary>
        /// The same hit with its damage times <paramref name="factor"/> (a power strike 1.5, rage 1.2, a combo 2);
        /// several multiply together.
        /// </summary>
        public AttackContext WithMultiplier(float factor) =>
            new AttackContext(AttackerId, TargetId, Tags, SourceId, FromEffect, IsCritical, Times(factor));

        private float Times(float factor)
        {
            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor < 0f)
                throw new System.ArgumentOutOfRangeException(nameof(factor), factor, "A multiplier is a finite number, 0 or more");
            return Multiplier * factor - 1f;
        }

        public override string ToString() =>
            $"{AttackerId}->{TargetId} tags {Tags} source {SourceId}{(FromEffect ? " effect" : "")}" +
            $"{(IsCritical ? " critical" : "")}{(Multiplier != 1f ? $" x{Multiplier}" : "")}";
    }

    /// <summary>
    /// A damage calculator that sees the whole hit (<see cref="AttackContext"/>): hit zones, damage kinds, effect
    /// damage. Rules that know a context use this when the calculator implements it, and the plain
    /// <see cref="IDamageCalculator.CalculateDamage"/> otherwise.
    /// </summary>
    public interface IContextualDamageCalculator
    {
        /// <param name="baseDamage">The attacker's strike, or the effect's amount.</param>
        /// <param name="defenseValue">The target's defense.</param>
        /// <param name="context">The hit, with attacker and target filled in.</param>
        /// <returns>Damage to deal (0 or more).</returns>
        float CalculateDamage(float baseDamage, float defenseValue, AttackContext context);
    }
}
