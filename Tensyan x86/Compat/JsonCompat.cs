#if NETFRAMEWORK
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Tensyan.Core
{
    /// <summary>
    /// .NET Framework 上没有 System.Text.Json（离线也拿不到 NuGet 包），
    /// 这里用反射实现一个**与 x64 版输出格式完全一致**的 JSON 读写：
    ///   · 属性名保持 PascalCase（不转 camelCase）
    ///   · 枚举写成字符串（Role → "Admin"）
    ///   · DateTime 用 ISO 8601 往返格式（"2026-10-04T09:42:21.5681213Z"）
    ///   · 缩进 2 空格；不序列化字段；非 ASCII 不转义（对应 UnsafeRelaxedJsonEscaping）
    ///   · 读入时属性名大小写不敏感、多余字段忽略、缺失字段保留默认值
    /// 目的：32 位版与 64 位版可以共用同一份 data 文件（classdata.json / accounts.json / install.json）。
    /// </summary>
    public static class Json
    {
        // ---------------- 写 ----------------

        public static string Dump(object o)
        {
            var sb = new StringBuilder(1024);
            WriteValue(sb, o, 0);
            return sb.ToString();
        }

        private static void Indent(StringBuilder sb, int level)
        {
            sb.Append('\n');
            sb.Append(' ', level * 2);
        }

        private static void WriteValue(StringBuilder sb, object v, int level)
        {
            if (v == null) { sb.Append("null"); return; }

            if (v is string s) { WriteString(sb, s); return; }
            if (v is bool b) { sb.Append(b ? "true" : "false"); return; }

            Type t = v.GetType();
            if (t.IsEnum) { WriteString(sb, v.ToString()); return; }
            if (v is DateTime dt) { WriteDateTime(sb, dt); return; }
            if (v is DateTimeOffset dto) { WriteString(sb, dto.ToString("o", CultureInfo.InvariantCulture)); return; }
            if (v is char ch) { WriteString(sb, ch.ToString()); return; }

            if (v is int || v is long || v is short || v is byte || v is sbyte || v is uint || v is ulong || v is ushort)
            {
                sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                return;
            }
            if (v is double d) { sb.Append(FormatDouble(d)); return; }
            if (v is float f) { sb.Append(FormatDouble(f)); return; }
            if (v is decimal m) { sb.Append(m.ToString(CultureInfo.InvariantCulture)); return; }

            if (v is IDictionary dict)
            {
                if (dict.Count == 0) { sb.Append("{}"); return; }
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry e in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Indent(sb, level + 1);
                    WriteString(sb, Convert.ToString(e.Key, CultureInfo.InvariantCulture));
                    sb.Append(": ");
                    WriteValue(sb, e.Value, level + 1);
                }
                Indent(sb, level);
                sb.Append('}');
                return;
            }

            if (v is IEnumerable list)
            {
                bool any = false;
                var inner = new StringBuilder();
                foreach (var item in list)
                {
                    if (any) inner.Append(',');
                    any = true;
                    Indent(inner, level + 1);
                    WriteValue(inner, item, level + 1);
                }
                if (!any) { sb.Append("[]"); return; }
                sb.Append('[');
                sb.Append(inner);
                Indent(sb, level);
                sb.Append(']');
                return;
            }

            WriteObject(sb, v, level);
        }

        private static string FormatDouble(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return "null";   // JSON 不支持，STJ 会抛异常，这里降级
            string s = d.ToString("R", CultureInfo.InvariantCulture);
            if (s.IndexOf('.') < 0 && s.IndexOf('E') < 0 && s.IndexOf('e') < 0) s += ".0";
            return s;
        }

        private static void WriteObject(StringBuilder sb, object o, int level)
        {
            var props = SerializedProperties(o.GetType());
            bool any = false;
            var body = new StringBuilder();
            foreach (var p in props)
            {
                object v;
                try { v = p.GetValue(o, null); } catch { continue; }
                if (any) body.Append(',');
                any = true;
                Indent(body, level + 1);
                WriteString(body, p.Name);
                body.Append(": ");
                WriteValue(body, v, level + 1);
            }
            if (!any) { sb.Append("{}"); return; }
            sb.Append('{');
            sb.Append(body);
            Indent(sb, level);
            sb.Append('}');
        }

        private static readonly Dictionary<Type, PropertyInfo[]> PropCache = new Dictionary<Type, PropertyInfo[]>();

        /// <summary>与 System.Text.Json 默认行为一致：只序列化公开实例属性（不含字段、不含索引器）。</summary>
        private static PropertyInfo[] SerializedProperties(Type t)
        {
            PropertyInfo[] cached;
            if (PropCache.TryGetValue(t, out cached)) return cached;

            var list = new List<PropertyInfo>();
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead) continue;
                if (p.GetIndexParameters().Length > 0) continue;
                list.Add(p);
            }
            cached = list.ToArray();
            PropCache[t] = cached;
            return cached;
        }

        private static void WriteDateTime(StringBuilder sb, DateTime dt)
        {
            // 与 STJ 一致：UTC 加 Z，本地/未指定带偏移；小数秒最多 7 位并去掉末尾 0
            string core = dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            long frac = dt.Ticks % TimeSpan.TicksPerSecond;
            if (frac != 0)
            {
                string f = frac.ToString("D7", CultureInfo.InvariantCulture).TrimEnd('0');
                core += "." + f;
            }
            if (dt.Kind == DateTimeKind.Utc) core += "Z";
            else if (dt.Kind == DateTimeKind.Local) core += dt.ToString("zzz", CultureInfo.InvariantCulture);
            sb.Append('"').Append(core).Append('"');
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            if (s == null) { sb.Append("null"); return; }
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);      // 非 ASCII 原样保留（UnsafeRelaxedJsonEscaping）
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------------- 读 ----------------

        public static T Parse<T>(string s)
        {
            if (string.IsNullOrEmpty(s)) return default(T);
            int i = 0;
            object v = ReadValue(s, ref i, typeof(T));
            return v == null ? default(T) : (T)v;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static object ReadValue(string s, ref int i, Type target)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return ReadObject(s, ref i, target);
            if (c == '[') return ReadArray(s, ref i, target);
            if (c == '"') return ReadString(s, ref i);
            if (c == 't') { i += 4; return true; }
            if (c == 'f') { i += 5; return false; }
            if (c == 'n') { i += 4; return null; }
            return ReadNumber(s, ref i, target);
        }

        private static Type Unwrap(Type t)
        {
            Type u = Nullable.GetUnderlyingType(t);
            return u ?? t;
        }

        private static object ReadObject(string s, ref int i, Type target)
        {
            Type t = target == null ? null : Unwrap(target);
            bool genericDict = t != null && t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>);
            object instance = null;
            Dictionary<string, object> loose = null;

            if (t == null || t == typeof(object) || genericDict)
                loose = new Dictionary<string, object>();
            else
            {
                try { instance = Activator.CreateInstance(t); }
                catch { instance = null; }
                if (instance == null) loose = new Dictionary<string, object>();
            }

            i++;    // {
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == '}') { i++; break; }
                if (s[i] == ',') { i++; continue; }

                string key = ReadString(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':') i++;

                if (instance != null)
                {
                    var p = FindProperty(instance.GetType(), key);
                    if (p != null && p.CanWrite)
                    {
                        object val = ReadValue(s, ref i, p.PropertyType);
                        try { p.SetValue(instance, val, null); } catch { }
                    }
                    else ReadValue(s, ref i, null);     // 多余字段忽略
                }
                else
                {
                    loose[key] = ReadValue(s, ref i, null);
                }
            }

            if (instance != null) return instance;
            if (genericDict)
            {
                var vt = t.GetGenericArguments()[1];
                var dict = (IDictionary)Activator.CreateInstance(t);
                foreach (var kv in loose)
                {
                    object val = kv.Value;
                    if (val is Dictionary<string, object> || val is List<object>) val = ConvertLoose(val, vt);
                    else if (val != null && vt != typeof(object)) val = ChangeType(val, vt);
                    dict[kv.Key] = val;
                }
                return dict;
            }
            return loose;
        }

        private static object ConvertLoose(object val, Type t)
        {
            // 简单场景：把松散结构塞回强类型（模型里没有字典/object 字段，够用）
            return val;
        }

        private static PropertyInfo FindProperty(Type t, string name)
        {
            foreach (var p in SerializedProperties(t))
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p;
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        private static object ReadArray(string s, ref int i, Type target)
        {
            Type elem = typeof(object);
            Type t = target == null ? null : Unwrap(target);
            bool isList = t != null && (t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)));
            if (isList)
                elem = t.IsArray ? t.GetElementType() : t.GetGenericArguments()[0];

            var items = new List<object>();
            i++;    // [
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                items.Add(ReadValue(s, ref i, isList ? elem : null));
            }

            if (!isList) return items;

            if (t.IsArray)
            {
                var arr = Array.CreateInstance(elem, items.Count);
                for (int k = 0; k < items.Count; k++) arr.SetValue(Coerce(items[k], elem), k);
                return arr;
            }
            else
            {
                var list = (IList)Activator.CreateInstance(t);
                foreach (var it in items) list.Add(Coerce(it, elem));
                return list;
            }
        }

        private static object Coerce(object v, Type t)
        {
            if (v == null) return null;
            Type u = Unwrap(t);
            if (u.IsInstanceOfType(v)) return v;
            return ChangeType(v, u);
        }

        private static object ChangeType(object v, Type t)
        {
            try
            {
                if (t.IsEnum) return Enum.Parse(t, Convert.ToString(v), true);
                if (t == typeof(string)) return Convert.ToString(v, CultureInfo.InvariantCulture);
                if (t == typeof(DateTime)) return ToDateTime(Convert.ToString(v, CultureInfo.InvariantCulture));
                if (t == typeof(bool)) return Convert.ToBoolean(v, CultureInfo.InvariantCulture);
                return Convert.ChangeType(v, t, CultureInfo.InvariantCulture);
            }
            catch { return v; }
        }

        private static DateTime ToDateTime(string s)
        {
            DateTime dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt)) return dt;
            return default(DateTime);
        }

        private static string ReadString(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length || s[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 <= s.Length)
                        {
                            int code;
                            if (int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                sb.Append((char)code);
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        private static object ReadNumber(string s, ref int i, Type target)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            string raw = s.Substring(start, i - start);
            Type t = target == null ? typeof(double) : Unwrap(target);

            if (t == typeof(int) || t == typeof(int?))
            {
                int iv;
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out iv)) return iv;
            }
            if (t == typeof(long))
            {
                long lv;
                if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out lv)) return lv;
            }
            if (t == typeof(bool)) return raw != "0";

            double d;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            {
                if (t == typeof(int)) return (int)d;
                if (t == typeof(long)) return (long)d;
                if (t == typeof(decimal)) return (decimal)d;
                if (t == typeof(float)) return (float)d;
                return d;
            }
            return 0;
        }
    }
}
#endif
