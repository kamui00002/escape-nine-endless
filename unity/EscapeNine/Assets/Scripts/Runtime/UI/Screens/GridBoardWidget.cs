// GridBoardWidget.cs
// Swift 正本: Views/Game/GridBoardView.swift (3x3 盤面) + GridCellView.swift のキャラ描画部分。
//
// 構造上の設計判断 (Swift との差分):
//   - Swift は各セルの中にプレイヤー/鬼スプライトを描くが、Unity 版はセル跨ぎの
//     スライド補間 (0.1 秒 Lerp) を行うため、スプライトを「盤面直上のオーバーレイ」として
//     セルとは独立に配置する。セル側は背景色ハイライトのみ担当。
//   - SwiftUI の .aspectRatio(1.0, contentMode: .fit) は AspectRatioFitter(FitInParent) で再現。
//     位置・サイズは全て親比率 0..1 (UIFactory.Place) = 固定 px 禁止ルール準拠。
//
// 担当A juice (Phase 4): 移動ホップ (PunchScale)・拍パルス (BeatPulse)・
// 衝突演出用の Flash/BurstAt/Shake 公開メソッド・霧/消失セルのフェード駆動 (Tick) を追加。
// GameController の既存イベント購読・状態参照は変更しない (GameScreen 側からのみ呼ばれる)。

using System;
using UnityEngine;
using UnityEngine.UI;
using EscapeNine.Core;
using EscapeNine.Runtime.UI.Fx;

namespace EscapeNine.Runtime.UI
{
    /// <summary>
    /// 3x3 盤面ウィジェット。GameSession を受け取って全セル + キャラスプライトを再描画する。
    /// タップは OnCellTapped / OnEnemyTapped に振り分ける (Swift: GridBoardView の onTap クロージャ)。
    /// </summary>
    public sealed class GridBoardWidget : MonoBehaviour
    {
        /// <summary>通常マスのタップ = 移動予約 (Swift: onCellTap)。</summary>
        public event Action<int> OnCellTapped;

        /// <summary>鬼がいるマスのタップ = エルフ拘束 (Swift: onEnemyTap)。</summary>
        public event Action OnEnemyTapped;

        /// <summary>移動スライド補間の所要秒 (タスク指定: Lerp 0.1s 程度)。</summary>
        private const float MoveDuration = 0.1f;

        /// <summary>移動ホップ (PunchScale) のパンチ量 / 秒数 (担当A juice: squash&amp;stretch 風の強調)。</summary>
        private const float MoveHopPunch = 0.18f;
        private const float MoveHopDuration = 0.18f;

        /// <summary>セルサイズ (正方形盤面に対する比率)。3 列 + 隙間で 0.31 × 3 + 余白。</summary>
        private const float CellSize = 0.31f;

        /// <summary>キャラスプライトのサイズ (正方形盤面に対する比率 ≒ セルの 6 割強)。</summary>
        private const float SpriteSize = 0.20f;

        private readonly GridCellWidget[] _cells = new GridCellWidget[GameConfig.GridSize + 1]; // 1-indexed

        private Image _playerImage;
        private Image _enemyImage;
        private RectTransform _playerRt;
        private RectTransform _enemyRt;

        // タップ振り分け用に直近 Render 時の鬼位置を控える (Swift: GridBoardView は props で受ける)
        private int _enemyPosition = -1;

        // ---- スライド補間の内部状態 (from → to を t: 0..1 で進める) ----
        private Vector2 _playerFrom, _playerTo;
        private float _playerT = 1f;
        private Vector2 _enemyFrom, _enemyTo;
        private float _enemyT = 1f;

        // 次の Render で補間せず即時配置する (ラン開始・階層切替のランダム再配置で
        // 盤面を横断するスライドが出るのを防ぐ。Swift も再配置はアニメなし)。
        private bool _snapNext = true;

