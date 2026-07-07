// GameController.cs
// Swift 正本からの忠実移植: ViewModels/GameViewModel.swift のゲームフロー層。
// ターン解決・移動判定・スキル・特殊ルールのルール本体は全て EscapeNine.Core.GameSession に
// 実装済みのため、本クラスは「時間 (Conductor の拍) と音 (AudioDirector) と永続化
// (PlayerState / RankingStore) を GameSession に結線する」ことだけを担う (二重実装禁止)。
//
// Swift との対応:
//   startGame(aiLevel:)      → StartNewRun
//   selectMove(to:)          → RequestMove
//   activateSkill()/bindEnemy() → ActivateSkill / TapEnemy
//   onTurnDeadline()         → HandleBeat → ResolveTurnNow (実体は GameSession.ResolveTurn)
//   nextFloor()              → AdvanceToNextFloor (階層クリア画面の「スタート」ボタンから呼ぶ)
//   endGame(result:)         → EndGame
//   pauseGame()/resumeGame()/resetGame() → PauseGame / ResumeGame / QuitToHome
//
// 状態遷移の順序は Swift の「音 → 状態 → 通知」をできる限り保持する (差分は各所コメント)。

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EscapeNine.Core;

namespace EscapeNine.Runtime
{
    public sealed class GameController : MonoBehaviour
    {
        // MARK: - 内部フェーズ
        // Swift は gameStatus (@Published) + isGameStartCountdownActive + showFloorClear +
        // showGameOverOverlay の組み合わせで表現していた状態を、拍の受理判定用に 1 本化する。
        private enum Phase
        {
            Idle,           // 未開始 / リセット後
            StartCountdown, // 3→2→1→GO (ゲーム開始 & 各階層開始)
            Playing,        // ターン進行中 (拍でカウントダウン)
            FloorClear,     // 階層クリア表示中 (AdvanceToNextFloor 待ち)
            GameOverDelay,  // 敗北オーバーレイ表示中 (1.5 秒後に EndGame)
            Paused,         // 一時停止
            Finished        // Win / Lose 確定後 (リザルト表示中)
        }

        // MARK: - 固定契約
        public GameSession Session { get; private set; }

        public event System.Action<int> OnCountdownTick;   // 3,2,1 / 0=GO
        public event System.Action OnGameStarted;
        public event System.Action<TurnResult> OnTurnResolved;
        public event System.Action<int> OnFloorAdvanced;   // newFloor
        public event System.Action<DefeatReason> OnGameOver;
        public event System.Action OnStateChanged;         // HUD 再描画用の汎用通知

        // MARK: - 追加公開 API (契約に対して additive。UI 層が Swift の @Published 相当を読むため)

        /// <summary>ボスフロア警告 (Swift: showBossWarning)。表示の自動消去 (2 秒) は UI 側で行う。</summary>
        public event System.Action OnBossWarning;

        /// <summary>ターンカウントダウンの現在値 (3→2→1→0)。Swift: turnCountdown</summary>
        public int TurnCountdown { get; private set; } = GameConfig.TurnCountdownBeats;

        /// <summary>ゲーム開始カウントダウン中か。Swift: isGameStartCountdownActive</summary>
        public bool IsGameStartCountdownActive { get; private set; }

        /// <summary>階層クリア表示中 (AdvanceToNextFloor 待ち)。Swift: showFloorClear</summary>
        public bool IsFloorClearPending { get; private set; }

        // ---- Phase 5a: レリックドラフト (docs/unity-phase5-roguelike-design.md §2.1/§6.3) ----
        // Swift正本には存在しない (Unity固有の追加機能)。IsFloorClearPending と同じ「ゲート」の
        // 設計思想: true の間は AdvanceToNextFloor を無視し、UI 側が ChooseRelic を呼ぶまで待つ。

        /// <summary>レリックドラフト提示中か。デイリーチャレンジ中 (Session.DailyChallengeMode) は
        /// 公平性のため常に false (§8: デイリーチャレンジ中はドラフト無効化)。</summary>
        public bool IsRelicDraftPending { get; private set; }

        /// <summary>現在提示中のドラフト候補 (既定3件)。プール枯渇時はそれ未満、ドラフト非提示時は空。</summary>
        public IReadOnlyList<RelicDefinition> CurrentDraftCandidates { get; private set; } = Array.Empty<RelicDefinition>();

        /// <summary>このランで所持中のレリックID一覧 (スタック分は同一IDが複数回含まれる)。
        /// ラン限り (§9 未決事項1: 永続要素はスターターパークのみで、5aはスコープ外)。
        /// HUD の簡易カウンタ表示用に公開する。</summary>
        public IReadOnlyList<string> OwnedRelicIds => _ownedRelicIds;

        /// <summary>階層クリア確定と同時にドラフト候補が生成された (Swift正本には対応なし)。</summary>
        public event System.Action OnRelicDraftOffered;

        /// <summary>ChooseRelic でレリックが確定した (Swift正本には対応なし)。</summary>
        public event System.Action OnRelicChosen;

        // ---- Phase 5c: 分岐ルート (docs/unity-phase5-roguelike-design.md §4/§6.3) ----
        // Swift正本には存在しない (Unity固有の追加機能)。順序: FloorCleared → RouteChoice →
        // RelicDraft(5a既存) → Advance。RouteChoice が pending の間は RelicDraft/Advance をブロックする
        // (IsRelicDraftPending ゲートの手前に挿入)。

        /// <summary>分岐ルート選択の開始階層 (§4「頻度」: 初心者配慮で Floor 6 以降から提示)。</summary>
        private const int RouteChoiceStartFloor = 6;

        /// <summary>深淵ルート選択時の残光ボーナス (§4「深淵の報酬側」。少額の [要検証] 仮値)。</summary>
        private const int AbyssGlowBonus = 15;

        /// <summary>分岐ルート選択提示中か。デイリーチャレンジ中は公平性のため常に false (§4)。
        /// IsRelicDraftPending と同じ「ゲート」設計思想: true の間は AdvanceToNextFloor を無視する。</summary>
        public bool IsRouteChoicePending { get; private set; }

        /// <summary>次の AdvanceToNextFloor で NextFloor(choice) に渡すルート選択 (この階層限定)。
        /// AdvanceToNextFloor が消費した後は Safe に戻す (Core 側も「1階層限定」で自動クリアする)。</summary>
        private RouteChoice _pendingRouteChoice = RouteChoice.Safe;

        /// <summary>階層クリア確定と同時に分岐ルート選択が提示された (Swift正本には対応なし)。</summary>
        public event System.Action OnRouteChoiceOffered;

        /// <summary>ChooseRoute でルートが確定した (Swift正本には対応なし)。</summary>
        public event System.Action OnRouteChosen;

        // ---- Phase 5c: ボスパターン・テレグラフ (§1.5/§5) ----

        /// <summary>ボス階でパターンが変わったターンに発火する (§5.2: UI が次パターンをテレグラフ表示)。
        /// ボス階進入時 (最初のパターン) と、ボス階内で 2 ターンごとに切り替わった時に発火する。</summary>
        public event System.Action<BossPattern> OnBossPatternChanged;

