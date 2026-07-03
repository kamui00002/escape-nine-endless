// ScreenPayloads.cs
// 画面間で Router.Show(id, payload) に載せて受け渡すデータ型の定義。
// Swift 正本: Views/Game/GameView.swift の .sheet { ResultView(...) } 呼び出し引数一式と、
//             ResultView の onPlayAgain クロージャ (即リトライ) に相当する情報。
// GameScreen と ResultScreen の双方から参照するため public で公開する。

using EscapeNine.Core;

namespace EscapeNine.Runtime.UI
{
    /// <summary>
    /// Router.Show(ScreenId.Game, payload) 用。
    /// AutoStart=false (または payload=null): Swift の pre-game オーバーレイ (「冒険を始める」) を表示。
    /// AutoStart=true: Result 画面のリトライ (Swift: onPlayAgain) — オーバーレイに戻らず即ゲーム再開。
    /// </summary>
    public sealed class GameStartRequest
    {
        /// <summary>true = 開始前オーバーレイを出さず即 StartNewRun (リトライ用)。</summary>
        public bool AutoStart;

        /// <summary>開始階層 (通常 1。デバッグ開始階層は GameController 側が別途反映する)。</summary>
        public int StartFloor = 1;
    }

    /// <summary>
    /// Router.Show(ScreenId.Result, payload) 用。Swift ResultView の引数一式に対応。
    /// </summary>
    public sealed class ResultPayload
    {
        /// <summary>到達階層。勝利時は Swift の quirk を踏襲して 101 が入る。</summary>
        public int Floor;

        /// <summary>true = 100 階層踏破 (Swift: result == .win)。</summary>
        public bool Won;

        /// <summary>敗因 (勝利時は null)。Swift: defeatReason</summary>
        public DefeatReason? DefeatReason;

        /// <summary>ラン通算の経過秒。Swift: elapsedSeconds</summary>
        public double ElapsedSeconds;

        /// <summary>敗北時の敵との Chebyshev 距離 (1 = 「あと 1 マスで生存」表示用)。Swift: nearMissDistance</summary>
        public int NearMissDistance;

        /// <summary>終了時のプレイヤー位置 (9マス絵文字シェア用)。Swift: playerPosition</summary>
        public int PlayerPosition;

        /// <summary>終了時の敵位置 (9マス絵文字シェア用)。Swift: enemyPosition</summary>
        public int EnemyPosition;

        /// <summary>
        /// 自己ベスト更新「前」のベスト階層。Swift: previousBest
        /// (GameController.EndGame が HighestFloor を更新済みのため、更新後の値と比較すると
        ///  新記録判定が常に false になる。GameScreen がラン開始時に確保した値を渡す)。
        /// </summary>
        public int PreviousBest;
    }
}
