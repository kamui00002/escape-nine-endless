// GameScreen.cs
// Swift 正本: Views/Game/GameView.swift (画面全体・HUD・オーバーレイ群)
//   - 盤面        → BoardStage (Phase 4.5 でワールド空間 3D 化。旧 uGUI 盤面は削除済み。
//                  Swift 正本は GridBoardView.swift / GridCellView.swift)
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
using TMPro;
using EscapeNine.Core;
using EscapeNine.Runtime.UI.Fx;
using EscapeNine.Runtime.Stage;

namespace EscapeNine.Runtime.UI
{
    public sealed class GameScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Game;

        // ---- 表示タイマー定数 (Swift: DispatchQueue.asyncAfter の秒数) ----
        private const float GoDisplaySeconds = 0.5f;    // GO! 表示 (GameController コメント指定)
        private const float BossWarningSeconds = 2.0f;  // ボス出現警告
        private const float SkillResetSeconds = 3.0f;   // スキル回復通知 (Swift: GameViewModel.swift:747-749 の Task.sleep(3秒) と一致)
        private const float GradeDisplaySeconds = 0.8f; // タイミング判定 (JUST/GOOD/MISS)

        // ---- 担当A juice (Phase 4): 衝突演出の色 ----
        private static readonly Color InvisibleAbsorbColor = new Color(0.72f, 0.42f, 0.98f); // 紫 (透明化吸収)
        private static readonly Color ShieldAbsorbColor = new Color(0.35f, 0.62f, 1f);        // 青 (盾ガード消費)

        // ---- Phase 5a: レリックドラフト (docs/unity-phase5-roguelike-design.md §2.2/§6.3) ----
        // UITheme にレアリティ色の定義が無いため本画面固有で追加する (InvisibleAbsorbColor 等と同じ作法)。
        private static readonly Color RelicRareColor = new Color(0.45f, 0.62f, 1f);      // 青
        private static readonly Color RelicEpicColor = new Color(0.72f, 0.42f, 0.98f);   // 紫
        // Phase 5b/5c: #18 蒐集家の目で候補が 3→4 に増えるため、カードスロットは最大 4 枚を確保する。
        // 3 枚時は従来と同じ配置を保ち、4 枚時のみ縦積みの高さ/間隔を詰める (ShowRelicDraft/LayoutRelicCards)。
        private const int RelicDraftMaxCards = 4;

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
        private TextMeshProUGUI _pauseButtonLabel;

        /// <summary>Phase 5a: 所持レリック数の簡易カウンタ (「遺物 N」)。タップでの一覧表示
        /// (レリック名/効果の詳細確認) は Phase 5b の MetaShopScreen 系と合わせて実装する予定
        /// (§6.3 の要件どおり「タップで一覧は5b送り」— 5a では非タップの Label に留める)。</summary>
        private TextMeshProUGUI _relicCountLabel;
        private BPMInfoWidget _bpmInfo;
        private BeatIndicatorWidget _beatIndicator;
        private IBoardView _board;

        // ---- Wave 2: 3D BoardStage 参照 (BuildWorldBoard が生成) ----
        private RectTransform _boardAnchor;
        private BoardStage _boardStage;
        private StageCameraDirector _cameraDirector;
        private StageRenderView _renderView;

        // ---- Wave 3 (a): ポストプロセス + ビート同期脈動 ----
        private StagePostFx _postFx;
        private BeatVolumePulse _beatVolumePulse;

        // ---- Wave 3 (b): カメラワーク (衝突インパルス/回り込み/圧迫ズーム) ----
        private CameraRig _cameraRig;

        private GameObject _comboRow;
        private TextMeshProUGUI _gradeLabel;
        private TextMeshProUGUI _comboLabel;
        private TextMeshProUGUI _multiplierLabel;

        private TextMeshProUGUI _turnValueLabel;
        private GameObject _skillInfoRow;
        private TextMeshProUGUI _skillNameLabel;
        private TextMeshProUGUI _skillCountLabel;

        private Button _skillButton;
        private Image _skillButtonImage;
        private TextMeshProUGUI _skillButtonLabel;

        private GameObject _specialRuleRoot;
        private TextMeshProUGUI _specialRuleLabel;

        // ---- オーバーレイ参照 (Swift の ZStack と同じ重ね順で生成する) ----
        private GameObject _floorClearOverlay;
        private TextMeshProUGUI _floorClearFloorLabel;
        private TextMeshProUGUI _floorClearNextLabel;

        // ---- Phase 5a: レリックドラフトオーバーレイ (Swift正本には対応なし) ----
        // 独立 ScreenBase (RelicDraftScreen) にせず GameScreen 内オーバーレイとして実装する
        // 意図的差分: Router.Show() で画面遷移すると OnHide が BoardStage を非表示にするため
        // (BuildWorldBoard / OnHide 参照)、階層クリア→ドラフト→次階層クリアの間、
        // §1.5 で規定する舞台演出 (背景ボケ・盤面) が一瞬消える隙間ができてしまう。
        // floorClearOverlay と同じ「兄弟オーバーレイ」に統一することで舞台を表示させ続ける。
        private GameObject _relicDraftOverlay;
        private GameObject[] _relicCardSlots;
        private Image[] _relicCardFrames;
        private Image[] _relicCardInnerImages;
        private TextMeshProUGUI[] _relicCardNameLabels;
        private TextMeshProUGUI[] _relicCardRarityLabels;
        private TextMeshProUGUI[] _relicCardDescLabels;

        /// <summary>
        /// UI ローカルの「ドラフトオーバーレイを実際に開いたか」フラグ。
        /// GameController.IsRelicDraftPending は「階層クリア確定と同時」に true になるが (§2.1)、
        /// UX 上は FloorClear オーバーレイの「スタート」を押すまでドラフト画面を出さない
        /// (§6.3 タスク仕様どおり)。両者を区別するため UI 側だけが持つ状態。
        /// </summary>
        private bool _relicDraftScreenOpen;