        /// <summary>
        /// 盤面を生成する。返り値の gameObject (外枠) を呼び出し側が UIFactory.Place で配置する。
        /// 外枠の中に AspectRatioFitter 付きの正方形領域を作り、セルはその中に置く。
        /// </summary>
        public static GridBoardWidget Create(Transform parent)
        {
            RectTransform area = UIFactory.Panel(parent, "GridBoard");
            var widget = area.gameObject.AddComponent<GridBoardWidget>();

            // SwiftUI: .aspectRatio(1.0, contentMode: .fit) の代替。
            // FitInParent は親の縦横どちらが短くても正方形を維持してくれる。
            RectTransform square = UIFactory.Panel(area, "Square");
            var fitter = square.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            // BPM に合わせて盤面全体 (セル + キャラスプライトの親) がわずかに脈動する
            // (担当A juice: 盤面背景/外周フレームへの BeatPulse 装着)。Graphic が無いので
            // scale のみ脈動し、alphaAmount は無視される (BeatPulse は null Graphic を許容する)。
            square.gameObject.AddComponent<BeatPulse>();

            for (int pos = 1; pos <= GameConfig.GridSize; pos++)
            {
                var cell = new GridCellWidget(square, pos, widget.HandleCellTap);
                Vector2 c = CenterOf(pos);
                UIFactory.Place(cell.Rect, c.x, c.y, CellSize, CellSize);
                widget._cells[pos] = cell;
            }

            // キャラスプライトはセルの子ではなく盤面直上のオーバーレイ (冒頭コメント参照)。
            // raycastTarget=false: タップは必ずセルの Button が受ける
            // (鬼タップ = 鬼がいるセルのタップ、として HandleCellTap で振り分ける)。
            widget._playerImage = UIFactory.SpriteImage(square, "PlayerSprite", null);
            widget._playerImage.raycastTarget = false;
            widget._playerImage.enabled = false;
            widget._playerRt = (RectTransform)widget._playerImage.transform;

            widget._enemyImage = UIFactory.SpriteImage(square, "EnemySprite", null);
            widget._enemyImage.raycastTarget = false;
            widget._enemyImage.enabled = false;
            widget._enemyRt = (RectTransform)widget._enemyImage.transform;

            return widget;
        }

        /// <summary>次の Render はスライド補間せず即時配置する (ラン開始 / 階層切替時に呼ぶ)。</summary>
        public void SnapNextRender()
        {
            _snapNext = true;
        }

        /// <summary>
        /// 盤面全体を再描画する。session=null は空盤面 (プレゲーム時)。
        /// Swift: GridBoardView への props 受け渡し + ForEach 再構築に相当。
        /// </summary>
        public void Render(GameSession session, bool disabled, Sprite playerSprite, Sprite enemySprite)
        {
            if (session == null)
            {
                RenderEmpty();
                return;
            }

            _enemyPosition = session.EnemyPosition;
            var available = session.GetAvailableMoves(); // Swift: viewModel.getAvailableMoves()
            int? selected = session.PendingPlayerMove;   // Swift: viewModel.pendingPlayerMove

            for (int pos = 1; pos <= GameConfig.GridSize; pos++)
            {
                _cells[pos].Render(new CellVisual
                {
                    IsPlayer = session.PlayerPosition == pos,
                    IsEnemy = session.EnemyPosition == pos,
                    IsAvailable = available.Contains(pos),
                    IsSelected = selected == pos,
                    IsVisible = session.IsCellVisible(pos),
                    IsDisappeared = session.IsCellDisappeared(pos),
                    Disabled = disabled,
                });
            }

            // プレイヤー: 自分のマスは霧でも常に可視 (IsCellVisible は距離 0 で必ず true)
            _playerImage.sprite = playerSprite;
            _playerImage.enabled = playerSprite != null;
            SetSpriteTarget(ref _playerFrom, ref _playerTo, ref _playerT, _playerRt,
                CenterOf(session.PlayerPosition));

            // 鬼: 霧で見えない位置なら非表示 (Swift: isEnemy && isVisible)
            bool enemyVisible = session.IsCellVisible(session.EnemyPosition);
            _enemyImage.sprite = enemySprite;
            _enemyImage.enabled = enemySprite != null && enemyVisible;
            SetSpriteTarget(ref _enemyFrom, ref _enemyTo, ref _enemyT, _enemyRt,
                CenterOf(session.EnemyPosition));

            _snapNext = false;
        }

        // MARK: - 内部実装

        /// <summary>プレゲーム時の空盤面 (全マス既定色・減光・スプライト非表示)。</summary>
        private void RenderEmpty()
        {
            _enemyPosition = -1;
            for (int pos = 1; pos <= GameConfig.GridSize; pos++)
            {
                _cells[pos].Render(new CellVisual { IsVisible = true, Disabled = true });
            }
            _playerImage.enabled = false;
            _enemyImage.enabled = false;
        }

