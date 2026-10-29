using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RPGFramework.Settings
{
    public enum DisplayWindowMode
    {
        FullScreen,
        Borderless,
        Windowed
    }

    /// <summary>
    /// What the display settings ask of the screen.
    /// </summary>
    public readonly struct DisplayState : IEquatable<DisplayState>
    {
        public DisplayState(int width, int height, DisplayWindowMode windowMode, int quality, bool vSync, int frameRate)
        {
            Width = width;
            Height = height;
            WindowMode = windowMode;
            Quality = quality;
            VSync = vSync;
            FrameRate = frameRate;
        }

        public int Width { get; }
        public int Height { get; }
        public DisplayWindowMode WindowMode { get; }

        /// <summary>
        /// The index of a quality level.
        /// </summary>
        public int Quality { get; }

        public bool VSync { get; }

        /// <summary>
        /// Frames a second at most; 0: no cap. Vertical sync, when on, decides instead.
        /// </summary>
        public int FrameRate { get; }

        public bool Equals(DisplayState other) =>
            Width == other.Width && Height == other.Height && WindowMode == other.WindowMode && Quality == other.Quality
            && VSync == other.VSync && FrameRate == other.FrameRate;

        public override bool Equals(object obj) => obj is DisplayState other && Equals(other);
        public override int GetHashCode() => (Width * 397) ^ Height ^ ((int)WindowMode << 20) ^ (Quality << 24);
        public override string ToString() => $"{Width}x{Height} {WindowMode} q{Quality} vsync {VSync} fps {FrameRate}";
    }

    /// <summary>
    /// The screen, for <see cref="DisplaySettings"/>: Unity's (<c>UnityDisplayOutput</c>) or a test's.
    /// </summary>
    public interface IDisplayOutput
    {
        /// <summary>
        /// Sizes the screen can take, any order.
        /// </summary>
        IReadOnlyList<(int Width, int Height)> Resolutions { get; }

        IReadOnlyList<string> QualityLevels { get; }

        /// <summary>
        /// The screen as it is before any setting is applied: the defaults (a game's first start changes nothing).
        /// </summary>
        DisplayState Current { get; }

        /// <summary>
        /// Display options make no sense here (a server, a phone): none are added.
        /// </summary>
        bool CanChangeResolution { get; }

        void Apply(DisplayState state);
    }

    /// <summary>
    /// The display part of a settings page (group "Display"): screen size, window mode, quality, vertical sync and a frame
    /// cap, applied at once and kept. Phones and consoles (<see cref="IDisplayOutput.CanChangeResolution"/> false) get
    /// quality and the frame cap only.
    /// </summary>
    public sealed class DisplaySettings
    {
        public const string Group = "Display";
        public const string ResolutionId = "display.resolution";
        public const string WindowModeId = "display.windowMode";
        public const string QualityId = "display.quality";
        public const string VSyncId = "display.vsync";
        public const string FrameRateId = "display.frameRate";

        private static readonly string[] WindowModes = { "Full screen", "Borderless", "Windowed" };
        private static readonly int[] FrameRates = { 30, 60, 120, 144, 0 };

        private readonly IDisplayOutput _output;
        private readonly List<(int Width, int Height)> _sizes;
        private DisplayState? _applied;

        private DisplaySettings(ISettingsService settings, IDisplayOutput output)
        {
            _output = output;
            _sizes = output.Resolutions.Where(r => r.Width > 0 && r.Height > 0).Distinct()
                .OrderByDescending(r => r.Width).ThenByDescending(r => r.Height).ToList();
            var now = output.Current;
            var current = (now.Width, now.Height);
            if (current.Width > 0 && !_sizes.Contains(current))
                _sizes.Insert(0, current);

            if (output.CanChangeResolution && _sizes.Count > 0)
            {
                Resolution = settings.Add(new ChoiceSetting(ResolutionId, "Resolution", _sizes.Select(s => SizeText(s)),
                    Math.Max(0, _sizes.IndexOf(current)), Group));
                WindowMode = settings.Add(new ChoiceSetting(WindowModeId, "Window", WindowModes, (int)now.WindowMode, Group));
                VSync = settings.Add(new ToggleSetting(VSyncId, "Vertical sync", now.VSync, Group));
            }
            var levels = output.QualityLevels.Count > 0 ? output.QualityLevels : new[] { "Default" };
            Quality = settings.Add(new ChoiceSetting(QualityId, "Quality", levels,
                Math.Max(0, Math.Min(levels.Count - 1, now.Quality)), Group));
            int rate = Array.IndexOf(FrameRates, now.FrameRate > 0 ? now.FrameRate : 0);
            FrameRate = settings.Add(new ChoiceSetting(FrameRateId, "Frame rate cap", FrameRates.Select(r => RateText(r)),
                rate >= 0 ? rate : FrameRates.Length - 1, Group));
            _applied = now;

            foreach (var setting in new Setting[] { Resolution, WindowMode, VSync, Quality, FrameRate })
            {
                if (setting != null)
                    setting.Changed += _ => Apply();
            }
        }

        /// <summary>
        /// Null where the screen size cannot change.
        /// </summary>
        public ChoiceSetting Resolution { get; }

        public ChoiceSetting WindowMode { get; }
        public ToggleSetting VSync { get; }
        public ChoiceSetting Quality { get; }
        public ChoiceSetting FrameRate { get; }

        /// <summary>
        /// The state the settings ask for now.
        /// </summary>
        public DisplayState State
        {
            get
            {
                var now = _output.Current;
                var size = Resolution != null && _sizes.Count > 0 ? _sizes[Resolution.Index] : (now.Width, now.Height);
                return new DisplayState(size.Width, size.Height,
                    WindowMode != null ? (DisplayWindowMode)WindowMode.Index : now.WindowMode,
                    Quality.Index, VSync?.Value ?? now.VSync, FrameRates[FrameRate.Index]);
            }
        }

        /// <summary>
        /// Adds the display options and applies their kept values (nothing when they are the screen's as it is).
        /// </summary>
        public static DisplaySettings Add(ISettingsService settings, IDisplayOutput output)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            var display = new DisplaySettings(settings, output);
            display.Apply();
            return display;
        }

        /// <summary>
        /// Gives the screen the state again (after the game changed it by hand); nothing when it is the same.
        /// </summary>
        public void Apply(bool force = false)
        {
            var state = State;
            if (!force && _applied.HasValue && _applied.Value.Equals(state))
                return;
            _applied = state;
            _output.Apply(state);
        }

        private static string SizeText((int Width, int Height) size) =>
            size.Width.ToString(CultureInfo.InvariantCulture) + " x " + size.Height.ToString(CultureInfo.InvariantCulture);

        private static string RateText(int rate) => rate == 0 ? "Unlimited" : rate.ToString(CultureInfo.InvariantCulture);
    }
}
