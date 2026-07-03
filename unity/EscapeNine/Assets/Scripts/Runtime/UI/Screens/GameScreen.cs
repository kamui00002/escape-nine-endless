// GameScreen.cs
// Swift 正本: Views/Game/GameView.swift (画面全体・HUD・オーバーレイ群)
//   - 盤面        → GridBoardWidget (GridBoardView.swift / GridCellView.swift)
//   - ビート表示  → BeatIndicatorWidget (BeatIndicatorView.swift)
//   - BPM 情報    → BPMInfoWidget (BPMInfoView.swift)
//
// SwiftUI の @Published バインディングの置き換えとして GameController のイベント
// (OnStateChanged / OnTurnResolved / OnFloorAdvanced / OnGameOver / OnCountdownTick /
//  OnBossWarning) を購読して再描画する。
//
// フロー上の差分 (Foundation の設計に追従):
//   - HomeScreen / ResultScreen は StartNewRun() 済みで Show(Game) してくる (payload=null)。
//     → ラン進行中を検知したらプレゲームオーバーレイを出さず即 HUD に接続する。
//   - payload が GameStartRequest(AutoStart=false) または「ラン未開始」の場合のみ、
//     Swift の startGameOverlay (「冒険を始める」+ AI 難易度選択) を表示する。
//   - リザルトへの遷移は「EndGame 完了」を OnStateChanged + Status(Win/Lose) +
//     ElapsedSeconds > 0 (EndGame でのみ確定する値) で検知して Router.Show(Result, payload)。
//     敗北時は OnGameOver → 1.5 秒の敗因オーバーレイ表示中は遷移しない (Swift と同テンポ)。

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using EscapeNine.Core;

namespace EscapeNine.Runtime.UI
{
    public sealed class GameScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Game;

        // ---- 表示タイマー定数 (Swift: DispatchQueue.asyncAfter の秒数) ----
        private const float GoDisplaySeconds = 0.5f;    // GO! 表示 (GameController コメント指定)
        private const float BossWarningSeconds = 2.0f;  // ボス出現警告
        private const float SkillResetSeconds = 2.0f;   // スキル回復通知
        private const float GradeDisplaySeconds = 0.8f; // タイミング判定 (JUST/GOOD/MISS)

        /// <summary>
        /// プレゲームの AI 難易度選択肢 (Swift: GameView.aiLevelSelector の
        /// `ForEach(AILevel.allCases, id: \.self)`。全 4 種を忠実に列挙する)。
        /// Boss を選ぶと Floor.GetEffectiveAILevel が全階層で AILevel.Boss を返す
        /// (= 常時ボス相当の高難度) ため、意図された 1 つの難易度選択肢として扱う。
        /// </summary>
        private static readonly AILevel[] SelectableAiLevels =
        {
            AILevel.Easy, AILevel.Normal, AILevel.Hard, AILevel.Boss
        };

        // ---- 二重構築ガード (App.Awake は BuildUI → Register(内部でも BuildUI) の順で呼ぶ) ----
        private bool _built;
        private bool _subscribed;

        // ---- HUD 参照 ----
        private Text _pauseButtonLabel;
        private BPMInfoWidget _bpmInfo;
        private BeatIndicatorWidget _beatIndicator;
        private GridBoardWidget _board;

        private GameObject _comboRow;
        private Text _gradeLabel;
        private Text _comboLabel;
        private Text _multiplierLabel;

        private Text _turnValueLabel;
        private GameObject _skillInfoRow;
        private Text _skillNameLabel;
        private Text _skillCountLabel;

        private Button _skillButton;
        private Image _skillButtonImage;
        private Text _skillButtonLabel;

        private GameObject _specialRuleRoot;
        private Text _specialRuleLabel;

        // ---- オーバーレイ参照 (Swift の ZStack と同じ重ね順で生成する) ----
        private GameObject _floorClearOverlay;
        private Text _floorClearFloorLabel;
        private Text _floorClearNextLabel;

        private GameObject _skillResetToast;

        private GameObject _pregameOverlay;
        private Text _pregameFloorLabel;
        private Image[] _aiButtonImages;
        private Text[] _aiButtonLabels;

        private GameObject _pausedOverlay;

        private GameObject _countdownOverlay;
        private Text _countdownLabel;

        private GameObject _gameOverOverlay;
        private Image _gameOverBg;
        private Text _gameOverIcon;
        private Text _gameOverText;

        private GameObject _bossOverlay;
        private Text _bossFloorLabel;

        // ---- ラン状態 ----

        /// <summary>プレゲームで選択中の AI 難易度 (Swift: @State selectedAILevel)。</summary>
        private AILevel _selectedAILevel = AILevel.Easy;

        /// <summary>プレゲームから始める場合の開始階層 (payload: GameStartRequest.StartFloor)。</summary>
        private int _pendingStartFloor = 1;

        /// <summary>
        /// ラン開始時点の自己ベスト (Swift: previousBest)。
        /// GameController.EndGame が HighestFloor を先に永続化するため、リザルト表示時に
        /// PlayerState を読むと新記録判定が常に false になる — ここで開始時に控えて payload で渡す。
        /// </summary>
        private int _bestBeforeRun = -1;

        /// <summary>リザルトへの二重遷移防止 (OnStateChanged は終了後も発火し得るため)。</summary>
        private bool _resultShown = true;

        /// <summary>タイミング判定の表示開始時刻 (負 = 非表示)。0.8 秒で自動消去 (UI 側の責務)。</summary>
        private float _gradeShownAt = -1f;

        // スプライトキャッシュ (再描画のたびに Resources.Load しないため)
        private Sprite _playerSprite;
        private string _playerSpriteName;
        private Sprite _enemySprite;
        private string _enemySpriteName;

