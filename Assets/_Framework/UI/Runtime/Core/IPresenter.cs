using System;
using System.Threading.Tasks;

namespace RPGFramework.UI
{
    /// <summary>
    /// Base interface for presenters in MVP (Model-View-Presenter) pattern.
    /// Presenters handle business logic and mediate between views and models.
    /// </summary>
    public interface IPresenter : IDisposable
    {
        /// <summary>
        /// Initializes the presenter asynchronously.
        /// Called when presenter is created.
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Called when presenter should be cleaned up.
        /// </summary>
        new void Dispose();
    }

    /// <summary>
    /// Generic presenter interface with typed view.
    /// </summary>
    public interface IPresenter<out TView> : IPresenter where TView : IView
    {
        /// <summary>
        /// The view this presenter controls.
        /// </summary>
        TView View { get; }
    }
}
