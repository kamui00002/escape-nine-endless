// BeatIndicatorWidget.cs
// Swift 正本: Views/Game/BeatIndicatorView.swift
// ターンカウントダウン (3→2→1) の大表示 + 拍の進行ゲージ + ターン進行ドット。
//
// Swift との表現差分:
//   - Swift の円形プログレスリング (Circle.trim) は、uGUI の Filled Image が
//     円形スプライト必須 (built-in スプライトへの実行時アクセスは不安定) のため、
//     水平バーで代替する (円形リング化は Phase 4 / juice 送り)。
//   - 外周リング回転は Phase 4 送りのまま。
// 進行ゲージは Swift 同様「次の拍までの残り」を毎フレーム描画する
// (Swift: Timer 0.05s + audioManager.timeUntilNextBeat() → Unity: Update + Conductor 拍位相)。
//
// Phase 4 (juice) 追記 (担当B): Swift の「拍パルス (scale 1.5 → 1.0 の spring)」を
// FxKit.PunchScale + Flash で移植。円形リングが無いため、対象は「メイン円の代役」である
// カウント数字ラベル (常に存在し破棄されない安全な対象) に絞る。個々のターンドットへの
// パンチは RebuildDots() が古い Image を Destroy するため MissingReferenceException の
// トラップになり得るので採用しない。加えて BeatPulse を全体ルートに付け、常時の微呼吸を足す。

using System;
using UnityEngine;
using UnityEngine.UI;
using EscapeNine.Runtime.UI.Fx;

namespace EscapeNine.Runtime.UI
{
    public sealed class BeatIndicatorWidget : MonoBehaviour
    {
        /// <summary>
        /// Swift の Color.orange (iOS システムオレンジ #FF9500) 相当。
        /// GameColors (UITheme) に含まれない色を Swift 側が直接使っているため、ここで定義して
        /// GameScreen (時間切れ表示) とも共用する。
        /// </summary>
        public static readonly Color SwiftOrange = new Color(1f, 0.584f, 0f);

        private Text _countLabel;
        private RectTransform _barFill;
        private Image _barFillImage;
        private RectTransform _dotsRow;

        private Image[] _dots = Array.Empty<Image>();
        private int _builtMaxTurns = -1;

        // カウントダウン値に応じた現在色 (数字とゲージで共用)。Swift: countdownColor
        private Color _currentColor;

        // Phase 4 (juice): 拍ヒット検知用の直近値。int.MinValue = 未初期化 (初回 Render で誤発火しない)。
        private int _lastCountdown = int.MinValue;

        public static BeatIndicatorWidget Create(Transform parent)
        {
            RectTransform root = UIFactory.Panel(parent, "BeatIndicator");
            var widget = root.gameObject.AddComponent<BeatIndicatorWidget>();
            widget._currentColor = UITheme.Available;

            // Phase 4 (juice): ウィジェット全体の常時微呼吸 (Swift: 外周リング回転 + glow の代替アンビエンス)。
            // 拍ヒットの瞬間の強い PunchScale/Flash (Render 内) とは別レイヤーで、対象も同じ RectTransform ではないため競合しない。
            BeatPulse ambientPulse = root.gameObject.AddComponent<BeatPulse>();
            ambientPulse.scaleAmount = 0.03f;
            ambientPulse.alphaAmount = 0f;
            ambientPulse.onlyWhilePlaying = true;

            // カウントダウン数字 (Swift: メイン円の中央数字。円は省略し数字を主役に)
            widget._countLabel = UIFactory.Label(root, "CountLabel", "3", 96, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)widget._countLabel.transform, 0.5f, 0.66f, 0.5f, 0.62f);

            // 拍進行ゲージ (Swift: プログレスリングの代替バー)
            var barBg = UIFactory.ColorRect(root, "BeatBarBg", UITheme.WithAlpha(UITheme.GridBorder, 0.2f));
            RectTransform barRt = (RectTransform)barBg.transform;
            UIFactory.Place(barRt, 0.5f, 0.28f, 0.6f, 0.08f);

            widget._barFillImage = UIFactory.ColorRect(barRt, "Fill", UITheme.Available);
            widget._barFill = (RectTransform)widget._barFillImage.transform;
            // fill はバー内で左詰め: anchorMax.x を進行率で動かす (固定 px を使わない伸縮)
            widget._barFill.anchorMin = Vector2.zero;
            widget._barFill.anchorMax = Vector2.one;
            widget._barFill.offsetMin = Vector2.zero;
            widget._barFill.offsetMax = Vector2.zero;