        private Coroutine _goHideRoutine;
        private Coroutine _bossHideRoutine;
        private Coroutine _skillResetRoutine;

        // MARK: - BuildUI

        public override void BuildUI()
        {
            if (_built) return; // App.Awake + Router.Register の二重呼び出し対策
            _built = true;

            // 画面ルートを親いっぱいに固定 (シーン側の配置ミスに影響されない防御。HomeScreen と同じ)
            var root = GetComponent<RectTransform>();
            if (root != null)
            {
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
            }

            // 背景 (Swift: GameBackground。パーティクル演出は Phase 4/juice 送り)
            var bg = UIFactory.ColorRect(transform, "Background", UITheme.Background);
            UIFactory.Place((RectTransform)bg.transform, 0.5f, 0.5f, 1f, 1f);

            // HUD はセーフエリア内 (SwiftUI が自動処理していた部分)
            var safe = UIFactory.Panel(transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildHeader(safe);
            BuildTopInfo(safe);
            BuildBoard(safe);
            BuildBottom(safe);

            // オーバーレイは Swift の ZStack と同じ順で兄弟生成 (後に生成したものが上に描画される):
            // floorClear < skillReset < startGame(pregame) < paused < countdown < gameOver < bossWarning
            BuildFloorClearOverlay();
            BuildSkillResetToast();
            BuildPregameOverlay();
            BuildPausedOverlay();
            BuildCountdownOverlay();
            BuildGameOverOverlay();
            BuildBossOverlay();

            TrySubscribe();
        }

        // MARK: - Header (Swift: gameHeader)

        private void BuildHeader(RectTransform parent)
        {
            var back = UIFactory.TextButton(parent, "BackButton", "< 戻る", 38,
                UITheme.BackgroundSecondary, UITheme.TextColor, HandleBackTapped);
            UIFactory.Place((RectTransform)back.transform, 0.16f, 0.968f, 0.26f, 0.036f);

            var pause = UIFactory.TextButton(parent, "PauseButton", "一時停止", 38,
                UITheme.BackgroundSecondary, UITheme.TextColor, HandlePauseTapped);
            UIFactory.Place((RectTransform)pause.transform, 0.82f, 0.968f, 0.30f, 0.036f);
            _pauseButtonLabel = pause.GetComponentInChildren<Text>();
        }

        // MARK: - Top Info (Swift: BPMInfoView + BeatIndicatorView + comboDisplay + turnAndSkillInfo)

        private void BuildTopInfo(RectTransform parent)
        {
            _bpmInfo = new BPMInfoWidget(parent);
            UIFactory.Place(_bpmInfo.Rect, 0.5f, 0.906f, 0.92f, 0.062f);

            _beatIndicator = BeatIndicatorWidget.Create(parent);
            UIFactory.Place((RectTransform)_beatIndicator.transform, 0.5f, 0.795f, 0.70f, 0.145f);

            // コンボ表示 (Swift: comboDisplay。combo>=2 か判定表示中のみ行ごと表示)
            var comboRow = UIFactory.Panel(parent, "ComboRow");
            UIFactory.Place(comboRow, 0.5f, 0.708f, 0.92f, 0.026f);
            _comboRow = comboRow.gameObject;

            _gradeLabel = UIFactory.Label(comboRow, "GradeLabel", "", 32, UITheme.Success,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_gradeLabel.transform, 0.24f, 0.5f, 0.22f, 1f);

            _comboLabel = UIFactory.Label(comboRow, "ComboLabel", "", 32, UITheme.TextColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_comboLabel.transform, 0.52f, 0.5f, 0.32f, 1f);

            _multiplierLabel = UIFactory.Label(comboRow, "MultiplierLabel", "", 32, UITheme.Success,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_multiplierLabel.transform, 0.76f, 0.5f, 0.16f, 1f);
            _comboRow.SetActive(false);

            // ターン行 (Swift: turnAndSkillInfo の 1 行目)
            var turnCaption = UIFactory.Label(parent, "TurnCaption", "ターン", 32,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleRight);
            UIFactory.Place((RectTransform)turnCaption.transform, 0.40f, 0.676f, 0.22f, 0.024f);
            _turnValueLabel = UIFactory.Label(parent, "TurnValue", "0 / 5", 36, UITheme.Available,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place((RectTransform)_turnValueLabel.transform, 0.62f, 0.676f, 0.30f, 0.024f);

            // スキル残数行 (Swift: 2 行目。斜め移動キャラでは非表示)
            var skillRow = UIFactory.Panel(parent, "SkillInfoRow");
            UIFactory.Place(skillRow, 0.5f, 0.648f, 0.92f, 0.024f);
            _skillInfoRow = skillRow.gameObject;

            _skillNameLabel = UIFactory.Label(skillRow, "SkillName", "", 32,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleRight);
            UIFactory.Place((RectTransform)_skillNameLabel.transform, 0.37f, 0.5f, 0.32f, 1f);
            _skillCountLabel = UIFactory.Label(skillRow, "SkillCount", "", 36, UITheme.Available,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place((RectTransform)_skillCountLabel.transform, 0.63f, 0.5f, 0.30f, 1f);
        }

        // MARK: - Board (Swift: gridBoard)

        private void BuildBoard(RectTransform parent)
        {
            _board = GridBoardWidget.Create(parent);
            UIFactory.Place((RectTransform)_board.transform, 0.5f, 0.425f, 0.94f, 0.36f);
            _board.OnCellTapped += HandleCellTapped;
            _board.OnEnemyTapped += HandleEnemyTapped;
        }

        // MARK: - Bottom (Swift: skillButton + specialRuleLabel)

        private void BuildBottom(RectTransform parent)
        {
            _skillButton = UIFactory.TextButton(parent, "SkillButton", "", 44,
                UITheme.Available, Color.white, HandleSkillTapped);
            UIFactory.Place((RectTransform)_skillButton.transform, 0.5f, 0.168f, 0.72f, 0.055f);
            _skillButtonImage = _skillButton.GetComponent<Image>();
            _skillButtonLabel = _skillButton.GetComponentInChildren<Text>();
            _skillButtonLabel.fontStyle = FontStyle.Bold;
            _skillButton.gameObject.SetActive(false);

            var ruleRoot = UIFactory.Panel(parent, "SpecialRule", UITheme.BackgroundSecondary);
            UIFactory.Place(ruleRoot, 0.5f, 0.106f, 0.86f, 0.036f);
            _specialRuleRoot = ruleRoot.gameObject;
            _specialRuleLabel = UIFactory.Label(ruleRoot, "Label", "", 32, UITheme.Warning);
            UIFactory.Place((RectTransform)_specialRuleLabel.transform, 0.5f, 0.5f, 1f, 1f);
            _specialRuleRoot.SetActive(false);
        }

        // MARK: - Overlays 構築

        /// <summary>階層クリア (Swift: floorClearOverlay)。CelebrationEffect / glow は Phase 4 送り。</summary>
        private void BuildFloorClearOverlay()
        {
            var overlay = UIFactory.Panel(transform, "FloorClearOverlay",
                UITheme.WithAlpha(UITheme.Background, 0.95f));
            _floorClearOverlay = overlay.gameObject;

            _floorClearFloorLabel = UIFactory.Label(overlay, "FloorLabel", "", 110, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_floorClearFloorLabel.transform, 0.5f, 0.62f, 0.9f, 0.08f);

            var clearLabel = UIFactory.Label(overlay, "ClearLabel", "クリア！", 64, UITheme.GoldText,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)clearLabel.transform, 0.5f, 0.545f, 0.8f, 0.05f);

            var divider = UIFactory.ColorRect(overlay, "Divider", UITheme.WithAlpha(UITheme.Available, 0.4f));
            UIFactory.Place((RectTransform)divider.transform, 0.5f, 0.505f, 0.6f, 0.0025f);

            _floorClearNextLabel = UIFactory.Label(overlay, "NextLabel", "", 44,
                UITheme.WithAlpha(UITheme.TextColor, 0.8f));
            UIFactory.Place((RectTransform)_floorClearNextLabel.transform, 0.5f, 0.465f, 0.8f, 0.035f);

            var start = UIFactory.TextButton(overlay, "StartButton", "スタート", 54,
                UITheme.Main, UITheme.Background, HandleFloorClearStart);
            UIFactory.Place((RectTransform)start.transform, 0.5f, 0.375f, 0.5f, 0.06f);
            var startLabel = start.GetComponentInChildren<Text>();
            if (startLabel != null) startLabel.fontStyle = FontStyle.Bold;

            _floorClearOverlay.SetActive(false);
        }

        /// <summary>スキル回復通知 (Swift: skillResetNotification)。10 階層ごとの回復時に 2 秒表示。</summary>
        private void BuildSkillResetToast()
        {
            var toast = UIFactory.Panel(transform, "SkillResetToast", UITheme.BackgroundSecondary);
            UIFactory.Place(toast, 0.5f, 0.80f, 0.56f, 0.045f);
            // 通知はタップを遮らない (Swift も操作を妨げない)
            var img = toast.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;

            // 上下の成功色ライン (Swift: stroke(success, 2pt) の簡易表現)
            var top = UIFactory.ColorRect(toast, "LineTop", UITheme.Success);
            UIFactory.Place((RectTransform)top.transform, 0.5f, 1f, 1f, 0.05f);
            var bottom = UIFactory.ColorRect(toast, "LineBottom", UITheme.Success);
            UIFactory.Place((RectTransform)bottom.transform, 0.5f, 0f, 1f, 0.05f);

            var label = UIFactory.Label(toast, "Label", "スキル回復！", 40, UITheme.TextColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)label.transform, 0.5f, 0.5f, 1f, 1f);

            _skillResetToast = toast.gameObject;
            _skillResetToast.SetActive(false);
        }

        /// <summary>ゲーム開始前 (Swift: startGameOverlay + aiLevelSelector)。</summary>
        private void BuildPregameOverlay()
        {
            var overlay = UIFactory.Panel(transform, "PregameOverlay",
                UITheme.WithAlpha(UITheme.Background, 0.97f));
            _pregameOverlay = overlay.gameObject;

            var ready = UIFactory.Label(overlay, "ReadyLabel", "準備はできましたか？", 56, UITheme.TextColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)ready.transform, 0.5f, 0.72f, 0.9f, 0.05f);

            _pregameFloorLabel = UIFactory.Label(overlay, "FloorLabel", "1階層", 96, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_pregameFloorLabel.transform, 0.5f, 0.65f, 0.9f, 0.07f);

            var aiCaption = UIFactory.Label(overlay, "AiCaption", "鬼の強さを選ぶ", 38, UITheme.TextColor);
            UIFactory.Place((RectTransform)aiCaption.transform, 0.5f, 0.545f, 0.8f, 0.03f);

            _aiButtonImages = new Image[SelectableAiLevels.Length];
            _aiButtonLabels = new Text[SelectableAiLevels.Length];
            // Swift の HStack は要素数ぶん均等に並ぶ。3→4 種になっても崩れないよう、
            // 固定 3 枠の cx 式ではなく [areaLeft, areaRight] を要素数で均等割りする式にする
            // (n=3 のときは概ね旧来の 0.23/0.50/0.77, 幅 0.24 と同等になる)。
            const float areaLeft = 0.08f;
            const float areaRight = 0.92f;
            float slotWidth = (areaRight - areaLeft) / SelectableAiLevels.Length;
            for (int i = 0; i < SelectableAiLevels.Length; i++)
            {
                AILevel level = SelectableAiLevels[i]; // クロージャ用固定
                var btn = UIFactory.TextButton(overlay, "Ai" + level, level.RawValue(), 40,
                    UITheme.BackgroundSecondary, UITheme.TextColor, () => HandleAiLevelSelected(level));
                float cx = areaLeft + slotWidth * (i + 0.5f);
                UIFactory.Place((RectTransform)btn.transform, cx, 0.49f, slotWidth * 0.88f, 0.045f);
                _aiButtonImages[i] = btn.GetComponent<Image>();
                _aiButtonLabels[i] = btn.GetComponentInChildren<Text>();
            }

            var start = UIFactory.TextButton(overlay, "StartButton", "冒険を始める", 56,
                UITheme.Main, UITheme.Background, HandlePregameStart);
            UIFactory.Place((RectTransform)start.transform, 0.5f, 0.375f, 0.62f, 0.06f);
            var startLabel = start.GetComponentInChildren<Text>();
            if (startLabel != null) startLabel.fontStyle = FontStyle.Bold;

            var back = UIFactory.TextButton(overlay, "BackButton", "戻る", 48,
                UITheme.BackgroundSecondary, UITheme.TextColor, HandlePregameBack);
            UIFactory.Place((RectTransform)back.transform, 0.5f, 0.295f, 0.62f, 0.05f);

            _pregameOverlay.SetActive(false);
        }

