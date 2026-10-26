using UnityEngine;

namespace RPGFramework.Presentation
{
    /// <summary>
    /// Marks the GameObject that shows an entity. The entity (in the framework's entity manager) owns the state and
    /// the rules; the view only shows them. Bound and unbound by <see cref="EntityViewRegistry"/>; pooled views are
    /// rebound to a new entity every time they are reused.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityView : MonoBehaviour
    {
        public const int Unbound = -1;

        public int EntityId { get; private set; } = Unbound;

        public bool IsBound => EntityId != Unbound;

        internal void Bind(int entityId) => EntityId = entityId;

        internal void Unbind() => EntityId = Unbound;
    }
}
