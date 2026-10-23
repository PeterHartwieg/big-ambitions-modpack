using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HARTModPack.Core
{
    // Only plain values cross this boundary. No references to live lists escape a capture.
    public static class CompanionSnapshot
    {
        public const string Key = "hartmodpack:companion";
        public const int MaxBytes = 4 * 1024 * 1024, MaxRows = 10000;
        public static Dictionary<string, object> Row(params object[] pairs)
        {
            var row = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < pairs.Length; i += 2) row.Add((string)pairs[i], pairs[i + 1]);
            return row;
        }
        public static Dictionary<string, object> Module(string status, object data = null, string reason = null)
        {
            if (!new[] { "ready", "disabled", "unavailable", "failed" }.Contains(status) || (status != "ready" && data != null))
                throw new ArgumentException("Invalid companion module");
            return Row("version", 1, "status", status, "data", data, "reason", reason);
        }
        public static Dictionary<string, object> Feature(bool requested, bool? effective, string state, string reason = null)
        { return Row("requested", requested, "effective", effective, "state", state, "reason", reason); }
        public static string Capture(string version, object captured, IDictionary<string, Func<Dictionary<string, object>>> readers)
        {
            var modules = Row();
            foreach (var key in readers.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                Dictionary<string, object> module;
                try { module = readers[key](); Encode(module, 2); }
                catch (Exception) { module = Module("failed", null, "capture-failed"); }
                modules.Add(key, module);
            }
            return Json(Row("schemaVersion", 1, "provider", "hart-mod-pack", "modVersion", version, "captured", captured, "modules", modules));
        }
        public static string Json(object value) { return Encode(value, 0); }
        static string Encode(object value, int depth)
        {
            var text = new StringBuilder(); Write(text, value, depth);
            var result = text.ToString();
            if (Encoding.UTF8.GetByteCount(result) > MaxBytes) throw new ArgumentException("Report too large");
            return result;
        }
        static void Write(StringBuilder text, object value, int depth)
        {
            if (depth > 16 || text.Length > MaxBytes) throw new ArgumentException("Report too large or deep");
            if (value == null) { text.Append("null"); return; }
            if (value is string word)
            {
                if (word.Length < 1 || word.Length > 1024) throw new ArgumentException("Invalid report text");
                text.Append('"');
                foreach (char c in word)
                    if (c == '"' || c == '\\') text.Append('\\').Append(c);
                    else if (c < 32 || char.IsSurrogate(c)) text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else text.Append(c);
                text.Append('"'); return;
            }
            if (value is bool flag) { text.Append(flag ? "true" : "false"); return; }
            if (value is int || value is long)
            {
                long n = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (n < -9007199254740991L || n > 9007199254740991L) throw new ArgumentException("Integer overflow");
                text.Append(n.ToString(CultureInfo.InvariantCulture)); return;
            }
            if (value is IDictionary<string, object> row)
            {
                text.Append('{'); bool first = true;
                if (row.Count > MaxRows) throw new ArgumentException("Too many fields");
                foreach (var key in row.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    if (!first) text.Append(','); first = false;
                    Write(text, key, depth + 1); text.Append(':'); Write(text, row[key], depth + 1);
                }
                text.Append('}'); return;
            }
            if (value is IEnumerable rows)
            {
                text.Append('['); int count = 0;
                foreach (var entry in rows)
                {
                    if (++count > MaxRows) throw new ArgumentException("Too many rows");
                    if (count > 1) text.Append(','); Write(text, entry, depth + 1);
                }
                text.Append(']'); return;
            }
            throw new ArgumentException("Unsupported report value");
        }
        // The only shared helper allowed to change a save: the separate normal-save publisher calls it.
        public static void Publish(IDictionary<string, string> data, string report)
        {
            if (data == null) return;
            if (report == null) data.Remove(Key); else data[Key] = report;
        }
    }
}