        // ---- Phase 5c: 分岐ルート選択オーバーレイ (docs/unity-phase5-roguelike-design.md §4/§1.5) ----
        // レリックドラフトと同じ「画面内オーバーレイ」方式 (Router 遷移せず舞台を表示し続ける)。
        // フロー: FloorClear「スタート」→ RouteChoice (提示階層のみ) → RelicDraft → Advance。
        private GameObject _routeChoiceOverlay;

        /// <summary>UI ローカルの「ルート選択オーバーレイを実際に開いたか」フラグ。
        /// GameController.IsRouteChoicePending は階層クリア確定と同時に true になるため、
        /// _relicDraftScreenOpen と同様に「スタート」を押すまで実際の表示はしない。</summary>
        private bool _routeChoiceScreenOpen;

        private GameObject _skillResetToast;

        private GameObject _pregameOverlay;
        private TextMeshProUGUI _pregameFloorLabel;
        private Image[] _aiButtonImages;
        private TextMeshProUGUI[] _aiButtonLabels;

        private GameObject _pausedOverlay;

        private GameObject _countdownOverlay;
        private Image _countdownBg;
        private Color _countdownBgBaseColor; // Flash 中断残留の復帰用基準色 (レビュー G2)
        private TextMeshProUGUI _countdownLabel;

        private GameObject _gameOverOverlay;
        private Image _gameOverBg;
        private TextMeshProUGUI _gameOverIcon;
        private TextMeshProUGUI _gameOverText;

        private GameObject _bossOverlay;
        private TextMeshProUGUI _bossFloorLabel;

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

        // ---- 担当A juice (Phase 4): 衝突/コンボ演出のエッジ検知用スナップショット ----

        /// <summary>直近 RefreshAll 時点の Game.IsInvisible (透明化吸収の立ち上がり検知用)。</summary>
        private bool _prevIsInvisible;

        /// <summary>直近 RefreshAll 時点の Session.ShieldActive (盾ガード消費の立ち下がり検知用)。</summary>
        private bool _prevShieldActive;

        /// <summary>直近のコンボ数 (3/5 到達エッジでの PunchScale トリガー用)。</summary>
        private int _lastComboCount;

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
            if (_built) return; // Router.Register が 1 回だけ呼ぶ (再入防御。呼び出し元は ScreenRouter.Register のみ)
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
            // floorClear < routeChoice < relicDraft < skillReset < startGame(pregame) < paused < countdown < gameOver < bossWarning
            // (relicDraft は Phase 5a 追加、routeChoice は Phase 5c 追加。どちらも floorClear 直後 =
            //  階層クリアフローの一部として直後に重なる。routeChoice は relicDraft の「前」に提示される)
            BuildFloorClearOverlay();
            BuildRouteChoiceOverlay();
            BuildRelicDraftOverlay();
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
            _pauseButtonLabel = pause.GetComponentInChildren<TextMeshProUGUI>();