        // ボスパターン変化検知用のスナップショット (floor が変われば強制再通知)。
        private BossPattern? _lastBroadcastBossPattern;
        private int _lastBroadcastBossFloor = -1;

        /// <summary>直近の移動タイミング判定。Swift: lastTimingGrade (表示の 0.8 秒消去は UI 側)</summary>
        public TimingGrade? LastTimingGrade { get; private set; }

        /// <summary>1 ラン通算の経過秒 (EndGame で確定)。Swift: elapsedSeconds</summary>
        public double ElapsedSeconds { get; private set; }

        /// <summary>敗北時の敵との Chebyshev 距離 (1 = あと 1 マス)。Swift: nearMissDistance</summary>
        public int NearMissDistance { get; private set; }

        /// <summary>
        /// 直近のラン (EndGame) で新規解除された実績一覧。ラン開始 (StartNewRun) のたびにリセットされる。
        /// Swift: AchievementPopupView に渡す「初めて解除された」実績集合 (AchievementManager 相当の
        /// 判定は PersistUnlockedAchievements 内で行う)。ResultScreen が OnShow で読み、ポップアップ演出を出す。
        /// </summary>
        public IReadOnlyList<Achievement> LastUnlockedAchievements { get; private set; } = Array.Empty<Achievement>();

        // MARK: - 依存 (App が Configure で注入)
        private Conductor _conductor;
        private AudioDirector _audio;
        private PlayerState _player;
        private RankingStore _ranking;
        private DailyChallengeStore _dailyChallenge;

        // MARK: - 内部状態
        private Phase _phase = Phase.Idle;
        private Phase _phaseBeforePause;                             // Pause 前のフェーズ (Playing/StartCountdown) を Resume で復元
        private int _startCountdownRemaining;                        // ゲーム開始カウントダウン残り (Pause 中も保持)
        private double _startCountdownBpm;                           // GO! 到達時に Conductor.ChangeBPM へ渡す BPM (Swift: startBGM(bpm:))
        private Coroutine _startCountdownCoroutine;                  // 実時間 1.0s 間隔の開始カウントダウン (Swift: gameStartCountdownTimer)
        private bool _resultPersisted;                              // このランのスコア送信/自己ベスト更新が済んだか (二重送信防止)
        private bool _glowAwarded;                                  // このランの残光付与が済んだか (通貨二重付与防止、EndGame が万一二重呼び出しされた場合の保険)
        private int _turnBeats = GameConfig.TurnCountdownBeats;     // 1 ターンの拍数 (デバッグで可変)

        /// <summary>Phase 5b: レリック #16 刻の猶予 (TurnCountdownBonus) を加算した実効ターン拍数 (§2.4)。
        /// Relics.None (=0) のときは _turnBeats と同値 = 既存挙動と完全一致。</summary>
        private int EffectiveTurnBeats => _turnBeats + (Session?.Relics.TurnCountdownBonus ?? 0);

        /// <summary>Phase 5b: レリック #15 加速の証 (BpmMultiplierBonus) を乗算した実効BPM (§2.4)。
        /// Core は値の保持のみで、乗算は Runtime (ここ) が行う。Relics.None (=0.0) なら恒等。</summary>
        private double ApplyRelicBpm(double bpm) =>
            bpm * (1.0 + (Session != null ? Session.Relics.BpmMultiplierBonus : 0.0));

        // ---- Phase 5a: レリックドラフト内部状態 ----
        // GameSession と同じ「IRandomSource 既定値 = new SystemRandomSource() (時刻シード)」の
        // 作法に合わせ、GameSession 同様に StartNewRun のたびに新規インスタンスを持たせる
        // (GameController は GameSession 生成時も rng を明示注入していないため、ここでも揃える)。
        private RelicDraftService _relicDraftService;
        private readonly List<string> _ownedRelicIds = new List<string>(); // ラン限りの所持レリックID (§9未決事項1)

        // ドラフト (階層クリア報酬) で取得したレリック数。MaxRelicsPerRun 上限判定はこれを使う。
        // _ownedRelicIds.Count とは意図的に別管理: スターターパーク (§3.2「ドラフト消費なしで付与」) は
        // _ownedRelicIds には入るが本カウントには入らないため、上限枠を圧迫しない。
        // これを混同すると、スターターパーク装備 + 毎階層ドラフトで序盤に上限到達し、
        // 深淵ルートの Rare+ 確定報酬まで無言で不発になる (2026-07-04 /review-full C3/G1)。
        private int _draftAcquiredCount;

        private float _runStartRealtime;                            // Swift: gameStartTime
        private float _floorStartRealtime;                          // Swift: floorStartTime (Phase 3 の計装用)
        private const float GameOverOverlaySeconds = 1.5f;          // Swift: asyncAfter(.now() + 1.5)
        private const float GameStartGoDisplaySeconds = 0.5f;       // Swift: asyncAfter(.now() + 0.5) — GO! 表示 & 入力ブロック猶予
        // Swift: AchievementManager.achievementsKey。internal: AchievementScreen (Phase 2.5, 実績一覧画面) が
        // 同一書式 (enum 名 CSV) で読むために同アセンブリ内へ公開する (定数複製禁止のため widen のみ)。
        internal const string UnlockedAchievementsKey = "unlockedAchievements";

        /// <summary>
        /// App の Awake から呼ぶ依存注入。Conductor.OnBeat の購読もここで 1 回だけ行う。
        /// </summary>
        public void Configure(Conductor conductor, AudioDirector audio, PlayerState player, RankingStore ranking,
            DailyChallengeStore dailyChallenge)
        {
            if (_conductor != null) _conductor.OnBeat -= HandleBeat; // 再 Configure 安全
            _conductor = conductor;
            _audio = audio;
            _player = player;
            _ranking = ranking;
            _dailyChallenge = dailyChallenge;
            _conductor.OnBeat += HandleBeat;
        }

        private void OnDestroy()
        {
            if (_conductor != null) _conductor.OnBeat -= HandleBeat;
        }

        // MARK: - Game Control (固定契約)

