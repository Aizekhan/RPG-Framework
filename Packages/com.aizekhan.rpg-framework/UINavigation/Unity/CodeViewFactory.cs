using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGFramework.UINavigation.Unity
{
    /// <summary>
    /// <see cref="IViewFactory"/> for screens that build themselves in code, with no prefab: each view id has a
    /// function that makes the screen under a parent (a panel that adds its own rows and buttons). A made screen is
    /// kept and reused, hidden while closed. Ids it does not know go to <see cref="Fallback"/> (a
    /// <see cref="PrefabViewFactory"/> for screens that do have prefabs).
    /// </summary>
    public sealed class CodeViewFactory : IViewFactory, IDisposable
    {
        private readonly Dictionary<string, Func<Transform, INavigableView>> _builders =
            new Dictionary<string, Func<Transform, INavigableView>>(StringComparer.Ordinal);
        private readonly Dictionary<string, INavigableView> _made = new Dictionary<string, INavigableView>(StringComparer.Ordinal);
        private readonly Transform _parent;

        /// <param name="parent">Where screens are made (a canvas, or a layer under one).</param>
        public CodeViewFactory(Transform parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// Asked for view ids that have no function here; null fails them.
        /// </summary>
        public IViewFactory Fallback { get; set; }

        /// <summary>
        /// Called for every new screen before it is returned (e.g. to hand it the navigation or the world).
        /// </summary>
        public Action<string, INavigableView> ViewCreated { get; set; }

        public bool IsRegistered(string viewId) => viewId != null && _builders.ContainsKey(viewId);

        /// <summary>
        /// The function that makes the screen; it is called once, the first time the screen opens.
        /// </summary>
        public CodeViewFactory Register(string viewId, Func<Transform, INavigableView> build)
        {
            if (string.IsNullOrEmpty(viewId))
                throw new ArgumentException("A screen needs an id", nameof(viewId));
            if (build == null)
                throw new ArgumentNullException(nameof(build));
            if (_builders.ContainsKey(viewId))
                throw new InvalidOperationException($"Screen '{viewId}' is already registered");
            _builders.Add(viewId, build);
            return this;
        }

        /// <summary>
        /// A screen that is a component <typeparamref name="TView"/> on a new full-screen object; <paramref name="setUp"/>
        /// builds its content.
        /// </summary>
        public CodeViewFactory Register<TView>(string viewId, Action<TView> setUp = null) where TView : NavigableView =>
            Register(viewId, parent =>
            {
                var screen = new GameObject(viewId, typeof(RectTransform));
                var rect = (RectTransform)screen.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var view = screen.AddComponent<TView>();
                setUp?.Invoke(view);
                return view;
            });

        public UniTask<INavigableView> CreateAsync(string viewId, CancellationToken cancellationToken)
        {
            if (viewId != null && _made.TryGetValue(viewId, out var made) && !(made is Object unity && unity == null))
                return UniTask.FromResult(made);
            if (viewId == null || !_builders.TryGetValue(viewId, out var build))
            {
                if (Fallback != null)
                    return Fallback.CreateAsync(viewId, cancellationToken);
                throw new InvalidOperationException($"No screen '{viewId}' is registered");
            }
            var view = build(_parent) ?? throw new InvalidOperationException($"Screen '{viewId}' was not made");
            view.Hide();
            _made[viewId] = view;
            ViewCreated?.Invoke(viewId, view);
            return UniTask.FromResult(view);
        }

        public void Release(string viewId, INavigableView view)
        {
            if (viewId != null && _made.TryGetValue(viewId, out var made) && ReferenceEquals(made, view))
                view.Hide();
            else
                Fallback?.Release(viewId, view);
        }

        /// <summary>
        /// Destroys the screens it made.
        /// </summary>
        public void Dispose()
        {
            foreach (var view in _made.Values)
            {
                if (view is Component component && component != null)
                {
                    if (Application.isPlaying)
                        Object.Destroy(component.gameObject);
                    else
                        Object.DestroyImmediate(component.gameObject);
                }
            }
            _made.Clear();
        }
    }
}
