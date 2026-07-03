// GameSession.cs
// Swift 正本からの忠実移植: ViewModels/GameViewModel.swift のゲーム進行ロジックを
// UI(SwiftUI/@Published)・音声(AudioManager)・タイマー非依存で再現したヘッドレス版。
//
// 目的:
//   - Unity のどの UI 層からも呼べる純粋なゲームルール本体
//   - EditMode テスト & ヘッドレスなバランスシミュレーションに使える
// 乱数(配置・消失マス・Easy/Boss AI)は IRandomSource で注入し決定論化。
//
// 命名メモ (型名衝突回避): Swift の currentCharacter/specialRule/defeatReason は
//   CurrentCharacter / CurrentSpecialRule / LastDefeatReason に対応。

using System;
using System.Collections.Generic;
using System.Linq;

namespace EscapeNine.Core
{
    /// <summary>ResolveTurn の結果。</summary>
    public enum TurnResult
    {
        Continued,    // ターン継続
        FloorCleared, // 規定ターン到達 → 階層クリア (呼び出し側で NextFloor)
        Defeated      // 敗北 (LastDefeatReason 参照)
    }

    /// <summary>NextFloor の結果。</summary>
    public enum FloorAdvanceResult
    {
        Advanced, // 次階層へ
        GameWon   // 100階層踏破
    }

    public sealed class GameSession
    {
        private readonly AIEngine _ai;
        private readonly IRandomSource _rng;

        // --- character / skill ---
        public Character CurrentCharacter { get; private set; }
        public Skill Skill => CurrentCharacter.Skill;
        public AILevel SelectedAILevel { get; set; }

        // --- state (Swift @Published 相当。ヘッドレスのため公開) ---
        public int CurrentFloor { get; set; } = 1;
        public int TurnCount { get; set; }
        public int PlayerPosition { get; set; } = 1;
        public int EnemyPosition { get; set; } = 9;
        public GameStatus Status { get; set; } = GameStatus.Idle;
        public int SkillUsageCount { get; set; }
        public int? PendingPlayerMove { get; set; }
        public HashSet<int> DisappearedCells { get; private set; } = new HashSet<int>();
        public bool IsSkillActive { get; set; }
        public int EnemyStoppedTurns { get; set; }
        public bool ShieldActive { get; set; }
        public int ComboCount { get; set; }
        public SpecialRule CurrentSpecialRule { get; set; } = SpecialRule.None;
        public DefeatReason? LastDefeatReason { get; private set; }
        public bool DailyChallengeMode { get; set; }
        public List<ChallengeCondition> DailyChallengeConditions { get; set; } = new List<ChallengeCondition>();

        public GameSession(Character character, AILevel selectedAILevel = AILevel.Easy, AIEngine ai = null, IRandomSource rng = null)
        {
            CurrentCharacter = character;
            SelectedAILevel = selectedAILevel;
            _rng = rng ?? new SystemRandomSource();
            _ai = ai ?? new AIEngine(_rng);
        }

        public void SetCharacter(Character character) => CurrentCharacter = character;

        // --- derived ---
        public int RemainingSkillUses => Skill.MaxUsage - SkillUsageCount;

        /// <summary>現在階層の必要ターン数。Floor 0 はプロローグ短縮。Swift: maxTurns</summary>
        public int MaxTurns =>
            CurrentFloor == TutorialConstants.PrologueFloor
                ? TutorialConstants.PrologueClearTurns
                : GameConfig.GetMaxTurns(CurrentFloor);

        public bool IsBossFloor => Floor.IsBossFloor(CurrentFloor);

        /// <summary>コンボによるスコア倍率。Swift: scoreMultiplier</summary>
        public double ScoreMultiplier =>
            ComboCount >= GameConfig.ComboMultiplierThreshold2 ? 2.0 :
            ComboCount >= GameConfig.ComboMultiplierThreshold1 ? 1.5 : 1.0;

