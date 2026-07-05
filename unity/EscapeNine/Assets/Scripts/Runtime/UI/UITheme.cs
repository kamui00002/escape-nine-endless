// UITheme.cs
// Swift 正本: Utilities/Constants.swift の `enum GameColors`(全15色) と Utilities/Fonts.swift。
// 「明るい冒険ファンタジー系カラーパレット」を Unity 側の唯一のテーマ定義として移植する。
// 色値の複製を防ぐため、UI コードは必ず本クラス経由で色・フォントを取得すること。

using UnityEngine;
using TMPro;

namespace EscapeNine.Runtime.UI
{
    /// <summary>
    /// アプリ全体のカラーパレットとフォントの供給元。
    /// Swift 版 GameColors の hex 文字列をそのまま保持し、ColorUtility でパースする
    /// (RGB 値をハードコードすると Swift 正本との突き合わせが困難になるため)。
    /// </summary>
    public static class UITheme
    {
        // ---- GameColors 移植 (Constants.swift 148-165行) ----

        /// <summary>main #f4a460 サンディブラウン (明るい冒険の色)</summary>
        public static readonly Color Main = Hex("#f4a460");

        /// <summary>accent #daa520 ゴールデンロッド (明るいゴールド)</summary>
        public static readonly Color Accent = Hex("#daa520");

        /// <summary>background #2c1810 明るい茶色のダンジョン</summary>
        public static readonly Color Background = Hex("#2c1810");

        /// <summary>backgroundSecondary #3d2817 少し明るい茶色</summary>
        public static readonly Color BackgroundSecondary = Hex("#3d2817");

        /// <summary>text #f5deb3 ベージュ (羊皮紙の色)</summary>
        public static readonly Color TextColor = Hex("#f5deb3");

        /// <summary>textSecondary #ffd700 明るいゴールドテキスト。Swift の textSecondary に対応。</summary>
        public static readonly Color GoldText = Hex("#ffd700");

        /// <summary>warning #ff6347 トマトレッド (危険)</summary>
        public static readonly Color Warning = Hex("#ff6347");

        /// <summary>success #90ee90 ライトグリーン (成功)</summary>
        public static readonly Color Success = Hex("#90ee90");

        /// <summary>player #98fb98 ペールグリーン (勇者マスのハイライト)</summary>
        public static readonly Color Player = Hex("#98fb98");

        /// <summary>enemy #ff6347 トマトレッド (敵マスのハイライト)</summary>
        public static readonly Color Enemy = Hex("#ff6347");

        /// <summary>grid #4a3728 明るいグリッドの色</summary>
        public static readonly Color Grid = Hex("#4a3728");

        /// <summary>gridBorder #daa520 ゴールドの枠</summary>
        public static readonly Color GridBorder = Hex("#daa520");

        /// <summary>available #ffd700 ゴールド (移動可能マス)</summary>
        public static readonly Color Available = Hex("#ffd700");

        /// <summary>fog #3d2817 霧マスの色 (特殊ルール: 霧)</summary>
        public static readonly Color Fog = Hex("#3d2817");

        /// <summary>disappeared #1a1a1a 消失マスの色 (特殊ルール: マス消失)</summary>
        public static readonly Color Disappeared = Hex("#1a1a1a");

        // ---- HD-2D ボタン/タイトル装飾色 (2026-07-06 追加) ----
        // オーナー実機確認で「サブボタンが背景と同色で見えない」(BackgroundSecondary #3d2817 が
        // 背景 #2c1810 とほぼ同明度) と指摘された修正のための専用色。既存の Main/Accent 等の
        // 汎用色と役割が違う (ボタンの塗り/縁取り専用) ため、用途名で新規に定義する。

        /// <summary>buttonFill #6b4a30: Home画面のサブボタン塗り色。背景 (#2c1810) から明度差を確保する
        /// ため BackgroundSecondary より明確に明るい暖色にした。TextButton の BevelSprite (旧
        /// ButtonBevelSprite) が上明下暗のベベルを自動で焼き込むため、この1色を渡すだけで
        /// 「上が明るく下がやや暗い」木目調のグラデ塗りになる。</summary>
        public static readonly Color ButtonFill = Hex("#6b4a30");

        /// <summary>buttonHighlightLine #9a7550: ボタン上辺のハイライト線色 (エンボス表現)。</summary>
        public static readonly Color ButtonHighlightLine = Hex("#9a7550");

        /// <summary>titleGradientTop #ffe27a: タイトルロゴの金グラデ上端 (明るいメタリックゴールド)。</summary>
        public static readonly Color TitleGradientTop = Hex("#ffe27a");

        /// <summary>titleGradientBottom #d4901f: タイトルロゴの金グラデ下端。</summary>
        public static readonly Color TitleGradientBottom = Hex("#d4901f");

        /// <summary>titleOutline #1a0e08: タイトルロゴの太いアウトライン色 (濃茶)。</summary>
        public static readonly Color TitleOutline = Hex("#1a0e08");

        // ---- フォント ----

        private static Font _font;

