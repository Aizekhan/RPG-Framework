using System;
using UnityEngine;

namespace RPGFramework.AssetManagement.Unity
{
    /// <summary>
    /// Keeps an asset handle alive for as long as the GameObject exists (also for objects created by editor tools).
    /// </summary>
    [ExecuteAlways]
    public sealed class AssetHandleOwner : MonoBehaviour
    {
        private IDisposable _handle;

        internal void Bind(IDisposable handle)
        {
            _handle?.Dispose();
            _handle = handle;
        }

        private void OnDestroy()
        {
            _handle?.Dispose();
            _handle = null;
        }
    }
}
