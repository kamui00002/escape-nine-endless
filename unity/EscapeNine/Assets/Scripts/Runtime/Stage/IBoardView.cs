// IBoardView.cs
// Wave 2 (3D BoardStage 導入) で GameScreen が盤面ウィジェットを一枚岩の型として
// 扱うための共通契約 (Render/SnapNextRender/FlashPlayer/BurstAtPlayer/Shake/
// ResetFxState/イベント購読)。
//
// Wave 2〜4 の間は旧 uGUI 盤面 (GridBoardWidget + GridBoardWidgetAdapter) と
// BoardStage の 2 実装を切り替え可能にする役割だったが、W4 ゲート通過に伴い
// 旧 uGUI 盤面は W5 で削除 (D4)。現在の実装は BoardStage のみで、この契約は
// GameScreen が盤面の具象型に依存しないための境界として維持する。
//
// デフォルト引数値の注意 — 呼び出し側の変数の静的型がこの interface になるため、
// 呼び出し箇所で省略された引数はここの宣言値が採用される (実装クラス側の
// デフォルト値は無視される)。値は旧 GridBoardWidget の公開メソッドから引き継いだもの。

using System;
using UnityEngine;
using EscapeNine.Core;

namespace EscapeNine.Runtime.Stage
{
    public interface IBoardView
    {
        /// <summary>通常マスのタップ = 移動予約 (Swift: onCellTap)。</summary>
        event Action<int> OnCellTapped;

        /// <summary>鬼がいるマスのタップ = エルフ拘束 (Swift: onEnemyTap)。</summary>
        event Action OnEnemyTapped;

        /// <summary>盤面全体を再描画する。session=null は空盤面 (プレゲーム時)。</summary>
        void Render(GameSession session, bool disabled, Sprite playerSprite, Sprite enemySprite);

        /// <summary>次の Render はスライド補間せず即時配置する (ラン開始 / 階層切替時に呼ぶ)。</summary>
        void SnapNextRender();

        /// <summary>プレイヤー表現を瞬間的に指定色へフラッシュする (透明化吸収=紫 / 盾消費=青 / 敗北=赤)。</summary>
        void FlashPlayer(Color color, float duration = 0.2f);

        /// <summary>プレイヤー位置を中心に破片バーストを放つ (透明化吸収 / 敗北時)。</summary>
        void BurstAtPlayer(Color color, int count = 12, float speed = 600f);

        /// <summary>盤面全体を振動させる (敗北時)。</summary>
        void Shake(float amplitude = 12f, float duration = 0.3f);

        /// <summary>演出の中断残留を基準値へ戻す (GameScreen.ResetTransientUI から呼ばれる)。</summary>
        void ResetFxState();
    }
}