        /// <summary>現在のプレイヤー・敵間の Chebyshev 距離 (惜しさメーター)。</summary>
        public int CurrentNearMissDistance => ChebyshevDistance(PlayerPosition, EnemyPosition);

        // MARK: - Start / Floor progression

        /// <summary>ゲーム開始。Swift: startGame(aiLevel:) のロジック部分 (音声/カウントダウン/計装は除く)。</summary>
        public void StartGame(int startFloor = 1, int? playerPos = null, int? enemyPos = null)
        {
            CurrentFloor = Math.Max(1, Math.Min(startFloor, GameConfig.MaxFloors));
            TurnCount = 0;
            Status = GameStatus.Playing;
            SkillUsageCount = 0;
            PendingPlayerMove = null;
            IsSkillActive = false;
            EnemyStoppedTurns = 0;
            ShieldActive = false;
            LastDefeatReason = null;
            ComboCount = 0;

            // 配置 (Swift: 1..9 の distinct。消失計算は配置後なので player/enemy は消えない)
            int p = playerPos ?? RandomPosition();
            int e = enemyPos ?? RandomPositionExcluding(p);
            PlayerPosition = p;
            EnemyPosition = e;
            PendingPlayerMove = null;

            // Swift 順序踏襲 (GameViewModel.startGame): デイリーチャレンジの条件適用 (startFloor 上書き
            // 含む) を先に行い、特殊ルール/消失マスは「上書き後」の CurrentFloor で計算する。
            // 逆順だと StartFloor 条件付きチャレンジで霧/消失マスの発動階層がずれる (Phase 2.5 で発見・修正)。
            if (DailyChallengeMode) ApplyDailyChallengeConditions();

            CurrentSpecialRule = Floor.GetSpecialRule(CurrentFloor);
            UpdateDisappearedCells();
        }

        /// <summary>次階層へ。Swift: nextFloor() のロジック部分。100階層踏破で GameWon。</summary>
        public FloorAdvanceResult NextFloor()
        {
            CurrentFloor += 1;
            TurnCount = 0;

            // 10階層ごとにスキル使用回数をリセット (Swift と同条件: floor % interval == 1)
            if (CurrentFloor % GameConfig.SkillResetInterval == 1)
            {
                SkillUsageCount = 0;
            }

            PendingPlayerMove = null;
            IsSkillActive = false;
            EnemyStoppedTurns = 0;
            ShieldActive = false;
            LastDefeatReason = null;
            // 注意: comboCount は Swift の nextFloor でもリセットしない (階層跨ぎで継続)

            if (CurrentFloor > GameConfig.MaxFloors)
            {
                Status = GameStatus.Win;
                return FloorAdvanceResult.GameWon;
            }

            CurrentSpecialRule = Floor.GetSpecialRule(CurrentFloor);
            UpdateDisappearedCells();

            // 配置 (消失マスを避ける)
            var available = Enumerable.Range(1, GameConfig.GridSize).Where(p => !DisappearedCells.Contains(p)).ToList();
            if (available.Count < 2)
            {
                PlayerPosition = 1;
                EnemyPosition = 9;
                return FloorAdvanceResult.Advanced;
            }

            int playerPos = available[_rng.NextInt(available.Count)];
            var enemyCandidates = available.Where(p => p != playerPos).ToList();
            int enemyPos = enemyCandidates.Count > 0
                ? enemyCandidates[_rng.NextInt(enemyCandidates.Count)]
                : (playerPos == 1 ? 9 : 1);

            PlayerPosition = playerPos;
            EnemyPosition = enemyPos;
            PendingPlayerMove = null;

            return FloorAdvanceResult.Advanced;
        }

        // MARK: - Turn resolution (Swift: onTurnDeadline)