            // Phase 5a: 所持レリック数カウンタ。Back(cx=0.16,w=0.26)/Pause(cx=0.82,w=0.30) の間の
            // 空き ([0.29, 0.67]) に収まる幅で配置する。
            _relicCountLabel = UIFactory.Label(parent, "RelicCount", "", 32, UITheme.GoldText,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_relicCountLabel.transform, 0.5f, 0.968f, 0.30f, 0.036f);
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
            // cx は左右ラベルの箱が接触しないよう振り分け (2026-07-04 重なり監査で検出。
            // MiddleRight/MiddleLeft 揃えのため箱が接すると実際にグリフも接近する — fontSize は
            // 変えず幅配分だけ調整する)。
            var turnCaption = UIFactory.Label(parent, "TurnCaption", "ターン", 32,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleRight);
            UIFactory.Place((RectTransform)turnCaption.transform, 0.375f, 0.676f, 0.22f, 0.024f);
            _turnValueLabel = UIFactory.Label(parent, "TurnValue", "0 / 5", 36, UITheme.Available,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place((RectTransform)_turnValueLabel.transform, 0.645f, 0.676f, 0.30f, 0.024f);

            // スキル残数行 (Swift: 2 行目。斜め移動キャラでは非表示)
            var skillRow = UIFactory.Panel(parent, "SkillInfoRow");
            UIFactory.Place(skillRow, 0.5f, 0.648f, 0.92f, 0.024f);
            _skillInfoRow = skillRow.gameObject;

            // cx は TurnCaption/TurnValue と同じ理由 (2026-07-04 重なり監査で検出) で振り分け直す。
            _skillNameLabel = UIFactory.Label(skillRow, "SkillName", "", 32,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleRight);
            UIFactory.Place((RectTransform)_skillNameLabel.transform, 0.34f, 0.5f, 0.32f, 1f);
            _skillCountLabel = UIFactory.Label(skillRow, "SkillCount", "", 36, UITheme.Available,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place((RectTransform)_skillCountLabel.transform, 0.66f, 0.5f, 0.30f, 1f);
        }

        // MARK: - Board (Swift: gridBoard)

        private void BuildBoard(RectTransform parent)
        {
            BuildWorldBoard(parent);

            _board.OnCellTapped += HandleCellTapped;
            _board.OnEnemyTapped += HandleEnemyTapped;
        }

        /// <summary>
        /// Wave 2: 3D BoardStage の生成 (docs/unity-phase4-5-visual-upgrade-design.md)。
        /// W4 ゲート通過に伴い旧 uGUI 盤面 (GridBoardWidget) は削除され、本経路のみが残る (D4)。
        /// BoardAnchor は旧盤面と同じ配置比率 (0.5, 0.425, 0.94, 0.36) を踏襲する透明な
        /// RectTransform。RawImage 経由でワールド空間の 3D BoardStage をカメラ→RT→RawImage で見せる。
        /// </summary>
        private void BuildWorldBoard(RectTransform parent)
        {
            // v2 (RenderTexture 方式): 盤面と同じ配置比率の BoardAnchor に RawImage を
            // 付け、StageRenderView がカメラ→RT→RawImage を結線する。穴あけバンド方式は廃止
            // (全画面 Background と共存できず HUD も覆った。詳細は StageRenderView.cs 冒頭)。
            RectTransform anchor = UIFactory.Panel(parent, "BoardAnchor");
            UIFactory.Place(anchor, 0.5f, 0.425f, 0.94f, 0.36f);
            _boardAnchor = anchor;

            var rawImage = anchor.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            rawImage.raycastTarget = false; // タップは StageInput の物理レイキャストが受ける
            rawImage.enabled = false;       // RT 結線まで白矩形を出さない (StageRenderView が有効化)

            _boardStage = BoardStage.Create();
            _boardStage.SetConductor(App.I != null ? App.I.Conductor : null); // Phase 5c 修正: テレグラフ拍同期用
            _boardStage.gameObject.SetActive(false); // OnShow まで無効 (他画面表示中は消す)

            // Camera.main が見つからないケース (例: OverlapAudit.cs の Edit モード BuildUI
            // フォールバック経路が MainCamera 未タグのシーンで動く場合) でも BuildUI 自体は
            // クラッシュさせない。カメラ演出無しの盤面のみ生成される劣化状態で継続する。
            _cameraDirector = StageCameraDirector.EnsureOnMainCamera();
            _renderView = _boardStage.gameObject.AddComponent<StageRenderView>();
            var input = _boardStage.gameObject.AddComponent<StageInput>();
            if (_cameraDirector != null)
            {
                _renderView.Configure(anchor, rawImage, _cameraDirector.Cam);
                input.Configure(_cameraDirector.Cam, _boardStage, anchor);
            }

            // Wave 3 (a): URP Volume (Bloom/Vignette/ColorAdjustments) + ビート同期脈動。
            // _cameraDirector が null (Camera.main 不在) の場合も StagePostFx.Create は
            // camera=null を許容し、Volume 自体は生成される (演出無しの劣化状態で継続)。
            _postFx = StagePostFx.Create(_boardStage.transform, _cameraDirector != null ? _cameraDirector.Cam : null);
            _beatVolumePulse = _boardStage.gameObject.AddComponent<BeatVolumePulse>();
            _beatVolumePulse.Configure(App.I != null ? App.I.Conductor : null, _postFx);
            _cameraRig = _boardStage.gameObject.AddComponent<CameraRig>();
            _cameraRig.Configure(_cameraDirector != null ? _cameraDirector.Cam : null);

            _board = _boardStage;
        }

        // MARK: - Bottom (Swift: skillButton + specialRuleLabel)

        private void BuildBottom(RectTransform parent)
        {
            _skillButton = UIFactory.TextButton(parent, "SkillButton", "", 44,
                UITheme.Available, Color.white, HandleSkillTapped);
            UIFactory.Place((RectTransform)_skillButton.transform, 0.5f, 0.168f, 0.72f, 0.055f);
            _skillButtonImage = _skillButton.GetComponent<Image>();
            _skillButtonLabel = _skillButton.GetComponentInChildren<TextMeshProUGUI>();
            _skillButtonLabel.fontStyle = FontStyles.Bold;
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
            var startLabel = start.GetComponentInChildren<TextMeshProUGUI>();
            if (startLabel != null) startLabel.fontStyle = FontStyles.Bold;

            _floorClearOverlay.SetActive(false);
        }

        /// <summary>
        /// Phase 5c: 分岐ルート選択 (docs/unity-phase5-roguelike-design.md §4/§1.5)。Swift正本には対応なし。
        /// 「安全なルート」「深淵のルート」の 2 択カード。深淵側は警告色 (UITheme.Warning) で危険を示す。
        ///
        /// §1.5 の「深淵カーソル時に StageLights を赤黒く沈める」照明プレビューは本 UI では縮退して
        /// 実装していない (カード色のみ)。理由: (1) 本オーバーレイはタップ/1・2キーの二者択一で、
        /// SwiftUI 的な「カーソルホバー」概念が無く「合わせる」瞬間が存在しない。(2) 設計 §1.5 末尾は
        /// 「UI 層が BoardStage の内部へ直接触ることは禁止 (イベント経由のみ)」と明記しており、
        /// GameScreen から StageLights を直接駆動するのは規約違反。よって警告色カードで危険を伝えるに留める
        /// (真の照明プレビューはイベント境界を足す別課題)。
        /// </summary>
        private void BuildRouteChoiceOverlay()
        {
            var overlay = UIFactory.Panel(transform, "RouteChoiceOverlay",
                UITheme.WithAlpha(UITheme.Background, 0.95f));
            _routeChoiceOverlay = overlay.gameObject;

            var title = UIFactory.Label(overlay, "Title", "進む道を選べ", 56, UITheme.GoldText,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.5f, 0.80f, 0.9f, 0.06f);

            BuildRouteCard(overlay, "SafeCard", 0.585f,
                UITheme.Success, "1  安全なルート", "通常の階層をそのまま進む。堅実に。",
                () => HandleRouteChosen(RouteChoice.Safe));

            BuildRouteCard(overlay, "AbyssCard", 0.375f,
                UITheme.Warning, "2  深淵のルート", "敵が賢く・罠も早まるが、報酬に Rare 以上を確定。",
                () => HandleRouteChosen(RouteChoice.Abyss));

            _routeChoiceOverlay.SetActive(false);
        }

        /// <summary>ルート選択カード 1 枚を構築する (レリックカードと同じ「枠 + inner ボタン」作法)。</summary>
        private GameObject BuildRouteCard(RectTransform overlay, string name, float cy,
            Color accent, string title, string desc, UnityEngine.Events.UnityAction onTap)
        {
            var slot = UIFactory.Panel(overlay, name);
            UIFactory.Place(slot, 0.5f, cy, 0.88f, 0.185f);

            var frame = UIFactory.ColorRect(slot, "Frame", accent);
            UIFactory.Place((RectTransform)frame.transform, 0.5f, 0.5f, 1f, 1f);

            var inner = UIFactory.Panel(slot, "Inner", UITheme.BackgroundSecondary);
            UIFactory.Place(inner, 0.5f, 0.5f, 0.965f, 0.90f);
            var btn = inner.gameObject.AddComponent<Button>();
            btn.targetGraphic = inner.GetComponent<Image>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(onTap);

            var titleLabel = UIFactory.Label(inner, "Title", title, 44, accent,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)titleLabel.transform, 0.5f, 0.66f, 0.92f, 0.34f);

            var descLabel = UIFactory.Label(inner, "Desc", desc, 28,
                UITheme.WithAlpha(UITheme.TextColor, 0.85f));
            UIFactory.Place((RectTransform)descLabel.transform, 0.5f, 0.26f, 0.9f, 0.40f);

            return slot.gameObject;
        }

