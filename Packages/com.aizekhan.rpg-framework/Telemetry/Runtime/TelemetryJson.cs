using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RPGFramework.Telemetry
{
    /// <summary>
    /// The wire format of a batch: <c>{ "events": [ envelope, ... ] }</c>, each envelope with <c>event_id</c>,
    /// <c>event_name</c>, <c>schema_version</c>, <c>build_id</c>, <c>session_id</c>, <c>player_id</c>,
    /// <c>timestamp</c> (Unix ms) and, when set, <c>game_time</c>, <c>level_id</c>, <c>zone_id</c>,
    /// <c>entity_id</c>, <c>mechanic_id</c>, <c>quest_id</c>, <c>position</c> ([x, y, z]), <c>context</c>
    /// (<c>platform</c>, <c>mode</c>, <c>cohort</c>, <c>variant</c>) and <c>properties</c>.
    /// </summary>
    public static class TelemetryJson
    {
        public static string Batch(IReadOnlyList<TelemetryEvent> events)
        {
            var json = new StringBuilder(256 * (events.Count + 1));
            json.Append("{\"events\":[");
            for (int i = 0; i < events.Count; i++)
            {
                if (i > 0)
                    json.Append(',');
                Envelope(json, events[i]);
            }
            json.Append("]}");
            return json.ToString();
        }

        public static void Envelope(StringBuilder json, TelemetryEvent e)
        {
            var who = e.Identity;
            json.Append('{');
            Field(json, "event_id", e.EventId, first: true);
            Field(json, "event_name", e.Name);
            json.Append(",\"schema_version\":").Append(who != null ? who.SchemaVersion : 1);
            Field(json, "build_id", who?.BuildId ?? "");
            Field(json, "session_id", who?.SessionId ?? "");
            Field(json, "player_id", who?.PlayerId ?? "");
            json.Append(",\"timestamp\":").Append(e.Timestamp.ToString(CultureInfo.InvariantCulture));
            if (e.GameTime.HasValue)
                json.Append(",\"game_time\":").Append(Number(e.GameTime.Value));
            Optional(json, "level_id", e.LevelId);
            Optional(json, "zone_id", e.ZoneId);
            Optional(json, "entity_id", e.EntityId);
            Optional(json, "mechanic_id", e.MechanicId);
            Optional(json, "quest_id", e.QuestId);
            if (e.Position.HasValue)
            {
                var p = e.Position.Value;
                json.Append(",\"position\":[").Append(Number(p.X)).Append(',').Append(Number(p.Y)).Append(',').Append(Number(p.Z)).Append(']');
            }
            var context = e.Context;
            if (context != null)
            {
                json.Append(",\"context\":{");
                bool first = true;
                first = Optional(json, "platform", context.Platform, first);
                first = Optional(json, "mode", context.Mode, first);
                first = Optional(json, "cohort", context.Cohort, first);
                Optional(json, "variant", context.Variant, first);
                json.Append('}');
            }
            if (e.Properties.Count > 0)
            {
                json.Append(",\"properties\":{");
                for (int i = 0; i < e.Properties.Count; i++)
                {
                    var property = e.Properties[i];
                    if (i > 0)
                        json.Append(',');
                    String(json, property.Key);
                    json.Append(':');
                    Value(json, property.Value);
                }
                json.Append('}');
            }
            json.Append('}');
        }

        private static void Value(StringBuilder json, object value)
        {
            switch (value)
            {
                case null: json.Append("null"); break;
                case bool flag: json.Append(flag ? "true" : "false"); break;
                case long whole: json.Append(whole.ToString(CultureInfo.InvariantCulture)); break;
                case double number: json.Append(Number(number)); break;
                default: String(json, value.ToString()); break;
            }
        }

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static void Field(StringBuilder json, string key, string value, bool first = false)
        {
            if (!first)
                json.Append(',');
            String(json, key);
            json.Append(':');
            String(json, value ?? "");
        }

        private static void Optional(StringBuilder json, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
                Field(json, key, value);
        }

        // Returns whether the object is still empty
        private static bool Optional(StringBuilder json, string key, string value, bool first)
        {
            if (string.IsNullOrEmpty(value))
                return first;
            Field(json, key, value, first);
            return false;
        }

        public static void String(StringBuilder json, string value)
        {
            json.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            json.Append(c);
                        break;
                }
            }
            json.Append('"');
        }
    }
}