        /// <summary>
        /// タップの振り分け (Swift: GridBoardView の onTap クロージャと同一分岐)。
        /// 鬼がいるマスは常に「鬼タップ」扱い = 鬼マスを移動先として予約することはできない。
        /// </summary>
        private void HandleCellTap(int position)
        {
            if (position == _enemyPosition) OnEnemyTapped?.Invoke();
            else OnCellTapped?.Invoke(position);
        }

        /// <summary>position (1..9) → 正方形盤面内の中心比率。Swift の row*3+col+1 配置と同じ並び (1=左上)。</summary>
        private static Vector2 CenterOf(int position)
        {
            int row = GameConfig.RowFromPosition(position);    // 0=上段 (Swift と同じ)
            int col = GameConfig.ColumnFromPosition(position); // 0=左列
            float cx = (col + 0.5f) / GameConfig.GridColumns;
            // Unity は左下原点なので上段ほど cy が大きい (座標系差分は UIFactory 冒頭コメント参照)
            float cy = 1f - (row + 0.5f) / GameConfig.GridRows;
            return new Vector2(cx, cy);
        }

        /// <summary>スライド補間の目標を設定する。目標が変わった時だけ from を現在位置に取り直す。</summary>
        private void SetSpriteTarget(ref Vector2 from, ref Vector2 to, ref float t,
            RectTransform rt, Vector2 target)
        {
            if (_snapNext)
            {
                from = target;
                to = target;
                t = 1f;
                Apply(rt, target);
                return;
            }

            if (target != to)
            {
                from = Vector2.Lerp(from, to, Mathf.Clamp01(t)); // 補間途中でも現在見えている位置から
                to = target;
                t = 0f;
                Apply(rt, from);

                // 移動ホップ (担当A juice: squash&stretch 風の強調)。ラン開始/階層切替の
                // 即時再配置 (_snapNext 分岐) では鳴らさない — 実際の移動時のみ。
                FxKit.PunchScale(this, rt, MoveHopPunch, MoveHopDuration);
            }
        }

        private void Update()
        {
            // 同時移動 (プレイヤーと鬼が同じターンで動く) を両方 0.1 秒でスライドさせる
            Advance(ref _playerFrom, ref _playerTo, ref _playerT, _playerRt);
            Advance(ref _enemyFrom, ref _enemyTo, ref _enemyT, _enemyRt);

            // 霧/消失マスのフェード進行 (担当A juice: GridCellWidget は非 MonoBehaviour のため駆動する)
            float dt = Time.deltaTime;
            for (int pos = 1; pos <= GameConfig.GridSize; pos++)
            {
                _cells[pos].Tick(dt);
            }
        }

        private static void Advance(ref Vector2 from, ref Vector2 to, ref float t, RectTransform rt)
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.deltaTime / MoveDuration);
            // 単純 Lerp より着地感が出るイージングで強化 (担当A juice)。
            float eased = Mathf.SmoothStep(0f, 1f, t);
            Apply(rt, Vector2.Lerp(from, to, eased));
        }

        // MARK: - 衝突演出フック (担当A juice: GameScreen から呼ばれる公開 API)

        /// <summary>プレイヤースプライトを瞬間的に指定色へフラッシュする (透明化吸収=紫 / 盾消費=青 / 敗北=赤)。</summary>
        public void FlashPlayer(Color color, float duration = 0.2f)
        {
            FxKit.Flash(this, _playerImage, color, duration);
        }

        /// <summary>プレイヤー位置を中心に破片バーストを放つ (透明化吸収 / 敗北時)。</summary>
        public void BurstAtPlayer(Color color, int count = 12, float speed = 600f)
        {
            if (FxLayer.I != null) FxLayer.I.BurstAt(_playerRt, color, count, speed);
        }

        /// <summary>盤面全体を振動させる (敗北時)。</summary>
        public void Shake(float amplitude = 12f, float duration = 0.3f)
        {
            FxKit.ShakeRect(this, (RectTransform)transform, amplitude, duration);
        }

        private static void Apply(RectTransform rt, Vector2 center)
        {
            UIFactory.Place(rt, center.x, center.y, SpriteSize, SpriteSize);
        }
    }
}
