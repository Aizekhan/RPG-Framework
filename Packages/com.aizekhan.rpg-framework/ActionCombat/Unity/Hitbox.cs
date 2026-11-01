using System.Collections.Generic;
using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// A weapon's trigger collider: what it touches while its owner's swing is live is reported to the rules
    /// (<see cref="CombatRules.ReportContact"/>), which decide whether it is a hit. A blow lands once per target per
    /// swing. The first touch of a body is reported even outside the active window, so the timeline shows a swing
    /// that touched too early. Unity sends trigger messages only when one side has a Rigidbody: without one in the
    /// parents, a kinematic one is added here.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Hitbox : MonoBehaviour
    {
        [Tooltip("Whose weapon this is; empty: the Combatant in the parents")]
        [SerializeField] private Combatant _owner;

        private readonly HashSet<int> _reported = new HashSet<int>();
        private int _swing;

        public Combatant Owner => _owner != null ? _owner : _owner = GetComponentInParent<Combatant>();

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (GetComponentInParent<Rigidbody>() == null)
            {
                var body = gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        private void OnTriggerEnter(Collider other) => Touch(other, entered: true);

        private void OnTriggerStay(Collider other) => Touch(other, entered: false);

        /// <summary>
        /// Reports a touch of a collider (what the trigger messages call; a game with its own detection calls it too).
        /// </summary>
        /// <param name="entered">The first touch: reported even outside the active window.</param>
        public ContactResult Touch(Collider other, bool entered)
        {
            var owner = Owner;
            var hurtbox = Hurtbox.Of(other);
            if (owner == null || hurtbox == null || owner.Arena == null)
                return ContactResult.Ignored;
            int attacker = owner.Id;
            int target = hurtbox.EntityId;
            if (attacker < 0 || target < 0 || target == attacker)
                return ContactResult.Ignored;

            var rules = owner.Arena.Rules;
            int swing = rules.SwingOf(attacker);
            if (swing != _swing)
            {
                _swing = swing;
                _reported.Clear();
            }
            bool live = swing != 0 && rules.IsSwingLive(swing);
            // Outside the window only the first touch during an attack is worth recording
            if (!live && (!entered || swing == 0))
                return ContactResult.Ignored;
            // Once per body per swing while it is live (the rules say what it was)
            if (live && !_reported.Add(target))
                return ContactResult.Ignored;
            // ClosestPoint takes primitives and convex meshes only
            var point = other is MeshCollider mesh && !mesh.convex
                ? other.bounds.ClosestPoint(transform.position)
                : other.ClosestPoint(transform.position);
            return rules.ReportContact(attacker, target, swing, Combatant.ToCombat(point));
        }
    }
}
