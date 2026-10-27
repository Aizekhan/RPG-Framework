using System;
using System.Collections.Generic;

namespace RPGFramework.ECS
{
    /// <summary>
    /// Entity/component store of one world. Entity IDs are positive integers and are never reused.
    /// Not thread-safe: use it from the simulation thread; hand copies (e.g. <see cref="GetEntitiesWith{TComponent}()"/>
    /// results or component values) to worker threads.
    /// </summary>
    public interface IEntityManager
    {
        event Action<int> EntityCreated;

        /// <summary>
        /// Raised before the entity's components are removed, so handlers can still read them.
        /// </summary>
        event Action<int> EntityDestroyed;

        event Action<int, Type> ComponentAdded;
        event Action<int, Type> ComponentRemoved;

        int EntityCount { get; }

        int CreateEntity();
        void DestroyEntity(int entityId);
        bool Exists(int entityId);

        /// <summary>
        /// Adds the component or replaces the existing value. Throws for unknown/destroyed entities.
        /// </summary>
        void AddComponent<TComponent>(int entityId, TComponent component)
            where TComponent : IComponent;

        bool HasComponent<TComponent>(int entityId)
            where TComponent : IComponent;

        /// <summary>
        /// Returns the component, or default if the entity does not have it.
        /// </summary>
        TComponent GetComponent<TComponent>(int entityId)
            where TComponent : IComponent;

        bool TryGetComponent<TComponent>(int entityId, out TComponent component)
            where TComponent : struct, IComponent;

        /// <summary>
        /// Direct reference to the stored component for in-place mutation.
        /// Valid until components of this type are added or removed. Throws if missing.
        /// </summary>
        ref TComponent GetComponentRef<TComponent>(int entityId)
            where TComponent : struct, IComponent;

        void RemoveComponent<TComponent>(int entityId)
            where TComponent : IComponent;

        int[] GetAllEntities();

        int[] GetEntitiesWith<TComponent>()
            where TComponent : IComponent;

        int[] GetEntitiesWith<T1, T2>()
            where T1 : IComponent
            where T2 : IComponent;

        int[] GetEntitiesWith<T1, T2, T3>()
            where T1 : IComponent
            where T2 : IComponent
            where T3 : IComponent;

        /// <summary>
        /// Allocation-free variant: clears <paramref name="results"/> and fills it.
        /// </summary>
        void GetEntitiesWith<TComponent>(List<int> results)
            where TComponent : IComponent;

        /// <summary>
        /// Allocation-free variant: clears <paramref name="results"/> and fills it with the entities that have both.
        /// </summary>
        void GetEntitiesWith<T1, T2>(List<int> results)
            where T1 : IComponent
            where T2 : IComponent;

        /// <summary>
        /// Allocation-free variant: clears <paramref name="results"/> and fills it with the entities that have all three.
        /// </summary>
        void GetEntitiesWith<T1, T2, T3>(List<int> results)
            where T1 : IComponent
            where T2 : IComponent
            where T3 : IComponent;

        int CountWith<TComponent>()
            where TComponent : IComponent;

        /// <summary>
        /// Clears <paramref name="results"/> and fills it with the component types of the entity.
        /// </summary>
        void GetComponentTypes(int entityId, List<Type> results);
    }
}