        /// <summary>
        /// Phase 5a: レリックドラフト (docs/unity-phase5-roguelike-design.md §2.1/§6.3)。
        /// Swift正本には対応なし。floorClearOverlay の「スタート」タップで表示に切り替わる
        /// (BuildFloorClearOverlay の直下に生成する意図的な兄弟順序。理由は _relicDraftOverlay
        /// フィールドのコメント参照)。
        ///
        /// 縦積み3枚を採用: CanvasScaler の基準解像度 1170x2532 (UIFactory.cs 冒頭コメント参照) は
        /// 縦長のポートレート比率のため、横3分割よりも縦積みの方が説明文 (最大40文字程度の
        /// 日本語) を折り返しなく収められる幅を確保できる。
        /// </summary>
        private void BuildRelicDraftOverlay()
        {
            var overlay = UIFactory.Panel(transform, "RelicDraftOverlay",
                UITheme.WithAlpha(UITheme.Background, 0.95f));
            _relicDraftOverlay = overlay.gameObject;

            var title = UIFactory.Label(overlay, "Title", "レリックを選べ", 56, UITheme.GoldText,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.5f, 0.82f, 0.9f, 0.06f);

            _relicCardSlots = new GameObject[RelicDraftMaxCards];
            _relicCardFrames = new Image[RelicDraftMaxCards];
            _relicCardInnerImages = new Image[RelicDraftMaxCards];
            _relicCardNameLabels = new TextMeshProUGUI[RelicDraftMaxCards];
            _relicCardRarityLabels = new TextMeshProUGUI[RelicDraftMaxCards];
            _relicCardDescLabels = new TextMeshProUGUI[RelicDraftMaxCards];

            // 4 枚時 (#18 蒐集家の目) の初期配置で全スロットを生成する。実際の cy/高さは候補数に応じて
            // ShowRelicDraft → LayoutRelicCards が再設定するため、ここでの値は初期プレースホルダに過ぎない。
            for (int i = 0; i < RelicDraftMaxCards; i++)
            {
                int index = i; // クロージャ用固定
                var slot = UIFactory.Panel(overlay, "Card" + i);
                UIFactory.Place(slot, 0.5f, 0.66f - 0.16f * i, 0.88f, 0.135f);
                _relicCardSlots[index] = slot.gameObject;

                // フレーム: レアリティ色の縁取り。§1.5 は「レアリティ別発光 = Bloom 閾値超えの
                // エミッシブ色」を規定するが、uGUI (Canvas Overlay/Screen Space) は URP Bloom の
                // 対象外 (Bloom はカメラのポストプロセスであり Canvas 直描画には掛からない)。
                // 正直に: ここでは Bloom の代替として枠の明度 (レアリティ色そのもの) と太さ
                // (RarityFrameInset、レアリティが高いほど枠が太い = inner の inset が小さい) で
                // 「特別感」を表現するに留める。真の発光表現は Phase 4.5 W4 完了後の別課題。
                var frame = UIFactory.ColorRect(slot, "Frame", Color.white);
                UIFactory.Place((RectTransform)frame.transform, 0.5f, 0.5f, 1f, 1f);
                _relicCardFrames[index] = frame;

                var inner = UIFactory.Panel(slot, "Inner", UITheme.BackgroundSecondary);
                UIFactory.Place(inner, 0.5f, 0.5f, 0.965f, 0.90f); // 既定 inset (Common 相当)。実際の値は ShowRelicDraft が候補のレアリティで再設定する
                var btn = inner.gameObject.AddComponent<Button>();
                btn.targetGraphic = inner.GetComponent<Image>();
                btn.transition = Selectable.Transition.ColorTint;
                btn.navigation = new Navigation { mode = Navigation.Mode.None }; // TextButton と同じ理由 (レビューC2)
                btn.onClick.AddListener(() => HandleRelicCardTapped(index));
                _relicCardInnerImages[index] = inner.GetComponent<Image>();

                var nameLabel = UIFactory.Label(inner, "Name", "", 38, UITheme.TextColor,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Place((RectTransform)nameLabel.transform, 0.5f, 0.76f, 0.92f, 0.30f);
                _relicCardNameLabels[index] = nameLabel;

                var rarityLabel = UIFactory.Label(inner, "Rarity", "", 24, UITheme.TextColor);
                UIFactory.Place((RectTransform)rarityLabel.transform, 0.5f, 0.53f, 0.92f, 0.16f);
                _relicCardRarityLabels[index] = rarityLabel;

                var descLabel = UIFactory.Label(inner, "Desc", "", 26, UITheme.WithAlpha(UITheme.TextColor, 0.85f));
                UIFactory.Place((RectTransform)descLabel.transform, 0.5f, 0.20f, 0.9f, 0.36f);
                _relicCardDescLabels[index] = descLabel;
            }

            _relicDraftOverlay.SetActive(false);
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
            _aiButtonLabels = new TextMeshProUGUI[SelectableAiLevels.Length];
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
                _aiButtonLabels[i] = btn.GetComponentInChildren<TextMeshProUGUI>();
            }

            var start = UIFactory.TextButton(overlay, "StartButton", "冒険を始める", 56,
                UITheme.Main, UITheme.Background, HandlePregameStart);
            UIFactory.Place((RectTransform)start.transform, 0.5f, 0.375f, 0.62f, 0.06f);
            var startLabel = start.GetComponentInChildren<TextMeshProUGUI>();
            if (startLabel != null) startLabel.fontStyle = FontStyles.Bold;

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
            var resumeLabel = resume.GetComponentInChildren<TextMeshProUGUI>();
            if (resumeLabel != null) resumeLabel.fontStyle = FontStyles.Bold;

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
            _countdownBg = overlay.GetComponent<Image>();
            _countdownBgBaseColor = _countdownBg.color;

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

            // Wave 2 v2: GameScreen 表示中のみ BoardStage を有効化し、RT 結線 (StageRenderView) と
            // カメラフレーミングを 1 フレーム目から適用しておく (白フラッシュ/ズレ防止)。
            if (_boardStage != null)
            {
                _boardStage.gameObject.SetActive(true);

                // Wave 5: 品質ティアを毎回再適用する (Settings で変更された場合に次の表示から
                // 反映されるようにするため。RefreshDynamic 等と同じ「OnShow で live に読む」規約)。
                // _renderView.Apply() より前に呼ぶこと — RT フォーマット (SetFormat) を確定させて
                // から RT を生成しないと、直後の Apply() が旧フォーマットで作った RT を
                // 同フレーム内で作り直す無駄が出る。
                if (App.I != null && App.I.Player != null)
                {
                    StageQuality.Apply(App.I.Player.StageQualityTier, _postFx, _boardStage.Particles, _renderView);
                }

                _renderView.Apply();
                if (_cameraDirector != null) _cameraDirector.ApplyFraming();
            }

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

            // Wave 2 v2: 他画面へ切り替わる時は BoardStage を無効化する。RT の解放と
            // camera.targetTexture の切断は StageRenderView.OnDisable が行う。
            if (_boardStage != null)
            {
                _boardStage.gameObject.SetActive(false);
            }
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
            game.OnBossPatternChanged -= HandleBossPatternChanged;
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
            game.OnBossPatternChanged += HandleBossPatternChanged; // Phase 5c: ボスパターン切替のカメラ演出
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

            // Wave 3 (b): 圧迫ズーム更新 + 2階層目以降の GO で回り込み演出。
            // 設計上は「階層クリア時」だが、FloorClear オーバーレイ (全画面95%不透明) が
            // 盤面を覆って回り込みが見えないため、「新しい階へ入る瞬間」に移した (意図的差分)。
            if (_cameraRig != null && App.I.Game.Session != null)
            {
                int floor = App.I.Game.Session.CurrentFloor;
                _cameraRig.PressureZoom(floor);
                if (floor > 1) _cameraRig.OrbitOnFloorClear();
            }
        }

