using System.Collections.Generic;
using System.Linq;
using RPGFramework.Localization;
using RPGFramework.UI.Panels;

namespace RPGFramework.Settings.Unity
{
    /// <summary>
    /// A settings page on <see cref="ActionListPanel"/>: a row per setting ("Vertical sync   On" with "&lt;" and
    /// "&gt;"), group headings, and "Defaults". Texts read <c>settings.&lt;id&gt;</c>, <c>settings.&lt;id&gt;.&lt;choice&gt;</c>,
    /// <c>settings.on</c> / <c>settings.off</c>, <c>settings.group.&lt;group&gt;</c> and <c>settings.defaults</c> from the
    /// localization when there is one (<see cref="Keys"/> in English).
    /// </summary>
    public static class SettingsRows
    {
        public static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>
        {
            ["settings.on"] = "On",
            ["settings.off"] = "Off",
            ["settings.defaults"] = "Defaults",
            ["settings.row"] = "{label}   <b>{value}</b>"
        };

        /// <param name="group">Null: every group, each under its heading.</param>
        public static List<ActionRow> For(ISettingsService settings, ILocalization localization = null, string group = null)
        {
            var rows = new List<ActionRow>();
            if (settings == null)
                return rows;
            var groups = group != null ? new[] { group } : settings.Groups.ToArray();
            foreach (string name in groups)
            {
                if (group == null && name.Length > 0)
                {
                    string heading = name;
                    rows.Add(new ActionRow(() => "<b>" + Text(localization, "settings.group." + heading, heading) + "</b>"));
                }
                foreach (var setting in settings.InGroup(name))
                {
                    var shown = setting;
                    rows.Add(new ActionRow(() => Line(localization, shown), null,
                        new ActionButton("<", () => shown.Step(-1)),
                        new ActionButton(">", () => shown.Step(+1))));
                }
            }
            rows.Add(new ActionRow(() => "", null,
                new ActionButton(() => Text(localization, "settings.defaults", Keys["settings.defaults"]), () => settings.ResetToDefaults(group))));
            return rows;
        }

        /// <summary>
        /// A setting's line as the page shows it.
        /// </summary>
        public static string Line(ILocalization localization, Setting setting)
        {
            string label = Text(localization, "settings." + setting.Id, setting.Label);
            string value;
            switch (setting)
            {
                case ToggleSetting toggle:
                    value = toggle.Value ? Text(localization, "settings.on", Keys["settings.on"]) : Text(localization, "settings.off", Keys["settings.off"]);
                    break;
                case ChoiceSetting choice:
                    value = Text(localization, "settings." + setting.Id + "." + choice.Value, choice.Value);
                    break;
                default:
                    value = setting.Display;
                    break;
            }
            return localization != null
                ? localization.Text(LocalizedText.Of("settings.row", ("label", label), ("value", value)), Keys["settings.row"])
                : label + "   <b>" + value + "</b>";
        }

        private static string Text(ILocalization localization, string key, string fallback) =>
            localization != null ? localization.Text(key, fallback) : fallback;
    }
}
