using RPGFramework.Presentation;
using UnityEngine;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Base of the components on a character prefab. The presenter binds them to the entity the prefab shows;
    /// they read state from the world (<see cref="IActorWorld"/>) and act only through its API. Pooled prefabs are bound again
    /// every time they are reused. Derive from it for your own per-character behaviour.
    /// </summary>
    public abstract class ActorView : MonoBehaviour
    {
        protected IActorWorld World { get; private set; }
        protected IEntityViewRegistry Views { get; private set; }
        protected CharacterDefinition Definition { get; private set; }
        protected int EntityId { get; private set; } = EntityView.Unbound;

        protected bool IsBound => World != null;

        /// <summary>
        /// State of the shown character; null once it left the world.
        /// </summary>
        protected Actor Actor => World?.Get(EntityId);

        internal void Bind(IActorWorld world, IEntityViewRegistry views, CharacterDefinition definition, int entityId)
        {
            World = world;
            Views = views;
            Definition = definition;
            EntityId = entityId;
            OnBound();
        }

        internal void Unbind()
        {
            OnUnbound();
            World = null;
            Views = null;
            Definition = null;
            EntityId = EntityView.Unbound;
        }

        protected virtual void OnBound()
        {
        }

        protected virtual void OnUnbound()
        {
        }

        protected bool TryGetPosition(int entityId, out Vector3 position)
        {
            if (Views != null && Views.TryGetView(entityId, out var view))
            {
                position = view.transform.position;
                return true;
            }
            position = default;
            return false;
        }

        protected void Face(Vector3 direction, float degreesPerSecond)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
                degreesPerSecond * Time.deltaTime);
        }

        protected static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
