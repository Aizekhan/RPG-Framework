using UnityEngine;

namespace RPGFramework.Logging.Unity
{
    /// <summary>
    /// Writes entries to the Unity console with the matching Debug.Log* call.
    /// Optional rich-text colouring per severity.
    /// </summary>
    public sealed class UnityConsoleLogSink : ILogSink
    {
        public UnityConsoleLogSink(bool useRichText = true)
        {
            UseRichText = useRichText;
        }

        public bool UseRichText { get; set; }

        public void Write(in LogEntry entry)
        {
            string text = $"[{entry.Category}] {entry.Message}";

            if (entry.Properties != null && entry.Properties.Count > 0)
            {
                var builder = new System.Text.StringBuilder(text).Append(" {");
                bool first = true;
                foreach (var pair in entry.Properties)
                {
                    if (!first)
                        builder.Append(", ");
                    first = false;
                    builder.Append(pair.Key).Append('=').Append(pair.Value ?? "null");
                }
                text = builder.Append('}').ToString();
            }

            if (UseRichText)
                text = $"<color={ColorFor(entry.Severity)}>{text}</color>";

            switch (entry.Severity)
            {
                case LogSeverity.Error:
                case LogSeverity.Critical:
                    if (entry.Exception != null)
                    {
                        Debug.LogError(text);
                        Debug.LogException(entry.Exception);
                    }
                    else
                    {
                        Debug.LogError(text);
                    }
                    break;
                case LogSeverity.Warning:
                    Debug.LogWarning(entry.Exception == null ? text : text + "\n" + entry.Exception);
                    break;
                default:
                    Debug.Log(entry.Exception == null ? text : text + "\n" + entry.Exception);
                    break;
            }
        }

        public void Flush()
        {
        }

        private static string ColorFor(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Trace: return "#AAAAAA";
                case LogSeverity.Debug: return "#CCCCCC";
                case LogSeverity.Warning: return "#FFCC00";
                case LogSeverity.Error: return "#FF6666";
                case LogSeverity.Critical: return "#FF0000";
                default: return "#FFFFFF";
            }
        }
    }
}
