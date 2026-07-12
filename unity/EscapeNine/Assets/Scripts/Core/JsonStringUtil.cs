// JsonStringUtil.cs
// AnalyticsService.cs と OnlineRankingService.cs (Runtime) に private static で重複していた
// EscapeJsonString / UnescapeJsonString を共通化した移植先。
//
// 【挙動不変】本ファイルの Escape/Unescape の中身は、移植元の実装を一字一句そのまま移した
// ものであり、ロジックは一切変更していない (メソッド名のみ変更)。

using System.Globalization;
using System.Text;

namespace EscapeNine.Core
{
    /// <summary>Firestore/PostHog REST を手動で組み立て/解析するための JSON 文字列エスケープ処理。</summary>
    public static class JsonStringUtil
    {
        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;

            var sb = new StringBuilder(s.Length);
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
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        public static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case 'n': sb.Append('\n'); i++; break;
                        case 'r': sb.Append('\r'); i++; break;
                        case 't': sb.Append('\t'); i++; break;
                        default: sb.Append(c); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
