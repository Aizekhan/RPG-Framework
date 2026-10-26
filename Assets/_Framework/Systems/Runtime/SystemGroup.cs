using System;
using System.Collections.Generic;
using System.Diagnostics;
using RPGFramework.Logging;

namespace RPGFramework.Systems
{
    /// <summary>
    /// Diagnostic view of one registered system.
    /// </summary>
    public readonly struct SystemInfo
    {
        public SystemInfo(ISystem system, string name, int priority, bool isActive, bool isInitialized, bool isFaulted, int errorCount)
        {
            System = system;
            Name = name;
            Priority = priority;
            IsActive = isActive;
            IsInitialized = isInitialized;
            IsFaulted = isFaulted;
            ErrorCount = errorCount;
        }

        public ISystem System { get; }
        public string Name { get; }
        public int Priority { get; }
        public bool IsActive { get; }
        public bool IsInitialized { get; }
        public bool IsFaulted { get; }
        public int ErrorCount { get; }

        public override string ToString() =>
            $"{Name} (priority {Priority}{(IsActive ? "" : ", inactive")}{(IsFaulted ? ", faulted" : "")}, errors {ErrorCount})";
    }

    public interface ISystemRegistry : IUpdateSystem, IFixedUpdateSystem, ILateUpdateSystem
    {
        string Name { get; }
        bool IsInitialized { get; }
        IPhaseProvider PhaseProvider { get; set; }

        /// <summary>
        /// Registers a system. Higher priority runs first; equal priorities keep registration order.
        /// Systems registered after <see cref="ISystem.Initialize"/> are initialized immediately.
        /// </summary>
        /// <returns>False if this instance (or a group with the same name) is already registered.</returns>
        bool Register(ISystem system, int priority = 0);

        /// <returns>False if the system was not registered here.</returns>
        bool Unregister(ISystem system, bool dispose = true);

        bool SetActive(ISystem system, bool isActive);
        bool IsActive(ISystem system);

        /// <summary>
        /// Finds the first system of type T, searching nested groups depth-first.
        /// </summary>
        T Get<T>() where T : class;

        bool TryGet<T>(out T system) where T : class;

        IReadOnlyList<ISystem> Systems { get; }
        IReadOnlyList<SystemInfo> GetSystemInfos();
    }

    /// <summary>
    /// Ordered collection of systems that runs them as one unit.
    /// <list type="bullet">
    /// <item>Priority order (descending, stable), separate Update/FixedUpdate/LateUpdate stages.</item>
    /// <item>Exceptions are isolated per system and counted; optionally a system is disabled after
    /// <see cref="MaxConsecutiveErrors"/> failures in a row.</item>
    /// <item>A system whose Initialize throws is marked faulted and never updated.</item>
    /// <item>Systems may be registered, unregistered or toggled while the group is running;
    /// additions take effect from the next stage call, removals immediately.</item>
    /// <item><see cref="IPhaseFilteredSystem"/> children are skipped outside their phases
    /// (requires <see cref="PhaseProvider"/>; nested groups inherit it).</item>
    /// </list>
    /// </summary>
    public class SystemGroup : ISystemRegistry
    {
        private const string Category = "Systems";

        private readonly List<Entry> _entries = new List<Entry>();
        private Entry[] _snapshot = Array.Empty<Entry>();
        private bool _dirty;
        private bool _disposed;
        private IPhaseProvider _phaseProvider;
        private ISystemProfiler _profiler;

        public SystemGroup(string name, ILogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Group name is required", nameof(name));
            Name = name;
            Logger = logger;
        }

        public string Name { get; }

        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 0 (default) never disables a failing system; N disables it after N consecutive failing calls.
        /// </summary>
        public int MaxConsecutiveErrors { get; set; }

        public IPhaseProvider PhaseProvider
        {
            get => _phaseProvider;
            set
            {
                _phaseProvider = value;
                foreach (var entry in _entries)
                {
                    if (entry.System is SystemGroup child && child.PhaseProvider == null)
                        child.PhaseProvider = value;
                }
            }
        }

        /// <summary>
        /// When set, every system call is timed and reported (nested groups inherit it). Null costs nothing.
        /// </summary>
        public ISystemProfiler Profiler
        {
            get => _profiler;
            set
            {
                _profiler = value;
                foreach (var entry in _entries)
                {
                    if (entry.System is SystemGroup child && child.Profiler == null)
                        child.Profiler = value;
                }
            }
        }

        protected ILogger Logger { get; }

        public IReadOnlyList<ISystem> Systems
        {
            get
            {
                var result = new List<ISystem>(_entries.Count);
                foreach (var entry in _entries)
                    result.Add(entry.System);
                return result;
            }
        }

        // ---------------- registration ----------------

