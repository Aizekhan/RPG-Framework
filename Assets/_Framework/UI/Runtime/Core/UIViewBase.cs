using UnityEngine;

namespace RPGFramework.UI
{
    /// <summary>
    /// Base MonoBehaviour for all UI views.
    /// Implements IView interface and provides common show/hide logic.
    /// </summary>
    public abstract class UIViewBase : MonoBehaviour, IView
    {
        [SerializeField] private bool startHidden = true;

        private CanvasGroup _canvasGroup;
        private bool _isVisible;

        public GameObject GameObject => gameObject;
        public bool IsVisible => _isVisible;

        protected virtual void Awake()
        {
            // Get or add CanvasGroup for smooth fade in/out
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (startHidden)
            {
                Hide();
            }
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            _isVisible = true;

            OnShow();
        }

        public virtual void Hide()
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _isVisible = false;
            gameObject.SetActive(false);

            OnHide();
        }

        /// <summary>
        /// Called when view is shown. Override for custom logic.
        /// </summary>
        protected virtual void OnShow() { }

        /// <summary>
        /// Called when view is hidden. Override for custom logic.
        /// </summary>
        protected virtual void OnHide() { }
    }
}
