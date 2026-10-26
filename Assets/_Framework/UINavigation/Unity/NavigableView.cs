using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RPGFramework.UINavigation.Unity
{
    /// <summary>
    /// Base for screens driven by <see cref="INavigationService"/>. Visibility toggles the GameObject and its
    /// <see cref="CanvasGroup"/> (added on demand) so transitions can animate alpha. Override the hooks as needed.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class NavigableView : MonoBehaviour, INavigableView
    {
        private CanvasGroup _canvasGroup;

        public bool IsVisible => gameObject.activeSelf;

        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null && !TryGetComponent(out _canvasGroup))
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                return _canvasGroup;
            }
        }

        public RectTransform RectTransform => (RectTransform)transform;

        public virtual void Show()
        {
            var group = CanvasGroup;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            var group = CanvasGroup;
            group.interactable = false;
            group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        public virtual UniTask OnOpenedAsync(NavigationParameters parameters, CancellationToken cancellationToken) => UniTask.CompletedTask;

        public virtual UniTask OnNavigatedToAsync(NavigationParameters parameters, CancellationToken cancellationToken) => UniTask.CompletedTask;

        public virtual UniTask OnNavigatedFromAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;

        public virtual UniTask OnClosedAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;
    }
}
