using System;
using System.Collections.Generic;

namespace RPGFramework.ECS
{
    /// <summary>
    /// Default <see cref="IEntityManager"/>. Components live in one dense, typed pool per component type,
    /// so struct components are never boxed and can be mutated in place via <see cref="GetComponentRef{TComponent}"/>.
    /// Queries return copies, so entities may be created/destroyed while iterating a result.
    /// </summary>
    public class EntityManager : IEntityManager
    {
        private readonly HashSet<int> _entities = new HashSet<int>();
        private readonly Dictionary<Type, ComponentPool> _pools = new Dictionary<Type, ComponentPool>();
        private int _nextEntityId = 1;

        public event Action<int> EntityCreated;
        public event Action<int> EntityDestroyed;
        public event Action<int, Type> ComponentAdded;
        public event Action<int, Type> ComponentRemoved;

        public int EntityCount => _entities.Count;

        public int CreateEntity()
        {
            int entityId = _nextEntityId++;
            _entities.Add(entityId);
            EntityCreated?.Invoke(entityId);
            return entityId;
        }

        public void DestroyEntity(int entityId)
        {
            if (!_entities.Contains(entityId))
                return;

            EntityDestroyed?.Invoke(entityId);

            foreach (var pool in _pools.Values)
            {
                if (pool.Remove(entityId))
                    ComponentRemoved?.Invoke(entityId, pool.ComponentType);
            }

            _entities.Remove(entityId);
        }

        public bool Exists(int entityId) => _entities.Contains(entityId);

        public void AddComponent<TComponent>(int entityId, TComponent component) where TComponent : IComponent
        {
            if (!_entities.Contains(entityId))
            {
                throw new ArgumentException(
                    $"Entity ID {entityId} has not been created or has already been destroyed.",
                    nameof(entityId));
            }

            if (GetOrCreatePool<TComponent>().Set(entityId, component))
                ComponentAdded?.Invoke(entityId, typeof(TComponent));
        }

        public bool HasComponent<TComponent>(int entityId) where TComponent : IComponent =>
            TryGetPool<TComponent>(out var pool) && pool.Contains(entityId);

        public TComponent GetComponent<TComponent>(int entityId) where TComponent : IComponent =>
            TryGetPool<TComponent>(out var pool) && pool.TryGet(entityId, out var component) ? component : default;

        public bool TryGetComponent<TComponent>(int entityId, out TComponent component) where TComponent : struct, IComponent
        {
            if (TryGetPool<TComponent>(out var pool))
                return pool.TryGet(entityId, out component);

            component = default;
            return false;
        }

        public ref TComponent GetComponentRef<TComponent>(int entityId) where TComponent : struct, IComponent
        {
            if (!TryGetPool<TComponent>(out var pool) || !pool.Contains(entityId))
                throw new KeyNotFoundException($"Entity {entityId} has no {typeof(TComponent).Name}");
            return ref pool.GetRef(entityId);
        }

        public void RemoveComponent<TComponent>(int entityId) where TComponent : IComponent
        {
            if (TryGetPool<TComponent>(out var pool) && pool.Remove(entityId))
                ComponentRemoved?.Invoke(entityId, typeof(TComponent));
        }

        public int[] GetAllEntities()
        {
            var result = new int[_entities.Count];
            _entities.CopyTo(result);
            return result;
        }

        public int[] GetEntitiesWith<TComponent>() where TComponent : IComponent =>
            TryGetPool<TComponent>(out var pool) ? pool.CopyEntities() : Array.Empty<int>();

        public int[] GetEntitiesWith<T1, T2>()
            where T1 : IComponent
            where T2 : IComponent =>
            Intersect(typeof(T1), typeof(T2));

        public int[] GetEntitiesWith<T1, T2, T3>()
            where T1 : IComponent
            where T2 : IComponent
            where T3 : IComponent =>
            Intersect(typeof(T1), typeof(T2), typeof(T3));

        public void GetEntitiesWith<TComponent>(List<int> results) where TComponent : IComponent
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            results.Clear();
            if (TryGetPool<TComponent>(out var pool))
                pool.CopyEntities(results);
        }

