using System.Threading.Tasks;

namespace RPGFramework.UI
{
    /// <summary>
    /// Core UI system interface for managing views.
    /// Provides view registration, showing/hiding, and lifecycle management.
    /// </summary>
    public interface IUISystem
    {
        /// <summary>
        /// Registers a view with the UI system.
        /// </summary>
        void RegisterView(ViewId viewId, IView view);

        /// <summary>
        /// Unregisters a view from the UI system.
        /// </summary>
        void UnregisterView(ViewId viewId);

        /// <summary>
        /// Shows a view by ID synchronously.
        /// </summary>
        void ShowView(ViewId viewId);

        /// <summary>
        /// Shows a view by ID asynchronously.
        /// </summary>
        Task<IView> ShowViewAsync(ViewId viewId);

        /// <summary>
        /// Hides a view by ID.
        /// </summary>
        void HideView(ViewId viewId);

        /// <summary>
        /// Gets a registered view by ID.
        /// </summary>
        IView GetView(ViewId viewId);

        /// <summary>
        /// Gets a registered view by ID with type casting.
        /// </summary>
        TView GetView<TView>(ViewId viewId) where TView : class, IView;

        /// <summary>
        /// Checks if a view is currently active/visible.
        /// </summary>
        bool IsViewActive(ViewId viewId);

        /// <summary>
        /// Hides all views.
        /// </summary>
        void HideAllViews();
    }
}