        /// <summary>一時停止 (Swift: pausedOverlay)。</summary>
        private void BuildPausedOverlay()
        {
            var overlay = UIFactory.Panel(transform, "PausedOverlay",
                UITheme.WithAlpha(UITheme.Background, 0.95f));
            _pausedOverlay = overlay.gameObject;

            var title = UIFactory.Label(overlay, "Title", "一時停止", 90, UITheme.TextColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.5f, 0.62f, 0.9f, 0.07f);

            var resume = UIFactory.TextButton(overlay, "ResumeButton", "再開", 54,
                UITheme.Main, UITheme.Background, HandleResumeTapped);
            UIFactory.Place((RectTransform)resume.transform, 0.5f, 0.50f, 0.50f, 0.06f);
            var resumeLabel = resume.GetComponentInChildren<Text>();
            if (resumeLabel != null) resumeLabel.fontStyle = FontStyle.Bold;

            var quit = UIFactory.TextButton(overlay, "QuitButton", "終了", 48,
                UITheme.BackgroundSecondary, UITheme.TextColor, HandleQuitTapped);
            UIFactory.Place((RectTransform)quit.transform, 0.5f, 0.42f, 0.55f, 0.055f);

            _pausedOverlay.SetActive(false);
        }

        /// <summary>ゲーム開始カウントダウン 3→2→1→GO! (Swift: gameStartCountdownOverlay)。</summary>
        private void BuildCountdownOverlay()
        {
            var overlay = UIFactory.Panel(transform, "CountdownOverlay", new Color(0f, 0f, 0f, 0.7f));
            _countdownOverlay = overlay.gameObject;

            _countdownLabel = UIFactory.Label(overlay, "CountLabel", "3", 280, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_countdownLabel.transform, 0.5f, 0.55f, 0.9f, 0.25f);

            _countdownOverlay.SetActive(false);
        }