        /// <summary>
        /// 1ターンを解決する。Swift の onTurnDeadline() を UI/音声/遅延なしで再現。
        /// 事前に PendingPlayerMove を SelectMove か直接代入で設定しておく。
        /// </summary>
        public TurnResult ResolveTurn()
        {
            if (Status != GameStatus.Playing) return TurnResult.Continued;

            // 移動先未選択 → 時間切れ
            if (PendingPlayerMove == null)
            {
                return Defeat(DefeatReason.TimeOut);
            }
            int next = PendingPlayerMove.Value;

            var (isValid, shouldConsume) = ValidateMove(PlayerPosition, next);
            if (!isValid)
            {
                return Defeat(DefeatReason.CaughtByEnemy);
            }

            if (shouldConsume && RemainingSkillUses > 0) SkillUsageCount++;
            IsSkillActive = false;

            if (DisappearedCells.Contains(next))
            {
                return Defeat(DefeatReason.CaughtByEnemy);
            }

            // 同時移動: 敵の次位置を「プレイヤー移動前」の位置を目標に計算
            int previousPlayer = PlayerPosition;
            int previousEnemy = EnemyPosition;
            int nextEnemy = EnemyPosition;

            if (EnemyStoppedTurns > 0)
            {
                EnemyStoppedTurns -= 1; // 拘束中は移動しない
            }
            else
            {
                AILevel effective = Floor.GetEffectiveAILevel(CurrentFloor, SelectedAILevel);
                nextEnemy = _ai.CalculateNextMove(EnemyPosition, PlayerPosition, effective);
            }

            PlayerPosition = next;
            EnemyPosition = nextEnemy;
            PendingPlayerMove = null;

            // すれ違い (位置入れ替え) 判定
            bool isCrossing = (previousPlayer == nextEnemy && previousEnemy == next);
            bool isCollision = (PlayerPosition == EnemyPosition) || isCrossing;

            if (isCollision)
            {
                if (Skill.Type == SkillType.Invisible && RemainingSkillUses > 0)
                {
                    SkillUsageCount++; // 透明化: 衝突時に自動消費
                }
                else if (Skill.Type == SkillType.Shield && ShieldActive)
                {
                    ShieldActive = false; // 盾ガード: 1回無効化
                    SkillUsageCount++;
                    ComboCount = 0;
                }
                else
                {
                    return Defeat(DefeatReason.CaughtByEnemy);
                }
            }

            TurnCount++;
            if (TurnCount >= MaxTurns) return TurnResult.FloorCleared;
            return TurnResult.Continued;
        }

        private TurnResult Defeat(DefeatReason reason)
        {
            LastDefeatReason = reason;
            Status = GameStatus.Lose;
            return TurnResult.Defeated;
        }

        // MARK: - Move validation & availability

        /// <summary>スキルを考慮した移動の有効性。Swift: validateMove(from:to:)</summary>
        public (bool isValid, bool shouldConsume) ValidateMove(int from, int to)
        {
            bool isValid = false;
            bool shouldConsume = false;

            if (IsSkillActive)
            {
                switch (Skill.Type)
                {
                    case SkillType.Dash:
                        isValid = GameEngine.IsValidDashMove(from, to);
                        shouldConsume = isValid;
                        break;
                    case SkillType.Diagonal:
                        bool isDiag = GameEngine.IsValidDiagonalMove(from, to);
                        bool isNorm = GameEngine.IsValidMove(from, to);
                        isValid = isDiag || isNorm;
                        shouldConsume = isDiag;
                        break;
                    default:
                        isValid = GameEngine.IsValidMove(from, to);
                        break;
                }
            }
            else if (Skill.Type == SkillType.Diagonal && RemainingSkillUses > 0)
            {
                bool isDiag = GameEngine.IsValidDiagonalMove(from, to);
                bool isNorm = GameEngine.IsValidMove(from, to);
                isValid = isDiag || isNorm;
                shouldConsume = isDiag;
            }
            else if (Skill.Type == SkillType.Diagonal)
            {
                isValid = GameEngine.IsValidMove(from, to);
            }
            else
            {
                isValid = GameEngine.IsValidMove(from, to);
            }

            return (isValid, shouldConsume);
        }

