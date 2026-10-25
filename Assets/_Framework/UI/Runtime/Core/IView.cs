using UnityEngine;

namespace RPGFramework.UI
{
    /// <summary>
    /// Base interface for all UI views.
    /// Views are responsible for rendering UI elements and handling user input.
    /// </summary>
    public interface IView
    {
        /// <summary>
        /// Shows the view (makes it visible).
        /// </summary>
        void Show();

        /// <summary>
        /// Hides the view (makes it invisible).
        /// </summary>
        void Hide();

        /// <summary>
        /// GameObject this view is attached to.
        /// </summary>
        GameObject GameObject { get; }

        /// <summary>
        /// Whether the view is currently visible.
        /// </summary>
        bool IsVisible { get; }
    }
}
