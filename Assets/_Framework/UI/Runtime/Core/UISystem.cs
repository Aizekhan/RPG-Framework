using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace RPGFramework.UI
{
    /// <summary>
    /// Core UI system implementation for managing views.
    /// Handles view registration, lifecycle, and visibility.
    /// </summary>
    public class UISystem : IUISystem
    {
        private readonly Dictionary<ViewId, IView> _registeredViews = new Dictionary<ViewId, IView>();
        private readonly IUILogger _logger;

        public UISystem(IUILogger logger = null)
        {
            _logger = logger ?? new UnityUILogger();
        }

        public void RegisterView(ViewId viewId, IView view)
        {
            if (view == null)
            {
                _logger.LogWarning($"Cannot register null view for ViewId {viewId}");
                return;
            }

            if (_registeredViews.ContainsKey(viewId))
            {
                _logger.LogWarning($"View {viewId} already registered, replacing...");
                _registeredViews[viewId] = view;
            }
            else
            {
                _registeredViews.Add(viewId, view);
                _logger.LogInfo($"Registered view: {viewId}");
            }
        }

        public void UnregisterView(ViewId viewId)
        {
            if (_registeredViews.Remove(viewId))
            {
                _logger.LogInfo($"Unregistered view: {viewId}");
            }
            else
            {
                _logger.LogWarning($"View {viewId} not found for unregister");
            }
        }

        public void ShowView(ViewId viewId)
        {
            if (_registeredViews.TryGetValue(viewId, out var view))
            {
                view.Show();
                _logger.LogInfo($"Showing view: {viewId}");
            }
            else
            {
                _logger.LogWarning($"View {viewId} not registered");
            }
        }

        public async Task<IView> ShowViewAsync(ViewId viewId)
        {
            if (_registeredViews.TryGetValue(viewId, out var view))
            {
                view.Show();
                _logger.LogInfo($"Showing view: {viewId}");
                await Task.Yield(); // Allow view to initialize
                return view;
            }

            _logger.LogWarning($"View {viewId} not registered");
            return null;
        }

        public void HideView(ViewId viewId)
        {
            if (_registeredViews.TryGetValue(viewId, out var view))
            {
                view.Hide();
                _logger.LogInfo($"Hiding view: {viewId}");
            }
            else
            {
                _logger.LogWarning($"View {viewId} not registered");
            }
        }

        public IView GetView(ViewId viewId)
        {
            if (_registeredViews.TryGetValue(viewId, out var view))
            {
                return view;
            }

            _logger.LogWarning($"View {viewId} not found");
            return null;
        }

        public TView GetView<TView>(ViewId viewId) where TView : class, IView
        {
            return GetView(viewId) as TView;
        }

        public bool IsViewActive(ViewId viewId)
        {
            if (_registeredViews.TryGetValue(viewId, out var view))
            {
                return view.IsVisible;
            }

            return false;
        }

        public void HideAllViews()
        {
            foreach (var view in _registeredViews.Values)
            {
                view.Hide();
            }

            _logger.LogInfo("All views hidden");
        }
    }

    /// <summary>
    /// Logger interface for UI system.
    /// Allows custom logging implementations.
    /// </summary>
    public interface IUILogger
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
    }

    /// <summary>
    /// Default Unity logger implementation.
    /// </summary>
    public class UnityUILogger : IUILogger
    {
        public void LogInfo(string message) => Debug.Log($"[UI] {message}");
        public void LogWarning(string message) => Debug.LogWarning($"[UI] {message}");
        public void LogError(string message) => Debug.LogError($"[UI] {message}");
    }
}
