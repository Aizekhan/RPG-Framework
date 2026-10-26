using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGFramework.AssetManagement.Unity
{
    /// <summary>
    /// Loads assets from <c>Resources/</c> folders (keys are paths relative to a Resources folder, without extension).
    /// </summary>
    public sealed class ResourcesAssetProvider : IAssetProvider
    {
        public ResourcesAssetProvider(int priority = 0)
        {
            Priority = priority;
        }

        public string Name => "Resources";

        public int Priority { get; }

        public async UniTask<T> LoadAsync<T>(string key, CancellationToken cancellationToken) where T : class
        {
            var type = typeof(Object).IsAssignableFrom(typeof(T)) ? typeof(T) : typeof(Object);
            var request = UnityEngine.Resources.LoadAsync(key, type);
            await request.ToUniTask(cancellationToken: cancellationToken);
            return request.asset as T;
        }

        public void Release(string key, object asset)
        {
            // GameObjects/components cannot be unloaded individually; Resources.UnloadUnusedAssets reclaims them
            if (asset is Object unityObject && !(unityObject is GameObject) && !(unityObject is Component))
                UnityEngine.Resources.UnloadAsset(unityObject);
        }
    }

    public static class AssetManagerUnityExtensions
    {
        /// <summary>
        /// Loads a prefab and instantiates it; the prefab stays loaded until the instance is destroyed.
        /// </summary>
        public static async UniTask<GameObject> InstantiateAsync(this IAssetManager assets, string key, Transform parent = null,
            CancellationToken cancellationToken = default)
        {
            var handle = await assets.LoadAsync<GameObject>(key, cancellationToken);
            try
            {
                var instance = Object.Instantiate(handle.Asset, parent);
                instance.AddComponent<AssetHandleOwner>().Bind(handle);
                return instance;
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }
    }
}
