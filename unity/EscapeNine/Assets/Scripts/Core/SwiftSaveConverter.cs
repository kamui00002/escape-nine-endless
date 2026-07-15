// SwiftSaveConverter.cs
// Swift 版セーブデータの一回限り移行 (B-1) の純ロジック部分。
// UnityEngine 非依存 (noEngineReferences の Core)。PlayerPrefs / ネイティブブリッジの
// 呼び出しは Runtime/SwiftSaveMigration.cs が行い、ここでは文字列変換のみを担当する。
// 壊れた入力・未知の値は例外を投げずスキップ/空リストにする (クラッシュ禁止)。

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EscapeNine.Core
{
    public static class SwiftSaveConverter
    {
        // Swift: Models/Character.swift の rawValue (Core/GameEnums.cs の RawValue() と 1:1)
        private static readonly HashSet<string> KnownCharacterRawValues = new HashSet<string>
        {
            "hero", "thief", "wizard", "elf", "knight"
        };

        // Swift: Models/Achievement.swift の rawValue (日本語) → Core/Achievement.cs の enum 名。
        // 両ファイルを実読した上での対応表 (Achievement.swift:11-20 / Achievement.cs:9-20)。
        private static readonly Dictionary<string, string> AchievementRawValueToEnumName = new Dictionary<string, string>
        {
            { "初勝利", "FirstWin" },
            { "階層10到達", "Floor10" },
            { "階層25到達", "Floor25" },
            { "階層50到達", "Floor50" },
            { "階層75到達", "Floor75" },
            { "階層100到達", "Floor100" },
            { "素手の達人", "NoSkillWin" },
            { "スピードランナー", "SpeedRunner" },
            { "生存者", "Survivor" },
        };

        // Swift: StoreKitService.swift の ProductID.allCases (com.escapenine.endless.* の productId)
        private const string KnownProductIdPrefix = "com.escapenine.";

        /// <summary>
        /// `["a","b"]` 形式 (フラットな文字列配列のみ) の JSON を List&lt;string&gt; に変換する。
        /// エスケープは JsonStringUtil.Unescape を利用。壊れた入力・null は空リストを返す
        /// (クラッシュ禁止)。
        /// </summary>
        public static List<string> ParseJsonStringArray(string json)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(json)) return result;

            string trimmed = json.Trim();
            if (trimmed.Length < 2 || trimmed[0] != '[' || trimmed[trimmed.Length - 1] != ']')
            {
                return new List<string>();
            }

            string inner = trimmed.Substring(1, trimmed.Length - 2);
            int i = 0;
            int n = inner.Length;

            while (i < n)
            {
                while (i < n && IsJsonWhitespaceOrComma(inner[i])) i++;
                if (i >= n) break;

                if (inner[i] != '"')
                {
                    // フラットな文字列配列以外 (数値・入れ子オブジェクト等) は非対応 → 空リスト
                    return new List<string>();
                }
                i++; // 開き引用符をスキップ

                var sb = new StringBuilder();
                bool closed = false;
                while (i < n)
                {
                    char c = inner[i];
                    if (c == '\\' && i + 1 < n)
                    {
                        sb.Append(c);
                        sb.Append(inner[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (c == '"')
                    {
                        closed = true;
                        i++;
                        break;
                    }
                    sb.Append(c);
                    i++;
                }
                if (!closed) return new List<string>(); // 閉じ引用符が無い壊れた入力

                result.Add(JsonStringUtil.Unescape(sb.ToString()));

                while (i < n && IsJsonWhitespace(inner[i])) i++;
            }

            return result;
        }

        private static bool IsJsonWhitespace(char c) => c == ' ' || c == '\t' || c == '\n' || c == '\r';
        private static bool IsJsonWhitespaceOrComma(char c) => IsJsonWhitespace(c) || c == ',';

        /// <summary>
        /// Swift の日本語 Achievement rawValue のリストを、Core/Achievement.cs の enum 名 CSV に変換する。
        /// 未知の値はスキップする。null は空文字列を返す。
        /// </summary>
        public static string MapAchievementsToEnumCsv(List<string> japaneseRawValues)
        {
            if (japaneseRawValues == null || japaneseRawValues.Count == 0) return string.Empty;

            var mapped = new List<string>();
            foreach (var raw in japaneseRawValues)
            {
                if (raw != null && AchievementRawValueToEnumName.TryGetValue(raw, out var enumName)
                    && !mapped.Contains(enumName))
                {
                    mapped.Add(enumName);
                }
            }
            return string.Join(",", mapped);
        }

        /// <summary>
        /// キャラクター rawValue の CSV (ネイティブ NSArray&lt;NSString&gt; をカンマ join したもの) から、
        /// 既知の rawValue (hero/thief/wizard/elf/knight) のみを残した CSV を返す。null/空は空文字列。
        /// </summary>
        public static string NormalizeCharactersCsv(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return string.Empty;

            var tokens = csv.Split(',')
                .Select(t => t.Trim())
                .Where(t => KnownCharacterRawValues.Contains(t))
                .Distinct()
                .ToList();
            return string.Join(",", tokens);
        }

        /// <summary>
        /// Keychain の JSON 配列を ParseJsonStringArray した結果から、
        /// 既知の productId 形式 (com.escapenine.*) のみを残した CSV を返す。null/空は空文字列。
        /// </summary>
        public static string NormalizeProductIdsCsv(List<string> productIds)
        {
            if (productIds == null || productIds.Count == 0) return string.Empty;

            var tokens = productIds
                .Where(id => !string.IsNullOrEmpty(id) && id.StartsWith(KnownProductIdPrefix))
                .Distinct()
                .ToList();
            return string.Join(",", tokens);
        }
    }
}
