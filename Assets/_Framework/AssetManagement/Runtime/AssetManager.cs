using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.Logging;

namespace RPGFramework.AssetManagement
{
    /// <summary>
    /// A source of assets (Resources folder, Addressables, asset bundles, mod folders, test fakes).
    /// </summary>
    public interface IAssetProvider
    {
        string Name { get; }

        /// <summary>
        /// Higher priority providers are asked first.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Loads the asset, or returns null if this provider does not have it.
        /// </summary>
        UniTask<T> LoadAsync<T>(string key, CancellationToken cancellationToken) where T : class;

        /// <summary>
        /// Called once the last handle to an asset loaded by this provider is released.
        /// </summary>
        void Release(string key, object asset);
    }

    public sealed class AssetNotFoundException : Exception
    {
        public AssetNotFoundException(string key, Type type, string details)
            : base($"Asset '{key}' ({type.Name}) was not found by any provider{(string.IsNullOrEmpty(details) ? "" : ": " + details)}")
        {
            Key = key;
        }

        public string Key { get; }
    }

    /// <summary>
    /// Reference-counted access to a loaded asset. Dispose exactly once when done with the asset;
    /// the asset is unloaded when its last handle is disposed.
    /// </summary>
    public sealed class AssetHandle<T> : IDisposable where T : class
    {
        private Action _release;

        internal AssetHandle(string key, T asset, Action release)
        {
            Key = key;
            Asset = asset;
            _release = release;
        }

        public string Key { get; }
        public T Asset { get; }
        public bool IsReleased => _release == null;

        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }

    public interface IAssetManager
    {
        IReadOnlyList<IAssetProvider> Providers { get; }

        void AddProvider(IAssetProvider provider);

        /// <summary>
        /// Loads (or reuses) an asset and returns a handle that keeps it loaded.
        /// Concurrent requests for the same key share one provider load.
        /// </summary>
        /// <exception cref="AssetNotFoundException">No provider has the asset.</exception>
        UniTask<AssetHandle<T>> LoadAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Loads several assets; on failure or cancellation every handle obtained so far is released.
        /// </summary>
        UniTask<AssetHandleGroup> PreloadAsync<T>(IEnumerable<string> keys, IProgress<float> progress = null,
            CancellationToken cancellationToken = default) where T : class;

        bool IsLoaded(string key);
        int GetReferenceCount(string key);
        IReadOnlyCollection<string> LoadedKeys { get; }

        /// <summary>
        /// Unloads every asset regardless of outstanding handles (e.g. on full teardown). Outstanding handles
        /// become no-ops.
        /// </summary>
        void UnloadAll();
    }

    /// <summary>
    /// Several handles released together (e.g. everything a level preloaded).
    /// </summary>
    public sealed class AssetHandleGroup : IDisposable
    {
        private readonly List<IDisposable> _handles = new List<IDisposable>();
        private bool _disposed;

        public int Count => _handles.Count;

        /// <summary>
        /// Adds a handle; if the group was already disposed the handle is released immediately.
        /// </summary>
        public void Add(IDisposable handle)
        {
            if (handle == null)
                return;
            if (_disposed)
                handle.Dispose();
            else
                _handles.Add(handle);
        }

        public void Dispose()
        {
            _disposed = true;
            for (int i = _handles.Count - 1; i >= 0; i--)
                _handles[i].Dispose();
            _handles.Clear();
        }
    }

    /// <summary>
    /// Default <see cref="IAssetManager"/>: priority-ordered providers with fallback, per-key reference counting,
    /// de-duplication of in-flight loads, and release through the provider that loaded the asset.
    /// Intended for the main thread (async continuations resume on the caller's context).
    /// </summary>
    public sealed class AssetManager : IAssetManager
    {
        private const string Category = "Assets";

        private readonly List<IAssetProvider> _providers = new List<IAssetProvider>();
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly ILogger _logger;
        private int _generation;

        public AssetManager(ILogger logger = null, params IAssetProvider[] providers)
        {
            _logger = logger;
            if (providers != null)
            {
                foreach (var provider in providers)
                    AddProvider(provider);
            }
        }

        public IReadOnlyList<IAssetProvider> Providers => _providers;

        public IReadOnlyCollection<string> LoadedKeys
        {
            get
            {
                var keys = new List<string>();
                foreach (var pair in _entries)
                {
                    if (pair.Value.IsLoaded)
                        keys.Add(pair.Key);
                }
                return keys;
            }
        }

        public void AddProvider(IAssetProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            if (_providers.Contains(provider))
                return;

            // Stable insert: higher priority first, equal priorities keep registration order
            int index = _providers.FindIndex(p => p.Priority < provider.Priority);
            _providers.Insert(index < 0 ? _providers.Count : index, provider);
        }

        public async UniTask<AssetHandle<T>> LoadAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Asset key is required", nameof(key));
            cancellationToken.ThrowIfCancellationRequested();

