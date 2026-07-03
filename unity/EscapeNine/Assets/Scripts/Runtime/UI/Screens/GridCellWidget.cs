// GridCellWidget.cs
// Swift 正本: Views/Game/GridCellView.swift
// 3x3 盤面の 1 マス分の見た目 (消失 / 霧 / 通常 + 陣営別ハイライト / 選択マーク) とタップ受付。
//
// uGUI には SwiftUI の stroke(枠線) / LinearGradient が無いため:
//   - 枠線 = 上下左右 4 本の細い ColorRect (太さは親比率。固定 px 禁止ルール準拠)
//   - グラデーション塗り = 「Background の上に半透明色を重ねた結果」を Color.Lerp で
//     事前計算した単色近似 (見た目のリッチ化は Phase 4 / juice 送り)
// キャラスプライト自体は本クラスでは描画しない — セル跨ぎのスライド補間を行うため
// GridBoardWidget が盤面直上のオーバーレイとして一括管理する (Swift との構造差分)。

using System;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeNine.Runtime.UI
{
    /// <summary>1 マス分の表示状態。Swift: GridCellView の let プロパティ群に対応。</summary>
    public struct CellVisual
    {
        public bool IsPlayer;      // Swift: isPlayer
        public bool IsEnemy;       // Swift: isEnemy
        public bool IsAvailable;   // Swift: isAvailable (移動可能マス)
        public bool IsSelected;    // Swift: isSelected (予約済み移動先)
        public bool IsVisible;     // Swift: isVisible (霧マップで見えるか)
        public bool IsDisappeared; // Swift: isDisappeared (消失マス)
        public bool Disabled;      // Swift: disabled (gameStatus != .playing)
    }

    /// <summary>
    /// 盤面 1 マス。MonoBehaviour ではなく素のクラス
    /// (毎フレーム処理を持たず、GridBoardWidget が生成・保持して描画指示するだけのため)。
    /// </summary>
    public sealed class GridCellWidget
    {
        private readonly Button _button;
        private readonly Image _fill;           // マス全面の塗り (Button の targetGraphic 兼タップ受け)
        private readonly Image[] _borders;      // 上下左右の枠線 4 本
        private readonly Text _selectedMark;    // 選択済みマーク (Swift: arrow.down.circle.fill の代替)
        private readonly Text _fogMark;         // 霧マーク (Swift: cloud.fog.fill の代替)
        private readonly Text _xMark;           // 消失マーク (Swift: xmark の代替)
        private readonly CanvasGroup _group;    // disabled 時の全体減光 (Swift: .opacity(0.5))

        /// <summary>枠線の太さ (セルに対する比率)。Swift の lineWidth 1.5〜3pt に相当する見た目。</summary>
        private const float BorderThickness = 0.04f;

        public GridCellWidget(Transform parent, int position, Action<int> onTap)
        {
            // ルート = 塗り Image。raycastTarget=true (Panel 既定) がそのままタップ受けになる。
            RectTransform root = UIFactory.Panel(parent, "Cell" + position, UITheme.Grid);
            _fill = root.GetComponent<Image>();
            _group = root.gameObject.AddComponent<CanvasGroup>();

            _button = root.gameObject.AddComponent<Button>();
            _button.targetGraphic = _fill;
            // Swift は PlainButtonStyle (標準の押下アニメ無効) + 独自 scale 演出。
            // scale 演出は Phase 4 送りなので Transition.None にして色を自前管理する。
            _button.transition = Selectable.Transition.None;
            int captured = position; // クロージャに loop 変数を直接掴ませないための固定 (定石)
            _button.onClick.AddListener(() => onTap(captured));

            // 枠線 4 本 (Swift: RoundedRectangle.stroke の代替。角丸は Phase 4 送り)
            _borders = new Image[4];
            _borders[0] = UIFactory.ColorRect(root, "BorderTop", UITheme.GridBorder);
            UIFactory.Place((RectTransform)_borders[0].transform, 0.5f, 1f - BorderThickness * 0.5f, 1f, BorderThickness);
            _borders[1] = UIFactory.ColorRect(root, "BorderBottom", UITheme.GridBorder);
            UIFactory.Place((RectTransform)_borders[1].transform, 0.5f, BorderThickness * 0.5f, 1f, BorderThickness);
            _borders[2] = UIFactory.ColorRect(root, "BorderLeft", UITheme.GridBorder);
            UIFactory.Place((RectTransform)_borders[2].transform, BorderThickness * 0.5f, 0.5f, BorderThickness, 1f);
            _borders[3] = UIFactory.ColorRect(root, "BorderRight", UITheme.GridBorder);
            UIFactory.Place((RectTransform)_borders[3].transform, 1f - BorderThickness * 0.5f, 0.5f, BorderThickness, 1f);

            // 選択済みマーク (Swift: 右上に arrow.down.circle.fill を offset 表示)
            _selectedMark = UIFactory.Label(root, "SelectedMark", "▼", 40, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_selectedMark.transform, 0.80f, 0.80f, 0.36f, 0.36f);

            // 霧マーク (Swift: cloud.fog.fill を text.opacity(0.15) で表示 → 文字で代替)
            _fogMark = UIFactory.Label(root, "FogMark", "霧", 44, UITheme.WithAlpha(UITheme.TextColor, 0.15f));
            UIFactory.Place((RectTransform)_fogMark.transform, 0.5f, 0.5f, 1f, 1f);

            // 消失マーク (Swift: xmark を warning.opacity(0.3) で表示)
            _xMark = UIFactory.Label(root, "XMark", "×", 72, UITheme.WithAlpha(UITheme.Warning, 0.3f));
            UIFactory.Place((RectTransform)_xMark.transform, 0.5f, 0.5f, 1f, 1f);

            SetMarks(selected: false, fog: false, x: false);
        }

        /// <summary>配置用 (GridBoardWidget が UIFactory.Place で位置決めする)。</summary>
        public RectTransform Rect => (RectTransform)_fill.transform;

        /// <summary>状態を見た目に反映する。Swift: GridCellView.body の分岐を移植。</summary>
        public void Render(CellVisual v)
        {
            // タップ可否は Swift の .disabled(disabled || !isAvailable || isDisappeared) と同一。
            // 霧マスでも available なら押せる (Swift 同挙動 = 見えない先へ賭けて移動できる)。
            // 鬼マスのタップ (エルフ拘束) も「鬼が移動可能マスにいる時のみ」成立する Swift 仕様を踏襲。
            _button.interactable = !v.Disabled && v.IsAvailable && !v.IsDisappeared;

            if (v.IsDisappeared)
            {
                // 消失マス (Swift: disappeared 塗り + warning 枠 + xmark)
                _fill.color = UITheme.Disappeared;
                SetBorder(UITheme.WithAlpha(UITheme.Warning, 0.3f));
                SetMarks(selected: false, fog: false, x: true);
                _group.alpha = 1f; // Swift は通常マス分岐にのみ .opacity(0.5) を掛ける
                return;
            }

            if (!v.IsVisible)
            {
                // 霧マス (Swift: fog 塗り .opacity(0.4) + 薄い枠 + 霧アイコン)
                _fill.color = Color.Lerp(UITheme.Background, UITheme.Fog, 0.4f);
                SetBorder(UITheme.WithAlpha(UITheme.GridBorder, 0.2f));
                SetMarks(selected: false, fog: true, x: false);
                _group.alpha = 1f;
                return;
            }

            // 通常マス — Into the Breach 風: マス背景色で陣営識別。
            // Swift の「半透明グラデ over 背景」を Background との Lerp で単色近似。
            if (v.IsPlayer) _fill.color = Color.Lerp(UITheme.Background, UITheme.Player, 0.30f);
            else if (v.IsEnemy) _fill.color = Color.Lerp(UITheme.Background, UITheme.Enemy, 0.30f);
            else if (v.IsSelected) _fill.color = Color.Lerp(UITheme.Background, UITheme.Available, 0.25f);
            else _fill.color = UITheme.Grid;

            // 枠線の優先順位も Swift と同じ: player > enemy > available > 既定
            if (v.IsPlayer) SetBorder(UITheme.Player);
            else if (v.IsEnemy) SetBorder(UITheme.Enemy);
            else if (v.IsAvailable) SetBorder(UITheme.Available);
            else SetBorder(UITheme.WithAlpha(UITheme.GridBorder, 0.75f));

            SetMarks(selected: v.IsSelected, fog: false, x: false);
            _group.alpha = v.Disabled ? 0.5f : 1f; // Swift: .opacity(disabled ? 0.5 : 1.0)
        }

        private void SetBorder(Color color)
        {
            for (int i = 0; i < _borders.Length; i++) _borders[i].color = color;
        }

        private void SetMarks(bool selected, bool fog, bool x)
        {
            _selectedMark.gameObject.SetActive(selected);
            _fogMark.gameObject.SetActive(fog);
            _xMark.gameObject.SetActive(x);
        }
    }
}
