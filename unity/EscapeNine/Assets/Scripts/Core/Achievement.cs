// Achievement.cs
// Swift 正本からの忠実移植: Models/Achievement.swift (enum Achievement + 判定ロジック)
// 判定は純関数 (AchievementChecker)。解除の永続化 (UserDefaults) は Runtime 層で実装。

using System.Collections.Generic;

namespace EscapeNine.Core
{
    public enum Achievement
    {
        FirstWin,
        Floor10,
        Floor25,
        Floor50,
        Floor75,
        Floor100,
        NoSkillWin,
        SpeedRunner,
        Survivor
    }

    public static class AchievementInfo
    {
        /// <summary>表示名。Swift: Achievement.rawValue</summary>
        public static string Title(this Achievement a) => a switch
        {
            Achievement.FirstWin => "初勝利",
            Achievement.Floor10 => "階層10到達",
            Achievement.Floor25 => "階層25到達",
            Achievement.Floor50 => "階層50到達",
            Achievement.Floor75 => "階層75到達",
            Achievement.Floor100 => "階層100到達",
            Achievement.NoSkillWin => "素手の達人",
            Achievement.SpeedRunner => "スピードランナー",
            Achievement.Survivor => "生存者",
            _ => ""
        };

        public static string Description(this Achievement a) => a switch
        {
            Achievement.FirstWin => "初めてゲームをクリアした",
            Achievement.Floor10 => "階層10に到達した",
            Achievement.Floor25 => "階層25に到達した",
            Achievement.Floor50 => "階層50に到達した",
            Achievement.Floor75 => "階層75に到達した",
            Achievement.Floor100 => "階層100に到達した",
            Achievement.NoSkillWin => "スキルを使わずに階層10をクリアした",
            Achievement.SpeedRunner => "BPM180以上で階層20に到達した",
            Achievement.Survivor => "階層30をノーミスでクリアした",
            _ => ""
        };
    }

    public static class AchievementChecker
    {
        /// <summary>
        /// 与えられた結果で解除条件を満たす実績集合を返す。Swift: AchievementManager.checkAchievements(...)
        /// 「初回のみ解除」「効果音」「永続化」は含まない (Runtime 層の責務)。
        /// </summary>
        public static HashSet<Achievement> CheckAchievements(int floor, bool skillUsed, double currentBPM, bool gameWon)
        {
            var result = new HashSet<Achievement>();

            // 階層到達 (勝敗問わず) — 敗北時もその到達階層に応じて解除される。
            // 呼び出し側 (GameController.EndGame) は勝利/敗北の両方でこのメソッドを呼ぶこと。
            if (floor >= 10) result.Add(Achievement.Floor10);
            if (floor >= 25) result.Add(Achievement.Floor25);
            if (floor >= 50) result.Add(Achievement.Floor50);
            if (floor >= 75) result.Add(Achievement.Floor75);
            if (floor >= 100) result.Add(Achievement.Floor100);

            if (!gameWon) return result;

            // 以下は勝利 (ゲームクリア) 時のみ。FirstWin は「初めてゲームをクリアした」実績のため、
            // floor>=1 で常時ではなく勝利ゲートの内側に置く (敗北時に誤って解除されないように)。
            result.Add(Achievement.FirstWin);
            if (floor >= 10 && !skillUsed) result.Add(Achievement.NoSkillWin);
            if (floor >= 20 && currentBPM >= 180) result.Add(Achievement.SpeedRunner);
            if (floor >= 30) result.Add(Achievement.Survivor);

            return result;
        }
    }
}
