using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace RPGFramework.Localization
{
    /// <summary>
    /// Puts a <see cref="LocalizedText"/> on a wire and back, so a host's refusal, prompt or log line reaches every
    /// player as a text to translate in their own language. In bytes (<see cref="Write"/> / <see cref="Read"/>) or as
    /// one string (<see cref="ToWire"/> / <see cref="FromWire"/>) for messages that carry strings. Values keep their
    /// kind: whole numbers (for plurals; back as int when they fit, long otherwise), fractions (as double),
    /// true / false, strings and nested texts; anything else goes as its invariant string. A string that was not encoded reads as a text whose key is that string, so a plain
    /// sentence still shows as written.
    /// </summary>
    public static class LocalizedTextCodec
    {
        // Starts every encoded string: a control character no sentence begins with
        private const char Marker = '\u001F';
        private const byte Null = 0, Text = 1, Whole = 2, Fraction = 3, True = 4, False = 5, Nested = 6;
        private const int MaxDepth = 8;

        public static void Write(BinaryWriter writer, LocalizedText text)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            Write(writer, text, 0);
        }

        public static LocalizedText Read(BinaryReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            return Read(reader, 0);
        }

        /// <summary>
        /// The text as one string: plain when it is a key alone that does not look encoded, encoded otherwise.
        /// </summary>
        public static string ToWire(LocalizedText text)
        {
            if (text.Args.Count == 0 && (text.Key.Length == 0 || text.Key[0] != Marker))
                return text.Key;
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                    Write(writer, text, 0);
                return Marker + Convert.ToBase64String(stream.ToArray());
            }
        }

        /// <summary>
        /// A string from <see cref="ToWire"/> back as the text; any other string (null is empty) as a text whose key
        /// is the string. A damaged encoding reads as that literal too.
        /// </summary>
        public static LocalizedText FromWire(string wire)
        {
            if (string.IsNullOrEmpty(wire))
                return new LocalizedText("");
            if (wire[0] != Marker)
                return new LocalizedText(wire);
            try
            {
                using (var stream = new MemoryStream(Convert.FromBase64String(wire.Substring(1))))
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8))
                    return Read(reader, 0);
            }
            catch (Exception e) when (e is FormatException || e is EndOfStreamException || e is IOException)
            {
                return new LocalizedText(wire);
            }
        }

        /// <summary>
        /// Whether a string is an encoded text (not a plain one).
        /// </summary>
        public static bool IsEncoded(string wire) => !string.IsNullOrEmpty(wire) && wire[0] == Marker;

        private static void Write(BinaryWriter writer, LocalizedText text, int depth)
        {
            writer.Write(text.Key ?? "");
            var args = text.Args;
            writer.Write(args.Count);
            foreach (var arg in args)
            {
                writer.Write(arg.Key ?? "");
                WriteValue(writer, arg.Value, depth);
            }
        }

        private static void WriteValue(BinaryWriter writer, object value, int depth)
        {
            switch (value)
            {
                case null:
                    writer.Write(Null);
                    break;
                case string text:
                    writer.Write(Text);
                    writer.Write(text);
                    break;
                case bool flag:
                    writer.Write(flag ? True : False);
                    break;
                case LocalizedText nested when depth < MaxDepth:
                    writer.Write(Nested);
                    Write(writer, nested, depth + 1);
                    break;
                case int _:
                case long _:
                case short _:
                case byte _:
                case sbyte _:
                case uint _:
                case ushort _:
                    writer.Write(Whole);
                    writer.Write(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    break;
                case float _:
                case double _:
                case decimal _:
                    writer.Write(Fraction);
                    writer.Write(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                    break;
                case IFormattable formattable:
                    writer.Write(Text);
                    writer.Write(formattable.ToString(null, CultureInfo.InvariantCulture));
                    break;
                default:
                    writer.Write(Text);
                    writer.Write(value.ToString() ?? "");
                    break;
            }
        }

        private static LocalizedText Read(BinaryReader reader, int depth)
        {
            string key = reader.ReadString();
            int count = reader.ReadInt32();
            if (count < 0 || count > 1024)
                throw new IOException("Not an encoded text");
            var args = new (string Name, object Value)[count];
            for (int i = 0; i < count; i++)
                args[i] = (reader.ReadString(), ReadValue(reader, depth));
            return new LocalizedText(key, args);
        }

        private static object ReadValue(BinaryReader reader, int depth)
        {
            switch (reader.ReadByte())
            {
                case Null:
                    return null;
                case Text:
                    return reader.ReadString();
                case Whole:
                    long whole = reader.ReadInt64();
                    return whole >= int.MinValue && whole <= int.MaxValue ? (object)(int)whole : whole;
                case Fraction:
                    return reader.ReadDouble();
                case True:
                    return true;
                case False:
                    return false;
                case Nested when depth < MaxDepth:
                    return Read(reader, depth + 1);
                default:
                    throw new IOException("Not an encoded text");
            }
        }
    }
}
