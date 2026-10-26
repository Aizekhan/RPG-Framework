using System;
using System.Collections.Generic;

namespace RPGFramework.ECS
{
    /// <summary>
    /// Marks which archetype an entity was spawned from (or detected as).
    /// Stored as a component, so it disappears with the entity and is queryable like any other data.
    /// </summary>
    public struct ArchetypeTag : IComponent
    {
        public string ArchetypeId;

        public ArchetypeTag(string archetypeId)
        {
            ArchetypeId = archetypeId;
        }
    }

    /// <summary>
    /// Immutable entity template: the components (with initial values) an entity of this kind starts with,
    /// optionally extending a parent archetype, plus requirements used to recognise existing entities.
    /// Build with <see cref="ArchetypeBuilder"/>.
    /// </summary>
    public sealed class EntityArchetype
    {
        private readonly Action<IEntityManager, int>[] _initializers;
        private readonly Func<IEntityManager, int, bool>[] _requirements;
        private readonly Type[] _componentTypes;

        internal EntityArchetype(
            string id,
            EntityArchetype parent,
            List<Type> componentTypes,
            List<Action<IEntityManager, int>> initializers,
            List<Func<IEntityManager, int, bool>> requirements)
        {
            Id = id;
            Parent = parent;

            var types = new List<Type>();
            if (parent != null)
                types.AddRange(parent._componentTypes);
            foreach (var type in componentTypes)
            {
                if (!types.Contains(type))
                    types.Add(type);
            }

            _componentTypes = types.ToArray();
            _initializers = initializers.ToArray();
            _requirements = requirements.ToArray();
        }

        public string Id { get; }

        public EntityArchetype Parent { get; }

        /// <summary>
        /// Component types an entity of this archetype has (own and inherited).
        /// </summary>
        public IReadOnlyList<Type> ComponentTypes => _componentTypes;

        /// <summary>
        /// Creates an entity with this archetype's components and an <see cref="ArchetypeTag"/>.
        /// </summary>
        public int Create(IEntityManager entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            int entityId = entities.CreateEntity();
            Apply(entities, entityId);
            entities.AddComponent(entityId, new ArchetypeTag(Id));
            return entityId;
        }

        /// <summary>
        /// Adds (or overwrites) this archetype's components on an existing entity; parent values first.
        /// </summary>
        public void Apply(IEntityManager entities, int entityId)
        {
            Parent?.Apply(entities, entityId);
            foreach (var initializer in _initializers)
                initializer(entities, entityId);
        }

        /// <summary>
        /// True if the entity has every component of this archetype and satisfies all its requirements
        /// (including the parent's).
        /// </summary>
        public bool Matches(IEntityManager entities, int entityId)
        {
            if (!entities.Exists(entityId))
                return false;
            if (Parent != null && !Parent.Matches(entities, entityId))
                return false;
            foreach (var requirement in _requirements)
            {
                if (!requirement(entities, entityId))
                    return false;
            }
            return true;
        }

        public bool IsOrExtends(string archetypeId)
        {
            for (var current = this; current != null; current = current.Parent)
            {
                if (current.Id == archetypeId)
                    return true;
            }
            return false;
        }

        public override string ToString() => Parent == null ? Id : $"{Id} : {Parent.Id}";
    }

    public sealed class ArchetypeBuilder
    {
        private readonly string _id;
        private readonly List<Type> _componentTypes = new List<Type>();
        private readonly List<Action<IEntityManager, int>> _initializers = new List<Action<IEntityManager, int>>();
        private readonly List<Func<IEntityManager, int, bool>> _requirements = new List<Func<IEntityManager, int, bool>>();
        private EntityArchetype _parent;

        public ArchetypeBuilder(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Archetype id is required", nameof(id));
            _id = id;
        }

        public ArchetypeBuilder Extends(EntityArchetype parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            return this;
        }

        /// <summary>
        /// Adds a component with an initial value; entities matching the archetype must have it.
        /// </summary>
        public ArchetypeBuilder With<TComponent>(TComponent initialValue = default) where TComponent : struct, IComponent
        {
            return With(() => initialValue);
        }

        /// <summary>
        /// Adds a component whose initial value is produced per entity (e.g. randomised stats).
        /// </summary>
        public ArchetypeBuilder With<TComponent>(Func<TComponent> factory) where TComponent : struct, IComponent
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            if (!_componentTypes.Contains(typeof(TComponent)))
            {
                _componentTypes.Add(typeof(TComponent));
                _requirements.Add((entities, id) => entities.HasComponent<TComponent>(id));
            }
            _initializers.Add((entities, id) => entities.AddComponent(id, factory()));
            return this;
        }

