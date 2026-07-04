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
//
// 担当A juice (Phase 4): 霧/消失マスの出現・解除は即時切替をやめ、Tick() でフェードする。
// GridCellWidget は MonoBehaviour ではない (毎フレーム処理を独自に持たない設計) ため、
// フェードの時間進行は GridBoardWidget.Update() から Tick(deltaTime) を呼んでもらう形で駆動する。

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        private readonly TextMeshProUGUI _selectedMark;    // 選択済みマーク (Swift: arrow.down.circle.fill の代替)
        private readonly TextMeshProUGUI _fogMark;         // 霧マーク (Swift: cloud.fog.fill の代替)
        private readonly TextMeshProUGUI _xMark;           // 消失マーク (Swift: xmark の代替)
        private readonly CanvasGroup _group;    // disabled 時の全体減光 (Swift: .opacity(0.5))

        /// <summary>枠線の太さ (セルに対する比率)。Swift の lineWidth 1.5〜3pt に相当する見た目。</summary>
        private const float BorderThickness = 0.04f;

        // ---- 霧/消失フェード (担当A juice。即時切替をやめ、出現/解除を滑らかにする) ----

        /// <summary>フェード所要秒 (0=非表示 ⇔ 1=完全表示 を Tick で往復する時間)。</summary>
        private const float SpecialFadeDuration = 0.28f;

        /// <summary>Swift の fog 塗り (.opacity(0.4)) と同じ値を事前計算した単色。</summary>
        private static readonly Color FogFillColor = Color.Lerp(UITheme.Background, UITheme.Fog, 0.4f);
        private static readonly Color FogBorderColor = UITheme.WithAlpha(UITheme.GridBorder, 0.2f);
        private static readonly Color DisappearedFillColor = UITheme.Disappeared;
        private static readonly Color DisappearedBorderColor = UITheme.WithAlpha(UITheme.Warning, 0.3f);

        /// <summary>霧アイコン / 消失アイコンの完全表示時のアルファ (元の即時実装と同じ値)。</summary>
        private const float FogMarkAlpha = 0.15f;
        private const float XMarkAlpha = 0.3f;

        private float _fogAlpha;       // 現在の表示量 [0,1] (Tick で目標へ滑らかに追従)
        private float _fogTarget;
        private float _disappearAlpha;
        private float _disappearTarget;

        // 通常時 (陣営ハイライト/既定色) は Swift 同様インスタント切替のまま保持し、
        // 霧/消失のオーバーレイのみこの色との間をフェードでブレンドする。
        private Color _normalFillColor = UITheme.Grid;
        private Color _normalBorderColor = UITheme.WithAlpha(UITheme.GridBorder, 0.75f);

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

            // 霧/消失アイコンは (担当A juice) 常に有効のまま濃度 0 で開始し、Tick でフェードさせる
            // (SetActive(false) にすると alpha を仕込んでも描画されないため)。選択マークのみ従来どおり瞬時トグル。
            _selectedMark.gameObject.SetActive(false);
            _fogMark.gameObject.SetActive(true);
            _xMark.gameObject.SetActive(true);
            ApplyBlend();
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

            // 通常マス (陣営ハイライト) は Swift 同様インスタント切替のまま計算しておく。
            // Into the Breach 風: マス背景色で陣営識別。Swift の「半透明グラデ over 背景」を
            // Background との Lerp で単色近似。
            if (v.IsPlayer) _normalFillColor = Color.Lerp(UITheme.Background, UITheme.Player, 0.30f);
            else if (v.IsEnemy) _normalFillColor = Color.Lerp(UITheme.Background, UITheme.Enemy, 0.30f);
            else if (v.IsSelected) _normalFillColor = Color.Lerp(UITheme.Background, UITheme.Available, 0.25f);
            else _normalFillColor = UITheme.Grid;

            // 枠線の優先順位も Swift と同じ: player > enemy > available > 既定
            if (v.IsPlayer) _normalBorderColor = UITheme.Player;
            else if (v.IsEnemy) _normalBorderColor = UITheme.Enemy;
            else if (v.IsAvailable) _normalBorderColor = UITheme.Available;
            else _normalBorderColor = UITheme.WithAlpha(UITheme.GridBorder, 0.75f);

            // 霧/消失は「見た目の即時切替をやめる」対象 (担当A juice) — 目標値だけ更新し、
            // 実際のブレンドは Tick() (GridBoardWidget.Update() から毎フレーム呼ばれる) が担う。
            _disappearTarget = v.IsDisappeared ? 1f : 0f;
            _fogTarget = (!v.IsDisappeared && !v.IsVisible) ? 1f : 0f;

            // 選択マークは霧/消失マスでは常に非表示 (Swift の早期 return 分岐と同じ結果)。
            _selectedMark.gameObject.SetActive(v.IsSelected && !v.IsDisappeared && v.IsVisible);

            // Swift: 通常マス分岐にのみ .opacity(disabled ? 0.5 : 1.0) を掛け、
            // 霧/消失マスは常時 alpha=1 (捕捉演出優先で減光しない)。
            bool isSpecial = v.IsDisappeared || !v.IsVisible;
            _group.alpha = isSpecial ? 1f : (v.Disabled ? 0.5f : 1f);

            ApplyBlend(); // Tick を待たず即座に見た目へ反映 (初回 Render 直後の 1 フレーム待ちを防ぐ)
        }

        /// <summary>
        /// 霧/消失フェードの時間進行 (担当A juice)。GridBoardWidget.Update() から毎フレーム呼ばれる。
        /// </summary>
        public void Tick(float deltaTime)
        {
            float step = deltaTime / SpecialFadeDuration;
            _fogAlpha = Mathf.MoveTowards(_fogAlpha, _fogTarget, step);
            _disappearAlpha = Mathf.MoveTowards(_disappearAlpha, _disappearTarget, step);
            ApplyBlend();
        }

        /// <summary>現在の _fogAlpha / _disappearAlpha を実際の塗り色・枠色・アイコン濃度に合成する。</summary>
        private void ApplyBlend()
        {
            Color fill = Color.Lerp(_normalFillColor, FogFillColor, _fogAlpha);
            fill = Color.Lerp(fill, DisappearedFillColor, _disappearAlpha);
            _fill.color = fill;

            Color border = Color.Lerp(_normalBorderColor, FogBorderColor, _fogAlpha);
            border = Color.Lerp(border, DisappearedBorderColor, _disappearAlpha);
            SetBorder(border);

            _fogMark.color = UITheme.WithAlpha(UITheme.TextColor, FogMarkAlpha * _fogAlpha);
            _xMark.color = UITheme.WithAlpha(UITheme.Warning, XMarkAlpha * _disappearAlpha);
        }

        private void SetBorder(Color color)
        {
            for (int i = 0; i < _borders.Length; i++) _borders[i].color = color;
        }
    }
}
