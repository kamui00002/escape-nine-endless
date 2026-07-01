// SeededRng.cs
// Swift 正本からの忠実移植: Services/DailyChallengeService.swift (private struct SeededRNG)
// デイリーチャレンジのシード生成に使う簡易 LCG。日付文字列から決定論的に条件を導く。
//
// Swift:
//   state = (state &* 1664525 &+ 1013904223) & 0x7fffffff
//   return abs(state)
// state は毎回 0x7fffffff でマスクされ [0, 2^31-1] に収まるため、long 演算で桁溢れなく再現できる。

namespace EscapeNine.Core
{
    public sealed class SeededRng
    {
        private long _state;

        public SeededRng(long seed)
        {
            _state = seed;
        }

        /// <summary>Swift の SeededRNG.nextInt() と同一の系列を返す。</summary>
        public int NextInt()
        {
            _state = (_state * 1664525L + 1013904223L) & 0x7fffffffL;
            return (int)_state; // マスク済みのため常に非負 (Swift の abs 相当)
        }

        /// <summary>日付文字列 (例 "2026-03-17") から Swift と同じシードを生成 (Unicode scalar 値の総和)。</summary>
        public static long SeedFromDateString(string dateString)
        {
            long sum = 0;
            foreach (char c in dateString)
            {
                sum += c; // ASCII 日付文字列では Swift の unicodeScalars と一致
            }
            return sum;
        }
    }
}
