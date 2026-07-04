// BossPattern.cs
// Unity Phase 5「ローグライク深化」設計文書 (docs/unity-phase5-roguelike-design.md) §5 (ボスパターン) /
// §6.1 に基づく Phase 5c 新規機構。Swift正本には存在しない (Unity固有の追加機能)。
//
// ボス (10の倍数階) は HP を持たない「AI難易度ラベル」に過ぎないため、ボスらしさは
// 体力フェーズではなく「移動アルゴリズムの切り替え」として表現する (§5冒頭)。
namespace EscapeNine.Core
{
    /// <summary>ボス階での移動パターン。Swift正本には存在しない。</summary>
    public enum BossPattern
    {
        /// <summary>① 追跡: 既存の BossAI (追跡95%/ランダム5%) をそのまま流用 (§5.1)。</summary>
        Pursuit,

        /// <summary>② 先読み: HardAI (PredictPlayerMove) を流用した決定論的な先回り (§5.1)。</summary>
        Foresight,

        /// <summary>③ 威圧: 追跡(BossAIと同じ移動)を続けながら、隣接マス1つを1ターンだけ進入不可にする
        /// (GameSession.TemporaryBossZone が担当、DisappearedCellsとは独立、§5.1・§5.3)。
        /// 移動を止めない加算的な効果にしているのは、§5.2 がこのパターンをFloor40+で解禁する理由を
        /// 「既存の難易度ランプ哲学と整合させる」としており、追跡を止める実装だと最新解禁パターンが
        /// 逆に休憩ターンになりランプが逆転するため。</summary>
        Intimidation
    }

    /// <summary>ボスパターンのローテーション規則 (§5.2)。</summary>
    public static class BossPatternRotation
    {
        /// <summary>
        /// Floor 40 未満のボスは①②のみ循環 (2パターン)、40以降で③が加わる (3パターン)。
        /// 40 は GameConfig.AINaturalHardFloor (=36) の直後に来るボス階 (10の倍数) を採用 (§5.2)。
        /// </summary>
        public const int ThirdPatternUnlockFloor = 40;

        private static readonly BossPattern[] TwoPatternCycle =
        {
            BossPattern.Pursuit, BossPattern.Foresight
        };

        private static readonly BossPattern[] ThreePatternCycle =
        {
            BossPattern.Pursuit, BossPattern.Foresight, BossPattern.Intimidation
        };

        /// <summary>
        /// ボス階内の経過ターン数 (0始まり) からパターンを求める。
        /// 2ターンごとに固定順で循環する (学習可能性を優先し、ランダム循環にはしない、§5.2)。
        /// </summary>
        /// <param name="turnIndexInFloor">ボス階に入ってから経過したターン数 (0始まり)。</param>
        /// <param name="floor">現在の階層 (3パターン解禁の判定に使用)。</param>
        public static BossPattern PatternForTurn(int turnIndexInFloor, int floor)
        {
            var cycle = floor >= ThirdPatternUnlockFloor ? ThreePatternCycle : TwoPatternCycle;
            int safeIndex = turnIndexInFloor < 0 ? 0 : turnIndexInFloor;
            int cycleIndex = (safeIndex / 2) % cycle.Length;
            return cycle[cycleIndex];
        }
    }
}