        /// <summary>移動可能マス (現在位置と消失マスを除外)。Swift: getAvailableMoves()</summary>
        public List<int> GetAvailableMoves()
        {
            var moves = new List<int>();
            moves.AddRange(GameEngine.GetAvailableMoves(PlayerPosition));

            if (IsSkillActive)
            {
                if (Skill.Type == SkillType.Dash) moves.AddRange(GetDashMoves(PlayerPosition));
                else if (Skill.Type == SkillType.Diagonal) moves.AddRange(GetDiagonalMoves(PlayerPosition));
            }
            else if (Skill.Type == SkillType.Diagonal && RemainingSkillUses > 0)
            {
                moves.AddRange(GetDiagonalMoves(PlayerPosition));
            }

            return new HashSet<int>(moves)
                .Where(p => !DisappearedCells.Contains(p) && p != PlayerPosition)
                .ToList();
        }

        private List<int> GetDashMoves(int position)
        {
            var moves = new List<int>();
            int row = GameConfig.RowFromPosition(position);
            int col = GameConfig.ColumnFromPosition(position);

            if (row >= 2) moves.Add(GameConfig.PositionFromRowColumn(row - 2, col));                       // 上2
            if (row <= GameConfig.GridRows - 3) moves.Add(GameConfig.PositionFromRowColumn(row + 2, col)); // 下2
            if (col >= 2) moves.Add(GameConfig.PositionFromRowColumn(row, col - 2));                       // 左2
            if (col <= GameConfig.GridColumns - 3) moves.Add(GameConfig.PositionFromRowColumn(row, col + 2)); // 右2

            return moves;
        }

        private List<int> GetDiagonalMoves(int position)
        {
            var moves = new List<int>();
            int row = GameConfig.RowFromPosition(position);
            int col = GameConfig.ColumnFromPosition(position);

            if (row > 0 && col > 0) moves.Add(GameConfig.PositionFromRowColumn(row - 1, col - 1));                                   // 左上
            if (row > 0 && col < GameConfig.GridColumns - 1) moves.Add(GameConfig.PositionFromRowColumn(row - 1, col + 1));          // 右上
            if (row < GameConfig.GridRows - 1 && col > 0) moves.Add(GameConfig.PositionFromRowColumn(row + 1, col - 1));             // 左下
            if (row < GameConfig.GridRows - 1 && col < GameConfig.GridColumns - 1) moves.Add(GameConfig.PositionFromRowColumn(row + 1, col + 1)); // 右下

            return moves;
        }

        /// <summary>移動先を予約 + コンボ更新。Swift: selectMove(to:) (タイミング grade は外部から渡す)。</summary>
        public bool SelectMove(int position, TimingGrade grade)
        {
            if (Status != GameStatus.Playing) return false;
            var available = GetAvailableMoves();
            if (!available.Contains(position)) return false;
            if (DisappearedCells.Contains(position)) return false;

            PendingPlayerMove = position;
            if (grade == TimingGrade.Just || grade == TimingGrade.Good) ComboCount++;
            else ComboCount = 0;
            return true;
        }

        // MARK: - Skill actions

        /// <summary>スキル発動。Swift: activateSkill()</summary>
        public void ActivateSkill()
        {
            if (Status != GameStatus.Playing) return;
            if (RemainingSkillUses <= 0) return;

            switch (Skill.Type)
            {
                case SkillType.Dash:
                    IsSkillActive = true;
                    break;
                case SkillType.Shield:
                    ShieldActive = true;
                    ComboCount = 0;
                    break;
                // diagonal(常時) / invisible(自動) / bind(BindEnemy) は no-op
            }
        }

        /// <summary>敵を拘束。Swift: bindEnemy()</summary>
        public void BindEnemy()
        {
            if (Status != GameStatus.Playing) return;
            if (Skill.Type != SkillType.Bind) return;
            if (RemainingSkillUses <= 0) return;

            EnemyStoppedTurns = GameConfig.BindDurationTurns;
            SkillUsageCount++;
        }