        private void HandleTurnResolved(TurnResult result)
        {
            if (!isActiveAndEnabled) return;
            if (result == TurnResult.FloorCleared)
            {
                _relicDraftScreenOpen = false; // 新しい階層クリアサイクルは必ずFloorClear表示から始まる
                _routeChoiceScreenOpen = false; // Phase 5c: 同上 (ルート選択も FloorClear から始まる)
                ShowFloorClear(); // 「スタート」で AdvanceToNextFloor (自動遷移しないのが正本仕様)
            }
            else if (result == TurnResult.Continued)
            {
                // 担当A juice: 透明化吸収 / 盾ガード消費の演出 (敗北しなかった衝突のみ該当)
                HandleCollisionAbsorbFx();
            }
            // Defeated の再描画は直後の OnGameOver が担う
        }

        /// <summary>
        /// 透明化吸収 (Game.IsInvisible 立ち上がり) / 盾ガード消費 (Session.ShieldActive 立ち下がり) を
        /// 検知して演出する (担当A juice)。_prevIsInvisible / _prevShieldActive は RefreshAll の末尾で
        /// 「このターン解決の直前」の値を保持している (OnTurnResolved は同じ拍の OnStateChanged より前に
        /// 発火するため) — ここで現在値と比較するだけで立ち上がり/立ち下がりを検知できる。
        /// </summary>
        private void HandleCollisionAbsorbFx()
        {
            var game = App.I.Game;
            var session = game.Session;
            if (session == null) return;

            if (!_prevIsInvisible && game.IsInvisible)
            {
                _board.FlashPlayer(InvisibleAbsorbColor);
                _board.BurstAtPlayer(InvisibleAbsorbColor);
                if (_cameraRig != null) _cameraRig.Impulse(0.04f); // 非致死衝突は死亡時(0.08)より弱く
            }
            else if (_prevShieldActive && !session.ShieldActive)
            {
                _board.FlashPlayer(ShieldAbsorbColor);
                if (_cameraRig != null) _cameraRig.Impulse(0.04f);
            }
        }

        private void HandleFloorAdvanced(int newFloor)
        {
            if (!isActiveAndEnabled) return;

            _floorClearOverlay.SetActive(false);
            _relicDraftOverlay.SetActive(false); // Phase 5a: 進行済みなら念のため閉じておく (防御的)
            _relicDraftScreenOpen = false;
            _routeChoiceOverlay.SetActive(false); // Phase 5c: 同上
            _routeChoiceScreenOpen = false;
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

            // 担当A juice: 敗北の「間」— ヒットストップ + 盤面シェイク + 赤フラッシュ + 破片
            FxKit.HitStop(this, 0.08f);
            _board.Shake();
            _board.FlashPlayer(UITheme.Warning);
            _board.BurstAtPlayer(UITheme.Warning);
            if (_cameraRig != null) _cameraRig.Impulse(0.08f); // Wave 3 (b): 死亡時のカメラインパルス
        }

        private void HandleBossWarning()
        {
            if (!isActiveAndEnabled) return;
            ShowBossWarning();
        }