            // Reserve the reference before starting/awaiting the load: a provider may complete synchronously,
            // and an in-flight load must not be released under us
            if (_entries.TryGetValue(key, out var entry))
            {
                entry.RefCount++;
            }
            else
            {
                entry = new Entry(_generation) { RefCount = 1 };
                _entries.Add(key, entry);
                entry.Loading = LoadFromProvidersAsync<T>(key, entry).Preserve();
            }

            object asset;
            try
            {
                asset = await entry.Loading.AttachExternalCancellation(cancellationToken);
            }
            catch
            {
                ReleaseReference(key, entry);
                throw;
            }

            if (!(asset is T typed))
            {
                ReleaseReference(key, entry);
                throw new InvalidCastException($"Asset '{key}' is {asset.GetType().Name}, not {typeof(T).Name}");
            }

            return new AssetHandle<T>(key, typed, () => ReleaseReference(key, entry));
        }

        public async UniTask<AssetHandleGroup> PreloadAsync<T>(IEnumerable<string> keys, IProgress<float> progress = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            var list = new List<string>(keys);
            var group = new AssetHandleGroup();
            try
            {
                var loads = new UniTask<AssetHandle<T>>[list.Count];
                int completed = 0;
                progress?.Report(list.Count == 0 ? 1f : 0f);
                for (int i = 0; i < list.Count; i++)
                    loads[i] = LoadAndReport(list[i]);

                await UniTask.WhenAll(loads);
                return group;

                async UniTask<AssetHandle<T>> LoadAndReport(string key)
                {
                    var handle = await LoadAsync<T>(key, cancellationToken);
                    // Track handles as they arrive so a later failure can release them
                    group.Add(handle);
                    completed++;
                    progress?.Report((float)completed / list.Count);
                    return handle;
                }
            }
            catch
            {
                group.Dispose();
                throw;
            }
        }

        public bool IsLoaded(string key) => key != null && _entries.TryGetValue(key, out var entry) && entry.IsLoaded;

        public int GetReferenceCount(string key) => key != null && _entries.TryGetValue(key, out var entry) ? entry.RefCount : 0;

        public void UnloadAll()
        {
            _generation++;
            foreach (var pair in _entries)
            {
                if (pair.Value.IsLoaded)
                    SafeRelease(pair.Key, pair.Value);
            }
            _entries.Clear();
        }

        private async UniTask<object> LoadFromProvidersAsync<T>(string key, Entry entry) where T : class
        {
            var errors = new List<string>();
            foreach (var provider in _providers.ToArray())
            {
                T asset;
                try
                {
                    asset = await provider.LoadAsync<T>(key, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errors.Add($"{provider.Name}: {ex.Message}");
                    _logger.LogWarning($"Provider {provider.Name} failed to load '{key}'", Category, ex);
                    continue;
                }

                if (asset != null)
                {
                    entry.Asset = asset;
                    entry.Provider = provider;
                    _logger.LogDebug($"Loaded '{key}' from {provider.Name}", Category);

                    if (entry.Generation != _generation)
                    {
                        // UnloadAll ran while loading: hand the asset out but do not keep it registered
                        SafeRelease(key, entry);
                    }
                    else if (entry.RefCount == 0 && _entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
                    {
                        // Every requester gave up while we were loading
                        _entries.Remove(key);
                        SafeRelease(key, entry);
                    }
                    return asset;
                }
            }

            if (_entries.TryGetValue(key, out var failed) && ReferenceEquals(failed, entry))
                _entries.Remove(key);

            _logger.LogWarning($"Asset '{key}' not found ({_providers.Count} providers)", Category);
            throw new AssetNotFoundException(key, typeof(T), string.Join("; ", errors));
        }

        private void ReleaseReference(string key, Entry entry)
        {
            if (entry.Generation != _generation || entry.RefCount <= 0)
                return; // unloaded by UnloadAll in the meantime

            entry.RefCount--;
            if (entry.RefCount > 0 || !entry.IsLoaded)
                return; // still in use, or still loading (the loader releases it when done)

            if (_entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
                _entries.Remove(key);
            SafeRelease(key, entry);
        }

        private void SafeRelease(string key, Entry entry)
        {
            var asset = entry.Asset;
            var provider = entry.Provider;
            entry.Asset = null;
            entry.Provider = null;
            if (provider == null)
                return;

            try
            {
                provider.Release(key, asset);
                _logger.LogDebug($"Released '{key}'", Category);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Provider {provider.Name} failed to release '{key}'", Category, ex);
            }
        }

        private sealed class Entry
        {
            public Entry(int generation)
            {
                Generation = generation;
            }

            public int Generation { get; }
            public UniTask<object> Loading;
            public object Asset;
            public IAssetProvider Provider;
            public int RefCount;
            public bool IsLoaded => Provider != null;
        }
    }
}
