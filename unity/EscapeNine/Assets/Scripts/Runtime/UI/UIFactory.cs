// UIFactory.cs
// Swift 正本: Utilities/ResponsiveLayout.swift の「固定 pt 禁止・全て親比率」思想を
// uGUI に移植したコード生成ファクトリ。SwiftUI の GeometryProxy 比率計算に相当する
// 役割を Place() (アンカー完全比率配置) が担う。
//
// ★レイアウト原則 (CLAUDE.md iPad/iPhone ルール移植):
//   - 位置・サイズは全て「親 RectTransform に対する 0..1 比率」で指定する。
//   - offsetMin/offsetMax は常に 0 = 固定 px を一切足さない。
//     こうすると CanvasScaler (1170x2532 / ScaleWithScreenSize / match 0.5) と組み合わせて
//     iPhone / iPad どちらでも相似形に崩れず並ぶ。
//   - 座標系は Unity 標準 = 左下原点 (cy=0 が画面下端)。Swift の top-left と逆なので注意。

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EscapeNine.Runtime.UI
{
    /// <summary>
    /// uGUI 部品のコード生成ヘルパー。シーンに手置きせず全 UI をコードで組む方針
    /// (SwiftUI の宣言的 View 構築に寄せ、プレハブ差分管理の手間を排除するため)。
    /// </summary>
    public static class UIFactory
    {
        /// <summary>
        /// 空のコンテナパネルを生成する。bg 指定時は Image を付ける。
        /// bg 付きパネルの Image は raycastTarget=true のまま
        /// (ポーズ用オーバーレイ等で背面のボタンを遮断する用途があるため)。
        /// 生成直後は親いっぱいに広がる。位置決めは Place() で行うこと。
        /// </summary>
        public static RectTransform Panel(Transform parent, string name, Color? bg = null)
        {
            RectTransform rt = NewRect(parent, name);
            if (bg.HasValue)
            {
                Image img = rt.gameObject.AddComponent<Image>();
                img.color = bg.Value;
            }
            return rt;
        }

        /// <summary>
        /// TMP ラベルを生成する (Wave 1: legacy Text から TextMeshPro へ移行)。
        /// フォントは必ず UITheme.FontAsset (DotGothic16、日本語対応)。
        /// 引数の型 (TextAnchor/FontStyle) は呼び出し側の変更を最小化するため維持し、
        /// 内部で TMP の型 (TextAlignmentOptions/FontStyles) へ変換する。
        /// raycastTarget=false: ラベルがボタン等のタップを吸わないようにする。
        /// </summary>
        public static TextMeshProUGUI Label(Transform parent, string name, string text, int fontSize, Color color,
            TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            RectTransform rt = NewRect(parent, name);
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = UITheme.FontAsset;
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = ToTmpAlignment(align);
            t.fontStyle = ToTmpFontStyle(style);
            // 横は折り返し・縦ははみ出し許容: 比率レイアウトで枠が小さくなっても
            // 文字が消えるより「はみ出して見える」方がデバッグしやすいため (legacy Text の
            // HorizontalWrapMode.Wrap / VerticalWrapMode.Overflow と同等の挙動)。
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>legacy TextAnchor → TMP TextAlignmentOptions の変換 (9 方向を網羅)。</summary>
        private static TextAlignmentOptions ToTmpAlignment(TextAnchor align)
        {
            switch (align)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        /// <summary>legacy FontStyle → TMP FontStyles の変換。</summary>
        private static FontStyles ToTmpFontStyle(FontStyle style)
        {
            switch (style)
            {
                case FontStyle.Bold: return FontStyles.Bold;
                case FontStyle.Italic: return FontStyles.Italic;
                case FontStyle.BoldAndItalic: return FontStyles.Bold | FontStyles.Italic;
                default: return FontStyles.Normal;
            }
        }

        /// <summary>
        /// 背景 Image + 子 Text 構成のボタンを生成する。遷移は ColorTint (uGUI 標準)。
        /// 効果音 (button_tap) はここでは鳴らさない: 画面側が App.I.Audio 経由で明示的に
        /// 鳴らす方針 (ファクトリを App シングルトンに依存させないため)。
        /// </summary>
        public static Button TextButton(Transform parent, string name, string label, int fontSize,
            Color bg, Color fg, Action onClick)
        {
            RectTransform rt = NewRect(parent, name);

            Image img = rt.gameObject.AddComponent<Image>();
            img.color = bg;

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint; // 押下で標準の暗転フィードバック
            // キーボードナビ/Submit を無効化: クリックで選択状態になったボタンが Enter の
            // uGUI Submit を受けると、KeyboardInput の Enter 処理と同一フレームで二重発火する
            // (レビュー C2)。キーボード操作は KeyboardInput が一元的に担うため Navigation は不要。
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null)
            {
                btn.onClick.AddListener(() => onClick());
            }

            // ラベルはボタン全面に広げる (Label 側で raycastTarget=false 済み → タップは Image が受ける)
            TextMeshProUGUI t = Label(rt, "Label", label, fontSize, fg);
            Place((RectTransform)t.transform, 0.5f, 0.5f, 1f, 1f);

            return btn;
        }

        /// <summary>
        /// スプライト表示 Image を生成する。ドット絵 (64x64) 前提で preserveAspect=true。
        /// raycastTarget はデフォルト (true) のまま: 敵タップ (エルフの拘束) 等で
        /// スプライトに Button を後付けするケースがあるため。
        /// </summary>
        public static Image SpriteImage(Transform parent, string name, Sprite sprite)
        {
            RectTransform rt = NewRect(parent, name);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true; // 比率配置でセルが正方形でなくてもドット絵を歪ませない
            return img;
        }

        /// <summary>
        /// 単色矩形 (罫線・セル背景・オーバーレイ装飾用)。raycastTarget=false 固定。
        /// タップを遮断したい場合は Panel(bg:) を使うこと。
        /// </summary>
        public static Image ColorRect(Transform parent, string name, Color color)
        {
            RectTransform rt = NewRect(parent, name);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// 中心 (cx, cy) + サイズ (w, h) を親比率 0..1 で完全指定する。
        /// アンカー自体を矩形に広げ offset を 0 にすることで、親のリサイズに常に追従する
        /// (SwiftUI の GeometryProxy 比率計算と等価な「固定 px ゼロ」レイアウト)。
        /// 例: Place(rt, 0.5f, 0.9f, 0.8f, 0.08f) = 画面上部に幅80%・高さ8%のバー。
        /// </summary>
        public static void Place(RectTransform rt, float cx, float cy, float w, float h)
        {
            rt.anchorMin = new Vector2(cx - w * 0.5f, cy - h * 0.5f);
            rt.anchorMax = new Vector2(cx + w * 0.5f, cy + h * 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Resources/Sprites/{name}.png をロードする。
        /// 例: LoadSprite("hero") / LoadSprite("red_oni")。
        /// 見つからない場合は null を返しつつ警告 (SpriteImage は sprite=null でも生成可能)。
        /// </summary>
        public static Sprite LoadSprite(string name)
        {
            Sprite sprite = Resources.Load<Sprite>("Sprites/" + name);
            if (sprite == null)
            {
                Debug.LogWarning($"[UIFactory] スプライトが見つからない: Resources/Sprites/{name}");
            }
            return sprite;
        }

        // ---- 内部実装 ----

        /// <summary>
        /// RectTransform 付き GameObject を親いっぱい (full-stretch) で生成する。
        /// SetParent(parent, false) でワールド座標を保持しない = UI の基本作法。
        /// </summary>
        private static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }
    }
}
