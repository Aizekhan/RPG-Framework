using System;
using RPGFramework.Combat;
using RPGFramework.Quests;

namespace RPGFramework.Integration.CombatQuests
{
    /// <summary>
    /// Advances Kill objectives when combat reports a death. The game says what a victim counts as
    /// (<c>targetIdOf</c>: entity → enemy type id) and, optionally, who gets the credit
    /// (<c>creditedEntityOf</c>: killer → quest owner, e.g. a summon's master); return null / a negative id to skip.
    /// Dispose to stop listening.
    /// </summary>
    public sealed class KillObjectiveBridge : IDisposable
    {
        private readonly ILifeStateManager _lifeStates;
        private readonly IQuestManager _quests;
        private readonly Func<int, string> _targetIdOf;
        private readonly Func<int, int> _creditedEntityOf;
        private bool _disposed;

        public KillObjectiveBridge(ILifeStateManager lifeStates, IQuestManager quests, Func<int, string> targetIdOf,
            Func<int, int> creditedEntityOf = null)
        {
            _lifeStates = lifeStates ?? throw new ArgumentNullException(nameof(lifeStates));
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _targetIdOf = targetIdOf ?? throw new ArgumentNullException(nameof(targetIdOf));
            _creditedEntityOf = creditedEntityOf;
            _lifeStates.OnDeath += HandleDeath;
        }

        /// <summary>
        /// Objectives advanced since creation.
        /// </summary>
        public int ObjectivesAdvanced { get; private set; }

        private void HandleDeath(DeathEvent death)
        {
            if (death.KillerEntityId < 0 || death.KillerEntityId == death.EntityId)
                return;

            int credited = _creditedEntityOf != null ? _creditedEntityOf(death.KillerEntityId) : death.KillerEntityId;
            if (credited < 0)
                return;

            string targetId = _targetIdOf(death.EntityId);
            if (targetId == null)
                return;

            ObjectivesAdvanced += _quests.ReportProgress(credited, ObjectiveType.Kill, targetId);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _lifeStates.OnDeath -= HandleDeath;
        }
    }
}
