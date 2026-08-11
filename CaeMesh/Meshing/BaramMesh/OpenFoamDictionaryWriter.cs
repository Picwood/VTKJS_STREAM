// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CaeMesh.Meshing.BaramMesh
{
    public class OpenFoamDictionaryWriter : IBaramDictionaryWriter
    {
        private const string Version = "2.0";

        public string BuildDictionaryText(string objectName, string location, IDictionary<string, object> data)
        {
            return BuildDictionaryText(objectName, location, (IEnumerable<KeyValuePair<string, object>>)data);
        }

        public string BuildDictionaryText(string objectName, string location, IEnumerable<KeyValuePair<string, object>> data)
        {
            return BuildDictionaryText(objectName, location, "dictionary", data);
        }

        public string BuildDictionaryText(string objectName, string location, string className,
                                          IEnumerable<KeyValuePair<string, object>> data)
        {
            if (string.IsNullOrWhiteSpace(objectName)) throw new ArgumentException("Object name is required.", "objectName");
            if (string.IsNullOrWhiteSpace(className)) throw new ArgumentException("Class name is required.", "className");
            if (data == null) throw new ArgumentNullException("data");

            StringBuilder sb = new StringBuilder();
            WriteHeader(sb, objectName, location, className);
            WriteDictionaryBody(sb, data, 0);
            return sb.ToString();
        }

        public void WriteDictionary(string fileName, string objectName, string location, IDictionary<string, object> data)
        {
            WriteDictionary(fileName, objectName, location, (IEnumerable<KeyValuePair<string, object>>)data);
        }

        public void WriteDictionary(string fileName, string objectName, string location,
                                    IEnumerable<KeyValuePair<string, object>> data)
        {
            WriteDictionary(fileName, objectName, location, "dictionary", data);
        }

        public void WriteDictionary(string fileName, string objectName, string location, string className,
                                    IEnumerable<KeyValuePair<string, object>> data)
        {
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("File name is required.", "fileName");
            string directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fileName, BuildDictionaryText(objectName, location, className, data), Encoding.ASCII);
        }

        private static void WriteHeader(StringBuilder sb, string objectName, string location, string className)
        {
            sb.AppendLine("FoamFile");
            sb.AppendLine("{");
            WriteEntry(sb, 1, "version", Version);
            WriteEntry(sb, 1, "format", "ascii");
            WriteEntry(sb, 1, "class", className);
            WriteEntry(sb, 1, "location", Quote(location ?? ""));
            WriteEntry(sb, 1, "object", objectName);
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private static void WriteDictionaryBody(StringBuilder sb, IEnumerable<KeyValuePair<string, object>> data, int indent)
        {
            foreach (KeyValuePair<string, object> entry in data)
            {
                WriteEntry(sb, indent, entry.Key, entry.Value);
            }
        }

        private static void WriteEntry(StringBuilder sb, int indent, string key, object value)
        {
            string pad = new string(' ', indent * 4);
            IEnumerable<KeyValuePair<string, object>> dictionary = value as IEnumerable<KeyValuePair<string, object>>;
            if (dictionary != null)
            {
                sb.Append(pad).AppendLine(key);
                sb.Append(pad).AppendLine("{");
                WriteDictionaryBody(sb, dictionary, indent + 1);
                sb.Append(pad).AppendLine("}");
                return;
            }

            sb.Append(pad).Append(key).Append(" ").Append(FormatValue(value, indent)).AppendLine(";");
        }

        private static string FormatValue(object value, int indent)
        {
            if (value == null) return "null";
            if (value is OpenFoamRawValue) return ((OpenFoamRawValue)value).Text;
            if (value is string) return (string)value;
            if (value is bool) return (bool)value ? "true" : "false";
            if (value is double) return ((double)value).ToString("G17", CultureInfo.InvariantCulture);
            if (value is float) return ((float)value).ToString("G9", CultureInfo.InvariantCulture);
            if (value is decimal) return ((decimal)value).ToString(CultureInfo.InvariantCulture);
            if (value is IFormattable && IsNumeric(value)) return ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                return FormatList(enumerable, indent);
            }

            return value.ToString();
        }

        private static string FormatList(IEnumerable values, int indent)
        {
            List<string> items = new List<string>();
            foreach (object value in values)
            {
                IEnumerable nested = value as IEnumerable;
                if (nested != null && !(value is string))
                {
                    items.Add(FormatList(nested, indent + 1));
                }
                else
                {
                    items.Add(FormatValue(value, indent + 1));
                }
            }

            return "(" + string.Join(" ", items.ToArray()) + ")";
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static bool IsNumeric(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort ||
                   value is int || value is uint || value is long || value is ulong;
        }
    }

    public class OpenFoamDictionary : List<KeyValuePair<string, object>>
    {
        public OpenFoamDictionary AddEntry(string key, object value)
        {
            Add(new KeyValuePair<string, object>(key, value));
            return this;
        }
    }

    public class OpenFoamRawValue
    {
        public string Text { get; private set; }

        public OpenFoamRawValue(string text)
        {
            Text = text ?? "";
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
