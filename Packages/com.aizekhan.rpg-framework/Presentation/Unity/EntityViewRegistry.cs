using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGFramework.Presentation
{
    /// <summary>
    /// Two-way link between entities and the GameObjects that show them: find the view of an entity (to play a hit
    /// effect, move a health bar) and the entity of a view (what did the player click, what did the sword touch).
    /// </summary>
    public interface IEntityViewRegistry
    {
        int Count { get; }
        IEnumerable<EntityView> Views { get; }

        /// <summary>
        /// Links an entity to a view, adding an <see cref="EntityView"/> to the GameObject if it has none.
        /// </summary>
        /// <exception cref="InvalidOperationException">The entity or the view is already bound.</exception>
        EntityView Bind(int entityId, GameObject view);

        /// <summary>
        /// Removes the link and returns the view (null when the entity had none), so the caller can despawn it.
        /// </summary>
        GameObject Unbind(int entityId);

        bool TryGetView(int entityId, out EntityView view);

        /// <summary>
        /// Entity shown by this GameObject or one of its parents (e.g. the collider of a child mesh).
        /// </summary>
        bool TryGetEntity(GameObject gameObject, out int entityId);
    }

    public sealed class EntityViewRegistry : IEntityViewRegistry
    {
        private readonly Dictionary<int, EntityView> _views = new Dictionary<int, EntityView>();

        public int Count => _views.Count;

        public IEnumerable<EntityView> Views => _views.Values;

        public EntityView Bind(int entityId, GameObject view)
        {
            if (entityId < 0)
                throw new ArgumentOutOfRangeException(nameof(entityId));
            if (view == null)
                throw new ArgumentNullException(nameof(view));
            if (_views.TryGetValue(entityId, out var existing) && existing != null)
                throw new InvalidOperationException($"Entity {entityId} already has a view ({existing.name})");

            if (!view.TryGetComponent(out EntityView entityView))
                entityView = view.AddComponent<EntityView>();
            if (entityView.IsBound && entityView.EntityId != entityId)
                throw new InvalidOperationException($"{view.name} already shows entity {entityView.EntityId}");

            entityView.Bind(entityId);
            _views[entityId] = entityView;
            return entityView;
        }

        public GameObject Unbind(int entityId)
        {
            if (!_views.TryGetValue(entityId, out var view))
                return null;
            _views.Remove(entityId);
            if (view == null)
                return null; // destroyed elsewhere
            view.Unbind();
            return view.gameObject;
        }

        public bool TryGetView(int entityId, out EntityView view)
        {
            if (_views.TryGetValue(entityId, out view) && view != null)
                return true;
            view = null;
            return false;
        }

        public bool TryGetEntity(GameObject gameObject, out int entityId)
        {
            entityId = EntityView.Unbound;
            if (gameObject == null)
                return false;
            var view = gameObject.GetComponentInParent<EntityView>();
            if (view == null || !view.IsBound || !_views.TryGetValue(view.EntityId, out var bound) || bound != view)
                return false;
            entityId = view.EntityId;
            return true;
        }
    }
}
