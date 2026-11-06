using System;
using System.Collections.Generic;
using System.Text;

namespace RPGFramework.Diagnostics
{
    /// <summary>
    /// One block of a diagnostics report (frame times, systems, events...). Writes plain text so the same report can
    /// be shown in an overlay, logged, or attached to a bug report.
    /// </summary>
    public interface IDiagnosticsSection
    {
        string Title { get; }
        void Write(StringBuilder output);
    }

    /// <summary>
    /// Collects diagnostics sections and renders them into one text report. A failing section is reported inline
    /// instead of breaking the report.
    /// </summary>
    public sealed class DiagnosticsHub
    {
        private readonly List<IDiagnosticsSection> _sections = new List<IDiagnosticsSection>();
        private readonly StringBuilder _buffer = new StringBuilder(1024);

        public IReadOnlyList<IDiagnosticsSection> Sections => _sections;

        public DiagnosticsHub Add(IDiagnosticsSection section)
        {
            if (section == null)
                throw new ArgumentNullException(nameof(section));
            if (!_sections.Contains(section))
                _sections.Add(section);
            return this;
        }

        public bool Remove(IDiagnosticsSection section) => _sections.Remove(section);

        public bool Remove(string title) => _sections.RemoveAll(s => s.Title == title) > 0;

        public string Render()
        {
            _buffer.Clear();
            foreach (var section in _sections)
                Append(section);
            return _buffer.ToString();
        }

        /// <summary>
        /// Renders the sections with these titles in the hub's order (null or empty: all of them), with or without
        /// their "== Title ==" headers.
        /// </summary>
        public string Render(IReadOnlyList<string> titles, bool headers = true)
        {
            _buffer.Clear();
            foreach (var section in _sections)
            {
                if (titles == null || titles.Count == 0 || Contains(titles, section.Title))
                    Append(section, headers);
            }
            return _buffer.ToString();
        }

        /// <summary>
        /// Renders only the section with this title, or an empty string when there is none.
        /// </summary>
        public string Render(string title)
        {
            _buffer.Clear();
            foreach (var section in _sections)
            {
                if (section.Title == title)
                    Append(section);
            }
            return _buffer.ToString();
        }

        private static bool Contains(IReadOnlyList<string> titles, string title)
        {
            for (int i = 0; i < titles.Count; i++)
            {
                if (titles[i] == title)
                    return true;
            }
            return false;
        }

        private void Append(IDiagnosticsSection section, bool headers = true)
        {
            if (headers)
            {
                if (_buffer.Length > 0)
                    _buffer.AppendLine();
                _buffer.Append("== ").Append(section.Title).AppendLine(" ==");
            }
            int start = _buffer.Length;
            try
            {
                section.Write(_buffer);
            }
            catch (Exception ex)
            {
                _buffer.Length = start;
                _buffer.Append("<section failed: ").Append(ex.GetType().Name).Append(": ").Append(ex.Message).AppendLine(">");
            }
        }
    }
}
