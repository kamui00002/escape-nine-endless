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
using EscapeNine.Core;

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
        private Text _floorNumberLabel;      // 最高到達階層の数字
        private Image _characterImage;       // 選択中キャラのスプライト
        private Text _characterNameLabel;    // 選択中キャラの名前
        private GameObject _dailyButtonRoot; // デイリーチャレンジボタン (階層10未到達では非表示)
        private Text _dailyMainLabel;        // 同ボタンのメインテキスト (完了状態で色切替。Swift: isCompleted 分岐)
        private Text _dailySubLabel;         // 同ボタンのサブテキスト (完了状態で文言切替)
        private GameObject _dailyNewBadge;   // NEW バッジ (未クリア時のみ表示)

        // トースト (記録確認等の簡易通知用。ShopScreen の同名パターンを踏襲した簡易実装)
        private RectTransform _toast;
        private Text _toastLabel;
        private Coroutine _toastRoutine;
        private const float ToastDisplaySeconds = 1.5f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private Text _dbgFloorValueLabel;    // 開始階層の現在値
        private Text _dbgBpmValueLabel;      // BPMオーバーライドの現在値
        private Text _dbgTurnCountdownValueLabel; // ターンカウントダウンビート数の現在値
        private Text _dbgAiLabel;            // AI難易度サイクルボタンのラベル
        private Text _dbgUnlockLabel;        // 全キャラ解放トグルのラベル
        private Text _dbgSkipLabel;          // 開始カウントダウン省略トグルのラベル
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

            // 背景はノッチ下まで全面に敷く (Swift: GameBackground。パーティクル演出は Phase 4/juice 送り)
            var bg = UIFactory.ColorRect(transform, "Background", UITheme.Background);
            UIFactory.Place((RectTransform)bg.transform, 0.5f, 0.5f, 1f, 1f);

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
            // グラデーション / グロー / シマー / バウンス演出は Phase 4 (juice) 送り。
            // 色は Swift の available (ゴールド) を採用。
            var title = UIFactory.Label(parent, "TitleLabel", "ESCAPE NINE", 110, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.5f, 0.905f, 0.94f, 0.06f);

            var subtitle = UIFactory.Label(parent, "SubtitleLabel", "Endless Dungeon", 52,
                UITheme.WithAlpha(UITheme.TextColor, 0.8f));
            UIFactory.Place((RectTransform)subtitle.transform, 0.5f, 0.862f, 0.8f, 0.03f);
        }

        // MARK: - Character Section (Swift 版 HomeView には無い。タスク要件「キャラ表示」による意図的追加)

        private void BuildCharacterSection(RectTransform parent)
        {
            // スプライトは OnShow のたびに選択キャラで差し替える (CharacterSelect から戻った時に反映)
            _characterImage = UIFactory.SpriteImage(parent, "CharacterImage", null);
            _characterImage.raycastTarget = false; // ホームでは飾りなのでタップを吸わせない
            UIFactory.Place((RectTransform)_characterImage.transform, 0.5f, 0.79f, 0.30f, 0.075f);

            _characterNameLabel = UIFactory.Label(parent, "CharacterNameLabel", "", 40, UITheme.TextColor);
            UIFactory.Place((RectTransform)_characterNameLabel.transform, 0.5f, 0.744f, 0.5f, 0.025f);
        }

        // MARK: - Button Section (Swift: buttonSection。並び順も正本に合わせる)

        private void BuildButtonSection(RectTransform parent)
        {
            const float w = 0.72f;  // Swift: ResponsiveLayout.buttonWidth 相当を比率で固定
            // セカンダリボタンが 5→6 個 (実績追加) になったため、行高/行間を詰めて
            // 最高到達階層セクションとの重なりを避ける (Phase 2.5)。
            const float h = 0.046f;
            const float gap = 0.050f;

            // 1. 冒険を始める (primary: 明色背景 + 濃色文字。glow/pulse は Phase 4 送り)
            var play = UIFactory.TextButton(parent, "PlayButton", "冒険を始める", 60,
                UITheme.Main, UITheme.Background, TriggerPlay);
            UIFactory.Place((RectTransform)play.transform, 0.5f, 0.685f, w, 0.06f);
            var playLabel = play.GetComponentInChildren<Text>();
            if (playLabel != null) playLabel.fontStyle = FontStyle.Bold;

            // 2. デイリーチャレンジ (Swift: highestFloor >= 10 のときだけ表示。可視制御は RefreshDynamic)
            BuildDailyChallengeButton(parent, w);

            // 3〜8. セカンダリボタン群 (Swift: GameButton style: .secondary、並び順も正本どおり
            // キャラクター→ランキング→ショップ→実績→遊び方→設定)
            CreateSecondaryButton(parent, "CharacterButton", "キャラクター", 0.550f, w, h,
                () => NavigateTo(ScreenId.CharacterSelect));
            CreateSecondaryButton(parent, "RankingButton", "ランキング", 0.550f - gap, w, h,
                () => NavigateTo(ScreenId.Ranking));
            CreateSecondaryButton(parent, "ShopButton", "ショップ", 0.550f - gap * 2, w, h,
                () => NavigateTo(ScreenId.Shop));
            CreateSecondaryButton(parent, "AchievementButton", "実績", 0.550f - gap * 3, w, h,
                () => NavigateTo(ScreenId.Achievements));
            CreateSecondaryButton(parent, "HowToButton", "遊び方", 0.550f - gap * 4, w, h,
                () => NavigateTo(ScreenId.Tutorial));
            CreateSecondaryButton(parent, "SettingsButton", "設定", 0.550f - gap * 5, w, h,
                () => NavigateTo(ScreenId.Settings));
        }

        private void CreateSecondaryButton(RectTransform parent, string name, string label,
            float cy, float w, float h, System.Action onClick)
        {
            var btn = UIFactory.TextButton(parent, name, label, 54,
                UITheme.BackgroundSecondary, UITheme.TextColor, onClick);
            UIFactory.Place((RectTransform)btn.transform, 0.5f, cy, w, h);
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
            var btn = UIFactory.TextButton(parent, "DailyChallengeButton", "デイリーチャレンジ", 48,
                UITheme.BackgroundSecondary, UITheme.GoldText, () =>
                {
                    App.I.Audio.PlaySfx("button_tap");
                    App.I.Router.Show(ScreenId.DailyChallenge);
                });
            var rt = (RectTransform)btn.transform;
            UIFactory.Place(rt, 0.5f, 0.617f, w, 0.05f);
            _dailyButtonRoot = btn.gameObject;

            // メインラベルを上寄せにして、下段にサブテキストを置く (Swift の 2 行構成を再現)
            _dailyMainLabel = btn.GetComponentInChildren<Text>();
            if (_dailyMainLabel != null)
            {
                UIFactory.Place((RectTransform)_dailyMainLabel.transform, 0.42f, 0.66f, 0.8f, 0.55f);
                _dailyMainLabel.alignment = TextAnchor.MiddleLeft;
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
            _dbgAiLabel = aiBtn.GetComponentInChildren<Text>();

            var unlockBtn = UIFactory.TextButton(panel, "UnlockAllButton", "", 30,
                UITheme.Background, UITheme.TextColor, ToggleUnlockAllCharacters);
            UIFactory.Place((RectTransform)unlockBtn.transform, 0.50f, 0.105f, 0.28f, 0.19f);
            _dbgUnlockLabel = unlockBtn.GetComponentInChildren<Text>();

            var skipBtn = UIFactory.TextButton(panel, "SkipCountdownButton", "", 30,
                UITheme.Background, UITheme.TextColor, ToggleSkipStartCountdown);
            UIFactory.Place((RectTransform)skipBtn.transform, 0.82f, 0.105f, 0.28f, 0.19f);
            _dbgSkipLabel = skipBtn.GetComponentInChildren<Text>();
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
