using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// A body collider (or several, on the bones) that can be hit: it belongs to the <see cref="Combatant"/> in its
    /// parents, or to <c>Entity Id</c> for something that is not one (a crate the kit's world knows).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Hurtbox : MonoBehaviour
    {
        [Tooltip("Whose body this is; empty: the Combatant in the parents")]
        [SerializeField] private Combatant _owner;

        [Tooltip("The entity id when there is no Combatant")]
        [SerializeField] private int _entityId = -1;

        public Combatant Owner => _owner != null ? _owner : _owner = GetComponentInParent<Combatant>();

        /// <summary>
        /// The entity hit through this collider (-1 when it has none).
        /// </summary>
        public int EntityId => Owner != null ? Owner.Id : _entityId;

        public void UseEntityId(int entityId) => _entityId = entityId;

        /// <summary>
        /// The hurtbox a collider belongs to (on it or a parent), or null.
        /// </summary>
        public static Hurtbox Of(Collider collider) => collider != null ? collider.GetComponentInParent<Hurtbox>() : null;
    }
}
