using System;
using System.Collections.Generic;
using RPGFramework.Combat;
using RPGFramework.Integration.CombatQuests;
using RPGFramework.Quests;

namespace RPGFramework.Bootstrap.Gameplay
{
    /// <summary>
    /// Advances Kill objectives from combat deaths through a <see cref="KillObjectiveBridge"/>.
    /// The game says what a victim counts as (<c>targetIdOf</c>: entity → enemy type id, null to skip) and,
    /// optionally, who gets the credit for a kill (<c>creditedEntityOf</c>).
    /// </summary>
    public sealed class CombatQuestsModule : GameFeatureModule
    {
        public const string ModuleName = "Gameplay.CombatQuests";

        private readonly Func<int, string> _targetIdOf;
        private readonly Func<int, int> _creditedEntityOf;

        public CombatQuestsModule(Func<int, string> targetIdOf, Func<int, int> creditedEntityOf = null)
        {
            _targetIdOf = targetIdOf ?? throw new ArgumentNullException(nameof(targetIdOf));
            _creditedEntityOf = creditedEntityOf;
        }

        public override string Name => ModuleName;

        public override IEnumerable<Type> Dependencies =>
            new[] { typeof(FrameworkCoreModule), typeof(CombatModule), typeof(QuestsModule) };

        /// <summary>
        /// The running bridge (null before initialization and after shutdown).
        /// </summary>
        public KillObjectiveBridge Bridge { get; private set; }

        protected override void OnConfigure()
        {
        }

        protected override void OnInitialize()
        {
            Bridge = new KillObjectiveBridge(Container.Resolve<ILifeStateManager>(), Container.Resolve<IQuestManager>(),
                _targetIdOf, _creditedEntityOf);
        }

        protected override void OnShutdown()
        {
            Bridge?.Dispose();
            Bridge = null;
        }
    }
}
