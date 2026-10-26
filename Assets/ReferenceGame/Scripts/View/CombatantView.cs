using RPGFramework.Presentation;
using UnityEngine;

namespace ReferenceGame
{
    /// <summary>
    /// Base of the components on a combatant prefab. The presenter binds them to the entity the prefab shows;
    /// they read state from <see cref="GoblinValleyGame"/> and act only through its API, never by changing state
    /// directly. Pooled prefabs are bound again every time they are reused.
    /// </summary>
    public abstract class CombatantView : MonoBehaviour
    {
        protected GoblinValleyGame Game { get; private set; }
        protected IEntityViewRegistry Views { get; private set; }
        protected int EntityId { get; private set; } = EntityView.Unbound;

        protected bool IsBound => Game != null;

        /// <summary>
        /// State of the shown entity; null once it left the valley.
        /// </summary>
        protected Combatant Combatant => Game?.Get(EntityId);

        internal void Bind(GoblinValleyGame game, IEntityViewRegistry views, int entityId)
        {
            Game = game;
            Views = views;
            EntityId = entityId;
            OnBound();
        }

        internal void Unbind()
        {
            OnUnbound();
            Game = null;
            Views = null;
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
    }
}
