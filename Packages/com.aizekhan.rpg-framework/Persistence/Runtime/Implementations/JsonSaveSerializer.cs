using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// JSON-based save serializer using reflection-based JSON serialization.
    /// Pure .NET implementation without external dependencies.
    /// </summary>
    public class JsonSaveSerializer : ISaveSerializer
    {
        public SerializationFormat Format => SerializationFormat.Json;

        public byte[] Serialize<T>(T data)
        {
            try
            {
                var json = SimpleJsonSerializer.Serialize(data);
                return Encoding.UTF8.GetBytes(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to serialize data: {ex.Message}", ex);
            }
        }

        public T Deserialize<T>(byte[] data)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                return SimpleJsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to deserialize data: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Reflection-based JSON for saves, without external dependencies. Writes public fields, public properties with a
    /// public setter, and get-only collection properties (filled on read). Strings are fully escaped; enums are written
    /// by name; DateTime, DateTimeOffset, TimeSpan and Guid as round-trip strings; NaN and infinities as strings.
    /// Reading is strict about the text (truncated or broken JSON fails) and lenient about the shape: unknown members
    /// are skipped and missing ones keep the defaults the type gives them, so saves from older versions keep loading.
    /// Earlier forms are still read: enums as numbers or {"value__":n}, dictionary keys without quotes.
    /// </summary>
    internal static class SimpleJsonSerializer
    {
        /// <summary>
        /// Deeper than this is almost surely a reference cycle; failing beats a stack overflow that takes the game down.
        /// </summary>
        private const int MaxDepth = 64;

        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;

        public static string Serialize(object obj)
        {
            var json = new StringBuilder();
            Write(json, obj, 0);
            return json.ToString();
        }

        public static T Deserialize<T>(string json)
        {
            if (json == null)
                return default(T);
            var reader = new Reader(json);
            var node = reader.ReadDocument();
            return (T)Convert(node, typeof(T), 0);
        }

        // ---- Writing ----

        private static void Write(StringBuilder json, object value, int depth)
        {
            if (depth > MaxDepth)
                throw new InvalidOperationException($"Data is nested deeper than {MaxDepth} levels (does an object refer back to itself?)");
            if (value == null)
            {
                json.Append("null");
                return;
            }

            var type = value.GetType();
            switch (value)
            {
                case string text:
                    WriteString(json, text);
                    return;
                case bool flag:
                    json.Append(flag ? "true" : "false");
                    return;
                case char character:
                    WriteString(json, character.ToString());
                    return;
                case float single:
                    WriteNumber(json, single, single.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case double number:
                    WriteNumber(json, number, number.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case decimal money:
                    json.Append(money.ToString(CultureInfo.InvariantCulture));
                    return;
                case DateTime time:
                    WriteString(json, time.ToString("o", CultureInfo.InvariantCulture));
                    return;
                case DateTimeOffset offset:
                    WriteString(json, offset.ToString("o", CultureInfo.InvariantCulture));
                    return;
                case TimeSpan span:
                    WriteString(json, span.ToString("c", CultureInfo.InvariantCulture));
                    return;
                case Guid id:
                    WriteString(json, id.ToString("D"));
                    return;
                case Enum _:
                    WriteString(json, value.ToString());
                    return;
            }
            if (type.IsPrimitive)
            {
                json.Append(System.Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is IDictionary dictionary)
            {
                json.Append('{');
                bool first = true;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (!first)
                        json.Append(',');
                    first = false;
                    WriteString(json, KeyText(entry.Key));
                    json.Append(':');
                    Write(json, entry.Value, depth + 1);
                }
                json.Append('}');
                return;
            }

            if (value is IEnumerable sequence)
            {
                json.Append('[');
                bool first = true;
                foreach (var item in sequence)
                {
                    if (!first)
                        json.Append(',');
                    first = false;
                    Write(json, item, depth + 1);
                }
                json.Append(']');
                return;
            }

            json.Append('{');
            bool firstMember = true;
            void Member(string name, object memberValue)
            {
                if (!firstMember)
                    json.Append(',');
                firstMember = false;
                WriteString(json, name);
                json.Append(':');
                Write(json, memberValue, depth + 1);
            }
            foreach (var property in type.GetProperties(Instance))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0 || property.GetGetMethod() == null)
                    continue;
                bool settable = property.GetSetMethod() != null;
                if (!settable && !IsFillable(property.PropertyType))
                    continue;
                Member(property.Name, property.GetValue(value));
            }
            foreach (var field in type.GetFields(Instance))
                Member(field.Name, field.GetValue(value));
            json.Append('}');
        }

        private static void WriteNumber(StringBuilder json, double value, string text)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                WriteString(json, text);
            else
                json.Append(text);
        }

        private static string KeyText(object key)
        {
            switch (key)
            {
                case string text:
                    return text;
                case DateTime time:
                    return time.ToString("o", CultureInfo.InvariantCulture);
                case Guid id:
                    return id.ToString("D");
                case Enum _:
                    return key.ToString();
                default:
                    return System.Convert.ToString(key, CultureInfo.InvariantCulture);
            }
        }

        private static void WriteString(StringBuilder json, string text)
        {
            json.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    case '\b': json.Append("\\b"); break;
                    case '\f': json.Append("\\f"); break;
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

        private static bool IsFillable(Type type) =>
            typeof(IDictionary).IsAssignableFrom(type) || (typeof(IList).IsAssignableFrom(type) && !type.IsArray);

        // ---- Reading: text → nodes ----

        private enum Kind
        {
            Null,
            Bool,
            Number,
            String,
            Array,
            Object
        }

        private sealed class Node
        {
            public Kind Kind;
            public string Text;
            public bool Flag;
            public List<Node> Items;
            public List<KeyValuePair<string, Node>> Members;

            public string Describe() => Kind == Kind.String ? $"\"{Text}\"" : Kind == Kind.Number ? Text : Kind.ToString().ToLowerInvariant();
        }

        private sealed class Reader
        {
            private readonly string _json;
            private int _at;

            public Reader(string json) => _json = json;

            public Node ReadDocument()
            {
                var node = ReadValue(0);
                SkipSpace();
                if (_at < _json.Length)
                    throw Error("unexpected text after the end");
                return node;
            }

            private Node ReadValue(int depth)
            {
                if (depth > MaxDepth)
                    throw Error($"nested deeper than {MaxDepth} levels");
                SkipSpace();
                if (_at >= _json.Length)
                    throw Error("the text ends where a value should be");
                char c = _json[_at];
                switch (c)
                {
                    case '{':
                        return ReadObject(depth);
                    case '[':
                        return ReadArray(depth);
                    case '"':
                        return new Node { Kind = Kind.String, Text = ReadString() };
                }
                string word = ReadBare();
                switch (word)
                {
                    case "null":
                        return new Node { Kind = Kind.Null };
                    case "true":
                        return new Node { Kind = Kind.Bool, Flag = true };
                    case "false":
                        return new Node { Kind = Kind.Bool, Flag = false };
                }
                if (word.Length == 0)
                    throw Error($"unexpected '{c}'");
                return new Node { Kind = Kind.Number, Text = word };
            }

            private Node ReadObject(int depth)
            {
                _at++; // {
                var node = new Node { Kind = Kind.Object, Members = new List<KeyValuePair<string, Node>>() };
                SkipSpace();
                if (Peek() == '}')
                {
                    _at++;
                    return node;
                }
                while (true)
                {
                    SkipSpace();
                    // Keys are strings; older saves wrote number keys without quotes
                    string key = Peek() == '"' ? ReadString() : ReadBare();
                    if (key.Length == 0 && Peek() != ':')
                        throw Error("a member name was expected");
                    SkipSpace();
                    Expect(':');
                    node.Members.Add(new KeyValuePair<string, Node>(key, ReadValue(depth + 1)));
                    SkipSpace();
                    char next = Next("',' or '}'");
                    if (next == '}')
                        return node;
                    if (next != ',')
                        throw Error($"expected ',' or '}}' but found '{next}'", -1);
                }
            }

            private Node ReadArray(int depth)
            {
                _at++; // [
                var node = new Node { Kind = Kind.Array, Items = new List<Node>() };
                SkipSpace();
                if (Peek() == ']')
                {
                    _at++;
                    return node;
                }
                while (true)
                {
                    node.Items.Add(ReadValue(depth + 1));
                    SkipSpace();
                    char next = Next("',' or ']'");
                    if (next == ']')
                        return node;
                    if (next != ',')
                        throw Error($"expected ',' or ']' but found '{next}'", -1);
                }
            }

            private string ReadString()
            {
                _at++; // opening quote
                var text = new StringBuilder();
                while (true)
                {
                    char c = Next("the closing quote of a text");
                    if (c == '"')
                        return text.ToString();
                    if (c != '\\')
                    {
                        text.Append(c); // older saves wrote some control characters as they are
                        continue;
                    }
                    char escaped = Next("an escaped character");
                    switch (escaped)
                    {
                        case '"': text.Append('"'); break;
                        case '\\': text.Append('\\'); break;
                        case '/': text.Append('/'); break;
                        case 'n': text.Append('\n'); break;
                        case 'r': text.Append('\r'); break;
                        case 't': text.Append('\t'); break;
                        case 'b': text.Append('\b'); break;
                        case 'f': text.Append('\f'); break;
                        case 'u':
                            if (_at + 4 > _json.Length || !int.TryParse(_json.Substring(_at, 4), NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture, out int code))
                                throw Error("a broken \\u escape");
                            text.Append((char)code);
                            _at += 4;
                            break;
                        default:
                            throw Error($"an unknown escape '\\{escaped}'", -1);
                    }
                }
            }

            private string ReadBare()
            {
                int start = _at;
                while (_at < _json.Length && (char.IsLetterOrDigit(_json[_at]) || _json[_at] == '-' || _json[_at] == '+' || _json[_at] == '.'))
                    _at++;
                return _json.Substring(start, _at - start);
            }

            private void SkipSpace()
            {
                while (_at < _json.Length && char.IsWhiteSpace(_json[_at]))
                    _at++;
            }

            private char Peek() => _at < _json.Length ? _json[_at] : '\0';

            private char Next(string expected)
            {
                if (_at >= _json.Length)
                    throw Error($"the text ends before {expected}");
                return _json[_at++];
            }

            private void Expect(char c)
            {
                if (Next($"'{c}'") != c)
                    throw Error($"expected '{c}'", -1);
            }

            private FormatException Error(string what, int offset = 0) =>
                new FormatException($"Broken save text at character {_at + offset}: {what}");
        }

        // ---- Reading: nodes → objects ----

        private static object Convert(Node node, Type type, int depth)
        {
            if (node.Kind == Kind.Null)
            {
                if (type.IsValueType && Nullable.GetUnderlyingType(type) == null)
                    throw new FormatException($"null cannot be a {type.Name}");
                return null;
            }
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                type = underlying;

            if (type == typeof(string))
            {
                if (node.Kind == Kind.String || node.Kind == Kind.Number)
                    return node.Text;
                if (node.Kind == Kind.Bool)
                    return node.Flag ? "true" : "false";
                throw Mismatch(node, type);
            }
            if (type == typeof(bool))
            {
                if (node.Kind == Kind.Bool)
                    return node.Flag;
                if (node.Kind == Kind.String && bool.TryParse(node.Text, out bool parsed))
                    return parsed;
                throw Mismatch(node, type);
            }
            if (type.IsEnum)
                return ToEnum(node, type);
            if (type == typeof(char))
            {
                if (node.Kind == Kind.String && node.Text.Length == 1)
                    return node.Text[0];
                throw Mismatch(node, type);
            }
            if (type == typeof(DateTime))
                return DateTime.Parse(Scalar(node, type), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (type == typeof(DateTimeOffset))
                return DateTimeOffset.Parse(Scalar(node, type), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (type == typeof(TimeSpan))
                return TimeSpan.Parse(Scalar(node, type), CultureInfo.InvariantCulture);
            if (type == typeof(Guid))
                return Guid.Parse(Scalar(node, type));
            if (type.IsPrimitive || type == typeof(decimal))
                return ToNumber(Scalar(node, type), type);

            if (type == typeof(object))
                return Natural(node, depth);

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                var items = Items(node, type);
                var array = Array.CreateInstance(elementType, items.Count);
                for (int i = 0; i < items.Count; i++)
                    array.SetValue(Convert(items[i], elementType, depth + 1), i);
                return array;
            }

            if (typeof(IDictionary).IsAssignableFrom(type) || typeof(IList).IsAssignableFrom(type))
            {
                var collection = Create(type);
                Fill(collection, node, type, depth);
                return collection;
            }

            var collectionOf = CollectionElementType(type);
            if (collectionOf != null)
            {
                // Sets and other collections that only add
                var collection = Create(type);
                var add = typeof(ICollection<>).MakeGenericType(collectionOf).GetMethod("Add");
                foreach (var item in Items(node, type))
                    add.Invoke(collection, new[] { Convert(item, collectionOf, depth + 1) });
                return collection;
            }

            if (node.Kind != Kind.Object)
                throw Mismatch(node, type);
            var obj = Create(type);
            foreach (var member in node.Members)
            {
                var property = type.GetProperty(member.Key, Instance);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    if (property.GetSetMethod() != null)
                    {
                        property.SetValue(obj, Convert(member.Value, property.PropertyType, depth + 1));
                        continue;
                    }
                    // A get-only collection: fill the one the object made
                    if (property.GetGetMethod() != null && IsFillable(property.PropertyType) && property.GetValue(obj) is object existing)
                    {
                        Fill(existing, member.Value, property.PropertyType, depth);
                        continue;
                    }
                }
                var field = type.GetField(member.Key, Instance);
                if (field != null && !field.IsInitOnly)
                    field.SetValue(obj, Convert(member.Value, field.FieldType, depth + 1));
                // Anything else is from an older version of the type: skipped
            }
            return obj;
        }

        private static void Fill(object collection, Node node, Type type, int depth)
        {
            if (collection is IDictionary dictionary)
            {
                if (node.Kind != Kind.Object)
                    throw Mismatch(node, type);
                var (keyType, valueType) = DictionaryTypes(collection.GetType());
                dictionary.Clear();
                foreach (var member in node.Members)
                    dictionary[ToKey(member.Key, keyType)] = Convert(member.Value, valueType, depth + 1);
                return;
            }
            var list = (IList)collection;
            var elementType = ListElementType(collection.GetType());
            list.Clear();
            foreach (var item in Items(node, type))
                list.Add(Convert(item, elementType, depth + 1));
        }

        private static object Create(Type type)
        {
            if (type.IsInterface || type.IsAbstract)
                throw new FormatException($"Cannot create {type.Name} (an interface or abstract type) from save data");
            try
            {
                return Activator.CreateInstance(type);
            }
            catch (MissingMethodException)
            {
                // No parameterless constructor: an empty object, filled from the save
                return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            }
        }

        private static List<Node> Items(Node node, Type type)
        {
            if (node.Kind != Kind.Array)
                throw Mismatch(node, type);
            return node.Items;
        }

        private static string Scalar(Node node, Type type)
        {
            if (node.Kind == Kind.String || node.Kind == Kind.Number)
                return node.Text;
            throw Mismatch(node, type);
        }

        private static object ToNumber(string text, Type type)
        {
            if (type == typeof(float) || type == typeof(double))
            {
                double value = text == "NaN" ? double.NaN
                    : text == "Infinity" ? double.PositiveInfinity
                    : text == "-Infinity" ? double.NegativeInfinity
                    : double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (type == typeof(float))
                    return text.Contains("Infinity") || text == "NaN"
                        ? (float)value
                        : float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                return value;
            }
            if (type == typeof(decimal))
                return decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (type == typeof(ulong))
                return ulong.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            if (type == typeof(long))
                return long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return System.Convert.ChangeType(long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture), type, CultureInfo.InvariantCulture);
        }

        private static object ToEnum(Node node, Type type)
        {
            switch (node.Kind)
            {
                case Kind.String:
                    if (long.TryParse(node.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long asNumber))
                        return Enum.ToObject(type, asNumber);
                    return Enum.Parse(type, node.Text);
                case Kind.Number:
                    return Enum.ToObject(type, long.Parse(node.Text, NumberStyles.Integer, CultureInfo.InvariantCulture));
                case Kind.Object:
                    // Older saves wrote an enum as {"value__":n}
                    foreach (var member in node.Members)
                    {
                        if (member.Key == "value__" && member.Value.Kind == Kind.Number)
                            return Enum.ToObject(type, long.Parse(member.Value.Text, NumberStyles.Integer, CultureInfo.InvariantCulture));
                    }
                    break;
            }
            throw Mismatch(node, type);
        }

        private static object ToKey(string key, Type keyType)
        {
            if (keyType == typeof(string) || keyType == typeof(object))
                return key;
            return Convert(new Node { Kind = Kind.String, Text = key }, keyType, 0);
        }

        private static object Natural(Node node, int depth)
        {
            switch (node.Kind)
            {
                case Kind.Bool:
                    return node.Flag;
                case Kind.String:
                    return node.Text;
                case Kind.Number:
                    return double.Parse(node.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
                case Kind.Array:
                    return node.Items.Select(item => Natural(item, depth + 1)).ToList();
                case Kind.Object:
                    var members = new Dictionary<string, object>();
                    foreach (var member in node.Members)
                        members[member.Key] = Natural(member.Value, depth + 1);
                    return members;
                default:
                    return null;
            }
        }

        private static (Type Key, Type Value) DictionaryTypes(Type type)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var arguments = t.GetGenericArguments();
                    return (arguments[0], arguments[1]);
                }
            }
            var generic = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
            if (generic != null)
                return (generic.GetGenericArguments()[0], generic.GetGenericArguments()[1]);
            return (typeof(object), typeof(object));
        }

        private static Type CollectionElementType(Type type)
        {
            var generic = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));
            return generic?.GetGenericArguments()[0];
        }

        private static Type ListElementType(Type type)
        {
            var generic = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
            return generic != null ? generic.GetGenericArguments()[0] : typeof(object);
        }

        private static FormatException Mismatch(Node node, Type type) =>
            new FormatException($"{node.Describe()} cannot be a {type.Name}");
    }
}