        // MARK: - Special rules

        private void UpdateDisappearedCells()
        {
            if (CurrentSpecialRule == SpecialRule.Disappear || CurrentSpecialRule == SpecialRule.FogDisappear)
            {
                int count = GetNumberOfDisappearingCells(CurrentFloor);
                var available = new List<int>();
                for (int i = 1; i <= GameConfig.GridSize; i++)
                {
                    if (i != PlayerPosition && i != EnemyPosition) available.Add(i);
                }

                var disappeared = new HashSet<int>();
                int toDisappear = Math.Min(count, available.Count);
                for (int i = 0; i < toDisappear; i++)
                {
                    if (available.Count == 0) break;
                    int idx = _rng.NextInt(available.Count);
                    disappeared.Add(available[idx]);
                    available.RemoveAt(idx);
                }
                DisappearedCells = disappeared;
            }
            else
            {
                DisappearedCells = new HashSet<int>();
            }
        }

        /// <summary>階層に応じた消失マス数 (段階的)。Swift: getNumberOfDisappearingCells(for:)</summary>
        private int GetNumberOfDisappearingCells(int floor)
        {
            foreach (var stage in GameConfig.DisappearCellStages)
            {
                if (floor >= stage.Floor) return stage.Count;
            }
            return 1;
        }

        /// <summary>霧マップ: プレイヤーから見えるマスか。Swift: isCellVisible(_:)</summary>
        public bool IsCellVisible(int position)
        {
            if (DisappearedCells.Contains(position)) return true; // 消失マスは常に見える

            if (CurrentSpecialRule == SpecialRule.Fog || CurrentSpecialRule == SpecialRule.FogDisappear)
            {
                int playerRow = GameConfig.RowFromPosition(PlayerPosition);
                int playerCol = GameConfig.ColumnFromPosition(PlayerPosition);
                int cellRow = GameConfig.RowFromPosition(position);
                int cellCol = GameConfig.ColumnFromPosition(position);
                return Math.Abs(cellRow - playerRow) <= 1 && Math.Abs(cellCol - playerCol) <= 1;
            }
            return true;
        }

        public bool IsCellDisappeared(int position) => DisappearedCells.Contains(position);

        // MARK: - Helpers

        /// <summary>3x3 グリッド上の Chebyshev 距離 (隣接=1)。Swift: chebyshevDistance(from:to:)</summary>
        public static int ChebyshevDistance(int a, int b)
        {
            int rowA = GameConfig.RowFromPosition(a);
            int colA = GameConfig.ColumnFromPosition(a);
            int rowB = GameConfig.RowFromPosition(b);
            int colB = GameConfig.ColumnFromPosition(b);
            return Math.Max(Math.Abs(rowA - rowB), Math.Abs(colA - colB));
        }

        /// <summary>デイリーチャレンジ条件の適用。Swift: applyDailyChallengeConditions()</summary>
        public void ApplyDailyChallengeConditions()
        {
            foreach (var condition in DailyChallengeConditions)
            {
                switch (condition.Kind)
                {
                    case ChallengeConditionKind.CharacterLock:
                        break; // キャラは View 側でロック
                    case ChallengeConditionKind.NoSkillAllowed:
                        SkillUsageCount = Skill.MaxUsage; // 使い切り状態に
                        break;
                    case ChallengeConditionKind.ForcedAI:
                        SelectedAILevel = condition.AILevel;
                        break;
                    case ChallengeConditionKind.StartFloor:
                        CurrentFloor = Math.Max(1, Math.Min(condition.Floor, GameConfig.MaxFloors));
                        break;
                }
            }
        }

        private int RandomPosition() => _rng.NextInt(GameConfig.GridSize) + 1; // 1..9

        private int RandomPositionExcluding(int exclude)
        {
            int p;
            int guard = 0;
            do { p = RandomPosition(); } while (p == exclude && ++guard < 100);
            return p;
        }
    }
}