        public bool Register(ISystem system, int priority = 0)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));
            if (_disposed)
                throw new ObjectDisposedException(Name);
            if (ReferenceEquals(system, this))
                throw new ArgumentException("A group cannot contain itself", nameof(system));

            foreach (var existing in _entries)
            {
                if (ReferenceEquals(existing.System, system))
                {
                    Logger.LogWarning($"{NameOf(system)} is already registered in '{Name}', skipping", Category);
                    return false;
                }

                if (system is SystemGroup group && existing.System is SystemGroup other && other.Name == group.Name)
                {
                    Logger.LogWarning($"Group '{group.Name}' is already registered in '{Name}', skipping", Category);
                    return false;
                }
            }

            if (system is SystemGroup childGroup)
            {
                if (childGroup.PhaseProvider == null)
                    childGroup.PhaseProvider = PhaseProvider;
                if (childGroup.Profiler == null)
                    childGroup.Profiler = Profiler;
            }

            if (system is IPhaseFilteredSystem && PhaseProvider == null)
                Logger.LogWarning($"{NameOf(system)} is phase-filtered but '{Name}' has no PhaseProvider; it will run in every phase", Category);

            var entry = new Entry(system, NameOf(system), priority);
            int insertAt = _entries.Count;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Priority < priority)
                {
                    insertAt = i;
                    break;
                }
            }
            _entries.Insert(insertAt, entry);
            _dirty = true;

            Logger.LogDebug($"Registered {entry.Name} in '{Name}' with priority {priority}", Category);

            if (IsInitialized)
                InitializeEntry(entry);

            return true;
        }

        public bool Unregister(ISystem system, bool dispose = true)
        {
            int index = IndexOf(system);
            if (index < 0)
                return false;

            var entry = _entries[index];
            _entries.RemoveAt(index);
            entry.IsRemoved = true;
            _dirty = true;

            if (dispose && entry.IsInitialized)
                DisposeEntry(entry);

            Logger.LogDebug($"Unregistered {entry.Name} from '{Name}'", Category);
            return true;
        }

        public bool SetActive(ISystem system, bool isActive)
        {
            int index = IndexOf(system);
            if (index < 0)
                return false;
            _entries[index].IsActive = isActive;
            return true;
        }

        public bool IsActive(ISystem system)
        {
            int index = IndexOf(system);
            return index >= 0 && _entries[index].IsActive;
        }

        public T Get<T>() where T : class => TryGet<T>(out var system) ? system : null;

        public bool TryGet<T>(out T system) where T : class
        {
            foreach (var entry in _entries)
            {
                if (entry.System is T match)
                {
                    system = match;
                    return true;
                }
            }

            foreach (var entry in _entries)
            {
                if (entry.System is SystemGroup group && group.TryGet(out system))
                    return true;
            }

            system = null;
            return false;
        }

        public IReadOnlyList<SystemInfo> GetSystemInfos()
        {
            var result = new SystemInfo[_entries.Count];
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                result[i] = new SystemInfo(e.System, e.Name, e.Priority, e.IsActive, e.IsInitialized, e.IsFaulted, e.ErrorCount);
            }
            return result;
        }

        // ---------------- lifecycle ----------------

        public virtual void Initialize()
        {
            if (_disposed)
                throw new ObjectDisposedException(Name);
            if (IsInitialized)
                return;

            IsInitialized = true;
            foreach (var entry in GetSnapshot())
            {
                if (!entry.IsRemoved && !entry.IsInitialized && !entry.IsFaulted)
                    InitializeEntry(entry);
            }
        }

        public virtual void Update(float deltaTime)
        {
            foreach (var entry in GetSnapshot())
            {
                if (entry.System is IUpdateSystem system && CanRun(entry))
                {
                    var profiler = _profiler;
                    long start = profiler != null ? Stopwatch.GetTimestamp() : 0;
                    try
                    {
                        system.Update(deltaTime);
                        entry.ConsecutiveErrors = 0;
                    }
                    catch (Exception ex)
                    {
                        OnSystemError(entry, "Update", ex);
                    }
                    if (profiler != null)
                        Report(profiler, entry, SystemStage.Update, start);
                }
            }
        }

        public virtual void FixedUpdate(float fixedDeltaTime)
        {
            foreach (var entry in GetSnapshot())
            {
                if (entry.System is IFixedUpdateSystem system && CanRun(entry))
                {
                    var profiler = _profiler;
                    long start = profiler != null ? Stopwatch.GetTimestamp() : 0;
                    try
                    {
                        system.FixedUpdate(fixedDeltaTime);
                        entry.ConsecutiveErrors = 0;
                    }
                    catch (Exception ex)
                    {
                        OnSystemError(entry, "FixedUpdate", ex);
                    }
                    if (profiler != null)
                        Report(profiler, entry, SystemStage.FixedUpdate, start);
                }
            }
        }

        public virtual void LateUpdate(float deltaTime)
        {
            foreach (var entry in GetSnapshot())
            {
                if (entry.System is ILateUpdateSystem system && CanRun(entry))
                {
                    var profiler = _profiler;
                    long start = profiler != null ? Stopwatch.GetTimestamp() : 0;
                    try
                    {
                        system.LateUpdate(deltaTime);
                        entry.ConsecutiveErrors = 0;
                    }
                    catch (Exception ex)
                    {
                        OnSystemError(entry, "LateUpdate", ex);
                    }
                    if (profiler != null)
                        Report(profiler, entry, SystemStage.LateUpdate, start);
                }
            }
        }

        /// <summary>
        /// Disposes initialized systems in reverse execution order and empties the group.
        /// </summary>
        public virtual void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                entry.IsRemoved = true;
                if (entry.IsInitialized)
                    DisposeEntry(entry);
            }

            _entries.Clear();
            _snapshot = Array.Empty<Entry>();
            IsInitialized = false;
        }

        public override string ToString() => $"{GetType().Name} '{Name}' ({_entries.Count} systems)";

        // ---------------- internals ----------------

        private Entry[] GetSnapshot()
        {
            if (_dirty)
            {
                _snapshot = _entries.ToArray();
                _dirty = false;
            }
            return _snapshot;
        }

        private bool CanRun(Entry entry)
        {
            if (entry.IsRemoved || !entry.IsActive || !entry.IsInitialized || entry.IsFaulted)
                return false;

            if (entry.System is IPhaseFilteredSystem filtered && PhaseProvider != null)
                return filtered.IsActiveInPhase(PhaseProvider.CurrentPhaseId);

            return true;
        }

        private static void Report(ISystemProfiler profiler, Entry entry, SystemStage stage, long start)
        {
            double milliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            try
            {
                profiler.Record(entry.Name, stage, milliseconds);
            }
            catch
            {
                // A broken profiler must never affect the game loop
            }
        }

        private void InitializeEntry(Entry entry)
        {
            try
            {
                entry.System.Initialize();
                entry.IsInitialized = true;
            }
            catch (Exception ex)
            {
                entry.IsFaulted = true;
                entry.ErrorCount++;
                Logger.LogError($"{entry.Name} failed to initialize in '{Name}' and will not run", Category, ex);
            }
        }

        private void DisposeEntry(Entry entry)
        {
            entry.IsInitialized = false;
            try
            {
                entry.System.Dispose();
            }
            catch (Exception ex)
            {
                entry.ErrorCount++;
                Logger.LogError($"{entry.Name} failed to dispose in '{Name}'", Category, ex);
            }
        }

        private void OnSystemError(Entry entry, string stage, Exception ex)
        {
            entry.ErrorCount++;
            entry.ConsecutiveErrors++;
            Logger.LogError($"{entry.Name}.{stage} failed in '{Name}'", Category, ex);

            if (MaxConsecutiveErrors > 0 && entry.ConsecutiveErrors >= MaxConsecutiveErrors)
            {
                entry.IsFaulted = true;
                Logger.LogError($"{entry.Name} disabled after {entry.ConsecutiveErrors} consecutive errors", Category);
            }
        }

        private int IndexOf(ISystem system)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (ReferenceEquals(_entries[i].System, system))
                    return i;
            }
            return -1;
        }

        private static string NameOf(ISystem system) =>
            system is SystemGroup group ? $"{group.GetType().Name}:{group.Name}"
            : system is ActionSystem action ? $"ActionSystem:{action.Name}"
            : system.GetType().Name;

        private sealed class Entry
        {
            public Entry(ISystem system, string name, int priority)
            {
                System = system;
                Name = name;
                Priority = priority;
            }

            public ISystem System { get; }
            public string Name { get; }
            public int Priority { get; }
            public bool IsActive = true;
            public bool IsInitialized;
            public bool IsFaulted;
            public bool IsRemoved;
            public int ErrorCount;
            public int ConsecutiveErrors;
        }
    }

    /// <summary>
    /// A group that only runs while the current phase is one of <see cref="ActivePhaseIds"/>
    /// (no phases = every phase).
    /// </summary>
    public class PhaseSystemGroup : SystemGroup, IPhaseFilteredSystem
    {
        private readonly HashSet<string> _phaseIds = new HashSet<string>(StringComparer.Ordinal);

        public PhaseSystemGroup(string name, ILogger logger = null, params string[] activePhaseIds) : base(name, logger)
        {
            SetActivePhaseIds(activePhaseIds);
        }

        public IReadOnlyCollection<string> ActivePhaseIds => _phaseIds;

        public void SetActivePhaseIds(params string[] phaseIds)
        {
            _phaseIds.Clear();
            if (phaseIds == null)
                return;
            foreach (var id in phaseIds)
            {
                if (!string.IsNullOrEmpty(id))
                    _phaseIds.Add(id);
            }
        }

        public bool IsActiveInPhase(string phaseId) => _phaseIds.Count == 0 || (phaseId != null && _phaseIds.Contains(phaseId));

        public override string ToString() => $"{base.ToString()} phases: [{string.Join(", ", _phaseIds)}]";
    }

    /// <summary>
    /// Root group of a game or world.
    /// </summary>
    public sealed class SystemRegistry : SystemGroup
    {
        public SystemRegistry(ILogger logger = null, IPhaseProvider phaseProvider = null) : base("Root", logger)
        {
            PhaseProvider = phaseProvider;
        }
    }
}