        /// <summary>
        /// Recognition-only rule: matching entities must have the component and satisfy the predicate.
        /// Nothing is added on <see cref="EntityArchetype.Create"/>.
        /// </summary>
        public ArchetypeBuilder Require<TComponent>(Func<TComponent, bool> predicate = null) where TComponent : struct, IComponent
        {
            _requirements.Add((entities, id) =>
                entities.TryGetComponent<TComponent>(id, out var component) && (predicate == null || predicate(component)));
            return this;
        }

        public EntityArchetype Build() => new EntityArchetype(_id, _parent, _componentTypes, _initializers, _requirements);
    }

    /// <summary>
    /// Catalogue of archetypes by id: spawning, lookup and recognition of existing entities.
    /// Definitions are game content; the catalogue holds no per-world state, so one instance can serve several worlds.
    /// </summary>
    public sealed class ArchetypeCatalog
    {
        private readonly Dictionary<string, EntityArchetype> _archetypes = new Dictionary<string, EntityArchetype>(StringComparer.Ordinal);

        public IEnumerable<EntityArchetype> All => _archetypes.Values;

        public int Count => _archetypes.Count;

        public EntityArchetype Register(EntityArchetype archetype)
        {
            if (archetype == null)
                throw new ArgumentNullException(nameof(archetype));
            if (_archetypes.ContainsKey(archetype.Id))
                throw new InvalidOperationException($"Archetype '{archetype.Id}' is already registered");
            _archetypes.Add(archetype.Id, archetype);
            return archetype;
        }

        /// <summary>
        /// Builds and registers an archetype in one step.
        /// </summary>
        public EntityArchetype Define(string id, Action<ArchetypeBuilder> configure)
        {
            var builder = new ArchetypeBuilder(id);
            configure?.Invoke(builder);
            return Register(builder.Build());
        }

        public bool TryGet(string id, out EntityArchetype archetype) => _archetypes.TryGetValue(id, out archetype);

        public EntityArchetype Get(string id) =>
            _archetypes.TryGetValue(id, out var archetype)
                ? archetype
                : throw new KeyNotFoundException($"Archetype '{id}' is not registered");

        /// <summary>
        /// Creates an entity from the archetype, then lets <paramref name="customize"/> override components.
        /// </summary>
        public int Spawn(string id, IEntityManager entities, Action<int> customize = null)
        {
            int entityId = Get(id).Create(entities);
            customize?.Invoke(entityId);
            return entityId;
        }

        /// <summary>
        /// Archetype id stored on the entity, or null.
        /// </summary>
        public static string GetArchetypeId(IEntityManager entities, int entityId) =>
            entities.TryGetComponent<ArchetypeTag>(entityId, out var tag) ? tag.ArchetypeId : null;

        /// <summary>
        /// Finds the most specific matching archetype (deepest inheritance, then most components) and, when
        /// <paramref name="tag"/> is true, stores it as the entity's <see cref="ArchetypeTag"/>.
        /// </summary>
        public EntityArchetype Detect(IEntityManager entities, int entityId, bool tag = true)
        {
            EntityArchetype best = null;
            int bestDepth = -1;
            foreach (var archetype in _archetypes.Values)
            {
                if (!archetype.Matches(entities, entityId))
                    continue;

                int depth = Depth(archetype);
                if (best == null || depth > bestDepth ||
                    (depth == bestDepth && archetype.ComponentTypes.Count > best.ComponentTypes.Count))
                {
                    best = archetype;
                    bestDepth = depth;
                }
            }

            if (best != null && tag)
                entities.AddComponent(entityId, new ArchetypeTag(best.Id));
            return best;
        }

        /// <summary>
        /// Entities tagged with the archetype (or, with <paramref name="includeDerived"/>, any archetype extending it).
        /// </summary>
        public List<int> GetEntities(IEntityManager entities, string archetypeId, bool includeDerived = false)
        {
            var result = new List<int>();
            foreach (int entityId in entities.GetEntitiesWith<ArchetypeTag>())
            {
                string id = entities.GetComponent<ArchetypeTag>(entityId).ArchetypeId;
                if (id == archetypeId ||
                    (includeDerived && id != null && _archetypes.TryGetValue(id, out var archetype) && archetype.IsOrExtends(archetypeId)))
                {
                    result.Add(entityId);
                }
            }
            return result;
        }

        private static int Depth(EntityArchetype archetype)
        {
            int depth = 0;
            for (var current = archetype.Parent; current != null; current = current.Parent)
                depth++;
            return depth;
        }
    }
}
