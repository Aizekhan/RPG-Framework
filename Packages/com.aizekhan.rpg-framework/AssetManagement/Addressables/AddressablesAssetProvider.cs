using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

namespace RPGFramework.AssetManagement.Addressables
{
    /// <summary>
    /// Loads assets through Unity Addressables (keys are addresses or labels). A key with no Addressables location
    /// returns null, so lower-priority providers (e.g. Resources) are asked next. Each loaded key keeps its
    /// Addressables handle until <see cref="IAssetManager"/> releases the last reference.
    /// Compiled only when the com.unity.addressables package is installed.
    /// </summary>
    public sealed class AddressablesAssetProvider : IAssetProvider
    {
        private readonly Dictionary<string, AsyncOperationHandle> _handles = new Dictionary<string, AsyncOperationHandle>(StringComparer.Ordinal);

        public AddressablesAssetProvider(int priority = 100)
        {
            Priority = priority;
        }

        public string Name => "Addressables";

        public int Priority { get; }

        /// <summary>
        /// Keys currently holding an Addressables handle.
        /// </summary>
        public int LoadedCount => _handles.Count;

        public async UniTask<T> LoadAsync<T>(string key, CancellationToken cancellationToken) where T : class
        {
            // Components live on prefabs (GameObject locations); other non-Object types are matched by any location
            var type = typeof(T);
            var filter = typeof(Object).IsAssignableFrom(type) && !typeof(Component).IsAssignableFrom(type) ? type : null;

            var locationsHandle = UnityEngine.AddressableAssets.Addressables.LoadResourceLocationsAsync(key, filter);
            IResourceLocation location;
            try
            {
                var locations = await locationsHandle.ToUniTask(cancellationToken: cancellationToken);
                location = locations != null && locations.Count > 0 ? locations[0] : null;
            }
            finally
            {
                if (locationsHandle.IsValid())
                    UnityEngine.AddressableAssets.Addressables.Release(locationsHandle);
            }

            if (location == null)
                return null;

            var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Object>(location);
            Object asset;
            try
            {
                asset = await handle.ToUniTask(cancellationToken: cancellationToken, autoReleaseWhenCanceled: true);
            }
            catch
            {
                if (handle.IsValid() && handle.Status == AsyncOperationStatus.Failed)
                    UnityEngine.AddressableAssets.Addressables.Release(handle);
                throw;
            }

            if (!(asset is T typed))
            {
                // Present but of another type (e.g. a texture asked for as an AudioClip): not ours to keep
                UnityEngine.AddressableAssets.Addressables.Release(handle);
                return null;
            }

            if (_handles.TryGetValue(key, out var previous) && previous.IsValid())
                UnityEngine.AddressableAssets.Addressables.Release(previous);
            _handles[key] = handle;
            return typed;
        }

        public void Release(string key, object asset)
        {
            if (key == null || !_handles.TryGetValue(key, out var handle))
                return;
            _handles.Remove(key);
            if (handle.IsValid())
                UnityEngine.AddressableAssets.Addressables.Release(handle);
        }
    }
}