        /// <summary>
        /// Phase 5c: ボスパターンが切り替わったターンのカメラ演出 (§1.5/§5.2)。タイルの
        /// 予告/赤熱表示自体は BoardStage.Render が毎拍 session から再計算するため、ここでは
        /// 「パターンが変わった手応え」だけを担う。威圧 (最も危険) への切替は少し強めの軽い衝撃、
        /// それ以外は控えめ。CameraRig 不在時は無演出で継続する (劣化許容)。
        /// </summary>
        private void HandleBossPatternChanged(BossPattern pattern)
        {
            if (!isActiveAndEnabled) return;
            if (_cameraRig == null) return;
            _cameraRig.Impulse(pattern == BossPattern.Intimidation ? 0.05f : 0.03f);
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

        /// <summary>
        /// Phase 6a (デスクトップ): キーボード (Esc/P) からの一時停止トグル。
        /// ヘッダーの一時停止ボタンと同処理を公開する (KeyboardInput.cs から呼ばれる)。
        /// </summary>
        public void TogglePauseFromKeyboard()
        {
            HandlePauseTapped();
        }

        /// <summary>
        /// Phase 6a (デスクトップ): キーボード (Space/Enter) からの階層クリア「スタート」相当。
        /// ドラフト提示中かどうかの分岐を含む HandleFloorClearStart と同一処理を公開する
        /// (KeyboardInput.cs から呼ばれる)。GameController.AdvanceToNextFloor を直接叩くと
        /// ドラフトオーバーレイへの分岐が起きない (IsRelicDraftPending ゲートで無視されるだけに
        /// なる) ため、Steam体験版のキーボード操作は必ずこの経由にすること (§2.1)。
        /// </summary>
        public void TriggerFloorClearStartFromKeyboard()
        {
            HandleFloorClearStart();
        }

        /// <summary>
        /// Phase 6a (デスクトップ): レリックドラフト提示中の 1/2/3 キー選択 (KeyboardInput.cs から
        /// 呼ばれる。§2.1: Steam体験版のキーボード操作対応の必須要件)。
        /// ドラフトオーバーレイが実際に開いている間 (_relicDraftScreenOpen) のみ受理する —
        /// GameController.IsRelicDraftPending は FloorClear オーバーレイ表示中から既に true のため、
        /// ここでガードしないと「スタート」を押す前 (ドラフト画面が見えていない状態) でも
        /// 1/2/3 キーが不可視のカードを選択できてしまう。
        /// </summary>
        public void SelectRelicCardFromKeyboard(int index)
        {
            if (!_relicDraftScreenOpen) return;
            HandleRelicCardTapped(index);
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
            var game = App.I.Game;

            // Phase 5c: ルート選択提示中 (§4: 階層クリア確定と同時に pending) なら、まずルート選択へ。
            // 順序 FloorClear → RouteChoice → RelicDraft → Advance を UI 側でも保つ。
            if (game.IsRouteChoicePending)
            {
                _routeChoiceScreenOpen = true;
                _floorClearOverlay.SetActive(false);
                ShowRouteChoice();
                return;
            }

            // Phase 5a: ドラフト提示中 (§2.1: 階層クリア確定と同時に候補生成済み) ならまだ
            // AdvanceToNextFloor を呼ばず、ドラフトオーバーレイへ切り替える (§6.3 のタスク仕様:
            // 「フロアクリアオーバーレイのスタート押下時にドラフトオーバーレイを表示」)。
            if (game.IsRelicDraftPending)
            {
                _relicDraftScreenOpen = true;
                _floorClearOverlay.SetActive(false);
                ShowRelicDraft();
                return;
            }

            game.AdvanceToNextFloor();
        }

        /// <summary>
        /// Phase 5c: ルート選択の確定 (§4)。GameController.ChooseRoute → ルート選択オーバーレイを閉じ、
        /// 深淵ルート報酬で生成され得るドラフトがあればドラフト画面へ、無ければ次階層へ進める。
        /// カードタップ / 1・2 キー共通のハンドラ。
        /// </summary>
        private void HandleRouteChosen(RouteChoice choice)
        {
            var game = App.I.Game;
            if (!game.IsRouteChoicePending) return; // 二重タップ等の防御

            App.I.Audio.PlaySfx("button_tap");
            game.ChooseRoute(choice); // IsRouteChoicePending=false + (Abyss なら) ドラフト候補生成

            _routeChoiceScreenOpen = false;
            _routeChoiceOverlay.SetActive(false);

            // ルート確定後: ドラフトが提示されたらドラフト画面へ、無ければ次階層へ。
            if (game.IsRelicDraftPending)
            {
                _relicDraftScreenOpen = true;
                _floorClearOverlay.SetActive(false);
                ShowRelicDraft();
            }
            else
            {
                game.AdvanceToNextFloor();
            }
        }

        /// <summary>
        /// Phase 6a (デスクトップ): ルート選択の 1/2 キー (KeyboardInput.cs から呼ばれる、§4)。
        /// ドラフトの 1/2/3/4 キーとは pending 状態で排他 (KeyboardInput 側で分岐)。
        /// ルート選択オーバーレイが実際に開いている間 (_routeChoiceScreenOpen) のみ受理する。
        /// index 0 = 安全 (Safe) / 1 = 深淵 (Abyss)。
        /// </summary>
        public void SelectRouteFromKeyboard(int index)
        {
            if (!_routeChoiceScreenOpen) return;
            HandleRouteChosen(index == 0 ? RouteChoice.Safe : RouteChoice.Abyss);
        }

        /// <summary>
        /// Phase 5a: レリックカードタップ (§6.3)。GameController.ChooseRelic → オーバーレイを閉じる →
        /// 既存の Advance フローへ、の順で処理する。index は候補配列の位置 (カードタップ / 1-2-3 キー共通)。
        /// </summary>
        private void HandleRelicCardTapped(int index)
        {
            var game = App.I.Game;
            var candidates = game.CurrentDraftCandidates;
            if (index < 0 || index >= candidates.Count) return; // プール枯渇で候補3枚未満の空きスロット対策

            App.I.Audio.PlaySfx("button_tap");
            string relicId = candidates[index].Id;
            game.ChooseRelic(relicId);

            _relicDraftScreenOpen = false;
            _relicDraftOverlay.SetActive(false);
            game.AdvanceToNextFloor(); // ChooseRelic で IsRelicDraftPending=false になっているためゲートを通過する
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

            // デイリーチャレンジ画面からの pending challenge をここで破棄する (Phase 2.5 発見・修正)。
            // DailyChallengeScreen が PendingChallenge をセットして本画面 (プレゲーム AI 選択) へ遷移した後、
            // ここで「戻る」を押すとラン開始 (StartNewRun) を経由しないため消費されないまま残ってしまい、
            // 次に通常の「冒険を始める」で StartNewRun を呼んだ際、意図せずデイリーチャレンジ条件
            // (キャラ固定/AI固定/開始階層) が通常ランに適用されてしまう。ここで明示的にクリアする。
            if (App.I.DailyChallenge != null) App.I.DailyChallenge.PendingChallenge = null;

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
            FxKit.PunchScale(this, (RectTransform)_countdownLabel.transform); // 担当A juice
        }

        private void ShowGo()
        {
            _countdownLabel.text = "GO!";
            _countdownLabel.fontSize = 240;
            _countdownLabel.color = UITheme.Success; // Swift: GO! は success (緑)
            _countdownOverlay.SetActive(true);

            // 担当A juice: GO! の強調パンチ + 背景の一瞬の明滅
            FxKit.PunchScale(this, (RectTransform)_countdownLabel.transform, 0.3f, 0.3f);
            FxKit.Flash(this, _countdownBg, UITheme.WithAlpha(UITheme.Success, 0.85f), 0.35f);

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

            // RefreshAll は IsFloorClearPending の間 (次階層へ進むまで) 毎拍このメソッドを呼び直す。
            // 演出は「非表示 → 表示」に切り替わった瞬間だけ鳴らす (担当A juice)。
            bool wasActive = _floorClearOverlay.activeSelf;
            _floorClearOverlay.SetActive(true);
            if (!wasActive)
            {
                FxKit.SlideIn(this, (RectTransform)_floorClearOverlay.transform, new Vector2(0f, -100f));
                if (FxLayer.I != null) FxLayer.I.BurstScreen(new Vector2(0.5f, 0.55f), UITheme.GoldText);
                FxKit.PunchScale(this, (RectTransform)_floorClearNextLabel.transform, 0.3f, 0.35f);
            }
        }

        /// <summary>
        /// Phase 5c: ルート選択オーバーレイの表示 (§4/§1.5)。RefreshAll から毎回呼ばれても安全な冪等実装
        /// (カードは静的テキストのため内容更新は不要、SlideIn 演出だけ立ち上がりエッジで実行)。
        /// </summary>
        private void ShowRouteChoice()
        {
            bool wasActive = _routeChoiceOverlay.activeSelf;
            _routeChoiceOverlay.SetActive(true);
            if (!wasActive)
            {
                FxKit.SlideIn(this, (RectTransform)_routeChoiceOverlay.transform, new Vector2(0f, -100f));
            }
        }

        /// <summary>
        /// Phase 5a: レリックドラフトオーバーレイの表示 (§2.1/§6.3)。RefreshAll から毎回呼ばれても
        /// 安全な冪等実装 (カード内容は候補配列から都度再構築、SlideIn 演出だけ立ち上がりエッジで実行)。
        /// </summary>
        private void ShowRelicDraft()
        {
            var game = App.I.Game;
            var candidates = game.CurrentDraftCandidates;

            LayoutRelicCards(candidates.Count); // Phase 5c: 3 枚 / 4 枚で縦積みの高さ・間隔を切り替える

            for (int i = 0; i < RelicDraftMaxCards; i++)
            {
                bool hasCard = i < candidates.Count;
                _relicCardSlots[i].SetActive(hasCard);
                if (!hasCard) continue;

                var def = candidates[i];
                Color rarityColor = RelicRarityColor(def.Rarity);

                _relicCardFrames[i].color = rarityColor;
                // §1.5: uGUI (Canvas Overlay) は URP Bloom (ポストプロセス) の対象外のため、
                // 真のレアリティ発光は表現できない。ここでは枠の太さ (inset が小さいほど太い枠)
                // で代替する — 正直な劣化表現であり、真の発光は Phase 4.5 W4 完了後の別課題。
                float inset = RelicRarityFrameInset(def.Rarity);
                UIFactory.Place((RectTransform)_relicCardInnerImages[i].transform, 0.5f, 0.5f, inset, inset * 0.90f);

                _relicCardNameLabels[i].text = def.Name;
                _relicCardNameLabels[i].color = rarityColor;
                _relicCardRarityLabels[i].text = RelicRarityLabelText(def.Rarity);
                _relicCardRarityLabels[i].color = rarityColor;
                _relicCardDescLabels[i].text = def.Description;
            }

            bool wasActive = _relicDraftOverlay.activeSelf;
            _relicDraftOverlay.SetActive(true);
            if (!wasActive)
            {
                FxKit.SlideIn(this, (RectTransform)_relicDraftOverlay.transform, new Vector2(0f, -100f));
            }
        }

        /// <summary>
        /// Phase 5c: 候補数に応じて縦積みカードの中心 cy と高さを設定する (8px 重なり監査ルール意識)。
        /// 3 枚以下は従来の 3 カード配置 (0.635/0.455/0.275, 高さ 0.155) を維持し、4 枚 (#18 蒐集家の目) の
        /// ときだけ高さ 0.135・間隔 0.16 に詰める (中心間隔 0.16×2532px ≒ 405px、カード高 342px → 隙間 ~63px)。
        /// </summary>
        private void LayoutRelicCards(int count)
        {
            bool four = count >= 4;
            float cardHeight = four ? 0.135f : 0.155f;
            // 4 枚: {0.66, 0.50, 0.34, 0.18} / 3 枚以下: {0.635, 0.455, 0.275, (未使用)}
            float[] cy = four
                ? new[] { 0.66f, 0.50f, 0.34f, 0.18f }
                : new[] { 0.635f, 0.455f, 0.275f, 0.275f };

            for (int i = 0; i < RelicDraftMaxCards; i++)
            {
                UIFactory.Place((RectTransform)_relicCardSlots[i].transform, 0.5f, cy[i], 0.88f, cardHeight);
            }
        }

        /// <summary>レアリティ色 (§2.2 の Common〜Legendary)。UITheme に無い色は本画面固有の定義を使う。</summary>
        private static Color RelicRarityColor(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common: return Color.white;
                case RelicRarity.Uncommon: return UITheme.Success;    // 緑
                case RelicRarity.Rare: return RelicRareColor;          // 青
                case RelicRarity.Epic: return RelicEpicColor;          // 紫
                case RelicRarity.Legendary: return UITheme.GoldText;   // 金
                default: return Color.white;
            }
        }