        /// <summary>
        /// 日本語表示可能な動的フォント。
        /// TMP Essentials 未導入のため legacy Text 用の OS フォントを使う。
        /// iOS 実機では Hiragino 系、Android では Noto/Roboto 系が拾える想定。
        /// 全滅時は Unity 内蔵 LegacyRuntime.ttf (Liberation Sans = 日本語グリフ無し) に
        /// フォールバックするが、その場合は日本語が豆腐になるため警告を出す。
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font == null) _font = ResolveFont();
                return _font;
            }
        }

        // ---- TMP フォント (Wave 1: TextMeshPro 化) ----
        // Font/ResolveFont() は削除しない: Wave 1 完了直後は UIFactory 以外からの参照が
        // 残っている可能性があり、ここで消すとコンパイルが割れる。清掃は別タスクで行う。

        private static TMP_FontAsset _fontAsset;

        /// <summary>
        /// 日本語表示可能な TMP_FontAsset (DotGothic16、動的アトラス)。
        /// UIFactory.Label/TextButton はこれを唯一のフォント供給元として使う。
        /// Resources/Fonts/DotGothic16-Regular.ttf から Font をロードし、
        /// TMP_FontAsset.CreateFontAsset() でその場に動的フォントアセットを生成する
        /// (動的アトラスのため、日本語グリフは実際に使われた文字だけオンデマンドで
        /// アトラスへ焼かれる = .asset をリポジトリに増やさずに済む)。
        /// ロード/生成失敗時 (.ttf の Font Import Settings で "Include Font Data" が
        /// 無効な場合など) は ResolveFont() の OS フォント (Hiragino 等) を
        /// TMP_FontAsset でラップしてフォールバックする。
        /// 注意: TMP_Settings.defaultFontAsset は使わない — これは "TMP Settings" という
        /// 名前の Resources アセット (TMP Essentials インポートで生成される) に依存し、
        /// 本プロジェクトは TMP Essentials 未導入のため instance が null のままとなり、
        /// アクセスすると NullReferenceException になる (TMP_Settings.cs の instance
        /// ゲッターで確認済み)。CreateFontAsset(Font) 自体は TMP_Settings に依存しないため
        /// ResolveFont() 経由のフォールバックは Essentials 有無に関わらず安全。
        /// </summary>
        public static TMP_FontAsset FontAsset
        {
            get
            {
                if (_fontAsset == null) _fontAsset = ResolveFontAsset();
                return _fontAsset;
            }
        }

        private static TMP_FontAsset ResolveFontAsset()
        {
            const string resourcePath = "Fonts/DotGothic16-Regular";

            Font dotGothic = Resources.Load<Font>(resourcePath);
            if (dotGothic != null)
            {
                TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(dotGothic);
                if (created != null) return created;
            }

            Debug.LogWarning(
                $"[UITheme] DotGothic16 の TMP_FontAsset 生成に失敗 (Resources/{resourcePath})。" +
                "Font Import Settings で \"Include Font Data\" が有効か確認すること。" +
                "ResolveFont() の OS フォールバック (Hiragino 等) から TMP_FontAsset を生成する。");

            Font fallbackFont = ResolveFont();
            TMP_FontAsset fallbackAsset = TMP_FontAsset.CreateFontAsset(fallbackFont);
            if (fallbackAsset != null) return fallbackAsset;

            Debug.LogError("[UITheme] TMP_FontAsset のフォールバック生成にも失敗。文字が表示されない可能性がある。");
            return null;
        }

        /// <summary>色のアルファだけ差し替えるユーティリティ。Swift の .opacity(a) 相当。</summary>
        public static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        // ---- 内部実装 ----

        /// <summary>
        /// OS フォントを優先順で解決する。
        /// インストール済み一覧が取得できる環境では一覧照合してから生成する
        /// (CreateDynamicFontFromOSFont は存在しないフォント名でも非 null を返す
        /// プラットフォームがあり、その場合グリフが描画されないため)。
        /// </summary>
        private static Font ResolveFont()
        {
            // 日本語グリフを持つフォントを優先。Roboto は最終手段 (英数のみ)。
            string[] candidates =
            {
                "Hiragino Sans",
                "Hiragino Kaku Gothic ProN",
                "Noto Sans CJK JP",
                "Roboto",
            };

            string[] installed = Font.GetOSInstalledFontNames();
            bool hasList = installed != null && installed.Length > 0;

            foreach (string name in candidates)
            {
                if (hasList && !Contains(installed, name)) continue;

                // サイズは動的フォントなので任意 (Text 側の fontSize が優先される)。
                Font created = Font.CreateDynamicFontFromOSFont(name, 32);
                if (created != null) return created;
            }

            Debug.LogWarning("[UITheme] OS フォントが見つからないため LegacyRuntime.ttf にフォールバック (日本語が表示できない可能性)");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static bool Contains(string[] list, string name)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == name) return true;
            }
            return false;
        }

        /// <summary>hex 文字列 → Color。パース失敗はマゼンタで即座に視認できるようにする。</summary>
        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c)) return c;
            Debug.LogError($"[UITheme] 不正な hex 色: {hex}");
            return Color.magenta;
        }
    }
}
