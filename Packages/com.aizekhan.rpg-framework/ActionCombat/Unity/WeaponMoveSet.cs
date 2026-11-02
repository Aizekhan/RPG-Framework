using System.Collections.Generic;
using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// The moves of a kind of weapon (a dagger, a two-handed axe, a spear): each move is a strike clip
    /// (<see cref="AttackAsset"/>: its phases, the motion before and during it, the volume it sweeps) with its ranges,
    /// commit share, hold and combo link. Monsters that fight with the weapon take their moves from it
    /// (<see cref="EnemyBehaviourAsset.Weapon"/>), so a new goblin with a dagger needs no moves of its own.
    /// Create > RPG Framework > Action Combat > Weapon Move Set.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG Framework/Action Combat/Weapon Move Set", fileName = "WeaponMoveSet")]
    public sealed class WeaponMoveSet : ScriptableObject
    {
        [Tooltip("The game's name for the kind of weapon (\"dagger\", \"axe\"); empty: the asset's name")]
        [SerializeField] private string _weaponKind = "";

        [Tooltip("Its moves; Next links within this list make the combos")]
        [SerializeField] private List<EnemyBehaviourAsset.Move> _moves = new List<EnemyBehaviourAsset.Move>();

        public string WeaponKind => string.IsNullOrEmpty(_weaponKind) ? name : _weaponKind;

        public IReadOnlyList<EnemyBehaviourAsset.Move> Moves => _moves;

        /// <summary>
        /// The rules' moves, openers only (each built once per call; combo links lead to the rest).
        /// </summary>
        public List<EnemyMove> Build()
        {
            var moves = new List<EnemyMove>();
            EnemyBehaviourAsset.BuildMoves(_moves, moves);
            return moves;
        }

        /// <summary>
        /// What is wrong with the moves (see <see cref="EnemyBehaviourAsset.Problems"/>).
        /// </summary>
        public List<string> Problems() => EnemyBehaviourAsset.MoveProblems(_moves, "Move");

        private void OnValidate()
        {
            foreach (var problem in Problems())
                Debug.LogWarning($"[Weapon Move Set] {name}: {problem}", this);
        }
    }
}