            // ターン進行ドット行 (Swift: Turn indicator dots。円→矩形の簡略化は Phase 4 で解消)
            widget._dotsRow = UIFactory.Panel(root, "TurnDots");
            UIFactory.Place(widget._dotsRow, 0.5f, 0.09f, 0.8f, 0.12f);

            return widget;
        }

        /// <summary>
        /// 再入防御: 画面切替 (Game→Result 等) で GameScreen ごと SetActive(false) されると、
        /// PunchScale が中断され localScale がドリフトしたまま残ることがある。ScreenRouter は
        /// 子階層ごと非活性化するため OnEnable は次に Game 画面が表示されるたび確実に呼ばれる。
        /// 併せて _lastCountdown もリセットし、新しいランの初回 Render で拍ヒット演出が
        /// 抑制されない (直前ランの最終値と偶然一致して発火しないケース) ようにする。
        /// </summary>
        private void OnEnable()
        {
            if (_countLabel != null) _countLabel.transform.localScale = Vector3.one;
            _lastCountdown = int.MinValue;
        }

        /// <summary>
        /// カウントダウン・ターン進行を反映する。Swift: turnCountdown / turnCount / maxTurns。
        /// maxTurns は 10 階層ごとに増える (GameConfig.GetMaxTurns) ためドット列は必要時のみ再構築。
        /// </summary>
        public void Render(int countdown, int turnCount, int maxTurns)
        {
            _currentColor = ColorFor(countdown);
            _countLabel.text = Mathf.Max(countdown, 0).ToString();
            _countLabel.color = _currentColor;
            _barFillImage.color = _currentColor;

            if (maxTurns != _builtMaxTurns) RebuildDots(maxTurns);

            for (int i = 0; i < _dots.Length; i++)
            {
                bool isActive = (i + 1) <= turnCount; // Swift: turn <= turnCount
                _dots[i].color = isActive
                    ? UITheme.Available
                    : UITheme.WithAlpha(UITheme.GridBorder, 0.25f);
                // active ドットをやや大きく (Swift: 7/90 vs 5/90)。比率のみ変更 = 固定 px 不使用。
                float h = isActive ? 0.85f : 0.55f;
                float cx = (i + 0.5f) / _dots.Length;
                UIFactory.Place((RectTransform)_dots[i].transform, cx, 0.5f, 0.55f / _dots.Length, h);
            }

            // Phase 4 (juice): 拍ヒットの瞬間 (カウントダウン値が変わった瞬間) にパンチ+一瞬の発光。
            // Swift: onChange(of: turnCountdown) { scale = 1.5 → 1.0 の spring } に相当。
            // Render は OnStateChanged 経由で拍以外の理由でも呼ばれるため、値の変化そのものをトリガーにする。
            if (countdown != _lastCountdown)
            {
                _lastCountdown = countdown;
                FxKit.PunchScale(this, (RectTransform)_countLabel.transform, 0.4f, 0.22f);
                FxKit.Flash(this, _countLabel, Color.white, 0.18f);
            }
        }

        /// <summary>カウントダウン残に応じた色。Swift: countdownColor (3=金 / 2=橙 / 1=赤)。</summary>
        private static Color ColorFor(int countdown)
        {
            switch (countdown)
            {
                case 3: return UITheme.Available;
                case 2: return SwiftOrange;
                case 1: return UITheme.Warning;
                default: return UITheme.Available;
            }
        }

        private void RebuildDots(int maxTurns)
        {
            _builtMaxTurns = maxTurns;
            for (int i = _dotsRow.childCount - 1; i >= 0; i--)
            {
                Destroy(_dotsRow.GetChild(i).gameObject);
            }

            _dots = new Image[Mathf.Max(1, maxTurns)];
            for (int i = 0; i < _dots.Length; i++)
            {
                _dots[i] = UIFactory.ColorRect(_dotsRow, "Dot" + (i + 1),
                    UITheme.WithAlpha(UITheme.GridBorder, 0.25f));
            }
        }

        private void Update()
        {
            // 「次の拍までの残り」を 1→0 で描く (Swift: progress = timeUntilNextBeat())。
            // Conductor 停止中 (ポーズ・未開始) は満タン表示。
            float progress = 1f;
            var conductor = (App.I != null) ? App.I.Conductor : null;
            if (conductor != null)
            {
                double beats = conductor.SongPositionBeats;
                if (beats > 0)
                {
                    double frac = beats - Math.Floor(beats); // 拍内位相 [0,1)
                    progress = 1f - (float)frac;
                }
            }

            _barFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            _barFill.offsetMin = Vector2.zero;
            _barFill.offsetMax = Vector2.zero;
        }
    }
}
