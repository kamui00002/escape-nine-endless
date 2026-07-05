// HomeScreen.cs
// Swift 正本: Views/Home/HomeView.swift (タイトル / ボタン列 / 最高到達階層 / チュートリアル自動表示)
//             Views/Settings/SettingsView.swift の #if DEBUG debugSection (管理者用設定) の一部を
//             本画面の DangerZone として移植 (DEBUG ビルドのみ表示)。
// 注: Views/Home/DangerZoneView.swift は「鬼の隣接 8 マスの危険圏オーバーレイ」(ゲーム盤用) であり
//     本画面のデバッグ枠とは別物。あちらは GameScreen / オンボーディング側の担当。
//
// レイアウトは全て UIFactory.Place の親比率 0..1 (固定 px 禁止)。
// 座標系は Unity 左下原点: cy が大きいほど画面上部 (Swift の top-left と逆)。

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EscapeNine.Core;
using EscapeNine.Runtime.UI.Fx;

namespace EscapeNine.Runtime.UI
{
    /// <summary>
    /// ホーム画面。SwiftUI 版 HomeView の NavigationStack 遷移を ScreenRouter.Show に置き換える。
    /// バナー広告領域 (Swift: BannerAdView) は Phase 3 (AdMob) 送りのため置かない。
    /// </summary>
    public sealed class HomeScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Home;

        // App.cs は screen.BuildUI() → Router.Register(screen) の順で呼び、Register 内でも
        // BuildUI() が走るため計 2 回呼ばれる。二重構築 (UI が重なって全ボタン二重化) を防ぐガード。
        private bool _built;

        // ---- OnShow のたびに更新する動的要素への参照 ----
        private TextMeshProUGUI _floorNumberLabel;      // 最高到達階層の数字
        private Image _characterImage;       // 選択中キャラのスプライト
        private TextMeshProUGUI _characterNameLabel;    // 選択中キャラの名前
        private GameObject _dailyButtonRoot; // デイリーチャレンジボタン (階層10未到達では非表示)
        private TextMeshProUGUI _dailyMainLabel;        // 同ボタンのメインテキスト (完了状態で色切替。Swift: isCompleted 分岐)
        private TextMeshProUGUI _dailySubLabel;         // 同ボタンのサブテキスト (完了状態で文言切替)
        private GameObject _dailyNewBadge;   // NEW バッジ (未クリア時のみ表示)

        // トースト (記録確認等の簡易通知用。ShopScreen の同名パターンを踏襲した簡易実装)
        private RectTransform _toast;
        private TextMeshProUGUI _toastLabel;
        private Coroutine _toastRoutine;
        private const float ToastDisplaySeconds = 1.5f;

        // ---- HD-2D (2026-07-06 追加): 背景パララックス / タイトル浮遊の駆動状態 ----
        // どちらも「酔わない範囲の極小演出」。FxKit.MotionEnabled (Reduce Motion) を毎 tick 見て、
        // 無効時は即座に基準位置へ戻す (BeatPulse.SettleToBase と同じ考え方)。
        private RectTransform _bgFar;
        private RectTransform _bgMid;
        private RectTransform _bgNear;
        private Coroutine _parallaxRoutine;
        private RectTransform _titleLabelRt;
        private RectTransform _titleShadowRt;
        private Coroutine _titleFloatRoutine;
        private const float TitleBaseCy = 0.905f;
        private const float TitleShadowDx = 0.004f;
        private const float TitleShadowDy = 0.006f;

        // HD-2D (2026-07-06): タイトルをロゴ化する一環で一回り大きくする (110→122)。
        // 高さ比率もフォントサイズ増に合わせて拡張 (0.06→0.068)。フロート演出 (TitleFloatRoutine) 側の
        // Place() 呼び出しもこの定数を使うよう揃える (でないとフロートのたびに旧サイズへ戻ってしまう)。
        private const int TitleFontSize = 122;
        private const float TitleHeightRatio = 0.068f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private TextMeshProUGUI _dbgFloorValueLabel;    // 開始階層の現在値
        private TextMeshProUGUI _dbgBpmValueLabel;      // BPMオーバーライドの現在値
        private TextMeshProUGUI _dbgTurnCountdownValueLabel; // ターンカウントダウンビート数の現在値
        private TextMeshProUGUI _dbgAiLabel;            // AI難易度サイクルボタンのラベル
        private TextMeshProUGUI _dbgUnlockLabel;        // 全キャラ解放トグルのラベル
        private TextMeshProUGUI _dbgSkipLabel;          // 開始カウントダウン省略トグルのラベル
#endif

