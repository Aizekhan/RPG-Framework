using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RPGFramework.UINavigation.Unity
{
    /// <summary>
    /// Fade, slide and scale transitions for <see cref="NavigableView"/>s, driven by unscaled time so they also run
    /// while the game is paused. Views that are not <see cref="NavigableView"/>s, the <see cref="TransitionStyle.None"/>
    /// style, a zero duration and edit mode switch instantly. Positions, scale and alpha are restored afterwards.
    /// </summary>
    public sealed class CanvasGroupTransition : IViewTransition
    {
        private float _duration = 0.25f;

        public float Duration
        {
            get => _duration;
            set => _duration = value >= 0f ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Starting scale of the incoming view for <see cref="TransitionStyle.Scale"/>.
        /// </summary>
        public float ScaleFrom { get; set; } = 0.85f;

        public async UniTask PlayAsync(TransitionRequest request, CancellationToken cancellationToken)
        {
            var from = request.From as NavigableView;
            var to = request.To as NavigableView;
            bool animatable = (request.From == null || from != null) && (request.To == null || to != null);
            if (!animatable || request.Style == TransitionStyle.None || _duration <= 0f || !Application.isPlaying)
            {
                InstantTransition.Apply(request);
                return;
            }

            var offset = SlideOffset(request.Style, request.Direction);
            var fromStart = from != null ? from.RectTransform.anchoredPosition : Vector2.zero;
            var toStart = to != null ? to.RectTransform.anchoredPosition : Vector2.zero;
            var toScale = to != null ? to.transform.localScale : Vector3.one;
            bool fades = request.Style == TransitionStyle.Fade || request.Style == TransitionStyle.Scale || offset == Vector2.zero;

            to?.Show();
            try
            {
                for (float elapsed = 0f; elapsed < _duration; elapsed += UnityEngine.Time.unscaledDeltaTime)
                {
                    Apply(Mathf.SmoothStep(0f, 1f, elapsed / _duration));
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            finally
            {
                if (from != null)
                {
                    from.RectTransform.anchoredPosition = fromStart;
                    from.CanvasGroup.alpha = 1f;
                    from.Hide();
                }
                if (to != null)
                {
                    to.RectTransform.anchoredPosition = toStart;
                    to.transform.localScale = toScale;
                    to.CanvasGroup.alpha = 1f;
                }
            }

            void Apply(float t)
            {
                if (from != null)
                {
                    if (fades)
                        from.CanvasGroup.alpha = 1f - t;
                    if (offset != Vector2.zero)
                        from.RectTransform.anchoredPosition = fromStart - Scaled(offset, from.RectTransform) * t;
                }
                if (to != null)
                {
                    if (fades)
                        to.CanvasGroup.alpha = t;
                    if (offset != Vector2.zero)
                        to.RectTransform.anchoredPosition = toStart + Scaled(offset, to.RectTransform) * (1f - t);
                    if (request.Style == TransitionStyle.Scale)
                        to.transform.localScale = toScale * Mathf.Lerp(ScaleFrom, 1f, t);
                }
            }
        }

        /// <summary>
        /// Where the incoming view starts, as a fraction of its size (it moves opposite to the slide direction).
        /// Going back reverses the slide.
        /// </summary>
        private static Vector2 SlideOffset(TransitionStyle style, NavigationDirection direction)
        {
            Vector2 offset;
            switch (style)
            {
                case TransitionStyle.SlideLeft:
                    offset = Vector2.right;
                    break;
                case TransitionStyle.SlideRight:
                    offset = Vector2.left;
                    break;
                case TransitionStyle.SlideUp:
                    offset = Vector2.down;
                    break;
                case TransitionStyle.SlideDown:
                    offset = Vector2.up;
                    break;
                default:
                    return Vector2.zero;
            }
            return direction == NavigationDirection.Back ? -offset : offset;
        }

        private static Vector2 Scaled(Vector2 offset, RectTransform rect) =>
            new Vector2(offset.x * rect.rect.width, offset.y * rect.rect.height);
    }
}
