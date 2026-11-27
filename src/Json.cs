using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#if NET8_0_OR_GREATER
using System.Text.Json;
#else
using System.Web.Script.Serialization;
#endif

namespace AgyPet
{
    static class Json
    {
        public static Dictionary<string, object> ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
#if NET8_0_OR_GREATER
            return Parse(json) as Dictionary<string, object>;
#else
            var js = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
            return js.DeserializeObject(json) as Dictionary<string, object>;
#endif
        }

        public static object Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
#if NET8_0_OR_GREATER
            using (var doc = JsonDocument.Parse(json))
                return ToClrObject(doc.RootElement);
#else
            var js = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
            return js.DeserializeObject(json);
#endif
        }

#if NET8_0_OR_GREATER
        static object ToClrObject(JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    var d = new Dictionary<string, object>();
                    foreach (var p in el.EnumerateObject())
                        d[p.Name] = ToClrObject(p.Value);
                    return d;
                case JsonValueKind.Array:
                    var arr = new List<object>();
                    foreach (var item in el.EnumerateArray())
                        arr.Add(ToClrObject(item));
                    return arr.ToArray();
                case JsonValueKind.String:
                    return el.GetString();
                case JsonValueKind.Number:
                    long lv;
                    if (el.TryGetInt64(out lv)) return lv;
                    return el.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return null;
            }
        }
#endif

        public static string Pretty(string compactJson)
        {
#if NET8_0_OR_GREATER
            return Write(Parse(compactJson));
#else
            var js = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
            return Write(js.DeserializeObject(compactJson));
#endif
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object v, int depth)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is int || v is long || v is decimal || v is double || v is float || v is short || v is byte)
            {
                sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                return;
            }

            var dict = v as IDictionary<string, object>;
            if (dict != null)
            {
                if (dict.Count == 0) { sb.Append("{}"); return; }
                sb.Append("{\n");
                int i = 0;
                foreach (var kv in dict)
                {
                    sb.Append(' ', (depth + 1) * 2);
                    WriteString(sb, kv.Key);
                    sb.Append(": ");
                    WriteValue(sb, kv.Value, depth + 1);
                    if (++i < dict.Count) sb.Append(',');
                    sb.Append('\n');
                }
                sb.Append(' ', depth * 2);
                sb.Append('}');
                return;
            }

            var list = v as IEnumerable;
            if (list != null)
            {
                var items = new List<object>();
                foreach (var o in list) items.Add(o);
                if (items.Count == 0) { sb.Append("[]"); return; }
                sb.Append("[\n");
                for (int i = 0; i < items.Count; i++)
                {
                    sb.Append(' ', (depth + 1) * 2);
                    WriteValue(sb, items[i], depth + 1);
                    if (i < items.Count - 1) sb.Append(',');
                    sb.Append('\n');
                }
                sb.Append(' ', depth * 2);
                sb.Append(']');
                return;
            }

            WriteString(sb, v.ToString());
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        public static string Str(IDictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) && v != null
                ? Convert.ToString(v, CultureInfo.InvariantCulture)
                : "";
        }

        public static Dictionary<string, object> Obj(IDictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
        }
    }
}