        public override void BuildUI()
        {
            if (_built) return; // Router.Register が 1 回だけ呼ぶ (再入防御。呼び出し元は ScreenRouter.Register のみ)
            _built = true;

            // 画面ルートを親いっぱいに固定 (シーン側の配置ミスに影響されないための防御)
            var root = GetComponent<RectTransform>();
            if (root != null)
            {
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
            }

            // 背景はノッチ下まで全面に敷く (HD-2D: 単色 → 3層パララックスへ、2026-07-06)
            BuildBackgroundParallax(transform);

            // コンテンツはセーフエリア内に収める (SwiftUI では自動処理されていた部分)
            var safe = UIFactory.Panel(transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildTitleSection(safe);
            BuildCharacterSection(safe);
            BuildButtonSection(safe);
            BuildHighestFloorSection(safe);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BuildDangerZone(safe);
#endif

            // トーストは最後に構築し、DangerZone (DEBUG時) を含む他要素より必ず手前に描画されるようにする
            BuildToast(safe);
        }

        public override void OnShow(object payload)
        {
            // Swift: onAppear の playerViewModel.reload()。
            // ゲーム後に GameController が PlayerPrefs (highestFloor 等) を更新しているため再読込して同期。
            App.I.Player.Reload();
            RefreshDynamic();

            // Swift: onAppear / path の pop 検知でメニュー BGM を復帰。
            // AudioDirector.PlayBgm は同一曲再生中なら no-op なので毎回呼んで安全。
            App.I.Audio.PlayMenuBgm();

            // HD-2D (2026-07-06): 背景パララックス / タイトル浮遊を張り直す。
            // GameObject の非活性化で既存コルーチンは自動停止するため、再表示のたびに開始し直す
            // (ToastRoutine と同じ「既存があれば止めてから張り直す」パターン)。
            if (_parallaxRoutine != null) StopCoroutine(_parallaxRoutine);
            _parallaxRoutine = StartCoroutine(ParallaxDriftRoutine());
            if (_titleFloatRoutine != null) StopCoroutine(_titleFloatRoutine);
            _titleFloatRoutine = StartCoroutine(TitleFloatRoutine());

            // 初回起動はチュートリアルへ自動遷移 (Swift: fullScreenCover(showTutorial))。
            // 注意: ここで直接 Router.Show を呼ぶと Router.Show(Home) の実行途中に再入して
            //       _current / active 状態が壊れるため、必ず 1 フレーム遅らせる。
            // Swift の二段階判定 (HomeView.onAppear): hasSeenTutorialV1_1 == false なら
            // v1.1 動的オンボーディングを表示し、true なら hasSeenTutorial の値に関わらず
            // 表示しない。本画面の TutorialScreen は v1.1 の 6 ページ構成を移植したものなので、
            // 判定基準は HasSeenTutorialV11 に一本化する (HasSeenTutorial 単独では判定しない)。
            // TutorialScreen.Complete() は両方のフラグを true にするため、旧キー
            // (HasSeenTutorial のみ true) の既存ユーザーも 1 回だけ通る migration 挙動を保つ。
            if (!App.I.Player.HasSeenTutorialV11)
            {
                StartCoroutine(ShowTutorialNextFrame());
            }
        }

        public override void OnHide()
        {
            HideToast();
            if (_parallaxRoutine != null)
            {
                StopCoroutine(_parallaxRoutine);
                _parallaxRoutine = null;
            }
            if (_titleFloatRoutine != null)
            {
                StopCoroutine(_titleFloatRoutine);
                _titleFloatRoutine = null;
            }
        }

        private IEnumerator ShowTutorialNextFrame()
        {
            yield return null;
            // HasSeenTutorial / HasSeenTutorialV11 の書き込みは Tutorial 画面完了時の責務
            // (Swift: OnboardingTutorialView 完了時に hasSeenTutorialV1_1 = true)。
            App.I.Router.Show(ScreenId.Tutorial);
        }

        // MARK: - Title Section (Swift: titleSection)

        private void BuildTitleSection(RectTransform parent)
        {
            // HD-2D (2026-07-06): タイトルを「看板/ロゴ」に格上げする一環でグローを追加。
            // 手続き生成の SoftShadowSprite を暖色(ゴールド)で薄く敷き、本体より一回り大きく・中心は
            // 同じ位置に置く。シェーダー/フォント機能に依存しない確実な「輝き」表現 (DotGothic16 の
            // 動的生成フォントで TMP のアウトライン/underlay が効かない場合でも、このレイヤーだけで
            // 最低限の立体感は確保できる保険を兼ねる)。
            var titleGlow = UIFactory.FillImage(parent, "TitleGlow", UIFactory.SoftShadowSprite(40, 128, 46));
            titleGlow.color = UITheme.WithAlpha(UITheme.GoldText, 0.28f);
            titleGlow.raycastTarget = false;
            UIFactory.Place((RectTransform)titleGlow.transform, 0.5f, TitleBaseCy, 1.04f, TitleHeightRatio + 0.05f);

            // 影 (奥行き): 本体よりわずかに右下・暗色の複製を先に敷いてから本体を重ねる
            // (シェーダー不要の疑似ドロップシャドウ。HD-2D、2026-07-06 追加)。
            var titleShadow = UIFactory.Label(parent, "TitleShadow", "ESCAPE NINE", TitleFontSize,
                UITheme.WithAlpha(Color.black, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold);
            _titleShadowRt = (RectTransform)titleShadow.transform;
            UIFactory.Place(_titleShadowRt, 0.5f + TitleShadowDx, TitleBaseCy - TitleShadowDy, 0.94f, TitleHeightRatio);

            // 本体: メタリックゴールドの縦グラデ (TMP のメッシュ頂点カラー機能。シェーダーに依存せず
            // 確実に効く) + 太めの暗色アウトライン (TMP マテリアルのシェーダー機能。DotGothic16 の
            // 動的生成フォントが対応するかは実機確認が必要なため HasProperty で防御し、非対応でも
            // 例外や警告を出さず「効かないだけ」に留める。代替の立体感は上のグロー/影レイヤーが担保する)。
            var title = UIFactory.Label(parent, "TitleLabel", "ESCAPE NINE", TitleFontSize, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            _titleLabelRt = (RectTransform)title.transform;
            UIFactory.Place(_titleLabelRt, 0.5f, TitleBaseCy, 0.94f, TitleHeightRatio);
            ApplyTitleGoldGradientAndOutline(title);

            var subtitle = UIFactory.Label(parent, "SubtitleLabel", "Endless Dungeon", 52,
                UITheme.WithAlpha(UITheme.TextColor, 0.8f));
            UIFactory.Place((RectTransform)subtitle.transform, 0.5f, 0.862f, 0.8f, 0.03f);

            BuildTitleDivider(parent);
        }

        /// <summary>
        /// タイトルの金グラデ+アウトライン (HD-2D、2026-07-06)。enableVertexGradient は TMP のメッシュ
        /// 頂点カラーのみで完結する機能のためシェーダーに依存せず確実に効く。アウトラインはマテリアルの
        /// シェーダー機能 (_OutlineWidth/_OutlineColor) 依存のため、HasProperty で防御した上で設定する。
        /// title.fontMaterial (fontSharedMaterial ではない) を使うことで、このラベル1個体だけに
        /// マテリアルのインスタンスを複製させ、他の全 TMP テキスト (共有 FontAsset 経由) へ
        /// 副作用が及ばないようにする。
        /// </summary>
        private static void ApplyTitleGoldGradientAndOutline(TextMeshProUGUI title)
        {
            // colorGradient は color と乗算されるため、意図した色をそのまま出すには白にしておく。
            title.color = Color.white;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(UITheme.TitleGradientTop, UITheme.TitleGradientTop,
                UITheme.TitleGradientBottom, UITheme.TitleGradientBottom);

            Material mat = title.fontMaterial;
            if (mat != null && mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, UITheme.TitleOutline);
            }
        }

        /// <summary>タイトル下の装飾区切り (二重線 + 中央の小さな菱形)。HD-2D ロゴ化の一部 (2026-07-06)。</summary>
        private static void BuildTitleDivider(RectTransform parent)
        {
            var lineTop = UIFactory.ColorRect(parent, "TitleDividerTop", UITheme.WithAlpha(UITheme.Accent, 0.6f));
            UIFactory.Place((RectTransform)lineTop.transform, 0.5f, 0.840f, 0.30f, 0.0028f);

            var lineBottom = UIFactory.ColorRect(parent, "TitleDividerBottom", UITheme.WithAlpha(UITheme.Accent, 0.35f));
            UIFactory.Place((RectTransform)lineBottom.transform, 0.5f, 0.834f, 0.20f, 0.0018f);

            var diamond = UIFactory.ColorRect(parent, "TitleDividerDiamond", UITheme.WithAlpha(UITheme.GoldText, 0.85f));
            var diamondRt = (RectTransform)diamond.transform;
            UIFactory.Place(diamondRt, 0.5f, 0.837f, 0.016f, 0.007f);
            diamondRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        // MARK: - Character Section (Swift 版 HomeView には無い。タスク要件「キャラ表示」による意図的追加)

        private void BuildCharacterSection(RectTransform parent)
        {
            // 接地影 (足元の柔らかい楕円影。「浮いて見える」を「立っている」に変える。HD-2D、2026-07-06)
            var groundShadow = UIFactory.FillImage(parent, "CharacterGroundShadow",
                UIFactory.SoftShadowSprite(40, 128, 30));
            groundShadow.color = UITheme.WithAlpha(Color.black, 0.35f);
            groundShadow.raycastTarget = false;
            UIFactory.Place((RectTransform)groundShadow.transform, 0.5f, 0.748f, 0.22f, 0.045f);

            // スプライトは OnShow のたびに選択キャラで差し替える (CharacterSelect から戻った時に反映)
            _characterImage = UIFactory.SpriteImage(parent, "CharacterImage", null);
            _characterImage.raycastTarget = false; // ホームでは飾りなのでタップを吸わせない
            UIFactory.Place((RectTransform)_characterImage.transform, 0.5f, 0.79f, 0.30f, 0.075f);

            _characterNameLabel = UIFactory.Label(parent, "CharacterNameLabel", "", 40, UITheme.TextColor);
            UIFactory.Place((RectTransform)_characterNameLabel.transform, 0.5f, 0.744f, 0.5f, 0.025f);
        }

        // MARK: - HD-2D 背景パララックス (Swift 正本になし、2026-07-06 追加)
        // 3層 (遠景グラデ / 中景シルエット+灯り / 近景ヴィネット) を重ね、
        // ParallaxDriftRoutine が緩い自動ドリフトで奥ほど小さく動かす (奥行き知覚)。
        // gyro は使わず自動ドリフトのみ (酔い防止、振幅は極小)。

        private void BuildBackgroundParallax(Transform parent)
        {
            // 遠景: ダンジョン奥の縦グラデ (既存テーマ2色の組み合わせのみ、新規の彩度を足さない)
            _bgFar = (RectTransform)UIFactory.FillImage(parent, "BgFar",
                UIFactory.VerticalGradientSprite(UITheme.BackgroundSecondary, UITheme.Background, 128)).transform;
            UIFactory.Place(_bgFar, 0.5f, 0.5f, 1.06f, 1.06f);

            // 中景: 石柱シルエット + 篝火の暖色グロー (盤面 StageLights の暖色ライティング感と統一)
            _bgMid = UIFactory.Panel(parent, "BgMid");
            UIFactory.Place(_bgMid, 0.5f, 0.5f, 1.06f, 1.06f);
            BuildMidLayerDecor(_bgMid);

            // 近景: 下端のヴィネット (足元の暗がり。ボタン群に接地感を持たせる)
            _bgNear = (RectTransform)UIFactory.FillImage(parent, "BgNear",
                UIFactory.VerticalGradientSprite(new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.40f), 64)).transform;
            UIFactory.Place(_bgNear, 0.5f, 0.075f, 1.06f, 0.16f);
        }

        private void BuildMidLayerDecor(RectTransform parent)
        {
            // 石柱シルエット (低アルファの黒、装飾のみなので raycastTarget=false 固定の ColorRect でよい)
            Color pillarColor = UITheme.WithAlpha(Color.black, 0.22f);
            float[] pillarCx = { 0.12f, 0.34f, 0.66f, 0.88f };
            foreach (float cx in pillarCx)
            {
                var pillar = UIFactory.ColorRect(parent, "Pillar", pillarColor);
                UIFactory.Place((RectTransform)pillar.transform, cx, 0.55f, 0.05f, 0.9f);
            }

            // 篝火のグロー (暖色 Main を低アルファで。SoftShadowSprite を光暈の代用として流用)
            Color glow = UITheme.WithAlpha(UITheme.Main, 0.16f);
            float[] glowCx = { 0.22f, 0.78f };
            foreach (float cx in glowCx)
            {
                var g = UIFactory.FillImage(parent, "Glow", UIFactory.SoftShadowSprite(40, 128, 40));
                g.color = glow;
                g.raycastTarget = false;
                UIFactory.Place((RectTransform)g.transform, cx, 0.30f, 0.18f, 0.10f);
            }
        }

        /// <summary>
        /// 背景3層の緩い自動ドリフト。8-9Hz 程度で十分滑らかに見えるため毎フレームは呼ばない
        /// (Canvas の再レイアウトコストを抑える)。Reduce Motion 時は基準位置に固定する。
        /// </summary>
        private IEnumerator ParallaxDriftRoutine()
        {
            var wait = new WaitForSecondsRealtime(0.12f);
            float t = 0f;
            while (true)
            {
                if (FxKit.MotionEnabled)
                {
                    t += 0.12f;
                    float driftFar = Mathf.Sin(t * 0.10f) * 0.006f;
                    float driftMid = Mathf.Sin(t * 0.14f + 1.3f) * 0.014f;
                    float driftNear = Mathf.Sin(t * 0.18f + 2.6f) * 0.022f;
                    if (_bgFar != null) UIFactory.Place(_bgFar, 0.5f + driftFar, 0.5f, 1.06f, 1.06f);
                    if (_bgMid != null) UIFactory.Place(_bgMid, 0.5f + driftMid, 0.5f, 1.06f, 1.06f);
                    if (_bgNear != null) UIFactory.Place(_bgNear, 0.5f + driftNear, 0.075f, 1.06f, 0.16f);
                }
                else
                {
                    if (_bgFar != null) UIFactory.Place(_bgFar, 0.5f, 0.5f, 1.06f, 1.06f);
                    if (_bgMid != null) UIFactory.Place(_bgMid, 0.5f, 0.5f, 1.06f, 1.06f);
                    if (_bgNear != null) UIFactory.Place(_bgNear, 0.5f, 0.075f, 1.06f, 0.16f);
                }
                yield return wait;
            }
        }

        /// <summary>タイトルのごく緩い上下フロート (演出のみ、拍非同期)。Reduce Motion 時は基準位置に固定する。</summary>
        private IEnumerator TitleFloatRoutine()
        {
            var wait = new WaitForSecondsRealtime(0.08f);
            float t = 0f;
            while (true)
            {
                if (FxKit.MotionEnabled)
                {
                    t += 0.08f;
                    float offsetY = Mathf.Sin(t * 0.6f) * 0.0016f;
                    if (_titleLabelRt != null) UIFactory.Place(_titleLabelRt, 0.5f, TitleBaseCy + offsetY, 0.94f, TitleHeightRatio);
                    if (_titleShadowRt != null)
                    {
                        UIFactory.Place(_titleShadowRt, 0.5f + TitleShadowDx, TitleBaseCy - TitleShadowDy + offsetY, 0.94f, TitleHeightRatio);
                    }
                }
                else
                {
                    if (_titleLabelRt != null) UIFactory.Place(_titleLabelRt, 0.5f, TitleBaseCy, 0.94f, TitleHeightRatio);
                    if (_titleShadowRt != null)
                    {
                        UIFactory.Place(_titleShadowRt, 0.5f + TitleShadowDx, TitleBaseCy - TitleShadowDy, 0.94f, TitleHeightRatio);
                    }
                }
                yield return wait;
            }
        }

        // MARK: - Button Section (Swift: buttonSection。並び順も正本に合わせる)

        private void BuildButtonSection(RectTransform parent)
        {
            const float w = 0.72f;  // Swift: ResponsiveLayout.buttonWidth 相当を比率で固定
            const float h = 0.040f;
            // HD-2D (2026-07-06): 全ボタンを Card 化 (影で浮かせる) する。余白改善のため、頻度の低い
            // 参照系アクション「実績」「遊び方」を1行にペア化して空いた1行分を残り6行の gap 拡大に還元。
            // 旧: 7行 gap=0.044 (すき間はボタン高の約10%、最高到達階層とのクリアランス0.011) →
            // 新: 6行 gap=0.052 (約30%、クリアランス0.015)。並び順は Swift 正本どおり維持。
            const float gap = 0.052f;

            // 1. 冒険を始める (primary: 明色背景 + 濃色文字)。HD-2D (2026-07-06): 主役感を一段強めるため
            // 背後にゴールドの柔らかいグロー + 前面にゴールドの縁取りを追加 (金基調そのものは変えない)。
            var playGlow = UIFactory.FillImage(parent, "PlayGlow", UIFactory.SoftShadowSprite(40, 128, 40));
            playGlow.color = UITheme.WithAlpha(UITheme.GoldText, 0.22f);
            playGlow.raycastTarget = false;
            UIFactory.Place((RectTransform)playGlow.transform, 0.5f, 0.685f, w + 0.07f, 0.06f + 0.035f);

            Button play = CreateElevatedButton(parent, "PlayButton", "冒険を始める", 60,
                UITheme.Main, UITheme.Background, 0.5f, 0.685f, w, 0.06f, TriggerPlay);
            var playLabel = play.GetComponentInChildren<TextMeshProUGUI>();
            if (playLabel != null) playLabel.fontStyle = FontStyles.Bold;
            UIFactory.BorderTrim(play.transform, "PlayBorder", UITheme.GoldText, 0.7f);

            // 2. デイリーチャレンジ (Swift: highestFloor >= 10 のときだけ表示。可視制御は RefreshDynamic)
            BuildDailyChallengeButton(parent, w);

            // 3〜8. セカンダリボタン群 (Swift: GameButton style: .secondary、並び順も正本どおり
            // キャラクター→ランキング→ショップ→[実績|遊び方]→設定→遺物庫。遺物庫のみ Swift 正本に
            // 対応なし、Unity 独自の Phase 5c メタ進行導線)
            CreateSecondaryButton(parent, "CharacterButton", "キャラクター", 0.5f, 0.550f, w, h,
                () => NavigateTo(ScreenId.CharacterSelect));
            CreateSecondaryButton(parent, "RankingButton", "ランキング", 0.5f, 0.550f - gap, w, h,
                () => NavigateTo(ScreenId.Ranking));
            CreateSecondaryButton(parent, "ShopButton", "ショップ", 0.5f, 0.550f - gap * 2, w, h,
                () => NavigateTo(ScreenId.Shop));

            // ペア行 (実績 / 遊び方)。頻度の低い参照系アクションを1行にまとめて視覚的な階層を付ける。
            const float pairGap = 0.02f;
            const float pairW = (w - pairGap) * 0.5f;
            float pairCy = 0.550f - gap * 3;
            CreateSecondaryButton(parent, "AchievementButton", "実績",
                0.5f - pairW * 0.5f - pairGap * 0.5f, pairCy, pairW, h,
                () => NavigateTo(ScreenId.Achievements));
            CreateSecondaryButton(parent, "HowToButton", "遊び方",
                0.5f + pairW * 0.5f + pairGap * 0.5f, pairCy, pairW, h,
                () => NavigateTo(ScreenId.Tutorial));

            CreateSecondaryButton(parent, "SettingsButton", "設定", 0.5f, 0.550f - gap * 4, w, h,
                () => NavigateTo(ScreenId.Settings));

            // 遺物庫 (Phase 5c、MetaShopScreen)。既存の「ショップ」(ScreenId.Shop = IAP でのキャラ購入・
            // 広告削除) とは別物なので、混同を避けるため「遺物庫」(レリックを集め・管理する場所) と命名する。
            CreateSecondaryButton(parent, "RelicVaultButton", "遺物庫", 0.5f, 0.550f - gap * 5, w, h,
                () => NavigateTo(ScreenId.MetaShop));
        }

        /// <summary>
        /// HD-2D (2026-07-06) 実機確認で判明した最優先の修正: サブボタンの塗りが背景 (#2c1810) と
        /// ほぼ同じ暗さの UITheme.BackgroundSecondary (#3d2817) だったため、ボタンが背景に埋もれて
        /// 見えなくなっていた。塗りを明確に明るい UITheme.ButtonFill に差し替え、さらに
        /// EmbossTrim (上のハイライト線 + 下の影線 + ゴールデンロッドの縁取り) で「彫られた木の看板」風の
        /// 質感を足す。押下時の影縮みフィードバック (CreateElevatedButton 内) はそのまま維持される。
        /// </summary>
        private void CreateSecondaryButton(RectTransform parent, string name, string label,
            float cx, float cy, float w, float h, System.Action onClick)
        {
            Button btn = CreateElevatedButton(parent, name, label, 54, UITheme.ButtonFill, UITheme.TextColor,
                cx, cy, w, h, onClick);
            UIFactory.EmbossTrim(btn.transform, name + "Emboss", UITheme.ButtonHighlightLine, UITheme.Accent);
        }

        /// <summary>
        /// Card (影 + 角丸グラデ) で包んだ TextButton を生成する。HD-2D: 全ボタンを浮かせる (2026-07-06)。
        /// Card 自体は影+マスク+グラデ+ハイライトの4層コンテナで、TextButton は Card いっぱいに重ねる
        /// (TextButton 自身の bg 色がそのまま見た目の主色になり、Card は主に影の提供元になる)。
        /// </summary>
        private Button CreateElevatedButton(RectTransform parent, string name, string label, int fontSize,
            Color bg, Color fg, float cx, float cy, float w, float h, System.Action onClick)
        {
            RectTransform card = UIFactory.Card(parent, name + "Card", out RectTransform shadow);
            UIFactory.Place(card, cx, cy, w, h);

            Button btn = UIFactory.TextButton(card, name, label, fontSize, bg, fg, onClick);
            UIFactory.Place((RectTransform)btn.transform, 0.5f, 0.5f, 1f, 1f);
            UIFactory.AttachCardPressFeedback(btn.gameObject, shadow);

            return btn;
        }

        /// <summary>効果音 → 画面遷移の共通処理 (Swift: GameButton が内部で buttonTap を鳴らすのに対応)</summary>
        private void NavigateTo(ScreenId id)
        {
            App.I.Audio.PlaySfx("button_tap");
            App.I.Router.Show(id);
        }

        /// <summary>
        /// 冒険を始める (Swift: path.append(.game))。
        /// AI 難易度は PlayerState.SelectedAILevel を使用。開始階層はデフォルト 1
        /// (DEBUG ビルドでは GameController.StartNewRun 内で DebugStartFloor が適用される)。
        /// StartNewRun → Show(Game) の順: GameScreen.OnShow が新しい Session を読めるようにするため。
        /// Phase 6a (デスクトップ): KeyboardInput.cs (Space/Enter) からも同処理を呼べるよう公開する。
        /// </summary>
        public void TriggerPlay()
        {
            App.I.Audio.PlaySfx("button_tap");
            var player = App.I.Player;
            // Swift は遷移前に stopBGMMusic() するが、Unity では StartNewRun 内の
            // PlayBgmForFloor が即座に曲を差し替えるため明示 Stop は不要 (意図的省略)。
            App.I.Game.StartNewRun(player.SelectedCharacter, player.SelectedAILevel);
            App.I.Router.Show(ScreenId.Game);
        }

        // MARK: - Daily Challenge (Swift: dailyChallengeButton)

        private void BuildDailyChallengeButton(RectTransform parent, float w)
        {
            // HD-2D (2026-07-06): Card で包んで影を持たせる。可視切替は Card 全体 (影含む) に対して行う
            // (中の TextButton だけを隠すと、実体のない影だけが浮いた見た目になってしまうため)。
            RectTransform card = UIFactory.Card(parent, "DailyChallengeCard", out RectTransform shadow);
            UIFactory.Place(card, 0.5f, 0.617f, w, 0.05f);

            // HD-2D (2026-07-06): 塗りを ButtonFill に (旧 BackgroundSecondary は背景と同化して見えなく
            // なる問題があった。CreateSecondaryButton と同じ修正、詳細はそちらのコメント参照)。
            var btn = UIFactory.TextButton(card, "DailyChallengeButton", "デイリーチャレンジ", 48,
                UITheme.ButtonFill, UITheme.GoldText, () =>
                {
                    App.I.Audio.PlaySfx("button_tap");
                    App.I.Router.Show(ScreenId.DailyChallenge);
                });
            var rt = (RectTransform)btn.transform;
            UIFactory.Place(rt, 0.5f, 0.5f, 1f, 1f);
            UIFactory.AttachCardPressFeedback(btn.gameObject, shadow);
            _dailyButtonRoot = card.gameObject;

            // メインラベルを上寄せにして、下段にサブテキストを置く (Swift の 2 行構成を再現)
            _dailyMainLabel = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (_dailyMainLabel != null)
            {
                UIFactory.Place((RectTransform)_dailyMainLabel.transform, 0.42f, 0.66f, 0.8f, 0.55f);
                _dailyMainLabel.alignment = TextAlignmentOptions.Left; // TextAnchor.MiddleLeft 相当 (UIFactory.ToTmpAlignment と同じ対応)
            }
            _dailySubLabel = UIFactory.Label(rt, "SubLabel", "毎日新しい挑戦", 30,
                UITheme.WithAlpha(UITheme.TextColor, 0.6f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)_dailySubLabel.transform, 0.42f, 0.24f, 0.8f, 0.4f);

            // NEW バッジ (Swift: 未クリア時のみ赤地白文字。表示可否は RefreshDailyChallengeButton で切替)
            _dailyNewBadge = UIFactory.ColorRect(rt, "NewBadge", Color.red).gameObject;
            UIFactory.Place((RectTransform)_dailyNewBadge.transform, 0.90f, 0.5f, 0.13f, 0.5f);
            var badgeText = UIFactory.Label((RectTransform)_dailyNewBadge.transform, "NewBadgeLabel", "NEW", 26,
                Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)badgeText.transform, 0.5f, 0.5f, 1f, 1f);

            UIFactory.EmbossTrim(rt, "DailyEmboss", UITheme.ButtonHighlightLine, UITheme.Accent);
        }

        /// <summary>デイリーチャレンジボタンの完了状態表示。Swift: HomeView.swift:212-243 の isCompleted 分岐。</summary>
        private void RefreshDailyChallengeButton()
        {
            bool completed = App.I.DailyChallenge != null && App.I.DailyChallenge.TodaysChallenge.IsCompleted;

            if (_dailyMainLabel != null)
            {
                _dailyMainLabel.color = completed ? UITheme.Success : UITheme.TextColor;
            }
            if (_dailySubLabel != null)
            {
                _dailySubLabel.text = completed ? "本日クリア済み" : "毎日新しい挑戦";
            }
            if (_dailyNewBadge != null)
            {
                _dailyNewBadge.SetActive(!completed);
            }
        }

        // MARK: - Highest Floor Section (Swift: highestFloorSection)

        private void BuildHighestFloorSection(RectTransform parent)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // DEBUG ビルドでは下部の DangerZone (高さ 0.20) が Swift 準拠位置の数字を覆う
            // (2026-07-04 レイアウト監査で 51px の重なり)。設定ボタンとの隙間には
            // 見出し40px+数字96px の2行が入らないため、DEBUG では1行表示に畳む。
            _floorNumberLabel = UIFactory.Label(parent, "FloorNumberLabel", "最高到達階層: 0", 44,
                UITheme.Available, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_floorNumberLabel.transform, 0.5f, 0.235f, 0.8f, 0.04f);
#else
            // リリース: Swift 準拠の2行表示 (見出し + 大きな数字)
            var caption = UIFactory.Label(parent, "FloorCaptionLabel", "最高到達階層", 40,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f));
            UIFactory.Place((RectTransform)caption.transform, 0.5f, 0.238f, 0.6f, 0.025f);

