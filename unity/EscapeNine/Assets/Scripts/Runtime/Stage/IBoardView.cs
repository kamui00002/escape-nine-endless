// IBoardView.cs
// Wave 2 (3D BoardStage 導入) で GameScreen が盤面ウィジェットを一枚岩の型として
// 扱うための共通契約。GridBoardWidget.cs (Wave 1 uGUI 盤面) と BoardStage.cs
// (Wave 2 3D 盤面) の両方をこの契約の実装として扱うことで、GameScreen 側の
// 呼び出し箇所 (Render/SnapNextRender/FlashPlayer/BurstAtPlayer/Shake/ResetFxState/
// イベント購読) を GameScreen.BuildBoard() の UseWorldBoard 分岐 1 箇所に閉じ込め、
// それ以外の呼び出し箇所は新旧どちらの経路でも無改造で動く。
//
// GridBoardWidget.cs は W4 ゲート通過までの比較対象として温存する方針のため直接
// この interface を implements できない (変更禁止)。GridBoardWidgetAdapter.cs が
// 委譲実装を提供する。
//
// デフォルト引数値は GridBoardWidget.cs の公開メソッドと完全に一致させること —
// 呼び出し側の変数の静的型がこの interface になるため、呼び出し箇所で省略された
// 引数はここの宣言値が採用される (実装クラス側のデフォルト値は無視される)。

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