        /// <summary>敗北オーバーレイ (Swift: gameOverOverlay。捕まった=赤フラッシュ / 時間切れ=黒)。</summary>
        private void BuildGameOverOverlay()
        {
            var overlay = UIFactory.Panel(transform, "GameOverOverlay", new Color(0f, 0f, 0f, 0.7f));
            _gameOverOverlay = overlay.gameObject;
            _gameOverBg = overlay.GetComponent<Image>();

            // SF Symbols (exclamationmark.triangle / clock.badge.xmark) は文字で代替 (Phase 4 で画像化)
            _gameOverIcon = UIFactory.Label(overlay, "Icon", "！", 140, UITheme.Warning,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_gameOverIcon.transform, 0.5f, 0.60f, 0.5f, 0.12f);

            _gameOverText = UIFactory.Label(overlay, "Text", "捕まった！", 90, UITheme.Warning,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_gameOverText.transform, 0.5f, 0.50f, 0.9f, 0.07f);

            _gameOverOverlay.SetActive(false);
        }

        /// <summary>ボスフロア警告 (Swift: bossWarningOverlay)。2 秒で自動消去 (UI 側の責務)。</summary>
        private void BuildBossOverlay()
        {
            var overlay = UIFactory.Panel(transform, "BossOverlay", new Color(1f, 0f, 0f, 0.15f));
            _bossOverlay = overlay.gameObject;

            var title = UIFactory.Label(overlay, "Title", "ボス出現！", 100, Color.red,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.5f, 0.58f, 0.9f, 0.08f);

            _bossFloorLabel = UIFactory.Label(overlay, "FloorLabel", "", 56, UITheme.TextColor);
            UIFactory.Place((RectTransform)_bossFloorLabel.transform, 0.5f, 0.50f, 0.8f, 0.045f);

            _bossOverlay.SetActive(false);
        }

        // MARK: - ライフサイクル

