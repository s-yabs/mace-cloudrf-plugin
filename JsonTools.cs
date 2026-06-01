using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace CloudRFPlugin
{
    internal static class JsonTools
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 * 8 };

        public static Dictionary<string, object> DeserializeObject(string json)
        {
            return Serializer.Deserialize<Dictionary<string, object>>(json);
        }

        public static string SerializeObject(object value)
        {
            return Serializer.Serialize(value);
        }

        public static string PrettyPrint(string json)
        {
            object parsed = Serializer.DeserializeObject(json);
            return FormatValue(parsed, 0);
        }

        public static Dictionary<string, object> GetObject(Dictionary<string, object> root, string key)
        {
            if (!root.TryGetValue(key, out object value) || !(value is Dictionary<string, object> dictionary))
            {
                dictionary = new Dictionary<string, object>();
                root[key] = dictionary;
            }

            return dictionary;
        }

        public static string GetString(Dictionary<string, object> root, string key, string fallback = "")
        {
            return root.TryGetValue(key, out object value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : fallback;
        }

        private static string FormatValue(object value, int indent)
        {
            if (value is Dictionary<string, object> dictionary)
            {
                return FormatDictionary(dictionary, indent);
            }

            if (value is ArrayList list)
            {
                return FormatArray(list, indent);
            }

            return Serializer.Serialize(value);
        }

        private static string FormatDictionary(Dictionary<string, object> dictionary, int indent)
        {
            string pad = new string(' ', indent);
            string childPad = new string(' ', indent + 2);
            var lines = new List<string> { "{" };
            int index = 0;

            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                string comma = ++index == dictionary.Count ? "" : ",";
                lines.Add(childPad + Serializer.Serialize(pair.Key) + ": " + FormatValue(pair.Value, indent + 2) + comma);
            }

            lines.Add(pad + "}");
            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatArray(ArrayList list, int indent)
        {
            string pad = new string(' ', indent);
            string childPad = new string(' ', indent + 2);
            var lines = new List<string> { "[" };

            for (int i = 0; i < list.Count; i++)
            {
                string comma = i == list.Count - 1 ? "" : ",";
                lines.Add(childPad + FormatValue(list[i], indent + 2) + comma);
            }

            lines.Add(pad + "]");
            return string.Join(Environment.NewLine, lines);
        }
    }
}