        public void GetEntitiesWith<T1, T2>(List<int> results)
            where T1 : IComponent
            where T2 : IComponent
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            results.Clear();
            if (!TryGetPool<T1>(out var first) || !TryGetPool<T2>(out var second))
                return;
            var smallest = first.Count <= second.Count ? (ComponentPool)first : second;
            smallest.CopyEntities(results);
            KeepWhereAll(results, first, second, null);
        }

        public void GetEntitiesWith<T1, T2, T3>(List<int> results)
            where T1 : IComponent
            where T2 : IComponent
            where T3 : IComponent
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            results.Clear();
            if (!TryGetPool<T1>(out var first) || !TryGetPool<T2>(out var second) || !TryGetPool<T3>(out var third))
                return;
            ComponentPool smallest = first;
            if (second.Count < smallest.Count)
                smallest = second;
            if (third.Count < smallest.Count)
                smallest = third;
            smallest.CopyEntities(results);
            KeepWhereAll(results, first, second, third);
        }

        // Filters in place (no closure, no copy): keeps the entities every given pool contains
        private static void KeepWhereAll(List<int> results, ComponentPool a, ComponentPool b, ComponentPool c)
        {
            int kept = 0;
            for (int i = 0; i < results.Count; i++)
            {
                int entityId = results[i];
                if (a.Contains(entityId) && b.Contains(entityId) && (c == null || c.Contains(entityId)))
                    results[kept++] = entityId;
            }
            results.RemoveRange(kept, results.Count - kept);
        }

        public int CountWith<TComponent>() where TComponent : IComponent =>
            TryGetPool<TComponent>(out var pool) ? pool.Count : 0;

        public void GetComponentTypes(int entityId, List<Type> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            results.Clear();
            foreach (var pool in _pools.Values)
            {
                if (pool.Contains(entityId))
                    results.Add(pool.ComponentType);
            }
        }

        private int[] Intersect(params Type[] types)
        {
            // Iterate the smallest pool and probe the others
            ComponentPool smallest = null;
            var pools = new ComponentPool[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                if (!_pools.TryGetValue(types[i], out pools[i]))
                    return Array.Empty<int>();
                if (smallest == null || pools[i].Count < smallest.Count)
                    smallest = pools[i];
            }

            var result = new List<int>(smallest.Count);
            var candidates = smallest.CopyEntities();
            foreach (int entityId in candidates)
            {
                bool all = true;
                foreach (var pool in pools)
                {
                    if (!ReferenceEquals(pool, smallest) && !pool.Contains(entityId))
                    {
                        all = false;
                        break;
                    }
                }
                if (all)
                    result.Add(entityId);
            }
            return result.ToArray();
        }

        private ComponentPool<TComponent> GetOrCreatePool<TComponent>()
        {
            if (!_pools.TryGetValue(typeof(TComponent), out var pool))
            {
                pool = new ComponentPool<TComponent>();
                _pools.Add(typeof(TComponent), pool);
            }
            return (ComponentPool<TComponent>)pool;
        }

        private bool TryGetPool<TComponent>(out ComponentPool<TComponent> pool)
        {
            if (_pools.TryGetValue(typeof(TComponent), out var found))
            {
                pool = (ComponentPool<TComponent>)found;
                return true;
            }
            pool = null;
            return false;
        }

        private abstract class ComponentPool
        {
            public abstract Type ComponentType { get; }
            public abstract int Count { get; }
            public abstract bool Contains(int entityId);
            public abstract bool Remove(int entityId);
            public abstract int[] CopyEntities();
            public abstract void CopyEntities(List<int> results);
        }

        /// <summary>
        /// Dense arrays (entity ids + values) with an entity→index map; removal swaps the last element in.
        /// </summary>
        private sealed class ComponentPool<TComponent> : ComponentPool
        {
            private readonly Dictionary<int, int> _indexByEntity = new Dictionary<int, int>();
            private int[] _entities = new int[16];
            private TComponent[] _values = new TComponent[16];
            private int _count;

            public override Type ComponentType => typeof(TComponent);
            public override int Count => _count;

            public override bool Contains(int entityId) => _indexByEntity.ContainsKey(entityId);

            /// <returns>True if the component was added, false if an existing value was replaced.</returns>
            public bool Set(int entityId, TComponent value)
            {
                if (_indexByEntity.TryGetValue(entityId, out int index))
                {
                    _values[index] = value;
                    return false;
                }

                if (_count == _entities.Length)
                {
                    Array.Resize(ref _entities, _count * 2);
                    Array.Resize(ref _values, _count * 2);
                }

                _entities[_count] = entityId;
                _values[_count] = value;
                _indexByEntity[entityId] = _count;
                _count++;
                return true;
            }

            public bool TryGet(int entityId, out TComponent value)
            {
                if (_indexByEntity.TryGetValue(entityId, out int index))
                {
                    value = _values[index];
                    return true;
                }
                value = default;
                return false;
            }

            public ref TComponent GetRef(int entityId) => ref _values[_indexByEntity[entityId]];

            public override bool Remove(int entityId)
            {
                if (!_indexByEntity.TryGetValue(entityId, out int index))
                    return false;

                int last = _count - 1;
                if (index != last)
                {
                    _entities[index] = _entities[last];
                    _values[index] = _values[last];
                    _indexByEntity[_entities[index]] = index;
                }

                _values[last] = default;
                _indexByEntity.Remove(entityId);
                _count--;
                return true;
            }

            public override int[] CopyEntities()
            {
                var result = new int[_count];
                Array.Copy(_entities, result, _count);
                return result;
            }

            public override void CopyEntities(List<int> results)
            {
                for (int i = 0; i < _count; i++)
                    results.Add(_entities[i]);
            }
        }
    }
}