        public override void OnShow(object payload)
        {
            TrySubscribe(); // BuildUI 時点で App 未初期化だった場合の保険
            ResetTransientUI();

            var req = payload as GameStartRequest;
            if (req != null)
            {
                _pendingStartFloor = Mathf.Max(1, req.StartFloor);
                if (req.AutoStart)
                {
                    // リトライ標準フロー: ResultScreen は StartNewRun 済みで AutoStart=true を渡してくる。
                    // ここで BeginRun すると StartNewRun の二重呼び出し (開始音の二重再生 +
                    // リトライ用キャラ/難易度の PlayerState 値による上書き) になるため、
                    // 進行中ランがあれば接続のみ行い、未開始の呼び出しだけ自前で開始する。
                    if (IsRunActive()) AttachToRun();
                    else BeginRun(_pendingStartFloor);
                }
                else ShowPregame();
            }
            else if (IsRunActive())
            {
                // 標準フロー: HomeScreen / ResultScreen が StartNewRun 済みで遷移してくる
                AttachToRun();
            }
            else
            {
                _pendingStartFloor = 1;
                ShowPregame();
            }

            RefreshAll();
        }

        public override void OnHide()
        {
            ResetTransientUI();
        }

        private void OnDestroy()
        {
            if (!_subscribed || App.I == null || App.I.Game == null) return;
            var game = App.I.Game;
            game.OnCountdownTick -= HandleCountdownTick;
            game.OnGameStarted -= HandleGameStarted;
            game.OnTurnResolved -= HandleTurnResolved;
            game.OnFloorAdvanced -= HandleFloorAdvanced;
            game.OnGameOver -= HandleGameOver;
            game.OnStateChanged -= HandleStateChanged;
            game.OnBossWarning -= HandleBossWarning;
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            if (App.I == null || App.I.Game == null) return;
            var game = App.I.Game;
            game.OnCountdownTick += HandleCountdownTick;
            game.OnGameStarted += HandleGameStarted;
            game.OnTurnResolved += HandleTurnResolved;
            game.OnFloorAdvanced += HandleFloorAdvanced;
            game.OnGameOver += HandleGameOver;
            game.OnStateChanged += HandleStateChanged;
            game.OnBossWarning += HandleBossWarning;
            _subscribed = true;
        }

        private void Update()
        {
            // タイミング判定の 0.8 秒自動消去 (GameController は表示消去を UI 側に委ねている)
            if (_gradeShownAt >= 0f && Time.unscaledTime - _gradeShownAt >= GradeDisplaySeconds)
            {
                _gradeShownAt = -1f;
                var session = (App.I != null && App.I.Game != null) ? App.I.Game.Session : null;
                RefreshComboRow(session);
            }
        }

        // MARK: - ラン開始 / 接続

        private static bool IsRunActive()
        {
            if (App.I == null || App.I.Game == null) return false;
            var session = App.I.Game.Session;
            return session != null
                && (session.Status == GameStatus.Playing || session.Status == GameStatus.Paused);
        }

        /// <summary>プレゲームから新規ランを開始する (Swift: viewModel.startGame(aiLevel:))。</summary>
        private void BeginRun(int startFloor)
        {
            var player = App.I.Player;
            App.I.Game.StartNewRun(player.SelectedCharacter, player.SelectedAILevel, startFloor);
            AttachToRun();
        }

        /// <summary>進行中のランに HUD を接続する (開始済み・リトライ両対応)。</summary>
        private void AttachToRun()
        {
            // ラン中は HighestFloor が変化しない (EndGame でのみ更新) ため、
            // この時点のスナップショットが Swift の previousBest と一致する。
            _bestBeforeRun = App.I.Player.HighestFloor;
            _resultShown = false;
            _board.SnapNextRender(); // 再配置をスライドさせない
            _pregameOverlay.SetActive(false);

            var game = App.I.Game;
            if (game.IsGameStartCountdownActive)
            {
                // StartNewRun は Show(Game) より前に呼ばれるため、開始時の OnBossWarning /
                // 初期カウントダウン表示はイベントでは受け取れない — ここで状態から補完する。
                ShowCountdownValue(GameConfig.GameStartCountdownBeats);
                if (game.Session != null && game.Session.IsBossFloor) ShowBossWarning();
            }
        }

        // MARK: - GameController イベント

        private void HandleStateChanged()
        {
            if (!isActiveAndEnabled) return; // Show(Game) 前の StartNewRun 内通知は OnShow が拾う

            var game = App.I.Game;
            var session = game.Session;

            // リザルト遷移: EndGame 完了の検知。ElapsedSeconds は EndGame でのみ 0 → 確定値になるため、
            // 敗北検知直後 (1.5 秒オーバーレイ中) の Status==Lose では遷移しない (Swift と同テンポ)。
            if (!_resultShown && session != null && game.ElapsedSeconds > 0
                && (session.Status == GameStatus.Win || session.Status == GameStatus.Lose))
            {
                _resultShown = true;
                App.I.Router.Show(ScreenId.Result, BuildResultPayload(game, session));
                return;
            }

            RefreshAll();
        }

        private void HandleCountdownTick(int value)
        {
            if (!isActiveAndEnabled) return;
            if (value > 0) ShowCountdownValue(value);
            else ShowGo();
        }

        private void HandleGameStarted()
        {
            // ベストのスナップショットはラン中不変なので毎階層 (GO のたび) 取り直しても同値
            if (App.I != null && App.I.Player != null)
            {
                _bestBeforeRun = App.I.Player.HighestFloor;
            }
            _resultShown = false;

            if (!isActiveAndEnabled) return;
            RefreshAll();
        }

        private void HandleTurnResolved(TurnResult result)
        {
            if (!isActiveAndEnabled) return;
            if (result == TurnResult.FloorCleared)
            {
                ShowFloorClear(); // 「スタート」で AdvanceToNextFloor (自動遷移しないのが正本仕様)
            }
            // Continued / Defeated の再描画は直後の OnStateChanged / OnGameOver が担う
        }

