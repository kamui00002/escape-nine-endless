// TimingGrade.cs
// Swift 正本からの忠実移植: Services/BeatEngine.swift (enum TimingGrade / timingGrade())
// コンボシステム用のタイミング判定。閾値は GameConfig(=Constants) の combo 比率。

using System;

namespace EscapeNine.Core
{
    /// <summary>移動タイミングの精度3段階。Swift: enum TimingGrade</summary>
    public enum TimingGrade
    {
        Just, // ±20%以内：完璧
        Good, // ±35%以内：良い
        Miss  // 範囲外
    }

    public static class ComboTiming
    {
        /// <summary>
        /// 拍内の位相比率 (0=拍ジャスト, 0.5=拍間中央) から判定。Swift: BeatEngine.timingGrade()
        /// ratio = min(前の拍からの経過, 次の拍までの残り) / 拍間隔。
        /// </summary>
        public static TimingGrade Grade(double phaseRatio)
        {
            if (phaseRatio <= GameConfig.ComboJustTimingRatio) return TimingGrade.Just;
            if (phaseRatio <= GameConfig.ComboGoodTimingRatio) return TimingGrade.Good;
            return TimingGrade.Miss;
        }

        /// <summary>拍からの経過秒と拍間隔から判定 (Conductor 等から利用)。</summary>
        public static TimingGrade GradeFromElapsed(double elapsedSinceBeat, double beatInterval)
        {
            if (beatInterval <= 0) return TimingGrade.Miss;
            double fromPrev = elapsedSinceBeat;
            double toNext = beatInterval - elapsedSinceBeat;
            double timeDiff = Math.Min(fromPrev, toNext);
            return Grade(timeDiff / beatInterval);
        }
    }
}