        /// <summary>枠の太さ (Bloom 代替、値が小さいほど太い枠になる inset 比率)。</summary>
        private static float RelicRarityFrameInset(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common: return 0.980f;
                case RelicRarity.Uncommon: return 0.972f;
                case RelicRarity.Rare: return 0.962f;
                case RelicRarity.Epic: return 0.950f;
                case RelicRarity.Legendary: return 0.935f;
                default: return 0.980f;
            }
        }

        private static string RelicRarityLabelText(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common: return "Common";
                case RelicRarity.Uncommon: return "Uncommon";
                case RelicRarity.Rare: return "Rare";
                case RelicRarity.Epic: return "Epic";
                case RelicRarity.Legendary: return "Legendary";
                default: return "";
            }
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

            // 担当A juice: 画面再表示時に前ランの衝突/コンボ差分を誤検知しないようリセットする
            _prevIsInvisible = false;
            _prevShieldActive = false;
            _lastComboCount = 0;
            _relicDraftScreenOpen = false; // Phase 5a: 前ランの「ドラフト画面を開いた」状態を持ち越さない
            _routeChoiceScreenOpen = false; // Phase 5c: 同上 (ルート選択画面)

            if (_floorClearOverlay != null)
            {
                _floorClearOverlay.SetActive(false);
                // 担当A juice: StopAllCoroutines() で SlideIn が中断された場合、
                // anchoredPosition が中間値のまま残り次回の演出が原点からズレるのを防ぐ。
                ((RectTransform)_floorClearOverlay.transform).anchoredPosition = Vector2.zero;
            }
            if (_relicDraftOverlay != null)
            {
                _relicDraftOverlay.SetActive(false);
                ((RectTransform)_relicDraftOverlay.transform).anchoredPosition = Vector2.zero;
            }
            if (_routeChoiceOverlay != null)
            {
                _routeChoiceOverlay.SetActive(false);
                ((RectTransform)_routeChoiceOverlay.transform).anchoredPosition = Vector2.zero;
            }
            if (_skillResetToast != null) _skillResetToast.SetActive(false);
            if (_pregameOverlay != null) _pregameOverlay.SetActive(false);
            if (_pausedOverlay != null) _pausedOverlay.SetActive(false);
            if (_countdownOverlay != null) _countdownOverlay.SetActive(false);
            if (_gameOverOverlay != null) _gameOverOverlay.SetActive(false);
            if (_bossOverlay != null) _bossOverlay.SetActive(false);
            if (_comboRow != null) _comboRow.SetActive(false);

            // 担当A juice の中断残留対策 (レビュー G2): SetActive(false) で FxKit コルーチンが
            // 後始末前に打ち切られると scale/色/位置が中間値のまま残留し、PunchScale/Flash は
            // 「開始時の現在値」を基準に取るため自己修復しない。floorClearOverlay と同様に
            // 全ての演出対象を基準値へ強制復帰する。
            if (_board != null) _board.ResetFxState();
            if (_countdownLabel != null) _countdownLabel.transform.localScale = Vector3.one;
            if (_countdownBg != null) _countdownBg.color = _countdownBgBaseColor;
            if (_comboLabel != null) _comboLabel.transform.localScale = Vector3.one;
            if (_floorClearNextLabel != null) _floorClearNextLabel.transform.localScale = Vector3.one;
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
                _relicDraftOverlay.SetActive(false);
                _routeChoiceOverlay.SetActive(false);
                _relicCountLabel.gameObject.SetActive(false);
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

            // Phase 5a/5c: FloorClear / RouteChoice / RelicDraft の 3 オーバーレイの表示権はここで一元管理する。
            // IsRouteChoicePending / IsRelicDraftPending は階層クリア確定と同時に true になり得るが、UI 上は
            // 「スタート」を押す (_routeChoiceScreenOpen / _relicDraftScreenOpen=true) まで各画面を出さない —
            // よって pending でも screenOpen でない間は「まだ FloorClear を見せている」と判定して FloorClear を出す。
            if (game.IsRouteChoicePending && _routeChoiceScreenOpen)
            {
                _floorClearOverlay.SetActive(false);
                _relicDraftOverlay.SetActive(false);
                ShowRouteChoice();
            }
            else if (game.IsRelicDraftPending && _relicDraftScreenOpen)
            {
                _floorClearOverlay.SetActive(false);
                _routeChoiceOverlay.SetActive(false);
                ShowRelicDraft();
            }
            else if (game.IsFloorClearPending && !_relicDraftScreenOpen && !_routeChoiceScreenOpen)
            {
                ShowFloorClear();
                _relicDraftOverlay.SetActive(false);
                _routeChoiceOverlay.SetActive(false);
            }
            else
            {
                _floorClearOverlay.SetActive(false);
                _relicDraftOverlay.SetActive(false);
                _routeChoiceOverlay.SetActive(false);
            }

            // 所持レリック簡易カウンタ (§6.3。一覧表示へのタップ導線は Phase 5b 送り)
            int relicCount = game.OwnedRelicIds.Count;
            _relicCountLabel.gameObject.SetActive(relicCount > 0);
            if (relicCount > 0) _relicCountLabel.text = "遺物 " + relicCount;

            RenderBoard(session);

            // 担当A juice: 次回 HandleCollisionAbsorbFx (OnTurnResolved) が「このターン解決直前」の
            // 値と比較できるよう、今回描画した最新値をここで控えておく。
            _prevIsInvisible = game.IsInvisible;
            _prevShieldActive = session.ShieldActive;
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
                // Swift: 倍率発動中はコンボ文字が金色に昇格。担当A juice: さらに combo>=5 で警告色に昇格。
                _comboLabel.color = combo >= GameConfig.ComboMultiplierThreshold2 ? UITheme.Warning
                    : multiplier > 1.0 ? UITheme.GoldText
                    : UITheme.TextColor;

                if (multiplier > 1.0)
                {
                    _multiplierLabel.text = "×" + multiplier.ToString("0.0");
                    _multiplierLabel.gameObject.SetActive(true);
                }
            }

            // 担当A juice: コンボが 3/5 の節目に到達した瞬間だけ強調パンチ (毎フレーム再トリガーしない)。
            if (combo != _lastComboCount
                && (combo == GameConfig.ComboMultiplierThreshold1 || combo == GameConfig.ComboMultiplierThreshold2))
            {
                FxKit.PunchScale(this, (RectTransform)_comboLabel.transform, 0.25f, 0.3f);
            }
            _lastComboCount = combo;

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