        private void HandleFloorAdvanced(int newFloor)
        {
            if (!isActiveAndEnabled) return;

            _floorClearOverlay.SetActive(false);
            _board.SnapNextRender(); // 新階層のランダム配置をスライドさせない

            // スキル回復通知 (Swift: showSkillReset)。判定式は GameController のコメント指定どおり。
            if (newFloor % GameConfig.SkillResetInterval == 1)
            {
                ShowSkillResetToast();
            }

            RefreshAll();
        }

        private void HandleGameOver(DefeatReason reason)
        {
            if (!isActiveAndEnabled) return;

            bool caught = reason == DefeatReason.CaughtByEnemy;
            // Swift: 捕まった=warning 0.4 の赤フラッシュ / 時間切れ=黒 0.7
            _gameOverBg.color = caught
                ? UITheme.WithAlpha(UITheme.Warning, 0.4f)
                : new Color(0f, 0f, 0f, 0.7f);
            _gameOverIcon.color = caught ? UITheme.Warning : BeatIndicatorWidget.SwiftOrange;
            _gameOverText.text = caught ? "捕まった！" : "時間切れ！";
            _gameOverText.color = caught ? UITheme.Warning : BeatIndicatorWidget.SwiftOrange;
            _gameOverOverlay.SetActive(true);
            // 1.5 秒後の EndGame → OnStateChanged がリザルトへ遷移させる (OnHide で消える)
        }

        private void HandleBossWarning()
        {
            if (!isActiveAndEnabled) return;
            ShowBossWarning();
        }

        // MARK: - ユーザー操作

        /// <summary>セルタップ = 移動予約 (Swift: viewModel.selectMove(to:))。</summary>
        private void HandleCellTapped(int position)
        {
            var game = App.I.Game;
            var session = game.Session;
            if (session == null) return;

            game.RequestMove(position);

            // 受理された移動のみタイミング判定を表示 (RequestMove 内で LastTimingGrade が更新される)
            if (session.PendingPlayerMove == position && game.LastTimingGrade.HasValue)
            {
                ShowTimingGrade(game.LastTimingGrade.Value);
            }
        }

        /// <summary>鬼タップ = エルフの拘束 (Swift: viewModel.bindEnemy())。ガードは GameController 側。</summary>
        private void HandleEnemyTapped()
        {
            App.I.Game.TapEnemy();
        }

        /// <summary>スキルボタン (Swift: viewModel.activateSkill())。効果音は GameController 側。</summary>
        private void HandleSkillTapped()
        {
            App.I.Game.ActivateSkill();
        }

