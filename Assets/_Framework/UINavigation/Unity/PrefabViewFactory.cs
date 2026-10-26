using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.AssetManagement;
using RPGFramework.Logging;
using UnityEngine;
using ILogger = RPGFramework.Logging.ILogger;
using Object = UnityEngine.Object;

namespace RPGFramework.UINavigation.Unity
{
    /// <summary>
    /// <see cref="IViewFactory"/> that instantiates prefabs described by <see cref="ViewConfig"/>s.
    /// Views with <see cref="ViewConfig.KeepInstance"/> are hidden and reused instead of destroyed; prefabs loaded
    /// by asset key stay referenced while any instance of them exists.
    /// </summary>
    public sealed class PrefabViewFactory : IViewFactory, IDisposable
    {
        private const string Category = "Navigation";

        private readonly Dictionary<string, ViewConfig> _configs = new Dictionary<string, ViewConfig>(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> _layers = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Stack<INavigableView>> _idle = new Dictionary<string, Stack<INavigableView>>(StringComparer.Ordinal);
        private readonly Dictionary<INavigableView, AssetHandle<GameObject>> _handles = new Dictionary<INavigableView, AssetHandle<GameObject>>();
        private readonly Transform _defaultParent;
        private readonly IAssetManager _assets;
        private readonly ILogger _logger;

        public PrefabViewFactory(Transform defaultParent, IAssetManager assets = null, ILogger logger = null)
        {
            _defaultParent = defaultParent;
            _assets = assets;
            _logger = logger;
        }

        /// <summary>
        /// Called for every new instance before its view is returned (e.g. to inject dependencies).
        /// </summary>
        public Action<GameObject> InstanceCreated { get; set; }

        public PrefabViewFactory Register(ViewConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrEmpty(config.ViewId))
                throw new ArgumentException($"View config '{config.name}' has no view id", nameof(config));
            if (config.Prefab == null && string.IsNullOrEmpty(config.AssetKey))
                throw new ArgumentException($"View config '{config.ViewId}' needs a prefab or an asset key", nameof(config));
            if (config.Prefab == null && _assets == null)
                throw new ArgumentException($"View config '{config.ViewId}' uses an asset key but the factory has no asset manager", nameof(config));
            if (_configs.ContainsKey(config.ViewId))
                throw new InvalidOperationException($"View '{config.ViewId}' is already registered");
            _configs.Add(config.ViewId, config);
            return this;
        }

        public PrefabViewFactory RegisterAll(IEnumerable<ViewConfig> configs)
        {
            foreach (var config in configs)
                Register(config);
            return this;
        }

        public PrefabViewFactory SetLayer(string layer, Transform parent)
        {
            if (string.IsNullOrEmpty(layer))
                throw new ArgumentException("Layer name is required", nameof(layer));
            _layers[layer] = parent;
            return this;
        }

        public bool IsRegistered(string viewId) => viewId != null && _configs.ContainsKey(viewId);

        public int IdleCount(string viewId) => viewId != null && _idle.TryGetValue(viewId, out var idle) ? idle.Count : 0;

        public async UniTask<INavigableView> CreateAsync(string viewId, CancellationToken cancellationToken)
        {
            if (!_configs.TryGetValue(viewId, out var config))
                throw new KeyNotFoundException($"No view config registered for '{viewId}'");

            if (_idle.TryGetValue(viewId, out var idle))
            {
                while (idle.Count > 0)
                {
                    var cached = idle.Pop();
                    if (cached is Object unityObject && unityObject != null)
                        return cached;
                    ReleaseHandle(cached);
                }
            }

            AssetHandle<GameObject> handle = null;
            var prefab = config.Prefab;
            if (prefab == null)
            {
                handle = await _assets.LoadAsync<GameObject>(config.AssetKey, cancellationToken);
                prefab = handle.Asset;
            }

            var instance = Object.Instantiate(prefab, ParentFor(config), false);
            instance.name = prefab.name;
            if (!instance.TryGetComponent(out INavigableView view))
            {
                DestroyObject(instance);
                handle?.Dispose();
                throw new InvalidOperationException($"Prefab '{prefab.name}' for view '{viewId}' has no INavigableView component");
            }

            view.Hide();
            if (handle != null)
                _handles[view] = handle;
            InstanceCreated?.Invoke(instance);
            return view;
        }

        public void Release(string viewId, INavigableView view)
        {
            if (view == null)
                return;
            if (!(view is Component component) || component == null)
            {
                ReleaseHandle(view);
                return;
            }

            if (viewId != null && _configs.TryGetValue(viewId, out var config) && config.KeepInstance)
            {
                view.Hide();
                if (!_idle.TryGetValue(viewId, out var idle))
                {
                    idle = new Stack<INavigableView>();
                    _idle.Add(viewId, idle);
                }
                if (!idle.Contains(view))
                    idle.Push(view);
                return;
            }

            DestroyObject(component.gameObject);
            ReleaseHandle(view);
        }

        /// <summary>
        /// Creates and parks one hidden instance for each cached view id, so the first navigation does not hitch.
        /// </summary>
        public async UniTask PreloadAsync(IEnumerable<string> viewIds, CancellationToken cancellationToken = default)
        {
            foreach (var viewId in viewIds)
            {
                if (!_configs.TryGetValue(viewId, out var config) || !config.KeepInstance || IdleCount(viewId) > 0)
                    continue;
                var view = await CreateAsync(viewId, cancellationToken);
                Release(viewId, view);
            }
        }

        /// <summary>
        /// Destroys every parked instance. Instances currently on screen are not touched.
        /// </summary>
        public void ClearCache()
        {
            foreach (var idle in _idle.Values)
            {
                foreach (var view in idle)
                {
                    if (view is Component component && component != null)
                        DestroyObject(component.gameObject);
                    ReleaseHandle(view);
                }
            }
            _idle.Clear();
        }

        public void Dispose()
        {
            ClearCache();
            foreach (var handle in _handles.Values)
                handle.Dispose();
            _handles.Clear();
        }

        private Transform ParentFor(ViewConfig config)
        {
            if (!string.IsNullOrEmpty(config.Layer))
            {
                if (_layers.TryGetValue(config.Layer, out var parent) && parent != null)
                    return parent;
                _logger.LogWarning($"Layer '{config.Layer}' for view '{config.ViewId}' is not set; using the default parent", Category);
            }
            return _defaultParent;
        }

        private void ReleaseHandle(INavigableView view)
        {
            if (_handles.TryGetValue(view, out var handle))
            {
                _handles.Remove(view);
                handle.Dispose();
            }
        }

        private static void DestroyObject(GameObject gameObject)
        {
            if (Application.isPlaying)
                Object.Destroy(gameObject);
            else
                Object.DestroyImmediate(gameObject);
        }
    }
}