            // Swift: AnimatedNumber (カウントアップ演出) → 静的表示に簡略化。演出は Phase 4/juice 送り。
            _floorNumberLabel = UIFactory.Label(parent, "FloorNumberLabel", "0", 96, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_floorNumberLabel.transform, 0.5f, 0.19f, 0.6f, 0.05f);
#endif
        }

        // MARK: - Toast (デイリーチャレンジ等、未実装遷移のタップへの簡易応答。ShopScreen と同じ方式)

        private void BuildToast(RectTransform parent)
        {
            _toast = UIFactory.Panel(parent, "Toast", UITheme.WithAlpha(Color.black, 0.75f));
            UIFactory.Place(_toast, 0.5f, 0.155f, 0.7f, 0.04f);
            _toastLabel = UIFactory.Label(_toast, "Label", "", 30, UITheme.GoldText);
            UIFactory.Place((RectTransform)_toastLabel.transform, 0.5f, 0.5f, 1f, 1f);
            _toast.gameObject.SetActive(false);
        }

        private void ShowToast(string message)
        {
            _toastLabel.text = message;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            _toast.gameObject.SetActive(true);
            yield return new WaitForSeconds(ToastDisplaySeconds);
            _toast.gameObject.SetActive(false);
            _toastRoutine = null;
        }

        private void HideToast()
        {
            if (_toastRoutine != null)
            {
                StopCoroutine(_toastRoutine);
                _toastRoutine = null;
            }
            if (_toast != null) _toast.gameObject.SetActive(false);
        }

        // MARK: - 動的要素の更新 (Swift の @Published バインディング相当を OnShow で一括再描画)

        private void RefreshDynamic()
        {
            var player = App.I.Player;

            if (_floorNumberLabel != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _floorNumberLabel.text = "最高到達階層: " + player.HighestFloor; // DEBUG は1行表示 (BuildHighestFloorSection 参照)
#else
                _floorNumberLabel.text = player.HighestFloor.ToString();
#endif
            }

            // 選択キャラ (スプライト名 = CharacterType.RawValue() = Resources/Sprites のファイル名)
            var character = Character.GetCharacter(player.SelectedCharacter);
            if (_characterImage != null)
            {
                _characterImage.sprite = UIFactory.LoadSprite(character.SpriteName);
                _characterImage.enabled = _characterImage.sprite != null; // 欠損時に白矩形を出さない
            }
            if (_characterNameLabel != null)
            {
                _characterNameLabel.text = character.Name;
            }

            // デイリーチャレンジは階層 10 到達で開放 (Swift: highestFloor >= 10 のハードコード)。
            // GameConfig に専用定数が無く、リテラル 10 の複製は禁止 (バランス定数一元管理) のため、
            // 同値の ThiefUnlockFloor を借用する。将来値が分岐したら GameConfig 側に
            // DailyChallengeUnlockFloor を追加して差し替えること。
            if (_dailyButtonRoot != null)
            {
                _dailyButtonRoot.SetActive(player.HighestFloor >= GameConfig.ThiefUnlockFloor);
            }
            RefreshDailyChallengeButton();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RefreshDangerZone();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // MARK: - DangerZone (Swift: SettingsView.swift の #if DEBUG debugSection「管理者用設定」)
        // DEBUG ビルド (Editor / Development Build) のみコンパイル・表示される。
        // Swift の Picker/Slider/Toggle を、uGUI 依存を増やさないボタン操作に置き換える。

        private void BuildDangerZone(RectTransform parent)
        {
            var panel = UIFactory.Panel(parent, "DangerZone", UITheme.WithAlpha(UITheme.BackgroundSecondary, 0.9f));
            // BPM オーバーライド / ターンカウントダウン行の追加で 2 行→4 行になったため、
            // 元の 0.115 から縦幅を拡張する (画面下端をはみ出さないよう cy も合わせて調整)。
            UIFactory.Place(panel, 0.5f, 0.115f, 0.92f, 0.20f);

            // 枠線代わりの警告色バー (Swift: GameCard(borderColor: warning) の簡略表現)
            var topBar = UIFactory.ColorRect(panel, "WarnBar", UITheme.Warning);
            UIFactory.Place((RectTransform)topBar.transform, 0.5f, 0.99f, 1f, 0.018f);

            var title = UIFactory.Label(panel, "DangerTitle", "管理者用設定 (DEBUG)", 34, UITheme.Warning,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.5f, 0.90f, 0.9f, 0.16f);

            // --- 1 行目: 開始階層 (Swift: Picker 1...maxFloors → ステッパーに簡略化) ---
            var floorCaption = UIFactory.Label(panel, "FloorCaption", "開始階層", 30,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)floorCaption.transform, 0.13f, 0.695f, 0.20f, 0.19f);

            _dbgFloorValueLabel = UIFactory.Label(panel, "FloorValue", "1", 36, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_dbgFloorValueLabel.transform, 0.30f, 0.695f, 0.12f, 0.19f);

            CreateDebugStepButton(panel, "FloorMinus10", "-10", 0.46f, 0.695f, -10);
            CreateDebugStepButton(panel, "FloorMinus1", "-1", 0.58f, 0.695f, -1);
            CreateDebugStepButton(panel, "FloorPlus1", "+1", 0.70f, 0.695f, +1);
            CreateDebugStepButton(panel, "FloorPlus10", "+10", 0.82f, 0.695f, +10);

            // --- 2 行目: BPM オーバーライド (Swift: SettingsView.swift Slider 0...300 step10。
            //             uGUI 依存を増やさないよう Slider ではなく ±10 ボタンに簡略化) ---
            var bpmCaption = UIFactory.Label(panel, "BpmCaption", "BPM上書き", 30,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)bpmCaption.transform, 0.13f, 0.50f, 0.20f, 0.19f);

            _dbgBpmValueLabel = UIFactory.Label(panel, "BpmValue", "自動", 32, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_dbgBpmValueLabel.transform, 0.38f, 0.50f, 0.20f, 0.19f);

            CreateBpmStepButton(panel, "BpmMinus10", "-10", 0.68f, 0.50f, -10f);
            CreateBpmStepButton(panel, "BpmPlus10", "+10", 0.85f, 0.50f, +10f);

            // --- 3 行目: ターンカウントダウンビート数 (Swift: Stepper 1...10) ---
            var turnCaption = UIFactory.Label(panel, "TurnCdCaption", "ターンCD", 30,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)turnCaption.transform, 0.13f, 0.305f, 0.20f, 0.19f);

            _dbgTurnCountdownValueLabel = UIFactory.Label(panel, "TurnCdValue", "3", 36, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_dbgTurnCountdownValueLabel.transform, 0.38f, 0.305f, 0.12f, 0.19f);

            CreateTurnCountdownStepButton(panel, "TurnCdMinus1", "-1", 0.68f, 0.305f, -1);
            CreateTurnCountdownStepButton(panel, "TurnCdPlus1", "+1", 0.85f, 0.305f, +1);

            // --- 4 行目: AI 難易度 / 全キャラ解放 / 開始カウントダウン省略 ---
            var aiBtn = UIFactory.TextButton(panel, "AiCycleButton", "", 30,
                UITheme.Background, UITheme.TextColor, CycleDebugAiLevel);
            UIFactory.Place((RectTransform)aiBtn.transform, 0.18f, 0.105f, 0.30f, 0.19f);
            _dbgAiLabel = aiBtn.GetComponentInChildren<TextMeshProUGUI>();

            var unlockBtn = UIFactory.TextButton(panel, "UnlockAllButton", "", 30,
                UITheme.Background, UITheme.TextColor, ToggleUnlockAllCharacters);
            UIFactory.Place((RectTransform)unlockBtn.transform, 0.50f, 0.105f, 0.28f, 0.19f);
            _dbgUnlockLabel = unlockBtn.GetComponentInChildren<TextMeshProUGUI>();

            var skipBtn = UIFactory.TextButton(panel, "SkipCountdownButton", "", 30,
                UITheme.Background, UITheme.TextColor, ToggleSkipStartCountdown);
            UIFactory.Place((RectTransform)skipBtn.transform, 0.82f, 0.105f, 0.28f, 0.19f);
            _dbgSkipLabel = skipBtn.GetComponentInChildren<TextMeshProUGUI>();
        }

        private void CreateDebugStepButton(RectTransform parent, string name, string label, float cx, float cy, int delta)
        {
            var btn = UIFactory.TextButton(parent, name, label, 30,
                UITheme.Background, UITheme.TextColor, () =>
                {
                    var player = App.I.Player;
                    player.DebugStartFloor = Mathf.Clamp(player.DebugStartFloor + delta, 1, GameConfig.MaxFloors);
                    player.Save(); // Swift: onChange で saveData()
                    RefreshDangerZone();
                });
            UIFactory.Place((RectTransform)btn.transform, cx, cy, 0.10f, 0.19f);
        }

        /// <summary>BPM オーバーライド ±10 (Swift: Slider 0...300 step10 の簡略版)。0 = フロア曲線に従う。</summary>
        private void CreateBpmStepButton(RectTransform parent, string name, string label, float cx, float cy, float delta)
        {
            var btn = UIFactory.TextButton(parent, name, label, 30,
                UITheme.Background, UITheme.TextColor, () =>
                {
                    var player = App.I.Player;
                    player.DebugBPMOverride = Mathf.Clamp(player.DebugBPMOverride + delta, 0f, 300f);
                    player.Save();
                    RefreshDangerZone();
                });
            UIFactory.Place((RectTransform)btn.transform, cx, cy, 0.12f, 0.19f);
        }

        /// <summary>ターンカウントダウンビート数 ±1 (Swift: Stepper value 1...10)。</summary>
        private void CreateTurnCountdownStepButton(RectTransform parent, string name, string label, float cx, float cy, int delta)
        {
            var btn = UIFactory.TextButton(parent, name, label, 30,
                UITheme.Background, UITheme.TextColor, () =>
                {
                    var player = App.I.Player;
                    player.DebugTurnCountdownBeats = Mathf.Clamp(player.DebugTurnCountdownBeats + delta, 1, 10);
                    player.Save();
                    RefreshDangerZone();
                });
            UIFactory.Place((RectTransform)btn.transform, cx, cy, 0.12f, 0.19f);
        }

        /// <summary>
        /// AI 難易度サイクル (Easy→Normal→Hard→Easy)。
        /// Boss は 10 の倍数階の内部専用難易度のため選択肢から除外 (Swift のプレイヤー選択肢も 3 種)。
        /// 変更するのは SelectedAILevel: GameController は DebugAILevel を暗黙適用しない設計
        /// (Foundation の意図的差分) のため、実際にランへ効く値を直接切り替える。
        /// </summary>
        private void CycleDebugAiLevel()
        {
            var player = App.I.Player;
            switch (player.SelectedAILevel)
            {
                case AILevel.Easy: player.SelectedAILevel = AILevel.Normal; break;
                case AILevel.Normal: player.SelectedAILevel = AILevel.Hard; break;
                default: player.SelectedAILevel = AILevel.Easy; break;
            }
            player.Save();
            RefreshDangerZone();
        }

        /// <summary>
        /// 全キャラ解放トグル (Swift: PlayerViewModel.toggleUnlockAllCharacters)。
        /// OFF に戻す時は「本来の解放状態」(勇者 + 階層10到達なら盗賊 + 購入済みキャラ) を
        /// 再構築してから保存する。ON 中の Save() で全解放リストが PlayerPrefs に書かれるため、
        /// Reload() だけでは元に戻せない (PlayerState の既知の挙動への対処)。
        /// </summary>
        private void ToggleUnlockAllCharacters()
        {
            var player = App.I.Player;
            player.DebugUnlockAllCharacters = !player.DebugUnlockAllCharacters;

            if (player.DebugUnlockAllCharacters)
            {
                player.UnlockedCharacters = new System.Collections.Generic.List<CharacterType>
                {
                    CharacterType.Hero, CharacterType.Thief, CharacterType.Wizard,
                    CharacterType.Elf, CharacterType.Knight
                };
            }
            else
            {
                var baseline = new System.Collections.Generic.List<CharacterType> { CharacterType.Hero };
                if (player.HighestFloor >= GameConfig.ThiefUnlockFloor) baseline.Add(CharacterType.Thief);
                if (player.IsPurchased(PlayerState.ProductWizard)) baseline.Add(CharacterType.Wizard);
                if (player.IsPurchased(PlayerState.ProductElf)) baseline.Add(CharacterType.Elf);
                if (player.IsPurchased(PlayerState.ProductKnight)) baseline.Add(CharacterType.Knight);
                player.UnlockedCharacters = baseline;

                // 選択中キャラがロックに戻ったら勇者へフォールバック (Swift と同じ安全策)
                if (!baseline.Contains(player.SelectedCharacter))
                {
                    player.SelectedCharacter = CharacterType.Hero;
                }
            }

            player.Save();
            RefreshDynamic(); // キャラ表示 (選択キャラのフォールバック) にも反映
        }

        private void ToggleSkipStartCountdown()
        {
            var player = App.I.Player;
            player.DebugSkipStartCountdown = !player.DebugSkipStartCountdown;
            player.Save();
            RefreshDangerZone();
        }

        private void RefreshDangerZone()
        {
            var player = App.I.Player;
            if (_dbgFloorValueLabel != null) _dbgFloorValueLabel.text = player.DebugStartFloor.ToString();
            // Swift: debugBPMOverride == 0 ? "自動" : "\(Int(debugBPMOverride)) BPM"
            if (_dbgBpmValueLabel != null)
            {
                _dbgBpmValueLabel.text = player.DebugBPMOverride <= 0f
                    ? "自動"
                    : Mathf.RoundToInt(player.DebugBPMOverride) + " BPM";
            }
            if (_dbgTurnCountdownValueLabel != null) _dbgTurnCountdownValueLabel.text = player.DebugTurnCountdownBeats.ToString();
            if (_dbgAiLabel != null) _dbgAiLabel.text = "AI: " + player.SelectedAILevel.RawValue();
            if (_dbgUnlockLabel != null) _dbgUnlockLabel.text = "全解放: " + (player.DebugUnlockAllCharacters ? "ON" : "OFF");
            if (_dbgSkipLabel != null) _dbgSkipLabel.text = "CD省略: " + (player.DebugSkipStartCountdown ? "ON" : "OFF");
        }
#endif
    }
}
