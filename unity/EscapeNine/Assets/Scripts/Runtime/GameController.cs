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
        private int _turnBeats = GameConfig.TurnCountdownBeats;     // 1 ターンの拍数 (デバッグで可変)
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

            Session.StartGame(startFloor);

            // ラン計測の初期化 (Swift: gameStartTime / floorStartTime / elapsedSeconds / nearMissDistance)
            _runStartRealtime = Time.realtimeSinceStartup;
            _floorStartRealtime = _runStartRealtime;
            ElapsedSeconds = 0;
            NearMissDistance = 0;
            LastTimingGrade = null;
            IsFloorClearPending = false;
            IsInvisible = false; // 前ランの透明化表示フラグを持ち越さない
            TurnCountdown = _turnBeats;
            _resultPersisted = false; // 新しいランのスコア送信/自己ベスト更新をまた許可する
            LastUnlockedAchievements = Array.Empty<Achievement>(); // 前ランのポップアップ対象を持ち越さない

            // --- 音 → 状態 → 通知 の順 (Swift: startGame と同順) ---
            _audio.PlaySfx("game_start");                    // Swift: playSoundEffect(.gameStart)
            _audio.PlayBgmForFloor(Session.CurrentFloor);    // Swift: playBGMMusic(.forFloor(currentFloor))

            double bpm = bpmOverride > 0 ? bpmOverride : Floor.CalculateBPM(Session.CurrentFloor);

            if (Session.IsBossFloor) OnBossWarning?.Invoke(); // Swift: showBossWarning (2秒消去は UI 側)

            BeginStartCountdown(bpm, skipCountdown);

            // TODO(Phase 3): AnalyticsLogger.logGameStarted 相当 (floor / characterId)。
            OnStateChanged?.Invoke();
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
            if (Session.Skill.Type != SkillType.Bind) return;
            if (Session.RemainingSkillUses <= 0) return;

            _audio.PlaySfx("skill");
            Session.BindEnemy();
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// 階層クリア画面の「スタート」ボタンから呼ぶ (Swift: GameView の floorClearOverlay →
        /// viewModel.nextFloor()。自動遷移ではなくユーザー操作起点なのが正本仕様)。
        /// </summary>
        public void AdvanceToNextFloor()
        {
            if (!IsFloorClearPending) return;
            IsFloorClearPending = false;

            var advance = Session.NextFloor();
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

            double bpm = Floor.CalculateBPM(Session.CurrentFloor);
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
            TurnCountdown = _turnBeats; // Swift: resetTurnCountdown()

            if (skipCountdown)
            {
                // Swift: startGameStartCountdown の #if DEBUG 早期 return (completion() を同期的に呼ぶ)
                IsGameStartCountdownActive = false;
                _phase = Phase.Playing;
                _conductor.ChangeBPM(bpm);
                OnGameStarted?.Invoke();
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
            TurnCountdown = _turnBeats;
            _conductor.ChangeBPM(_startCountdownBpm);
            OnGameStarted?.Invoke();
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
                    TurnCountdown = _turnBeats; // Swift: onBeat 側の turnCountdown リセット相当
                    OnTurnResolved?.Invoke(result);
                    break;

                case TurnResult.FloorCleared:
                    _audio.PlaySfx("move");
                    _phase = Phase.FloorClear; // 以後の拍は無視 (Swift: pauseBGM でメトロノーム停止)
                    IsFloorClearPending = true;
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
            if (won && Session.DailyChallengeMode)
            {
                _dailyChallenge?.MarkCompleted(Session.CurrentFloor);
                Session.DailyChallengeMode = false; // Swift: dailyChallengeMode = false
            }

            // 4) スコア送信 + 自己ベスト更新 (敗北は HandleDefeat で即時実行済み。
            //    PersistRunResult 内の _resultPersisted ガードにより二重送信はしない)
            PersistRunResult();

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