        /// <summary>ヘッダー戻る (Swift: buttonTap 音 → resetGame → dismiss)。</summary>
        private void HandleBackTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            App.I.Game.QuitToHome();
            App.I.Router.Show(ScreenId.Home);
        }

        /// <summary>ヘッダー一時停止 / 再開トグル (Swift は効果音なし)。</summary>
        private void HandlePauseTapped()
        {
            var session = App.I.Game.Session;
            if (session == null) return;
            if (session.Status == GameStatus.Playing) App.I.Game.PauseGame();
            else if (session.Status == GameStatus.Paused) App.I.Game.ResumeGame();
        }

        private void HandleResumeTapped()
        {
            App.I.Audio.PlaySfx("button_tap"); // Swift: GameButton 内蔵の buttonTap
            App.I.Game.ResumeGame();
        }

        private void HandleQuitTapped()
        {
            // Swift の「終了」は素の Button (効果音なし)
            App.I.Game.QuitToHome();
            App.I.Router.Show(ScreenId.Home);
        }

        private void HandleFloorClearStart()
        {
            App.I.Audio.PlaySfx("button_tap"); // Swift: GameButton 内蔵の buttonTap
            App.I.Game.AdvanceToNextFloor();
        }

        private void HandleAiLevelSelected(AILevel level)
        {
            _selectedAILevel = level;
            RefreshAiButtons();
        }

        private void HandlePregameStart()
        {
            App.I.Audio.PlaySfx("button_tap");
            var player = App.I.Player;
            // Swift は @State のままだが、Unity では HomeScreen 発ランと同じ経路
            // (PlayerState.SelectedAILevel) に集約して永続化する (意図的差分)。
            player.SelectedAILevel = _selectedAILevel;
            player.Save();

            _pregameOverlay.SetActive(false);
            BeginRun(_pendingStartFloor);
            RefreshAll();
        }

        private void HandlePregameBack()
        {
            App.I.Audio.PlaySfx("button_tap");
            App.I.Router.Show(ScreenId.Home);
        }

        // MARK: - オーバーレイ表示

        private void ShowPregame()
        {
            // 遷移前ランの終了状態でリザルトへ誤遷移しないよう塞いでおく (開始時に解除)
            _resultShown = true;

            // Boss は SelectableAiLevels に含まれる正規の選択肢になったため、
            // 以前あった「選択肢外への強制ダウングレード」は行わない (Swift の allCases 挙動に合わせる)。
            _selectedAILevel = App.I.Player.SelectedAILevel;
            _pregameFloorLabel.text = _pendingStartFloor + "階層";
            RefreshAiButtons();
            _pregameOverlay.SetActive(true);
        }

        private void RefreshAiButtons()
        {
            for (int i = 0; i < SelectableAiLevels.Length; i++)
            {
                bool selected = SelectableAiLevels[i] == _selectedAILevel;
                // Swift: 選択中 = available 背景 + 白文字 / 非選択 = backgroundSecondary + text 色
                _aiButtonImages[i].color = selected ? UITheme.Available : UITheme.BackgroundSecondary;
                _aiButtonLabels[i].color = selected ? Color.white : UITheme.TextColor;
            }
        }

        private void ShowCountdownValue(int value)
        {
            if (_goHideRoutine != null)
            {
                StopCoroutine(_goHideRoutine);
                _goHideRoutine = null;
            }
            _countdownLabel.text = value.ToString();
            _countdownLabel.fontSize = 280;
            _countdownLabel.color = UITheme.Available; // Swift: カウント数字は available (金)
            _countdownOverlay.SetActive(true);
        }

        private void ShowGo()
        {
            _countdownLabel.text = "GO!";
            _countdownLabel.fontSize = 240;
            _countdownLabel.color = UITheme.Success; // Swift: GO! は success (緑)
            _countdownOverlay.SetActive(true);

            if (_goHideRoutine != null) StopCoroutine(_goHideRoutine);
            _goHideRoutine = StartCoroutine(HideCountdownAfterGo());
        }

        private IEnumerator HideCountdownAfterGo()
        {
            yield return new WaitForSeconds(GoDisplaySeconds);
            _countdownOverlay.SetActive(false);
            _goHideRoutine = null;
        }

        private void ShowFloorClear()
        {
            var session = App.I.Game.Session;
            if (session == null) return;
            _floorClearFloorLabel.text = session.CurrentFloor + "階層";
            _floorClearNextLabel.text = "次: " + (session.CurrentFloor + 1) + "階層";
            _floorClearOverlay.SetActive(true);
        }

        private void ShowSkillResetToast()
        {
            _skillResetToast.SetActive(true);
            if (_skillResetRoutine != null) StopCoroutine(_skillResetRoutine);
            _skillResetRoutine = StartCoroutine(HideSkillResetToast());
        }

        private IEnumerator HideSkillResetToast()
        {
            yield return new WaitForSeconds(SkillResetSeconds);
            _skillResetToast.SetActive(false);
            _skillResetRoutine = null;
        }

        private void ShowBossWarning()
        {
            var session = App.I.Game.Session;
            _bossFloorLabel.text = (session != null ? session.CurrentFloor : 0) + "階層";
            _bossOverlay.SetActive(true);
            if (_bossHideRoutine != null) StopCoroutine(_bossHideRoutine);
            _bossHideRoutine = StartCoroutine(HideBossWarning());
        }

        private IEnumerator HideBossWarning()
        {
            yield return new WaitForSeconds(BossWarningSeconds);
            _bossOverlay.SetActive(false);
            _bossHideRoutine = null;
        }

        private void ShowTimingGrade(TimingGrade grade)
        {
            _gradeShownAt = Time.unscaledTime;
            switch (grade)
            {
                case TimingGrade.Just:
                    _gradeLabel.text = "JUST!";
                    _gradeLabel.color = UITheme.Success;
                    break;
                case TimingGrade.Good:
                    _gradeLabel.text = "GOOD";
                    _gradeLabel.color = UITheme.Available;
                    break;
                default:
                    _gradeLabel.text = "MISS";
                    _gradeLabel.color = UITheme.Warning;
                    break;
            }
            RefreshComboRow(App.I.Game.Session);
        }

        /// <summary>全ての一時表示 (オーバーレイ・トースト・判定) を閉じ、コルーチンを破棄する。</summary>
        private void ResetTransientUI()
        {
            StopAllCoroutines();
            _goHideRoutine = null;
            _bossHideRoutine = null;
            _skillResetRoutine = null;
            _gradeShownAt = -1f;

            if (_floorClearOverlay != null) _floorClearOverlay.SetActive(false);
            if (_skillResetToast != null) _skillResetToast.SetActive(false);
            if (_pregameOverlay != null) _pregameOverlay.SetActive(false);
            if (_pausedOverlay != null) _pausedOverlay.SetActive(false);
            if (_countdownOverlay != null) _countdownOverlay.SetActive(false);
            if (_gameOverOverlay != null) _gameOverOverlay.SetActive(false);
            if (_bossOverlay != null) _bossOverlay.SetActive(false);
            if (_comboRow != null) _comboRow.SetActive(false);
        }

        // MARK: - 再描画

        /// <summary>SwiftUI の @Published バインディング相当を一括再描画する。</summary>
        private void RefreshAll()
        {
            if (App.I == null || App.I.Game == null) return;
            var game = App.I.Game;
            var session = game.Session;

            if (session == null)
            {
                // プレゲーム (ラン未開始): 盤面は空、HUD は開始予定階層のみ
                _bpmInfo.Render(_pendingStartFloor, Floor.CalculateBPM(_pendingStartFloor));
                _skillButton.gameObject.SetActive(false);
                _specialRuleRoot.SetActive(false);
                _pausedOverlay.SetActive(false);
                _floorClearOverlay.SetActive(false);
                _board.Render(null, true, null, null);
                return;
            }

            int floor = session.CurrentFloor;
            _bpmInfo.Render(floor, Floor.CalculateBPM(floor));
            _beatIndicator.Render(game.TurnCountdown, session.TurnCount, session.MaxTurns);
            RefreshComboRow(session);

            _turnValueLabel.text = session.TurnCount + " / " + session.MaxTurns;

            // スキル残数行 (Swift: 斜め移動 (盗賊) は常時パッシブのため非表示)
            bool showSkillInfo = session.Skill.Type != SkillType.Diagonal;
            _skillInfoRow.SetActive(showSkillInfo);
            if (showSkillInfo)
            {
                _skillNameLabel.text = session.Skill.Name;
                _skillCountLabel.text = session.RemainingSkillUses + " / " + session.Skill.MaxUsage;
                _skillCountLabel.color = session.RemainingSkillUses > 0 ? UITheme.Available : UITheme.Warning;
            }

            RefreshSkillButton(session);
            RefreshSpecialRule(session);

            _pauseButtonLabel.text = session.Status == GameStatus.Paused ? "再開" : "一時停止";
            _pausedOverlay.SetActive(session.Status == GameStatus.Paused);

            if (game.IsFloorClearPending) ShowFloorClear();
            else _floorClearOverlay.SetActive(false);

            RenderBoard(session);
        }

        /// <summary>コンボ / タイミング判定行 (Swift: comboDisplay)。</summary>
        private void RefreshComboRow(GameSession session)
        {
            bool gradeVisible = _gradeShownAt >= 0f;
            int combo = (session != null) ? session.ComboCount : 0;
            bool comboVisible = combo >= 2;

            _gradeLabel.gameObject.SetActive(gradeVisible);
            _comboLabel.gameObject.SetActive(comboVisible);
            _multiplierLabel.gameObject.SetActive(false);

            if (comboVisible)
            {
                double multiplier = session.ScoreMultiplier;
                _comboLabel.text = "×" + combo + " コンボ";
                // Swift: 倍率発動中はコンボ文字が金色に昇格
                _comboLabel.color = multiplier > 1.0 ? UITheme.GoldText : UITheme.TextColor;

                if (multiplier > 1.0)
                {
                    _multiplierLabel.text = "×" + multiplier.ToString("0.0");
                    _multiplierLabel.gameObject.SetActive(true);
                }
            }

            _comboRow.SetActive(gradeVisible || comboVisible);
        }

        /// <summary>スキルボタン (Swift: skillButton。dash / invisible / shield のみ表示)。</summary>
        private void RefreshSkillButton(GameSession session)
        {
            SkillType type = session.Skill.Type;
            bool show = type == SkillType.Dash || type == SkillType.Invisible || type == SkillType.Shield;
            _skillButton.gameObject.SetActive(show);
            if (!show) return;

            // Swift: isActive = (dash && isSkillActive) || (invisible && isInvisible) || (shield && shieldActive)
            // (GameView.swift skillButton)。GameController.IsInvisible は担当A (GameController.cs) 側で
            // 追加予定のプロパティ — 未追加の間はこの参照がコンパイルエラーになる (要 GameController 側対応)。
            bool isActive = (type == SkillType.Dash && session.IsSkillActive)
                         || (type == SkillType.Invisible && App.I.Game.IsInvisible)
                         || (type == SkillType.Shield && session.ShieldActive);
            int remaining = session.RemainingSkillUses;

            _skillButtonLabel.text = session.Skill.Name + " (残り" + remaining + "回)" + (isActive ? "  ON" : "");
            // Swift のグラデ 3 態 (発動中 / 使用可 / 使い切り) を単色近似 (グラデは Phase 4)
            _skillButtonImage.color = isActive ? UITheme.Success
                : remaining > 0 ? UITheme.Available
                : UITheme.WithAlpha(UITheme.GridBorder, 0.5f);
            _skillButton.interactable = remaining > 0 && session.Status == GameStatus.Playing;
        }

        /// <summary>特殊ルール表示 (Swift: specialRuleLabel / specialRuleText)。</summary>
        private void RefreshSpecialRule(GameSession session)
        {
            SpecialRule rule = session.CurrentSpecialRule;
            _specialRuleRoot.SetActive(rule != SpecialRule.None);
            switch (rule)
            {
                case SpecialRule.Fog:
                    _specialRuleLabel.text = "霧の呪い: 視界が制限されています";
                    break;
                case SpecialRule.Disappear:
                    _specialRuleLabel.text = "崩壊の罠: 消失したマスに注意";
                    break;
                case SpecialRule.FogDisappear:
                    _specialRuleLabel.text = "霧の呪い + 崩壊の罠";
                    break;
            }
        }

        /// <summary>盤面再描画。スプライトはキャラ / 階層帯が変わった時だけロードし直す。</summary>
        private void RenderBoard(GameSession session)
        {
            string playerName = session.CurrentCharacter.SpriteName;
            if (playerName != _playerSpriteName)
            {
                _playerSpriteName = playerName;
                _playerSprite = UIFactory.LoadSprite(playerName);
            }

            // 鬼の帯: 1-25 赤鬼 / 26-50 青鬼 / 51-75 骸骨 / 76-100 ドラゴン (正本: Floor.getEnemySprite)
            string enemyName = Floor.GetEnemySprite(session.CurrentFloor);
            if (enemyName != _enemySpriteName)
            {
                _enemySpriteName = enemyName;
                _enemySprite = UIFactory.LoadSprite(enemyName);
            }

            _board.Render(session, session.Status != GameStatus.Playing, _playerSprite, _enemySprite);
        }

        // MARK: - リザルト payload (ScreenPayloads.ResultPayload — ResultScreen と共有)

        private ResultPayload BuildResultPayload(GameController game, GameSession session)
        {
            return new ResultPayload
            {
                Floor = session.CurrentFloor, // 勝利時は Swift quirk 踏襲で 101
                Won = session.Status == GameStatus.Win,
                DefeatReason = session.LastDefeatReason,
                ElapsedSeconds = game.ElapsedSeconds,
                NearMissDistance = game.NearMissDistance,
                PlayerPosition = session.PlayerPosition,
                EnemyPosition = session.EnemyPosition,
                // 未取得 (-1) は Swift の previousBest 既定値 0 (= 新記録扱いの安全側) に倒す
                PreviousBest = _bestBeforeRun >= 0 ? _bestBeforeRun : 0,
            };
        }
    }
}
