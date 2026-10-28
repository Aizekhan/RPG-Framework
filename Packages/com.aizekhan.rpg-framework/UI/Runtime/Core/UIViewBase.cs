using Cysharp.Threading.Tasks;
using RPGFramework.UINavigation;
using RPGFramework.UINavigation.Unity;
using UnityEngine;

namespace RPGFramework.UI
{
    /// <summary>
    /// Base for the framework's RPG panels. A panel works either standalone (placed in a scene, shown and hidden
    /// directly) or as a screen of <see cref="INavigationService"/> (registered through a <c>ViewConfig</c> and
    /// opened with <c>NavigateToAsync</c>). Set <see cref="Navigation"/> for the second case so <see cref="Close"/>
    /// goes back through the navigation history instead of only hiding the panel.
    /// </summary>
    public abstract class UIViewBase : NavigableView
    {
        [Tooltip("Hide on Awake (for panels placed directly in a scene)")]
        [SerializeField] private bool startHidden = true;

        // Shown or hidden before Awake (a panel built from code inside a screen that is not active yet): that wins
        private bool _visibilitySet;

        /// <summary>
        /// Hide on Awake (for panels placed directly in a scene). A panel already shown or hidden before its Awake
        /// keeps that, so one built from code inside an inactive screen stays shown when the screen opens.
        /// </summary>
        public bool StartHidden
        {
            get => startHidden;
            set => startHidden = value;
        }

        /// <summary>
        /// Navigation that opened this panel, if any (e.g. assigned from <c>PrefabViewFactory.InstanceCreated</c>).
        /// </summary>
        public INavigationService Navigation { get; set; }

        protected virtual void Awake()
        {
            if (startHidden && !_visibilitySet)
                Hide();
        }

        public override void Show()
        {
            _visibilitySet = true;
            base.Show();
            OnShow();
        }

        public override void Hide()
        {
            _visibilitySet = true;
            base.Hide();
            OnHide();
        }

        /// <summary>
        /// Closes the panel: navigates back when it is the navigation's current screen, otherwise hides it.
        /// </summary>
        public virtual void Close()
        {
            if (Navigation != null && ReferenceEquals(Navigation.CurrentView, this) && Navigation.ModalCount == 0)
                Navigation.GoBackAsync().Forget();
            else
                Hide();
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
