using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RPGFramework.Localization
{
    /// <summary>
    /// String tables as one CSV for translators: a "key" column, then a column per language code
    /// ("key,en,uk"), quoted as spreadsheets do. An empty cell is a text not translated yet.
    /// </summary>
    public static class LocalizationCsv
    {
        public static string Write(IEnumerable<StringTable> tables)
        {
            var list = (tables ?? Enumerable.Empty<StringTable>()).Where(t => t != null).ToList();
            var keys = list.SelectMany(t => t.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();
            var csv = new StringBuilder();
            csv.Append("key");
            foreach (var table in list)
                csv.Append(',').Append(Quote(table.Language));
            csv.Append('\n');
            foreach (string key in keys)
            {
                csv.Append(Quote(key));
                foreach (var table in list)
                {
                    table.TryGet(key, out string text);
                    csv.Append(',').Append(Quote(text ?? ""));
                }
                csv.Append('\n');
            }
            return csv.ToString();
        }

        /// <summary>
        /// Reads tables back: one per language column; empty cells are left out.
        /// </summary>
        /// <exception cref="FormatException">No "key" column first.</exception>
        public static List<StringTable> Read(string csv)
        {
            var rows = Rows(csv ?? "").ToList();
            if (rows.Count == 0 || rows[0].Count == 0 || !string.Equals(rows[0][0].Trim(), "key", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("The first column must be \"key\", then one column per language code");
            var tables = rows[0].Skip(1).Select(language => new StringTable(language.Trim())).ToList();
            foreach (var row in rows.Skip(1))
            {
                if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]))
                    continue;
                for (int i = 0; i < tables.Count && i + 1 < row.Count; i++)
                {
                    if (row[i + 1].Length > 0)
                        tables[i].Set(row[0].Trim(), row[i + 1]);
                }
            }
            return tables;
        }

        private static string Quote(string text)
        {
            if (text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 && text.Trim() == text)
                return text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private static IEnumerable<List<string>> Rows(string csv)
        {
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        cell.Append(c);
                    }
                    continue;
                }
                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(cell.ToString());
                        cell.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(cell.ToString());
                        cell.Clear();
                        yield return row;
                        row = new List<string>();
                        break;
                    default:
                        cell.Append(c);
                        break;
                }
            }
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                yield return row;
            }
        }
    }

    /// <summary>
    /// Checks tables against each other, for a test that keeps translations whole: every key of the default language
    /// in every table, no key only in a translation, the same {names} in every language. Plural forms of a key count as
    /// the key (Ukrainian has few and many where English has other).
    /// </summary>
    public static class LocalizationChecks
    {
        private static readonly Regex Placeholder = new Regex(@"(?<!\{)\{([A-Za-z_][A-Za-z0-9_]*)(:[^}]*)?\}(?!\})");

        /// <returns>One line per problem ("uk: menu.quit is missing"); empty when the tables agree.</returns>
        public static List<string> Problems(IEnumerable<StringTable> tables, string defaultLanguage = null)
        {
            var list = (tables ?? Enumerable.Empty<StringTable>()).Where(t => t != null).ToList();
            var problems = new List<string>();
            if (list.Count == 0)
                return problems;
            var source = list.FirstOrDefault(t => string.Equals(t.Language, defaultLanguage, StringComparison.OrdinalIgnoreCase)) ?? list[0];
            var sourceKeys = Group(source);
            foreach (var table in list.Where(t => t != source))
            {
                var keys = Group(table);
                foreach (var key in sourceKeys.Keys.Where(k => !keys.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal))
                    problems.Add($"{table.Language}: {key} is missing");
                foreach (var key in keys.Keys.Where(k => !sourceKeys.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal))
                    problems.Add($"{table.Language}: {key} is not in {source.Language}");
                foreach (var key in keys.Keys.Where(sourceKeys.ContainsKey).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var wanted = Names(sourceKeys[key]);
                    var found = Names(keys[key]);
                    // {count} is optional in a plural form ("one" may say "a goblin")
                    wanted.Remove("count");
                    found.Remove("count");
                    if (!wanted.SetEquals(found))
                        problems.Add(table.Language + ": " + key + " has " + List(found) + " where " + source.Language + " has " + List(wanted));
                }
            }
            return problems;
        }

        // Texts by key, plural forms under their key
        private static Dictionary<string, List<string>> Group(StringTable table)
        {
            var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string key in table.Keys)
            {
                table.TryGet(key, out string text);
                string baseKey = BaseKey(key);
                if (!groups.TryGetValue(baseKey, out var texts))
                    groups[baseKey] = texts = new List<string>();
                texts.Add(text ?? "");
            }
            return groups;
        }

        private static string BaseKey(string key)
        {
            foreach (PluralForm form in Enum.GetValues(typeof(PluralForm)))
            {
                string suffix = "." + PluralRules.Suffix(form);
                if (key.EndsWith(suffix, StringComparison.Ordinal) && key.Length > suffix.Length)
                    return key.Substring(0, key.Length - suffix.Length);
            }
            return key;
        }

        // "{damage}, {name}"; "nothing" for none
        private static string List(IEnumerable<string> names)
        {
            var sorted = names.OrderBy(n => n, StringComparer.Ordinal).Select(n => "{" + n + "}").ToList();
            return sorted.Count == 0 ? "nothing" : string.Join(", ", sorted);
        }

        private static HashSet<string> Names(IEnumerable<string> texts) =>
            new HashSet<string>(texts.SelectMany(t => Placeholder.Matches(t).Cast<Match>().Select(m => m.Groups[1].Value)), StringComparer.Ordinal);
    }
}
