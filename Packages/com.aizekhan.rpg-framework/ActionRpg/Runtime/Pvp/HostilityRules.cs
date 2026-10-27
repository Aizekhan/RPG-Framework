using System;
using RPGFramework.Factions;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Who may attack whom. <see cref="ActorWorld.Attack"/> refuses every attack these rules call friendly.
    /// </summary>
    public interface IHostilityRules
    {
        /// <param name="attacker">Never null, never the same actor as <paramref name="target"/>.</param>
        bool AreHostile(Actor attacker, Actor target);
    }

    /// <summary>
    /// The built-in rules.
    /// </summary>
    public static class HostilityRules
    {
        /// <summary>
        /// Co-op: players fight non-players, nobody fights their own kind.
        /// </summary>
        public static readonly IHostilityRules Coop = new CoopHostility();

        /// <summary>
        /// PvP: actors of different teams fight, teammates do not; an actor without a team follows the co-op rule.
        /// </summary>
        public static readonly IHostilityRules Teams = new TeamHostility();

        /// <summary>
        /// The rules a game with these settings plays by: <see cref="ActionRpgSettings.Hostility"/> when set,
        /// otherwise <see cref="Teams"/> in PvP and <see cref="Coop"/> without it.
        /// </summary>
        public static IHostilityRules For(ActionRpgSettings settings) =>
            settings?.Hostility ?? (settings?.Pvp != null ? Teams : Coop);

        private sealed class CoopHostility : IHostilityRules
        {
            public bool AreHostile(Actor attacker, Actor target) => attacker.IsPlayer != target.IsPlayer;
        }
    }

    /// <summary>
    /// Actors of different teams are hostile, teammates are not. Actors without a team (enemies of a PvE wave)
    /// follow the co-op rule: players and non-players fight.
    /// </summary>
    public class TeamHostility : IHostilityRules
    {
        public bool AreHostile(Actor attacker, Actor target)
        {
            if (string.IsNullOrEmpty(attacker.Team) || string.IsNullOrEmpty(target.Team))
                return attacker.IsPlayer != target.IsPlayer;
            return attacker.Team != target.Team && AreHostileTeams(attacker.Team, target.Team);
        }

        /// <summary>
        /// Two different teams: hostile unless overridden.
        /// </summary>
        protected virtual bool AreHostileTeams(string attackerTeam, string targetTeam) => true;
    }

    /// <summary>
    /// Teams are faction ids: factions at war (<see cref="FactionRelationType.Enemy"/> or
    /// <see cref="FactionRelationType.Rival"/>) fight, allied, neutral and trading factions do not, a faction does
    /// not fight itself. Actors without a team follow the co-op rule.
    /// </summary>
    public sealed class FactionHostility : TeamHostility
    {
        private readonly IFactionRegistry _factions;

        public FactionHostility(IFactionRegistry factions)
        {
            _factions = factions ?? throw new ArgumentNullException(nameof(factions));
        }

        protected override bool AreHostileTeams(string attackerTeam, string targetTeam)
        {
            var relation = _factions.GetRelationship(attackerTeam, targetTeam);
            return relation != null
                   && (relation.RelationType == FactionRelationType.Enemy || relation.RelationType == FactionRelationType.Rival);
        }
    }
}
