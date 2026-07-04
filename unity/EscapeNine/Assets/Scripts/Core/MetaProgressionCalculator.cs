// MetaProgressionCalculator.cs
// Unity Phase 5「ローグライク深化」設計文書 §3.1 (通貨) / §6.1 に基づく。
// Swift正本には存在しない (Unity固有の追加機能)。
//
// 「残光 (ざんこう)」— 1ラン終了時に付与するメタ進行通貨の算出。
// AchievementChecker と同じ「静的・副作用なし」の作法に合わせた純関数。
// 消費導線 (コスメティック購入・レリックプール解放・スターターパーク・MetaShopScreen) は
// Phase 5c 送り (§7 Phase 5b のスコープは「蓄積のみ」)。

namespace EscapeNine.Core
{
    public static class MetaProgressionCalculator
    {
        /// <summary>
        /// 1ラン終了時の残光付与量。
        /// 式 (§3.1、[要検証・仮の式。ラン頻度と欲しいアンロック速度から逆算してチューニングする]):
        ///   残光 = 到達階層 + floor(到達階層/5)*2 + (勝利なら+100) + (デイリーチャレンジ達成なら+20)
        /// </summary>
        /// <param name="reachedFloor">到達階層 (GameSession.CurrentFloor。勝利時は101のままでよい —
        /// PersistRunResult のスコア送信と同じ Swift パリティ踏襲の値をそのまま使う)。</param>
        /// <param name="won">このランが勝利 (100階層踏破) で終了したか。</param>
        /// <param name="dailyChallengeCompleted">デイリーチャレンジ挑戦中に勝利で終えたか。</param>
        public static int CalculateGlow(int reachedFloor, bool won, bool dailyChallengeCompleted)
        {
            int floorClamped = reachedFloor > 0 ? reachedFloor : 0;
            int glow = floorClamped + (floorClamped / 5) * 2;
            if (won) glow += 100;
            if (dailyChallengeCompleted) glow += 20;
            return glow;
        }
    }
}
