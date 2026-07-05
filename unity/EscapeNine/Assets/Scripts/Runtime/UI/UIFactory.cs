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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
            // HD-2D (2026-07-06): 角丸 + 上明/下暗の baked ベベルスプライトに差し替え。
            // img.color = bg の外部契約 (GetComponent<Image>() で拾って動的に色を変える呼び出し元多数)
            // は一切変えない。sprite/type だけを足すことで、既存の色制御コードは無変更のまま
            // 全画面の TextButton が角丸・立体感を得る (UIFactory 一元集約の波及)。
            img.sprite = ButtonBevelSprite();
            img.type = Image.Type.Sliced;
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

        // ================================================================
        // HD-2D 深度プリミティブ (2026-07-06 追加)
        // 仕様: unity/verify/UI_HD2D_REDESIGN_SPEC.md セクション A。
        // 外部アセット非依存: 角丸/影/グラデは実行時 Texture2D 生成 + キャッシュ
        // (メトロノーム音等、本プロジェクトの手続き的生成方針と同じ思想)。
        // ================================================================

        /// <summary>生成済み Sprite のキャッシュ (RoundedSprite/SoftShadowSprite/ButtonBevelSprite 共用)。</summary>
        private static readonly Dictionary<string, Sprite> _depthSpriteCache = new Dictionary<string, Sprite>();

        /// <summary>Card() が使う既定の角丸半径。0 = 角丸なしの四角（オーナー指定: 角丸でなく四角のまま立体に）。
        /// 立体感は落ち影(SoftShadow)＋上明下暗グラデ＋上辺ハイライトで出す（角の丸みには依存しない）。</summary>
        private const int DefaultCardCornerRadiusPx = 0;

        // ---- Card() の影オフセット定数 (通常状態 / 押下状態で共用。CardShadowPress 側も同じ値を参照) ----
        private const float ShadowRestDx = 0.014f;
        private const float ShadowRestDy = 0.020f;
        private const float ShadowRestExtra = 0.05f;
        private const float ShadowPressDx = 0.004f;
        private const float ShadowPressDy = 0.006f;
        private const float ShadowPressExtra = 0.015f;

        /// <summary>
        /// 角丸矩形の Sprite (9-slice border 付き、キャッシュ)。色は白 (フル不透明) の単色シルエットで、
        /// 呼び出し側が Image.color で自由に tint する前提 (既存の TextButton/Panel 等の
        /// 「外部から .color を直接書き換える」契約を壊さないための設計)。
        /// </summary>
        public static Sprite RoundedSprite(int cornerRadiusPx, int sizePx = 64)
        {
            string key = "Round_" + cornerRadiusPx + "_" + sizePx;
            if (_depthSpriteCache.TryGetValue(key, out Sprite cached)) return cached;

            Texture2D tex = GenerateRoundedRectTexture(sizePx, cornerRadiusPx, 2f, 1f, 1f);
            float border = cornerRadiusPx + 2f;
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, sizePx, sizePx), new Vector2(0.5f, 0.5f),
                1f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            _depthSpriteCache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 柔らかい影の Sprite (角丸 + アルファのぼかし、9-slice border 付き、キャッシュ)。
        /// 色は白のシルエットなので、呼び出し側で黒半透明等に tint して使う (Card() の影レイヤー参照)。
        /// </summary>
        public static Sprite SoftShadowSprite(int cornerRadiusPx = 30, int sizePx = 128, int featherPx = 20)
        {
            string key = "Shadow_" + cornerRadiusPx + "_" + sizePx + "_" + featherPx;
            if (_depthSpriteCache.TryGetValue(key, out Sprite cached)) return cached;

            Texture2D tex = GenerateRoundedRectTexture(sizePx, cornerRadiusPx, featherPx, 1f, 1f);
            float border = cornerRadiusPx + featherPx;
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, sizePx, sizePx), new Vector2(0.5f, 0.5f),
                1f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            _depthSpriteCache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 縦グラデーションの Sprite (上明・下暗＝上からのライティング感、キャッシュ)。
        /// 9-slice ではなく Image.Type.Simple で全面ストレッチして使う前提 (border なし)。
        /// </summary>
        public static Sprite VerticalGradientSprite(Color top, Color bottom, int heightPx = 64)
        {
            string key = "Grad_" + ColorUtility.ToHtmlStringRGBA(top) + "_" + ColorUtility.ToHtmlStringRGBA(bottom) + "_" + heightPx;
            if (_depthSpriteCache.TryGetValue(key, out Sprite cached)) return cached;

            int width = 4;
            int height = Mathf.Max(2, heightPx);
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1); // y=0 (テクスチャ下端) → 描画時は矩形の下端に対応
                Color32 c = Color.Lerp(bottom, top, t);
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f),
                1f, 0, SpriteMeshType.FullRect, Vector4.zero);
            _depthSpriteCache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// TextButton() 専用の角丸ベベル Sprite (上をわずかに明るく・下をわずかに暗く baked した白ベース)。
        /// Image.color の外部 tint (bg) と乗算されるため、呼び出し側がどんな色を渡しても
        /// 「上からの光」を感じる立体感が乗る。VerticalGradientSprite と役割が違う (こちらは
        /// 角丸マスクと明暗を 1 枚に焼き込み、TextButton の単一 Image という既存構造を変えずに使える)。
        /// </summary>
        private static Sprite ButtonBevelSprite(int cornerRadiusPx = 0, int sizePx = 64)
        {
            string key = "Bevel_" + cornerRadiusPx + "_" + sizePx;
            if (_depthSpriteCache.TryGetValue(key, out Sprite cached)) return cached;

            Texture2D tex = GenerateRoundedRectTexture(sizePx, cornerRadiusPx, 2f, 1f, 0.80f);
            float border = cornerRadiusPx + 2f;
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, sizePx, sizePx), new Vector2(0.5f, 0.5f),
                1f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            _depthSpriteCache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 角丸矩形の signed-distance ベースアルファマスクを焼き込んだテクスチャを生成する。
        /// feather (px) の分だけ境界を smoothstep でぼかす (RoundedSprite は 2px = AA 目的のみ、
        /// SoftShadowSprite は 20px 前後 = 本格的なぼかし)。topBrightness/bottomBrightness で
        /// RGB に縦グラデの明度を焼き込める (ButtonBevelSprite が使用。RoundedSprite/SoftShadowSprite
        /// は両方 1 = 均一な白)。
        /// </summary>
        private static Texture2D GenerateRoundedRectTexture(int sizePx, float cornerRadiusPx, float featherPx,
            float topBrightness, float bottomBrightness)
        {
            int size = Mathf.Max(8, sizePx);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            // 角丸コア (丸める前の矩形) の半径。feather 分の余白をテクスチャ端に確保する。
            float coreHalf = Mathf.Max(0f, half - featherPx - cornerRadiusPx);

            for (int y = 0; y < size; y++)
            {
                float brightness = Mathf.Lerp(bottomBrightness, topBrightness, y / (float)(size - 1));
                byte c = (byte)Mathf.RoundToInt(Mathf.Clamp01(brightness) * 255f);

                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    float qx = Mathf.Abs(px) - coreHalf;
                    float qy = Mathf.Abs(py) - coreHalf;
                    float outsideX = Mathf.Max(qx, 0f);
                    float outsideY = Mathf.Max(qy, 0f);
                    float dist = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY)
                        + Mathf.Min(Mathf.Max(qx, qy), 0f) - cornerRadiusPx;

                    float t = featherPx > 0.0001f ? Mathf.Clamp01(dist / featherPx) : (dist > 0f ? 1f : 0f);
                    float smooth = t * t * (3f - 2f * t); // 0(内側)→1(外側) の smoothstep
                    float alpha = 1f - smooth;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);

                    pixels[y * size + x] = new Color32(c, c, c, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>
        /// スプライト指定の Image を生成する (SpriteImage() とは別系統: preserveAspect を立てない
        /// = 9-slice/全面ストレッチ背景用。Card() の影・グラデ層や、画面側のパララックス背景等、
        /// 「矩形いっぱいに敷き詰めたい」ケース全般で使う)。
        /// </summary>
        public static Image FillImage(Transform parent, string name, Sprite sprite, Image.Type type = Image.Type.Simple)
        {
            RectTransform rt = NewRect(parent, name);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            return img;
        }

        /// <summary>
        /// 「影 → 角丸グラデ背景 → 上辺ハイライト」の3層パネル (Panel() の立体版)。
        /// 返り値の root は Panel() と同じ「未配置の親いっぱい RectTransform」で、
        /// 呼び出し側が Place() で最終サイズへ配置してから子要素 (Label 等) を足す使い方をする。
        /// shadow (out) は CardButton 相当の合成 (押下時に影を縮める演出) を組みたい呼び出し側向けに
        /// 影レイヤーの RectTransform を返す (不要なら out _ で捨ててよい)。
        /// マスクは Unity 標準の Mask コンポーネント (showMaskGraphic=false) で実現しており、
        /// 角丸シルエット自体は非表示のまま、中のグラデーション/ハイライトだけがクリップされて見える。
        /// </summary>
        public static RectTransform Card(Transform parent, string name, out RectTransform shadow,
            Color? topColor = null, Color? bottomColor = null, int cornerRadiusPx = DefaultCardCornerRadiusPx,
            float shadowAlpha = 0.42f)
        {
            RectTransform root = NewRect(parent, name);

            // 1. 影: root よりわずかに大きく・右下にオフセットした柔らかい影
            Image shadowImg = FillImage(root, name + "_Shadow", SoftShadowSprite(cornerRadiusPx + 4, 128, 22), Image.Type.Sliced);
            shadowImg.color = new Color(0f, 0f, 0f, shadowAlpha);
            shadowImg.raycastTarget = false;
            shadow = (RectTransform)shadowImg.transform;
            Place(shadow, 0.5f + ShadowRestDx, 0.5f - ShadowRestDy, 1f + ShadowRestExtra, 1f + ShadowRestExtra);

            // 2. 角丸マスク (非表示の Mask グラフィック。中の子だけがこの形にクリップされる)
            RectTransform maskRt = NewRect(root, name + "_Mask");
            Image maskImg = maskRt.gameObject.AddComponent<Image>();
            maskImg.sprite = RoundedSprite(cornerRadiusPx, 64);
            maskImg.type = Image.Type.Sliced;
            maskImg.color = Color.white;
            maskImg.raycastTarget = false;
            Mask mask = maskRt.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            Place(maskRt, 0.5f, 0.5f, 1f, 1f);

            // 2a. 角丸グラデ背景 (マスクでクリップされて丸角に見える)
            Color top = topColor ?? UITheme.BackgroundSecondary;
            Color bottom = bottomColor ?? UITheme.Background;
            Image gradImg = FillImage(maskRt, "Gradient", VerticalGradientSprite(top, bottom, 64), Image.Type.Simple);
            gradImg.color = Color.white;
            gradImg.raycastTarget = false;
            Place((RectTransform)gradImg.transform, 0.5f, 0.5f, 1f, 1f);

            // 2b. 上辺ハイライト (上からのライティング感。マスク内なので角も自動でクリップされる)
            Image highlight = ColorRect(maskRt, "TopHighlight", new Color(1f, 1f, 1f, 0.10f));
            Place((RectTransform)highlight.transform, 0.5f, 0.965f, 0.94f, 0.05f);

            return root;
        }

        /// <summary>
        /// Card() の影に「押下で縮む」フィードバックを付ける。buttonGO は実際にタップを受ける
        /// GameObject (Button コンポーネントの乗った GameObject) を渡すこと — Card のルートに
        /// 付けると Button 自体がイベントを消費してしまい発火しないため。
        /// </summary>
        public static void AttachCardPressFeedback(GameObject buttonGO, RectTransform shadow)
        {
            if (buttonGO == null || shadow == null) return;
            CardShadowPress feedback = buttonGO.AddComponent<CardShadowPress>();
            feedback.Shadow = shadow;
        }

        /// <summary>
        /// Card() の影の押下フィードバック本体。ShadowRest/ShadowPress 定数を Place() で往復するだけ。
        /// IPointerCancelHandler は UnityEngine.EventSystems に存在しない (uGUI 標準の Selectable も
        /// Down/Up/Enter/Exit のみ) ため、Exit で release 相当のフィードバックに戻す方針でカバーする。
        /// </summary>
        private sealed class CardShadowPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
            IPointerExitHandler
        {
            public RectTransform Shadow;

            public void OnPointerDown(PointerEventData eventData) => Apply(true);
            public void OnPointerUp(PointerEventData eventData) => Apply(false);
            public void OnPointerExit(PointerEventData eventData) => Apply(false);

            private void Apply(bool pressed)
            {
                if (Shadow == null) return;
                float dx = pressed ? ShadowPressDx : ShadowRestDx;
                float dy = pressed ? ShadowPressDy : ShadowRestDy;
                float extra = pressed ? ShadowPressExtra : ShadowRestExtra;
                Place(Shadow, 0.5f + dx, 0.5f - dy, 1f + extra, 1f + extra);
            }
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