        /// <summary>
        /// 新しいランを開始。リトライも本メソッドの再呼び出しで成立する
        /// (GameSession を作り直し、Conductor / BGM も張り直すため)。
        /// Swift: startGame(aiLevel:)
        /// </summary>
        public void StartNewRun(CharacterType c, AILevel lvl, int startFloor = 1)
        {
            StopAllCoroutines(); // 前ランのオーバーレイ遅延などを破棄

            _turnBeats = GameConfig.TurnCountdownBeats;
            bool skipCountdown = false;
            double bpmOverride = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Swift: #if DEBUG の debug* 設定を反映。
            // 差分: Swift は debugAILevel が常に引数を上書きするが、Unity では
            //       呼び出し側 (デバッグ UI) が明示的に渡す設計とし、暗黙上書きしない。
            if (_player != null)
            {
                if (startFloor == 1 && _player.DebugStartFloor > 1) startFloor = _player.DebugStartFloor;
                if (_player.DebugBPMOverride > 0) bpmOverride = _player.DebugBPMOverride;
                _turnBeats = Mathf.Clamp(_player.DebugTurnCountdownBeats, 1, 10); // Swift: setTurnCountdownBeats のクランプ
                skipCountdown = _player.DebugSkipStartCountdown;
            }
#endif

            // ルール本体は GameSession に委譲 (配置・特殊ルール・消失マスまで全部)
            Session = new GameSession(Character.GetCharacter(c), lvl);

            // デイリーチャレンジ: pending challenge があれば適用 (Swift: startGame 内の
            // `if let pending = DailyChallengeService.shared.pendingChallenge` 分岐)。
            // StartGame() を呼ぶ「前」に DailyChallengeMode/Conditions を設定しておくことで、
            // StartGame() 内部の特殊ルール計算 (GameSession.cs) が「startFloor 上書き後」の
            // 階層を使うようになる (Core 側の適用順序も Swift に合わせて修正済み)。
            // 差分: Swift の「pendingChallenge が無ければ dailyChallengeMode を見て再適用」分岐は、
            //       Unity では Session を毎回新規生成する (dailyChallengeMode が前ランから持ち越されない)
            //       ため到達不能であり、意図的に移植していない。
            if (_dailyChallenge != null && _dailyChallenge.PendingChallenge != null)
            {
                var pending = _dailyChallenge.PendingChallenge;
                _dailyChallenge.PendingChallenge = null;
                Session.DailyChallengeMode = true;
                Session.DailyChallengeConditions = pending.Conditions;
            }

            // Phase 5a: レリック状態は完全にラン限り。前ランの所持レリック/ドラフトサービスを
            // 持ち越さない (§9未決事項1: 永続要素はスターターパークのみ、5aはスコープ外)。
            // Phase 5c: この reset を Session.StartGame() より前へ移動した (旧: StartGame の後)。
            // 理由: 直後の ApplyStarterPerk() が _ownedRelicIds に加える内容を、この Clear() が
            // 後から上書きしてしまう順序ミスを避けるため。GameController 自身のローカル状態リセットで
            // あり Session.StartGame の実行結果には依存しないため、移動しても既存挙動
            // (レリックなしランでの動作) に影響はない。
            _relicDraftService = new RelicDraftService();
            _ownedRelicIds.Clear();
            _draftAcquiredCount = 0;
            CurrentDraftCandidates = Array.Empty<RelicDefinition>();
            IsRelicDraftPending = false;

            // Phase 5c: スターターパーク (§3.2) の自動適用。Session.StartGame() より前に呼ぶ必要がある —
            // #10 護りの起点 (MinStartDistance) 等、Relics の値を GameSession.StartGame() 内の
            // 初期配置ロジックが直接参照するため (GameSession.cs 参照)、StartGame の後に適用すると
            // 1階層目だけ効果が反映されない事故になる。ChooseRelic (階層クリア後のみ呼ばれる) では
            // この問題が起きないが、「ラン開始時装備」であるスターターパークはここを踏む必要がある。
            ApplyStarterPerk();

            Session.StartGame(startFloor);

            // ラン計測の初期化 (Swift: gameStartTime / floorStartTime / elapsedSeconds / nearMissDistance)
            _runStartRealtime = Time.realtimeSinceStartup;
            _floorStartRealtime = _runStartRealtime;
            ElapsedSeconds = 0;
            NearMissDistance = 0;
            LastTimingGrade = null;
            IsFloorClearPending = false;
            IsInvisible = false; // 前ランの透明化表示フラグを持ち越さない

            // Phase 5c: 分岐ルート/ボスパターンの状態も完全にラン限りで持ち越さない。
            IsRouteChoicePending = false;
            _pendingRouteChoice = RouteChoice.Safe;
            _lastBroadcastBossPattern = null;
            _lastBroadcastBossFloor = -1;

            TurnCountdown = _turnBeats;
            _resultPersisted = false; // 新しいランのスコア送信/自己ベスト更新をまた許可する
            _glowAwarded = false;     // 新しいランの残光付与をまた許可する
            LastUnlockedAchievements = Array.Empty<Achievement>(); // 前ランのポップアップ対象を持ち越さない

            // --- 音 → 状態 → 通知 の順 (Swift: startGame と同順) ---
            _audio.PlaySfx("game_start");                    // Swift: playSoundEffect(.gameStart)
            _audio.PlayBgmForFloor(Session.CurrentFloor);    // Swift: playBGMMusic(.forFloor(currentFloor))

            // Phase 5b: #15 加速の証 (BpmMultiplierBonus)。通常はラン開始時点で Relics.None のため恒等だが、
            // Phase 5c: 上の ApplyStarterPerk() でスターターパークが装備されていれば既にこの時点で
            // Relics へ適用済みのため、そのボーナス (#15 が装備された場合) も自然に反映される。
            // デバッグ上書き (bpmOverride) はレリック乗算の対象外 (デバッグ値が最優先)。
            double bpm = bpmOverride > 0 ? bpmOverride : ApplyRelicBpm(Floor.CalculateBPM(Session.CurrentFloor));

            if (Session.IsBossFloor) OnBossWarning?.Invoke(); // Swift: showBossWarning (2秒消去は UI 側)

            BeginStartCountdown(bpm, skipCountdown);

            // TODO(Phase 3): AnalyticsLogger.logGameStarted 相当 (floor / characterId)。
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Phase 5c: スターターパーク (docs/unity-phase5-roguelike-design.md §3.2) をラン開始時に
        /// 自動適用する。MetaShopScreen で装備された、残光で解放済みの Common/Uncommon レリックを
        /// 1つだけ「ドラフト消費なしで」Session.Relics へ適用する (§3.2「ドラフト消費なしで
        /// 付与される点が通常レリックと異なる」)。ChooseRelic メソッドと同じ
        /// 「ApplyTo(Session.Relics) + _ownedRelicIds.Add」の適用経路を踏襲する。
        /// 呼び出し側 (StartNewRun) の制約: _ownedRelicIds リセット後・Session.StartGame() 呼び出し
        /// 「前」に呼ぶこと (理由は StartNewRun 内のコメント参照)。
        /// デイリーチャレンジ中は公平性のため適用しない (レリックドラフト/分岐ルートと同じ扱い、§4)。
        /// </summary>
        private void ApplyStarterPerk()
        {
            if (_player == null) return;
            if (Session.DailyChallengeMode) return;

            string relicId = _player.StarterPerkRelicId;
            if (string.IsNullOrEmpty(relicId)) return;
            if (!_player.IsRelicUnlocked(relicId)) return; // 解放取り消し等への防御 (二重チェック)

            RelicDefinition? def = RelicCatalog.Find(relicId);
            if (def == null) return;
            // §3.2: Rare以上は対象外 (MetaShopScreen 側でも同条件で選択肢を絞っているが、
            // PlayerPrefs の直接改変等に備えてここでも二重に防御する)。
            if (def.Value.Rarity > RelicRarity.Uncommon) return;

            def.Value.ApplyTo(Session.Relics);
            _ownedRelicIds.Add(relicId);
        }

        /// <summary>
        /// 移動先を予約する (実行は次の締切拍で同時移動)。Swift: selectMove(to:)
        /// タイミング判定は Conductor の拍位相から取り、コンボ更新は GameSession が行う。
        /// </summary>
        public void RequestMove(int position)
        {
            if (_phase != Phase.Playing) return;
            if (IsGameStartCountdownActive) return;               // Swift: selectMove の guard (GameViewModel.swift:577)。GO! 後 0.5 秒は入力を無視
            if (Session == null || Session.Status != GameStatus.Playing) return;

            TimingGrade grade = _conductor.TimingGradeNow();
            if (Session.SelectMove(position, grade))
            {
                LastTimingGrade = grade; // 表示の 0.8 秒後リセットは UI 側の責務
                OnStateChanged?.Invoke();
            }
        }

        /// <summary>スキル発動。Swift: activateSkill() (音 → Session の順を保持)</summary>
        public void ActivateSkill()
        {
            if (Session == null || Session.Status != GameStatus.Playing) return;
            if (Session.RemainingSkillUses <= 0) return;

            _audio.PlaySfx("skill"); // Swift はガード通過後・効果適用前に効果音
            Session.ActivateSkill();
            OnStateChanged?.Invoke();
        }

        /// <summary>敵タップ = 拘束スキル (エルフ)。Swift: bindEnemy()</summary>
        public void TapEnemy()
        {
            if (Session == null || Session.Status != GameStatus.Playing) return;

            // 拘束できる経路は2つ: ①エルフの拘束スキル (RemainingSkillUses 消費) か
            // ②レリック #17 心話の絆 (Relics.PseudoBindCharges 消費、非エルフでも発動)。
            // GameSession.BindEnemy() は両方を正しく捌く (Bind スキル優先→無ければ PseudoBind) ため、
            // ここでは「どちらか一方でも撃てる」ときだけ通す (旧実装は非エルフを無条件で弾き、
            // 心話の絆が Runtime から一切発動できなかった)。
            bool canNativeBind = Session.Skill.Type == SkillType.Bind && Session.RemainingSkillUses > 0;
            bool canRelicBind = Session.Relics.PseudoBindCharges > 0;
            if (!canNativeBind && !canRelicBind) return;

            _audio.PlaySfx("skill");
            Session.BindEnemy();
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Phase 5a: レリックドラフト候補から1つを確定する。Swift正本には対応なし。
        /// UI (RelicDraftScreen 相当のオーバーレイ) の1/2/3キー・カードタップの両方から呼ばれる。
        /// 候補外の ID (二重タップ等での不正な呼び出し) は無視する。
        /// </summary>
        public void ChooseRelic(string relicId)
        {
            if (!IsRelicDraftPending) return;

            RelicDefinition? chosen = null;
            foreach (var def in CurrentDraftCandidates)
            {
                if (def.Id == relicId) { chosen = def; break; }
            }
            if (chosen == null) return;

            chosen.Value.ApplyTo(Session.Relics);
            _ownedRelicIds.Add(relicId);
            _draftAcquiredCount++; // ドラフト取得のみ上限にカウント (スターターパークは含めない)

            IsRelicDraftPending = false;
            CurrentDraftCandidates = Array.Empty<RelicDefinition>();
            OnRelicChosen?.Invoke();
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Phase 5c: 分岐ルート選択を確定する (§4)。Swift正本には対応なし。
        /// 選択は「次の AdvanceToNextFloor が NextFloor(choice) へ渡す」ことで、その1階層限定で
        /// 実効AIレベル+特殊ルールの前倒しに反映される (Core: RouteFloorOverride)。
        /// 深淵ルートは残光ボーナス付与 + 直後のドラフトに Rare 以上を1枠確定で含める (§4「深淵の報酬側」)。
        /// UI (RouteChoiceScreen 相当のオーバーレイ) の 1/2 キー・カードタップの両方から呼ばれる。
        /// </summary>
        public void ChooseRoute(RouteChoice choice)
        {
            if (!IsRouteChoicePending) return;

            _pendingRouteChoice = choice;
            IsRouteChoicePending = false;

            if (choice == RouteChoice.Abyss)
            {
                // 残光ボーナス (§4)。_player は Configure で注入済みだが防御的に null 条件で呼ぶ。
                _player?.AddMetaCurrency(AbyssGlowBonus);
            }

            // ルート確定後に初めてドラフト候補を生成する (Abyss の Rare+ 確定枠を反映するため、
            // FloorCleared 時点ではなくここまで遅延させている)。
            OfferRelicDraft(guaranteeRarePlus: choice == RouteChoice.Abyss);

            OnRouteChosen?.Invoke();
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Phase 5a/5c: 階層クリア確定後のレリックドラフト候補を生成し、条件を満たせば提示状態にする。
        /// RouteChoice を提示しない階層 (Floor&lt;6/デイリー) では FloorCleared から直接、提示する階層では
        /// ChooseRoute から呼ばれる (§6.3 の順序)。デイリー中の無効化・DraftInterval/上限判定・
        /// #18 蒐集家の目の候補数+1 は既存 (Phase 5a/5b) と同一ロジック。
        /// </summary>
        /// <param name="guaranteeRarePlus">深淵ルート報酬 (§4): 候補に Rare 以上が1枠も無ければ差し替える。</param>
        private void OfferRelicDraft(bool guaranteeRarePlus)
        {
            if (Session.DailyChallengeMode) return; // 二重防御 (呼び出し側でも判定済み)
            // 上限判定はドラフト取得数のみ (スターターパークは枠を消費しない §3.2)。ここは従来通り
            // 「直前にクリアした階層」(Session.CurrentFloor) を渡す — RelicConfig.ShouldOfferDraft の
            // clearedFloor 契約と一致させる (RELIC_COHERENCE_AUDIT.md §4 は floor 引数の基準を混同しない
            // よう注意喚起している)。
            if (!RelicConfig.ShouldOfferDraft(Session.CurrentFloor, _draftAcquiredCount)) return;

            int draftCount = Session.Relics.DraftCandidateBonusFloorsRemaining > 0 ? 4 : 3;
            // RELIC_COHERENCE_AUDIT.md §2-J/§4: 重み付け・RequiresFog/RequiresDisappear のハード除外は
            // 「クリア済み階層」ではなく「次に入る階層」を基準にする必要がある。深淵ルート
            // (_pendingRouteChoice == Abyss) は次階層の特殊ルールを1段階前倒しするため、その前倒しを
            // 反映した実効値を ResolveDraftFloor で解決する。
            int draftFloor = ResolveDraftFloor(Session.CurrentFloor + 1, _pendingRouteChoice);
            var candidates = _relicDraftService.DraftCandidates(
                _ownedRelicIds,
                Session.CurrentCharacter.Type,
                count: draftCount,
                selectedAI: Session.SelectedAILevel,
                floor: draftFloor);
            if (Session.Relics.DraftCandidateBonusFloorsRemaining > 0)
            {
                Session.Relics.DraftCandidateBonusFloorsRemaining--;
            }

            if (guaranteeRarePlus && candidates.Count > 0)
            {
                EnsureRarePlusSlot(candidates);
            }

            if (candidates.Count > 0)
            {
                CurrentDraftCandidates = candidates;
                IsRelicDraftPending = true;
                OnRelicDraftOffered?.Invoke();
            }
            // candidates.Count == 0 (プール完全枯渇) はドラフト非提示のまま次階層へ進む。
        }

        /// <summary>
        /// レリックドラフトの重み付け・フィルタ (RelicDraftService.ComputeWeight の RequiresFog/
        /// RequiresDisappear ハード除外) に渡す floor 値を解決する (RELIC_COHERENCE_AUDIT.md §2-J)。
        /// nextFloor (次に入る階層) の自然な特殊ルールに、深淵ルートの1段階前倒し
        /// (RouteFloorOverride.ApplyToSpecialRule) を適用したうえで、Core側シグネチャ
        /// (DraftCandidates の floor:int) を変えずに渡せるよう、前倒し後のルールに対応する
        /// 代表階層 (各ルールの開始階層) へ変換する。前倒しが効かない場合は nextFloor をそのまま返す。
        /// </summary>
        private static int ResolveDraftFloor(int nextFloor, RouteChoice pendingRoute)
        {
            SpecialRule natural = Floor.GetSpecialRule(nextFloor);
            SpecialRule effective = RouteFloorOverride.For(pendingRoute).ApplyToSpecialRule(natural);
            if (effective == natural) return nextFloor;

            switch (effective)
            {
                case SpecialRule.Fog: return GameConfig.FogStartFloor;
                case SpecialRule.Disappear: return GameConfig.DisappearStartFloor;
                case SpecialRule.FogDisappear: return GameConfig.CombinedRulesStartFloor;
                default: return nextFloor;
            }
        }

        /// <summary>
        /// 深淵ルート報酬 (§4): 候補に Rare 以上が1枠も無ければ、RelicCatalog から未所持・未提示の
        /// Rare 以上を引いて最低レアリティのスロットへ差し替える。RelicDraftService に Rare+ 確定オプションが
        /// 無いため Runtime 側で実装する (Core は非改変)。差し替え候補が無ければ何もしない (正直な縮退)。
        /// §2.2 原則5 (魔法使いは HardAICounter を絶対に見せない) はここでも尊重する。
        /// </summary>
        private void EnsureRarePlusSlot(List<RelicDefinition> candidates)
        {
            foreach (var c in candidates)
            {
                if (c.Rarity >= RelicRarity.Rare) return; // 既に Rare+ を含む
            }

            var ownedCounts = new Dictionary<string, int>();
            foreach (var id in _ownedRelicIds)
            {
                ownedCounts[id] = ownedCounts.TryGetValue(id, out var c) ? c + 1 : 1;
            }

            var pool = new List<RelicDefinition>();
            foreach (var def in RelicCatalog.All)
            {
                if (def.Rarity < RelicRarity.Rare) continue;
                int owned = ownedCounts.TryGetValue(def.Id, out var oc) ? oc : 0;
                if (owned >= def.StackLimit) continue;

                bool alreadyOffered = false;
                foreach (var c in candidates)
                {
                    if (c.Id == def.Id) { alreadyOffered = true; break; }
                }
                if (alreadyOffered) continue;

                // §2.2 原則5: 魔法使いは HardAICounter レリックをドラフト対象外にする明示ルール。
                if (Session.CurrentCharacter.Type == CharacterType.Wizard
                    && (def.Tags & RelicTag.HardAICounter) != 0) continue;

                pool.Add(def);
            }
            if (pool.Count == 0) return; // 差し替えられる Rare+ が尽きている場合は諦める

            // ドラフトと同じ IRandomSource 経由で選ぶ (デイリーのシード再現性を保つ。旧: UnityEngine.Random 直呼び)。
            var replacement = _relicDraftService.PickOne(pool);
            int replaceIdx = 0;
            for (int i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].Rarity < candidates[replaceIdx].Rarity) replaceIdx = i;
            }
            candidates[replaceIdx] = replacement;
        }

        /// <summary>
        /// 階層クリア画面の「スタート」ボタンから呼ぶ (Swift: GameView の floorClearOverlay →
        /// viewModel.nextFloor()。自動遷移ではなくユーザー操作起点なのが正本仕様)。
        /// </summary>
        public void AdvanceToNextFloor()
        {
            if (!IsFloorClearPending) return;
            // Phase 5c: ルート選択が済むまで次階層へ進ませない (§6.3 の順序:
            // FloorCleared → RouteChoice → RelicDraft → AdvanceToNextFloor 解禁)。
            // RelicDraft ゲートの手前に挿入する。
            if (IsRouteChoicePending) return;
            // Phase 5a: レリック選択が済むまで次階層へ進ませない。
            if (IsRelicDraftPending) return;
            IsFloorClearPending = false;

            // Phase 5c: 選んだルートを「その1階層限定」で Core へ渡す。既定 Safe なら従来挙動。
            var advance = Session.NextFloor(_pendingRouteChoice);
            _pendingRouteChoice = RouteChoice.Safe; // 消費したら次回に持ち越さない (Core も同様に自動クリア)
            if (advance == FloorAdvanceResult.GameWon)
            {
                // Swift 同様、currentFloor=101 のまま endGame(.win) (スコアも 101 で送信される仕様を踏襲)
                EndGame(won: true);
                return;
            }

            _floorStartRealtime = Time.realtimeSinceStartup; // Swift: floorStartTime = Date()
            OnFloorAdvanced?.Invoke(Session.CurrentFloor);
            // スキルリセット通知 (Swift: showSkillReset) は UI 側で
            //   Session.CurrentFloor % GameConfig.SkillResetInterval == 1 を見て表示する。

            // BGM 帯域が変わった場合のみ切替 (AudioDirector 側の同一 BGM ガードが Swift の条件を再現)
            _audio.PlayBgmForFloor(Session.CurrentFloor);

            // Phase 5b: #15 加速の証 (BpmMultiplierBonus) を乗算してから Conductor へ渡す (§2.4)。
            // デバッグ上書き (DebugBPMOverride) はレリック乗算の対象外 (デバッグ値が最優先)。
            double bpm = ApplyRelicBpm(Floor.CalculateBPM(Session.CurrentFloor));
            bool skipCountdown = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_player != null)
            {
                if (_player.DebugBPMOverride > 0) bpm = _player.DebugBPMOverride;
                _turnBeats = Mathf.Clamp(_player.DebugTurnCountdownBeats, 1, 10); // Swift: nextFloor() 内の setTurnCountdownBeats 相当 (階層跨ぎでも再適用、GameViewModel.swift:782-784)
                skipCountdown = _player.DebugSkipStartCountdown;
            }
#endif

            if (Session.IsBossFloor) OnBossWarning?.Invoke();

            BeginStartCountdown(bpm, skipCountdown);
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// 一時停止。Swift: pauseGame() (GameViewModel.swift:967-971) はガード無しで常時呼べるが、
        /// Unity 版は状態機械の都合上 Playing / StartCountdown 中のみ許可する。
        /// FloorClear / GameOverDelay 中のポーズは複雑さに対して価値が低いため未対応 (現状維持)。
        /// </summary>
        public void PauseGame()
        {
            if (_phase != Phase.Playing && _phase != Phase.StartCountdown) return;

            if (_phase == Phase.StartCountdown && _startCountdownCoroutine != null)
            {
                StopCoroutine(_startCountdownCoroutine);
                _startCountdownCoroutine = null;
            }

            _phaseBeforePause = _phase;
            Session.Status = GameStatus.Paused; // Swift: gameStatus = .paused
            _phase = Phase.Paused;
            _conductor.StopSong(); // Conductor に pause API が無いため停止 (StartCountdown 中は元々未起動)
            _audio.PauseBgm();     // Swift: pauseBGMMusic()
            OnStateChanged?.Invoke();
        }

        public void ResumeGame()
        {
            if (_phase != Phase.Paused) return;
            Session.Status = GameStatus.Playing;
            _audio.ResumeBgm();

            if (_phaseBeforePause == Phase.StartCountdown)
            {
                // 開始カウントダウンの途中で一時停止していた場合は残りカウントから再開する
                // (Conductor は StartCountdown 中は未起動のため StartSong 不要)。
                _phase = Phase.StartCountdown;
                _startCountdownCoroutine = StartCoroutine(RunStartCountdown());
            }
            else
            {
                _phase = Phase.Playing;
                // 差分: Swift は BeatEngine を途中位相から再開する (BeatEngine.swift:144-153。
                //       pause()/resume() は turnCountdown に触れない) が、Conductor は再スタートのみ
                //       サポートするため拍サイクル (位相) はリセットされる。ただし TurnCountdown
                //       (残りターン締切) 自体は保持する — ポーズ連打で締切を無限に引き延ばせる穴を
                //       防ぐため、満タンには戻さない。
                _conductor.StartSong();
            }
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// ホームへ戻る。Swift: resetGame() 相当 + メニュー BGM 再生。
        /// 画面遷移 (Router.Show(Home)) は UI 側の責務 (本クラスは UI 非依存)。
        /// </summary>
        public void QuitToHome()
        {
            StopAllCoroutines();
            _startCountdownCoroutine = null;
            _phase = Phase.Idle;
            IsGameStartCountdownActive = false;
            IsFloorClearPending = false;
            // Phase 5c: 分岐ルート/ボスパターンの pending・スナップショットもホーム帰還時にリセット。
            IsRouteChoicePending = false;
            _pendingRouteChoice = RouteChoice.Safe;
            _lastBroadcastBossPattern = null;
            _lastBroadcastBossFloor = -1;
            LastTimingGrade = null;
            TurnCountdown = _turnBeats;

            if (Session != null) Session.Status = GameStatus.Idle; // Swift: gameStatus = .idle

            _conductor.StopSong(); // Swift: stopBGM (メトロノーム)
            _audio.StopBgm();      // Swift: stopBGMMusic
            _audio.PlayMenuBgm();  // ホーム画面 BGM (HomeScreen 側の再呼び出しは同一 BGM ガードで無害)
            OnStateChanged?.Invoke();
        }

        // MARK: - Start Countdown (3→2→1→GO)

        /// <summary>
        /// ゲーム開始 / 階層開始カウントダウンを開始する。
        /// Swift (GameViewModel.swift:363-400): 独立 Timer で実時間 1.0 秒固定間隔。BPM に連動しない。
        /// Conductor (拍クロック) は GO! に到達するまで起動しない (Swift が BGM を completion 後に
        /// 鳴らし始めるのと同じ順序、RunStartCountdown 参照)。
        /// </summary>
        private void BeginStartCountdown(double bpm, bool skipCountdown)
        {
            if (_startCountdownCoroutine != null)
            {
                StopCoroutine(_startCountdownCoroutine);
                _startCountdownCoroutine = null;
            }

            _startCountdownBpm = bpm;
            TurnCountdown = EffectiveTurnBeats; // Swift: resetTurnCountdown() + Phase 5b #16 刻の猶予

            if (skipCountdown)
            {
                // Swift: startGameStartCountdown の #if DEBUG 早期 return (completion() を同期的に呼ぶ)
                IsGameStartCountdownActive = false;
                _phase = Phase.Playing;
                _conductor.ChangeBPM(bpm);
                OnGameStarted?.Invoke();
                RaiseBossPatternIfChanged(); // Phase 5c: ボス階なら最初のパターンをテレグラフ通知
                return;
            }

            _startCountdownRemaining = GameConfig.GameStartCountdownBeats;
            IsGameStartCountdownActive = true;
            _phase = Phase.StartCountdown;
            _startCountdownCoroutine = StartCoroutine(RunStartCountdown());
        }

        /// <summary>
        /// 実時間 1.0 秒固定間隔のカウントダウン本体。Swift: Timer.scheduledTimer(withTimeInterval: 1.0, ...)。
        /// Pause 中に停止された場合、Resume で残りカウント (_startCountdownRemaining) からそのまま
        /// 再起動される (BeginStartCountdown を経由しない、PauseGame/ResumeGame 参照)。
        /// </summary>
        private IEnumerator RunStartCountdown()
        {
            while (_startCountdownRemaining > 0)
            {
                OnCountdownTick?.Invoke(_startCountdownRemaining); // 3, 2, 1 の表示 (Swift: gameStartCountdown)
                yield return new WaitForSeconds(1f);
                _startCountdownRemaining--;
            }

            // GO! → プレイ開始。拍クロックはここで初めて起動する (Swift: completion() 内の startBGM(bpm:))。
            OnCountdownTick?.Invoke(0);
            _phase = Phase.Playing;
            TurnCountdown = EffectiveTurnBeats; // Phase 5b #16 刻の猶予
            _conductor.ChangeBPM(_startCountdownBpm);
            OnGameStarted?.Invoke();
            RaiseBossPatternIfChanged(); // Phase 5c: ボス階なら最初のパターンをテレグラフ通知
            _startCountdownCoroutine = null;

            // Swift: GO! 表示を 0.5 秒後に消す。その間 isGameStartCountdownActive は true のままで
            // selectMove (RequestMove) をブロックする (GameViewModel.swift:392-396, 577)。
            StartCoroutine(ClearGameStartCountdownAfterDelay());
            OnStateChanged?.Invoke();
        }

        private IEnumerator ClearGameStartCountdownAfterDelay()
        {
            yield return new WaitForSeconds(GameStartGoDisplaySeconds);
            IsGameStartCountdownActive = false;
            OnStateChanged?.Invoke();
        }

        /// <summary>透明化の視覚フラグを invisibilityDuration 後に解除 (Swift: asyncAfter 相当)。</summary>
        private IEnumerator ClearInvisibleAfterDelay()
        {
            yield return new WaitForSeconds((float)GameConfig.InvisibilityDuration);
            IsInvisible = false;
            OnStateChanged?.Invoke();
        }

        // MARK: - Beat Handling (Swift: BeatEngine.onBeat → onTurnDeadline)

        private void HandleBeat(int beat)
        {
            switch (_phase)
            {
                case Phase.Playing:
                    // GO! 直後の入力ブロック中 (IsGameStartCountdownActive = GameStartGoDisplaySeconds 0.5秒) は
                    // プレイヤーがまだ動けない (RequestMove がブロックされる) ため、ターンカウントダウンを進めない。
                    // これを入れないと GO! 直後の 1〜2 拍がブロック中に消費され、最初のターンの「実際に動ける窓」が
                    // 削られる (2026-07-08 iOS実機ログで確定: floor1/BPM70 でも可動窓が約1.3秒しかなく時間切れ)。
                    // 入力解除後は通常どおり毎拍 1 減算 = 最初のターンも他ターンと同じ full 3 拍 (floor1 で約2.5秒) になる。
                    if (IsGameStartCountdownActive) break;

                    // Swift: onBeat() — turnCountdown を減らし、0 でアクセント音 + 締切処理
                    TurnCountdown--;
                    bool isDeadline = TurnCountdown <= 0;
                    _audio.PlayCountdownTick(isDeadline);
                    if (isDeadline)
                    {
                        ResolveTurnNow();
                    }
                    OnStateChanged?.Invoke(); // カウントダウン表示 / 盤面の再描画
                    break;

                default:
                    // Idle / StartCountdown / Paused / FloorClear / GameOverDelay / Finished 中の拍は無視。
                    // StartCountdown は Conductor 未起動 (BeginStartCountdown 参照) のためそもそも発火しないが、
                    // 専用コルーチン (RunStartCountdown) が実時間 1.0 秒間隔で処理するため二重に受けない。
                    // (Swift では pauseBGM でメトロノーム自体を止めていたのと等価)
                    break;
            }
        }

        /// <summary>
        /// 透明化スキルが「衝突を吸収した直後」だけ true になる視覚フラグ。
        /// Swift: GameViewModel.isInvisible (衝突吸収時に ON → invisibilityDuration 後に OFF)。
        /// Core は吸収を TurnResult.Continued に畳むため、SkillUsageCount の増分で検知する。
        /// GameScreen のスキルボタン ON バッジが参照する。
        /// </summary>
        public bool IsInvisible { get; private set; }

        /// <summary>締切拍のターン解決。ルールは GameSession.ResolveTurn が全て担う。</summary>
        private void ResolveTurnNow()
        {
            int previousPlayerPosition = Session.PlayerPosition;
            int skillUsesBefore = Session.SkillUsageCount;
            var result = Session.ResolveTurn();

            // 透明化の衝突吸収検知: 透明化キャラで Continued かつスキル消費が増えた = 吸収発生
            // (Swift: GameViewModel.swift:261-267 の isInvisible=true → asyncAfter(invisibilityDuration) で false)
            if (result == TurnResult.Continued
                && Session.Skill.Type == SkillType.Invisible
                && Session.SkillUsageCount > skillUsesBefore)
            {
                IsInvisible = true;
                StartCoroutine(ClearInvisibleAfterDelay());
            }

            switch (result)
            {
                case TurnResult.Continued:
                    _audio.PlaySfx("move");
                    TurnCountdown = EffectiveTurnBeats; // Swift: onBeat 側の turnCountdown リセット相当 + Phase 5b #16 刻の猶予
                    OnTurnResolved?.Invoke(result);
                    // Phase 5c: ボス階内で 2 ターンごとにパターンが切り替わる (Core が _bossPatternTurnCounter を
                    // ResolveTurn 内で加算済み)。切り替わったターンに次パターンをテレグラフ通知する (§5.2)。
                    RaiseBossPatternIfChanged();
                    break;

                case TurnResult.FloorCleared:
                    _audio.PlaySfx("move");
                    _phase = Phase.FloorClear; // 以後の拍は無視 (Swift: pauseBGM でメトロノーム停止)
                    IsFloorClearPending = true;

                    // Phase 5c: 順序 FloorCleared → RouteChoice → RelicDraft → Advance (§6.3)。
                    // 分岐ルート提示条件を満たす階層 (Floor 6 以降・非デイリー) では、まず RouteChoice を
                    // pending にし、ドラフト候補生成は ChooseRoute まで遅延させる (深淵ルートの Rare+ 確定枠を
                    // 反映するため)。条件を満たさない階層 (Floor<6/デイリー) は従来どおり即ドラフト提示。
                    //
                    // デイリーチャレンジ中は分岐ルート/ドラフトとも無効化する (§4/§8: 同一日替わりシードに
                    // 挑む全プレイヤー間の公平性を優先)。
                    if (ShouldOfferRouteChoice())
                    {
                        _pendingRouteChoice = RouteChoice.Safe; // 選択されるまでの既定
                        IsRouteChoicePending = true;
                        OnRouteChoiceOffered?.Invoke();
                    }
                    else
                    {
                        _pendingRouteChoice = RouteChoice.Safe;
                        // Floor<6/デイリーではルート選択がないため確定 Rare+ 枠なし (guaranteeRarePlus=false)。
                        OfferRelicDraft(guaranteeRarePlus: false);
                    }

                    _audio.PlaySfx("floor_clear"); // Swift: playSoundEffect(.floorClear)
                    OnTurnResolved?.Invoke(result);
                    // TODO(Phase 3): AnalyticsLogger.logFloorCleared(floor:, clearSeconds:) 相当。
                    //                per-floor 秒は Time.realtimeSinceStartup - _floorStartRealtime。
                    break;

                case TurnResult.Defeated:
                    // Swift (GameViewModel.swift:252) は位置更新直後・衝突判定前に move 音を鳴らすため、
                    // 移動自体は成立した上での衝突死でも move 音が鳴る。無効手/消失マス/時間切れによる
                    // 敗北は移動が成立していない (PlayerPosition 未更新) ため move 音は鳴らさない。
                    // GameSession.ResolveTurn は理由を DefeatReason だけでは区別できないため、
                    // PlayerPosition の変化有無で「移動成立後の衝突死」かどうかを判定する
                    // (GetAvailableMoves は現在地を候補から除外するため、移動成立時は必ず位置が変わる)。
                    if (Session.PlayerPosition != previousPlayerPosition)
                    {
                        _audio.PlaySfx("move");
                    }
                    OnTurnResolved?.Invoke(result);
                    HandleDefeat(Session.LastDefeatReason ?? DefeatReason.CaughtByEnemy);
                    break;
            }
        }

        /// <summary>Phase 5c: 分岐ルート選択を提示すべき階層か (§4)。
        /// デイリーチャレンジ中は公平性のため常に false、Floor 6 以降でのみ提示する。</summary>
        private bool ShouldOfferRouteChoice()
        {
            return !Session.DailyChallengeMode
                && Session.CurrentFloor >= RouteChoiceStartFloor;
        }

        /// <summary>
        /// Phase 5c: ボス階でパターンが変わったら OnBossPatternChanged を発火する (§5.2)。
        /// ボス階進入時の最初のパターン (Pursuit) も 1 回通知する (floor が変われば強制再通知)。
        /// 非ボス階ではスナップショットをリセットするだけで発火しない。
        /// </summary>
        private void RaiseBossPatternIfChanged()
        {
            if (Session == null || !Session.IsBossFloor)
            {
                _lastBroadcastBossPattern = null;
                _lastBroadcastBossFloor = -1;
                return;
            }

            BossPattern current = Session.CurrentBossPattern;
            if (_lastBroadcastBossFloor == Session.CurrentFloor
                && _lastBroadcastBossPattern == current) return;

            _lastBroadcastBossPattern = current;
            _lastBroadcastBossFloor = Session.CurrentFloor;
            OnBossPatternChanged?.Invoke(current);
        }

        // MARK: - Game Over / End Game

        /// <summary>
        /// 敗北演出: OnGameOver でオーバーレイ表示 → 1.5 秒後に EndGame。
        /// Swift: showGameOverOverlay = true → asyncAfter(1.5) { endGame(.lose) }
        /// (Swift の pauseBGM はメトロノームのみ停止で楽曲は流れ続ける。Unity 版も
        ///  フェーズゲートで拍処理だけ止め、BGM は EndGame まで流れ続ける = 同挙動)
        /// </summary>
        private void HandleDefeat(DefeatReason reason)
        {
            // PLAUSIBLE fix: スコア送信/自己ベスト更新は 1.5 秒のオーバーレイ演出を待たずに即時実行する。
            // 演出中に画面遷移やアプリ終了があっても記録が消えないようにするため
            // (演出・BGM 切替等の「見た目」だけを FinalizeDefeatAfterDelay 側に残す)。
            PersistRunResult();

            _phase = Phase.GameOverDelay;
            OnGameOver?.Invoke(reason);
            StartCoroutine(FinalizeDefeatAfterDelay());
        }

        private IEnumerator FinalizeDefeatAfterDelay()
        {
            yield return new WaitForSeconds(GameOverOverlaySeconds);
            EndGame(won: false);
        }

        /// <summary>
        /// スコア送信 (RankingStore) + 自己ベスト更新 (PlayerState) の永続化のみを行う。
        /// Swift: endGame(result:) の該当箇所 (RankingService.submitScore + updateHighestFloor)。
        /// 敗北は HandleDefeat から即時に呼ばれ、勝利は EndGame から直接呼ばれる。
        /// 1 ラン 1 回のみ実行 (二重送信防止、_resultPersisted は StartNewRun でリセット)。
        /// </summary>
        private void PersistRunResult()
        {
            if (_resultPersisted) return;
            _resultPersisted = true;

            int floor = Session.CurrentFloor;
            string characterId = Session.CurrentCharacter.Type.RawValue();
            _ranking.SubmitScore(floor, characterId);
            _player.UpdateHighestFloor(floor);
            // TODO(Phase 3): GameCenterService.submitScore / FirebaseService.submitScore 相当。
        }

        /// <summary>ラン終了処理。Swift: endGame(result:) と同順 (計測確定 → 音 → 永続化 → 実績)。</summary>
        private void EndGame(bool won)
        {
            // 1) 計測の確定 (Swift: elapsedSeconds / nearMissDistance)
            ElapsedSeconds = Time.realtimeSinceStartup - _runStartRealtime;
            NearMissDistance = Session.CurrentNearMissDistance;

            // TODO(Phase 3): AnalyticsLogger.logGameOverShown 相当 (.lose のみ / defeatReason 付き)。

            // 2) 状態確定 (Session.Status は GameSession / NextFloor が設定済み)
            _phase = Phase.Finished;

            // 3) 音 (Swift: stopBGM → stopBGMMusic → リザルト BGM)
            _conductor.StopSong();
            _audio.StopBgm();
            if (won)
            {
                _audio.PlayResultBgm(won: true);
            }
            else
            {
                _audio.PlaySfx("gameover");        // Swift: playSoundEffect(.gameOver)
                _audio.PlayResultBgm(won: false);
            }

            // デイリーチャレンジ完了記録 (勝利時のみ)。Swift: endGame(result:) の
            // `if result == .win && dailyChallengeMode { DailyChallengeService.shared.markCompleted(...) }`
            // 残光 (メタ進行通貨) の算出にも使うため、フラグリセット前に判定結果を控えておく。
            bool dailyChallengeCompletedThisRun = won && Session.DailyChallengeMode;
            if (dailyChallengeCompletedThisRun)
            {
                _dailyChallenge?.MarkCompleted(Session.CurrentFloor);
                Session.DailyChallengeMode = false; // Swift: dailyChallengeMode = false
            }

            // 4) スコア送信 + 自己ベスト更新 (敗北は HandleDefeat で即時実行済み。
            //    PersistRunResult 内の _resultPersisted ガードにより二重送信はしない)
            PersistRunResult();

            // Phase 5b: 残光 (メタ進行通貨) 付与 (docs/unity-phase5-roguelike-design.md §3.1、[要検証・仮の式])。
            // Swift正本には対応なし (Unity固有拡張)。消費導線 (MetaShopScreen、§3.2) は Phase 5c で追加済み
            // (PlayerState.TryUnlockRelic / SetStarterPerk)。
            // Session.CurrentFloor は勝利時 101 のまま (PersistRunResult と同じ Swift パリティ踏襲の値)。
            // スコア送信 (PersistRunResult) と同じく 1 ラン 1 回のみ。通貨のため二重付与を明示ガード。
            if (!_glowAwarded)
            {
                _glowAwarded = true;
                int glowEarned = MetaProgressionCalculator.CalculateGlow(Session.CurrentFloor, won, dailyChallengeCompletedThisRun);
                _player.AddMetaCurrency(glowEarned);
            }

            // 5) 実績チェック（勝利時のみ）。Swift: GameViewModel.swift:954-964 (AchievementManager.checkAchievements)。
            //    新規解除は LastUnlockedAchievements に控え、ResultScreen がポップアップ演出を出す (Phase 2.5)。
            //    実績解除の効果音は Phase 3/4 送り (Swift 側にも専用 SE は無い)。
            if (won)
            {
                bool skillUsed = Session.SkillUsageCount > 0;
                double currentBPM = Floor.CalculateBPM(Session.CurrentFloor);
                var unlocked = AchievementChecker.CheckAchievements(Session.CurrentFloor, skillUsed, currentBPM, gameWon: true);
                PersistUnlockedAchievements(unlocked);
            }

            // 6) 通知 — 勝利時は OnGameOver を発火しない (Swift 同様、UI は Status == Win を見て
            //    リザルトへ遷移する)。敗北時の OnGameOver は HandleDefeat で発火済み。
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// 実績解除の記録を PlayerPrefs へ永続化する (Swift: AchievementManager.unlock/saveAchievements)。
        /// 解除済み実績との和集合をとって保存するため、二度目以降の呼び出しでも既存の解除は失われない。
        /// 併せて「今回のランで初めて解除された」実績を LastUnlockedAchievements に控える
        /// (Swift の AchievementManager が初回のみポップアップ表示するのと同じ判定を、ここで
        /// 「保存前の解除済み集合に含まれていなかったか」で代替する)。
        /// </summary>
        private void PersistUnlockedAchievements(HashSet<Achievement> achievements)
        {
            string existingRaw = PlayerPrefs.GetString(UnlockedAchievementsKey, "");
            var existing = new HashSet<string>(existingRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

            var newlyUnlocked = new List<Achievement>();
            foreach (var a in achievements)
            {
                if (!existing.Contains(a.ToString())) newlyUnlocked.Add(a);
            }
            LastUnlockedAchievements = newlyUnlocked;

            var merged = new HashSet<string>(existing);
            foreach (var a in achievements) merged.Add(a.ToString());

            PlayerPrefs.SetString(UnlockedAchievementsKey, string.Join(",", merged));
            PlayerPrefs.Save();
        }
    }
}
