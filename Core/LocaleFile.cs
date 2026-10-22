using System.Collections.Generic;
using System.Text;

namespace HARTModPack.Core
{
    // The status lines on the Mods page change at runtime, but the page shows localisation keys only.
    // The mod writes the current lines as a small locale table and registers it with the game.
    public static class LocaleFile
    {
        public const string KeyPrefix = "hartmodpack_status_";

        public static string Key(int index) { return KeyPrefix + index; }

        public static string Build(IList<string> lines)
        {
            var sb = new StringBuilder("{\n");
            for (int i = 0; i < lines.Count; i++)
            {
                sb.Append("  \"").Append(Key(i)).Append("\": \"").Append(Escape(lines[i])).Append('"');
                sb.Append(i + 1 < lines.Count ? ",\n" : "\n");
            }
            return sb.Append("}\n").ToString();
        }

        public static string Escape(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text ?? "")
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
            return sb.ToString();
        }
    }
}
