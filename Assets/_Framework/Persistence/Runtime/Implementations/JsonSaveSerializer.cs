using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Simple JSON serializer using reflection for pure .NET serialization.
    /// Supports primitives, strings, arrays, lists, dictionaries, and complex objects.
    /// </summary>
    internal static class SimpleJsonSerializer
    {
        public static string Serialize(object obj)
        {
            if (obj == null)
                return "null";

            var type = obj.GetType();

            // Primitives and strings
            if (type.IsPrimitive)
            {
                if (type == typeof(bool))
                    return obj.ToString().ToLower();
                return obj.ToString();
            }

            if (type == typeof(string))
                return $"\"{EscapeString((string)obj)}\"";

            // Arrays
            if (type.IsArray)
            {
                var array = (Array)obj;
                var elements = new List<string>();
                foreach (var item in array)
                    elements.Add(Serialize(item));
                return $"[{string.Join(",", elements)}]";
            }

            // Lists
            if (obj is IList list)
            {
                var elements = new List<string>();
                foreach (var item in list)
                    elements.Add(Serialize(item));
                return $"[{string.Join(",", elements)}]";
            }

            // Dictionaries
            if (obj is IDictionary dict)
            {
                var elements = new List<string>();
                foreach (DictionaryEntry entry in dict)
                    elements.Add($"{Serialize(entry.Key)}:{Serialize(entry.Value)}");
                return $"{{{string.Join(",", elements)}}}";
            }

            // Complex objects
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var members = new List<string>();

            foreach (var prop in properties)
            {
                if (prop.CanRead)
                {
                    var value = prop.GetValue(obj);
                    members.Add($"\"{prop.Name}\":{Serialize(value)}");
                }
            }

            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                members.Add($"\"{field.Name}\":{Serialize(value)}");
            }

            return $"{{{string.Join(",", members)}}}";
        }

        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "null")
                return default(T);

            var type = typeof(T);
            var obj = DeserializeValue(json.Trim(), type);
            return (T)obj;
        }

        private static object DeserializeValue(string json, Type type)
        {
            if (json == "null")
                return null;

            // Primitives
            if (type == typeof(bool))
                return bool.Parse(json);
            if (type == typeof(int))
                return int.Parse(json);
            if (type == typeof(long))
                return long.Parse(json);
            if (type == typeof(float))
                return float.Parse(json);
            if (type == typeof(double))
                return double.Parse(json);
            if (type == typeof(string))
                return UnescapeString(json.Trim('"'));

            // Arrays
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                var elements = ParseArray(json);
                var array = Array.CreateInstance(elementType, elements.Count);
                for (int i = 0; i < elements.Count; i++)
                    array.SetValue(DeserializeValue(elements[i], elementType), i);
                return array;
            }

            // Lists
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = type.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = (IList)Activator.CreateInstance(listType);
                var elements = ParseArray(json);
                foreach (var element in elements)
                    list.Add(DeserializeValue(element, elementType));
                return list;
            }

            // Dictionaries
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var keyType = type.GetGenericArguments()[0];
                var valueType = type.GetGenericArguments()[1];
                var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                var dict = (IDictionary)Activator.CreateInstance(dictType);
                var pairs = ParseObject(json);
                foreach (var pair in pairs)
                {
                    var key = DeserializeValue(pair.Key, keyType);
                    var value = DeserializeValue(pair.Value, valueType);
                    dict[key] = value;
                }
                return dict;
            }

            // Complex objects
            var obj = Activator.CreateInstance(type);
            var members = ParseObject(json);
            foreach (var member in members)
            {
                var prop = type.GetProperty(member.Key, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    var value = DeserializeValue(member.Value, prop.PropertyType);
                    prop.SetValue(obj, value);
                    continue;
                }

                var field = type.GetField(member.Key, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    var value = DeserializeValue(member.Value, field.FieldType);
                    field.SetValue(obj, value);
                }
            }

            return obj;
        }

        private static List<string> ParseArray(string json)
        {
            json = json.Trim();
            if (!json.StartsWith("[") || !json.EndsWith("]"))
                throw new FormatException("Invalid array format");

            json = json.Substring(1, json.Length - 2).Trim();
            if (string.IsNullOrEmpty(json))
                return new List<string>();

            var elements = new List<string>();
            int depth = 0;
            int start = 0;
            bool inString = false;

            for (int i = 0; i < json.Length; i++)
            {
                if (json[i] == '"' && (i == 0 || json[i - 1] != '\\'))
                    inString = !inString;
                else if (!inString)
                {
                    if (json[i] == '[' || json[i] == '{')
                        depth++;
                    else if (json[i] == ']' || json[i] == '}')
                        depth--;
                    else if (json[i] == ',' && depth == 0)
                    {
                        elements.Add(json.Substring(start, i - start).Trim());
                        start = i + 1;
                    }
                }
            }

            if (start < json.Length)
                elements.Add(json.Substring(start).Trim());

            return elements;
        }

        private static Dictionary<string, string> ParseObject(string json)
        {
            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                throw new FormatException("Invalid object format");

            json = json.Substring(1, json.Length - 2).Trim();
            if (string.IsNullOrEmpty(json))
                return new Dictionary<string, string>();

            var members = new Dictionary<string, string>();
            int depth = 0;
            int start = 0;
            bool inString = false;

            for (int i = 0; i < json.Length; i++)
            {
                if (json[i] == '"' && (i == 0 || json[i - 1] != '\\'))
                    inString = !inString;
                else if (!inString)
                {
                    if (json[i] == '[' || json[i] == '{')
                        depth++;
                    else if (json[i] == ']' || json[i] == '}')
                        depth--;
                    else if (json[i] == ',' && depth == 0)
                    {
                        ParseKeyValue(json.Substring(start, i - start).Trim(), members);
                        start = i + 1;
                    }
                }
            }

            if (start < json.Length)
                ParseKeyValue(json.Substring(start).Trim(), members);

            return members;
        }

        private static void ParseKeyValue(string pair, Dictionary<string, string> members)
        {
            int colonIndex = pair.IndexOf(':');
            if (colonIndex < 0)
                return;

            var key = pair.Substring(0, colonIndex).Trim().Trim('"');
            var value = pair.Substring(colonIndex + 1).Trim();
            members[key] = value;
        }

        private static string EscapeString(string str)
        {
            return str.Replace("\\", "\\\\")
                     .Replace("\"", "\\\"")
                     .Replace("\n", "\\n")
                     .Replace("\r", "\\r")
                     .Replace("\t", "\\t");
        }

        private static string UnescapeString(string str)
        {
            return str.Replace("\\\"", "\"")
                     .Replace("\\\\", "\\")
                     .Replace("\\n", "\n")
                     .Replace("\\r", "\r")
                     .Replace("\\t", "\t");
        }
    }
}
