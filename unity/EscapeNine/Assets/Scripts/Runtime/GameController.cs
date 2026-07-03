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

        // MARK: - 依存 (App が Configure で注入)
        private Conductor _conductor;
        private AudioDirector _audio;
        private PlayerState _player;
        private RankingStore _ranking;

        // MARK: - 内部状態
        private Phase _phase = Phase.Idle;
        private int _startCountdownRemaining;                       // ゲーム開始カウントダウン残り
        private int _turnBeats = GameConfig.TurnCountdownBeats;     // 1 ターンの拍数 (デバッグで可変)
        private float _runStartRealtime;                            // Swift: gameStartTime
        private float _floorStartRealtime;                          // Swift: floorStartTime (Phase 3 の計装用)
        private const float GameOverOverlaySeconds = 1.5f;          // Swift: asyncAfter(.now() + 1.5)

        /// <summary>
        /// App の Awake から呼ぶ依存注入。Conductor.OnBeat の購読もここで 1 回だけ行う。
        /// </summary>
        public void Configure(Conductor conductor, AudioDirector audio, PlayerState player, RankingStore ranking)
        {
            if (_conductor != null) _conductor.OnBeat -= HandleBeat; // 再 Configure 安全
            _conductor = conductor;
            _audio = audio;
            _player = player;
            _ranking = ranking;
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
            Session.StartGame(startFloor);

            // ラン計測の初期化 (Swift: gameStartTime / floorStartTime / elapsedSeconds / nearMissDistance)
            _runStartRealtime = Time.realtimeSinceStartup;
            _floorStartRealtime = _runStartRealtime;
            ElapsedSeconds = 0;
            NearMissDistance = 0;
            LastTimingGrade = null;
            IsFloorClearPending = false;
            TurnCountdown = _turnBeats;

            // TODO(Phase 3): DailyChallengeService.pendingChallenge の適用 (Swift: startGame 内)。
            //                GameSession.DailyChallengeMode / ApplyDailyChallengeConditions は Core に移植済み。

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
            if (_phase != Phase.Playing) return;                 // Swift: guard !isGameStartCountdownActive 等
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
                skipCountdown = _player.DebugSkipStartCountdown;
            }
#endif

            if (Session.IsBossFloor) OnBossWarning?.Invoke();

            BeginStartCountdown(bpm, skipCountdown);
            OnStateChanged?.Invoke();
        }

        public void PauseGame()
        {
            if (_phase != Phase.Playing) return;
            Session.Status = GameStatus.Paused; // Swift: gameStatus = .paused
            _phase = Phase.Paused;
            _conductor.StopSong(); // Conductor に pause API が無いため停止 (差分は Resume 側コメント)
            _audio.PauseBgm();     // Swift: pauseBGMMusic()
            OnStateChanged?.Invoke();
        }

        public void ResumeGame()
        {
            if (_phase != Phase.Paused) return;
            Session.Status = GameStatus.Playing;
            _phase = Phase.Playing;
            _audio.ResumeBgm();
            // 差分: Swift は BeatEngine を途中位相から再開するが、Conductor は再スタートのみ
            //       サポートするため拍サイクルを仕切り直し、ターンカウントダウンも満タンに戻す
            //       (プレイヤー有利側に倒す)。
            _conductor.StartSong();
            TurnCountdown = _turnBeats;
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// ホームへ戻る。Swift: resetGame() 相当 + メニュー BGM 再生。
        /// 画面遷移 (Router.Show(Home)) は UI 側の責務 (本クラスは UI 非依存)。
        /// </summary>
        public void QuitToHome()
        {
            StopAllCoroutines();
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
        /// 差分: Swift は独立 Timer (1 秒固定間隔) だが、Unity 版は Conductor.OnBeat 駆動
        ///       (指示仕様)。カウントダウンから音楽と拍が完全に同期する代わりに、
        ///       1 tick の長さが 60/BPM 秒になる (高階層ほど短い)。
        /// </summary>
        private void BeginStartCountdown(double bpm, bool skipCountdown)
        {
            _startCountdownRemaining = skipCountdown ? 0 : GameConfig.GameStartCountdownBeats;
            IsGameStartCountdownActive = !skipCountdown;
            _phase = Phase.StartCountdown;
            TurnCountdown = _turnBeats; // Swift: resetTurnCountdown()

            // ChangeBPM は StartSong を内包 → dspTime クロックが新 BPM で走り出す
            _conductor.ChangeBPM(bpm);
        }

        // MARK: - Beat Handling (Swift: BeatEngine.onBeat → onTurnDeadline)

        private void HandleBeat(int beat)
        {
            switch (_phase)
            {
                case Phase.StartCountdown:
                    if (_startCountdownRemaining > 0)
                    {
                        // 3, 2, 1 の表示 (Swift: gameStartCountdown)
                        OnCountdownTick?.Invoke(_startCountdownRemaining);
                        _startCountdownRemaining--;
                    }
                    else
                    {
                        // GO! → プレイ開始。「GO 表示を 0.5 秒後に消す」のは UI 側の責務。
                        OnCountdownTick?.Invoke(0);
                        IsGameStartCountdownActive = false;
                        _phase = Phase.Playing;
                        TurnCountdown = _turnBeats;
                        OnGameStarted?.Invoke();
                        OnStateChanged?.Invoke();
                    }
                    break;

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
                    // Idle / Paused / FloorClear / GameOverDelay / Finished 中の拍は無視
                    // (Swift では pauseBGM でメトロノーム自体を止めていたのと等価)
                    break;
            }
        }

        /// <summary>締切拍のターン解決。ルールは GameSession.ResolveTurn が全て担う。</summary>
        private void ResolveTurnNow()
        {
            var result = Session.ResolveTurn();

            switch (result)
            {
                case TurnResult.Continued:
                    // 差分: Swift は移動確定直後 (衝突判定前) に move 音を鳴らすため、
                    //       衝突死の場合も move 音が鳴る。Session が解決を一括で行う都合上、
                    //       Unity 版は「生存したターンのみ」move 音を鳴らす。
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
            _phase = Phase.GameOverDelay;
            OnGameOver?.Invoke(reason);
            StartCoroutine(FinalizeDefeatAfterDelay());
        }

        private IEnumerator FinalizeDefeatAfterDelay()
        {
            yield return new WaitForSeconds(GameOverOverlaySeconds);
            EndGame(won: false);
        }

        /// <summary>ラン終了処理。Swift: endGame(result:) と同順 (計測確定 → 音 → 永続化)。</summary>
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

            // TODO(Phase 3): デイリーチャレンジ完了記録 (Swift: DailyChallengeService.markCompleted)。

            // 4) スコア送信 + 自己ベスト更新 (Swift: RankingService.submitScore + updateHighestFloor)
            int floor = Session.CurrentFloor;
            string characterId = Session.CurrentCharacter.Type.RawValue();
            _ranking.SubmitScore(floor, characterId);
            _player.UpdateHighestFloor(floor);
            // TODO(Phase 3): GameCenterService.submitScore / FirebaseService.submitScore 相当。

            // TODO(Phase 3): 実績チェック (Core の AchievementChecker.CheckAchievements は移植済み。
            //                解除状態の永続化ストアと通知 UI を実装したら勝利時にここで呼ぶ)。

            // 5) 通知 — 勝利時は OnGameOver を発火しない (Swift 同様、UI は Status == Win を見て
            //    リザルトへ遷移する)。敗北時の OnGameOver は HandleDefeat で発火済み。
            OnStateChanged?.Invoke();
        }
    }
}
