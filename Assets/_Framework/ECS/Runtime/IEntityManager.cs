namespace RPGFramework.ECS
{
    /// <summary>
    /// Current ECS entity-manager contract. Entity IDs remain integers during the first extraction step.
    /// </summary>
    public interface IEntityManager
    {
        int CreateEntity();
        void DestroyEntity(int entityId);

        void AddComponent<TComponent>(int entityId, TComponent component)
            where TComponent : IComponent;

        bool HasComponent<TComponent>(int entityId)
            where TComponent : IComponent;

        TComponent GetComponent<TComponent>(int entityId)
            where TComponent : IComponent;

        bool TryGetComponent<TComponent>(int entityId, out TComponent component)
            where TComponent : struct, IComponent;

        void RemoveComponent<TComponent>(int entityId)
            where TComponent : IComponent;

        int[] GetAllEntities();

        int[] GetEntitiesWith<TComponent>()
            where TComponent : IComponent;
    }
}
