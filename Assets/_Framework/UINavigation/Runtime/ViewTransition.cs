using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace RPGFramework.UINavigation
{
    public enum TransitionStyle
    {
        /// <summary>
        /// Resolve through <see cref="TransitionRules"/>.
        /// </summary>
        Default = 0,
        None,
        Fade,
        SlideLeft,
        SlideRight,
        SlideUp,
        SlideDown,
        Scale
    }

    public enum NavigationDirection
    {
        Forward,
        Back
    }

    public readonly struct TransitionRequest
    {
        public TransitionRequest(INavigableView from, INavigableView to, TransitionStyle style, NavigationDirection direction)
        {
            From = from;
            To = to;
            Style = style;
            Direction = direction;
        }

        /// <summary>
        /// View to hide; null when a modal opens.
        /// </summary>
        public INavigableView From { get; }

        /// <summary>
        /// View to show; null when a modal closes.
        /// </summary>
        public INavigableView To { get; }

        /// <summary>
        /// Never <see cref="TransitionStyle.Default"/>: the service resolves it first.
        /// </summary>
        public TransitionStyle Style { get; }

        public NavigationDirection Direction { get; }
    }

    /// <summary>
    /// Animates the switch between two views. When it completes, <c>From</c> must be hidden and <c>To</c> shown.
    /// </summary>
    public interface IViewTransition
    {
        UniTask PlayAsync(TransitionRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Switches views immediately, whatever the style. Default when no transition is supplied, and the fallback
    /// when an animated transition fails.
    /// </summary>
    public sealed class InstantTransition : IViewTransition
    {
        public static readonly InstantTransition Instance = new InstantTransition();

        public UniTask PlayAsync(TransitionRequest request, CancellationToken cancellationToken)
        {
            Apply(request);
            return UniTask.CompletedTask;
        }

        public static void Apply(TransitionRequest request)
        {
            request.From?.Hide();
            request.To?.Show();
        }
    }

    /// <summary>
    /// Chooses the style for a navigation that did not ask for one. Lookup order: exact pair, any → target,
    /// source → any, then the direction default. A null id in <see cref="Set"/> means "any view".
    /// </summary>
    public sealed class TransitionRules
    {
        private readonly Dictionary<(string From, string To), TransitionStyle> _rules = new Dictionary<(string, string), TransitionStyle>();

        public TransitionStyle DefaultForward { get; set; } = TransitionStyle.Fade;
        public TransitionStyle DefaultBack { get; set; } = TransitionStyle.Fade;
        public TransitionStyle DefaultModal { get; set; } = TransitionStyle.Fade;

        public TransitionRules Set(string fromViewId, string toViewId, TransitionStyle style)
        {
            if (fromViewId == null && toViewId == null)
                throw new ArgumentException("Use DefaultForward/DefaultBack for an any → any rule");
            if (style == TransitionStyle.Default)
                throw new ArgumentException("A rule needs a concrete style", nameof(style));
            _rules[(fromViewId, toViewId)] = style;
            return this;
        }

        public bool Remove(string fromViewId, string toViewId) => _rules.Remove((fromViewId, toViewId));

        public TransitionStyle Resolve(string fromViewId, string toViewId, TransitionStyle requested, NavigationDirection direction)
        {
            if (requested != TransitionStyle.Default)
                return requested;
            if (_rules.TryGetValue((fromViewId, toViewId), out var style)
                || _rules.TryGetValue((null, toViewId), out style)
                || (fromViewId != null && _rules.TryGetValue((fromViewId, null), out style)))
                return style;
            return direction == NavigationDirection.Forward ? DefaultForward : DefaultBack;
        }
    }
}
