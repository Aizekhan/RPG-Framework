using System;
using UnityEngine;

namespace RPGFramework.Diagnostics.Unity
{
    /// <summary>
    /// A look for the diagnostics panel (<see cref="DiagnosticsPanelLayout.For"/>).
    /// </summary>
    public enum DiagnosticsPanelStyle
    {
        /// <summary>
        /// Every section in a large box in the top left corner, refreshed four times a second.
        /// </summary>
        Full = 0,

        /// <summary>
        /// A small see-through panel in the top right corner, fitted to its text and refreshed every frame: frame rate
        /// and what the frame draws against the budget, in short lines, with the three heaviest models. For watching
        /// the numbers while playing.
        /// </summary>
        Compact = 1,

        /// <summary>
        /// The layout's own values.
        /// </summary>
        Custom = 2
    }

    /// <summary>
    /// Where the diagnostics panel stands, how it looks, how often it refreshes and which sections it shows, set at once
    /// on a <see cref="DiagnosticsOverlay"/> (<see cref="ApplyTo"/>). <see cref="Style"/> picks a preset instead of the
    /// values (<see cref="For"/>).
    /// </summary>
    [Serializable]
    public sealed class DiagnosticsPanelLayout
    {
        [Tooltip("Full: every section in a large box; Compact: a small see-through panel refreshed every frame; Custom: the values below")]
        public DiagnosticsPanelStyle Style = DiagnosticsPanelStyle.Full;

        public DiagnosticsCorner Corner = DiagnosticsCorner.TopLeft;

        [Tooltip("The background's opacity (0 = only the text)")]
        [Range(0f, 1f)] public float Opacity = 1f;

        [Min(1)] public int FontSize = 14;

        [Tooltip("Width and height in pixels (0 = fit the text)")]
        public Vector2 Size = new Vector2(520f, 420f);

        [Tooltip("Seconds between refreshes (0 = every frame)")]
        [Min(0f)] public float RefreshInterval = 0.25f;

        [Tooltip("Titles of the sections shown, e.g. Performance and Render (empty = all)")]
        public string[] Sections = Array.Empty<string>();

        [Tooltip("Show the '== Title ==' line above each section")]
        public bool Headers = true;

        [Tooltip("Short lines for the Performance and Render sections")]
        public bool ShortLines;

        [Tooltip("Heaviest models listed by the Render section")]
        [Min(0)] public int TopModels = 6;

        /// <summary>
        /// This layout for <see cref="DiagnosticsPanelStyle.Custom"/>, otherwise the style's preset.
        /// </summary>
        public DiagnosticsPanelLayout Resolved() => Style == DiagnosticsPanelStyle.Custom ? this : For(Style);

        /// <summary>
        /// The preset of a style.
        /// </summary>
        public static DiagnosticsPanelLayout For(DiagnosticsPanelStyle style)
        {
            if (style != DiagnosticsPanelStyle.Compact)
                return new DiagnosticsPanelLayout { Style = style };
            return new DiagnosticsPanelLayout
            {
                Style = style,
                Corner = DiagnosticsCorner.TopRight,
                Opacity = 0.35f,
                FontSize = 13,
                Size = Vector2.zero,
                RefreshInterval = 0f,
                Sections = new[] { "Performance", "Render" },
                Headers = false,
                ShortLines = true,
                TopModels = 3
            };
        }

        /// <summary>
        /// Sets the overlay's place, look, refresh and sections, and the short lines of the Performance and Render
        /// sections in its hub (assign <see cref="DiagnosticsOverlay.Hub"/> first). Uses <see cref="Resolved"/>.
        /// </summary>
        public void ApplyTo(DiagnosticsOverlay overlay)
        {
            if (overlay == null)
                throw new ArgumentNullException(nameof(overlay));
            var layout = Resolved();
            overlay.Corner = layout.Corner;
            overlay.Opacity = layout.Opacity;
            overlay.FontSize = layout.FontSize;
            overlay.Size = layout.Size;
            overlay.RefreshInterval = layout.RefreshInterval;
            overlay.Sections = layout.Sections;
            overlay.Headers = layout.Headers;
            if (overlay.Hub == null)
                return;
            foreach (var section in overlay.Hub.Sections)
            {
                if (section is PerformanceSection performance)
                    performance.Compact = layout.ShortLines;
                else if (section is RenderSection render)
                {
                    render.Compact = layout.ShortLines;
                    render.TopModels = layout.TopModels;
                }
            }
        }
    }
}
