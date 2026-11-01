using RPGFramework.ActionRpg;
using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// An <see cref="EnemyBrain"/> that fights with action combat: when a player is in reach it starts an attack of its
    /// <see cref="Combatant"/> (its weapon's <see cref="Hitbox"/> decides the hit), and while it swings, dodges or is
    /// staggered it stands its ground. Needs a <see cref="Combatant"/> on the same object.
    /// </summary>
    [RequireComponent(typeof(Combatant))]
    public class CombatEnemyBrain : EnemyBrain
    {
        [Tooltip("The attack it uses; empty: its first")]
        [SerializeField] private string _attackId = "";

        private Combatant _combatant;

        protected Combatant Combatant => _combatant != null ? _combatant : _combatant = GetComponent<Combatant>();

        protected override bool IsBusy
        {
            get
            {
                var state = Combatant.State;
                return state != CombatState.Idle && state != CombatState.Blocking;
            }
        }

        protected override void StrikeAt(int target) => Combatant.Attack(_attackId);
    }
}
