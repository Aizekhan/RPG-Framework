using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.ECS
{
    /// <summary>
    /// Реалізація менеджера сутностей
    /// </summary>
    public class EntityManager : IEntityManager
    {
        private int _nextEntityId = 1;
        private readonly Dictionary<int, Dictionary<Type, IComponent>> _components = new Dictionary<int, Dictionary<Type, IComponent>>();
        private readonly Dictionary<Type, HashSet<int>> _entitiesByComponent = new Dictionary<Type, HashSet<int>>();
        
        public int CreateEntity()
        {
            int entityId = _nextEntityId++;
            _components[entityId] = new Dictionary<Type, IComponent>();
            return entityId;
        }
        
        public void DestroyEntity(int entityId)
        {
            if (!_components.ContainsKey(entityId))
                return;
                
            // Видалення всіх компонентів
            foreach (var componentType in _components[entityId].Keys.ToList())
            {
                if (_entitiesByComponent.ContainsKey(componentType))
                {
                    _entitiesByComponent[componentType].Remove(entityId);
                }
            }
            
            _components.Remove(entityId);
        }
        
        public void AddComponent<TComponent>(int entityId, TComponent component) where TComponent : IComponent
        {
            if (!_components.TryGetValue(entityId, out var entityComponents))
            {
                throw new ArgumentException(
                    $"Entity ID {entityId} has not been created or has already been destroyed.",
                    nameof(entityId));
            }

            Type componentType = typeof(TComponent);
            entityComponents[componentType] = component;
            
            // Оновлення кешу для швидкого пошуку
            if (!_entitiesByComponent.ContainsKey(componentType))
                _entitiesByComponent[componentType] = new HashSet<int>();
                
            _entitiesByComponent[componentType].Add(entityId);
        }
        
        public bool HasComponent<TComponent>(int entityId) where TComponent : IComponent
        {
            return _components.ContainsKey(entityId) && _components[entityId].ContainsKey(typeof(TComponent));
        }
        
        public TComponent GetComponent<TComponent>(int entityId) where TComponent : IComponent
        {
            if (!HasComponent<TComponent>(entityId))
                return default!;
                
            return (TComponent)_components[entityId][typeof(TComponent)];
        }
        public bool TryGetComponent<T>(int entityId, out T component) where T : struct, IComponent
        {
            if (HasComponent<T>(entityId))
            {
                component = GetComponent<T>(entityId);
                return true;
            }

            component = default;
            return false;
        }
        public void RemoveComponent<TComponent>(int entityId) where TComponent : IComponent
        {
            if (!HasComponent<TComponent>(entityId))
                return;
                
            Type componentType = typeof(TComponent);
            _components[entityId].Remove(componentType);
            
            if (_entitiesByComponent.ContainsKey(componentType))
            {
                _entitiesByComponent[componentType].Remove(entityId);
            }
        }
        
        public int[] GetAllEntities()
        {
            return _components.Keys.ToArray();
        }
        
        public int[] GetEntitiesWith<TComponent>() where TComponent : IComponent
        {
            Type componentType = typeof(TComponent);
            
            if (!_entitiesByComponent.ContainsKey(componentType))
                return Array.Empty<int>();
                
            return _entitiesByComponent[componentType].ToArray();
        }
    }
}
