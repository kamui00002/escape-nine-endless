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

        // --- Phase 5 レリック (Unity固有拡張、Swift正本には存在しない) ---
        // docs/unity-phase5-roguelike-design.md §1原則1: 既存公開APIへの「追加のみ」。
        // 既定値 RelicEffects.None のとき、以下の全フックは既存(Phase 5以前)の挙動と完全に一致する。
        public RelicEffects Relics { get; set; } = RelicEffects.None;

        /// <summary>「影の抜け道」系(DisappearForgivenessPerFloor)の今階層内の残り使用回数。
        /// 階層開始時に Relics.DisappearForgivenessPerFloor へ再チャージされる (§2.4)。</summary>
        private int _disappearForgivenessRemainingThisFloor;

        public GameSession(Character character, AILevel selectedAILevel = AILevel.Easy, AIEngine ai = null, IRandomSource rng = null)
        {
            CurrentCharacter = character;
            SelectedAILevel = selectedAILevel;
            _rng = rng ?? new SystemRandomSource();
            _ai = ai ?? new AIEngine(_rng);
        }

        public void SetCharacter(Character character) => CurrentCharacter = character;

        // --- derived ---
        /// <summary>スキル最大使用回数のレリックボーナスを加算した残り使用回数。Relics.None なら既存挙動と同一。
        /// #3 影分身の型 (ThiefSkillMaxUsageBonus) は「盗賊専用」レリックのため、CurrentCharacter が
        /// 盗賊のときのみ加算する (他キャラが誤ってクロスピックしても効果が乗らないようにする)。</summary>
        public int RemainingSkillUses
        {
            get
            {
                int bonus = Relics.SkillMaxUsageBonus;
                if (CurrentCharacter.Type == CharacterType.Thief) bonus += Relics.ThiefSkillMaxUsageBonus;
                return Skill.MaxUsage + bonus - SkillUsageCount;
            }
        }

        /// <summary>現在階層の必要ターン数。Floor 0 はプロローグ短縮。Swift: maxTurns
        /// レリック(#4 老練の構え)の TurnCountReduction を減算し、最低3にクランプする。</summary>
        public int MaxTurns
        {
            get
            {
                if (CurrentFloor == TutorialConstants.PrologueFloor) return TutorialConstants.PrologueClearTurns;
                int baseTurns = GameConfig.GetMaxTurns(CurrentFloor);
                return Math.Max(3, baseTurns - Relics.TurnCountReduction);
            }
        }

        public bool IsBossFloor => Floor.IsBossFloor(CurrentFloor);

        /// <summary>コンボによるスコア倍率。Swift: scoreMultiplier
        /// レリック(#14 連鎖の証)の ComboThresholdReduction をしきい値から減算し、
        /// レリック(#15 加速の証)の ComboThresholdBonusMultiplier をしきい値到達時の倍率に加算する。</summary>
        public double ScoreMultiplier
        {
            get
            {
                int threshold1 = GameConfig.ComboMultiplierThreshold1 - Relics.ComboThresholdReduction;
                int threshold2 = GameConfig.ComboMultiplierThreshold2 - Relics.ComboThresholdReduction;
                if (ComboCount >= threshold2) return 2.0 + Relics.ComboThresholdBonusMultiplier;
                if (ComboCount >= threshold1) return 1.5 + Relics.ComboThresholdBonusMultiplier;
                return 1.0;
            }
        }

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
            // #11 影の抜け道 (5aカタログ外だがフックとして実装)。Relics.None なら常に0のため実質no-op。
            _disappearForgivenessRemainingThisFloor = Relics.DisappearForgivenessPerFloor;

            // 配置 (Swift: 1..9 の distinct。消失計算は配置後なので player/enemy は消えない)
            // #10 護りの起点: enemyPos が明示指定されていない場合のみ、Relics.MinStartDistance を
            // 満たす配置を保証する。Relics.MinStartDistance <= 0 (既定) のときは既存の
            // RandomPositionExcluding 経路のみを通り、乱数消費順を含めて既存挙動と完全に一致する。
            int p = playerPos ?? RandomPosition();
            int e;
            if (enemyPos.HasValue)
            {
                e = enemyPos.Value;
            }
            else if (Relics.MinStartDistance > 0)
            {
                e = RandomPositionSatisfyingDistance(p, Relics.MinStartDistance);
            }
            else
            {
                e = RandomPositionExcluding(p);
            }
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
            // #11 影の抜け道 (5aカタログ外だがフックとして実装)。Relics.None なら常に0のため実質no-op。
            _disappearForgivenessRemainingThisFloor = Relics.DisappearForgivenessPerFloor;

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
            int enemyPos;
            if (Relics.MinStartDistance > 0)
            {
                // #10 護りの起点: 距離条件を満たす候補があればそこから抽選、なければ既存のフォールバックへ。
                var distanceCandidates = available.Where(p => p != playerPos && ChebyshevDistance(p, playerPos) >= Relics.MinStartDistance).ToList();
                var candidates = distanceCandidates.Count > 0 ? distanceCandidates : available.Where(p => p != playerPos).ToList();
                enemyPos = candidates.Count > 0
                    ? candidates[_rng.NextInt(candidates.Count)]
                    : (playerPos == 1 ? 9 : 1);
            }
            else
            {
                var enemyCandidates = available.Where(p => p != playerPos).ToList();
                enemyPos = enemyCandidates.Count > 0
                    ? enemyCandidates[_rng.NextInt(enemyCandidates.Count)]
                    : (playerPos == 1 ? 9 : 1);
            }

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

            // 斜め移動 (盗賊スキル) を実際に消費するターンかどうか。#1/#2 の統合点判定に使う。
            bool usedDiagonalSkillThisTurn = shouldConsume && Skill.Type == SkillType.Diagonal;

            if (shouldConsume && RemainingSkillUses > 0)
            {
                // #1 影の軽業: 斜め移動消費時のみ、Relics.ThiefDiagonalSkillSaveChance の確率でスキル残数を温存する。
                // Relics.None (=0.0) のときは `> 0` で短絡し _rng.NextDouble() を一切呼ばないため、
                // 消失マス抽選などで使う既存の _rng 消費シーケンスに影響しない (§2.3 末尾の注記どおり)。
                bool skillSaved = usedDiagonalSkillThisTurn
                    && Relics.ThiefDiagonalSkillSaveChance > 0
                    && _rng.NextDouble() < Relics.ThiefDiagonalSkillSaveChance;
                if (!skillSaved) SkillUsageCount++;
            }
            IsSkillActive = false;

            if (DisappearedCells.Contains(next))
            {
                // #11 影の抜け道 (5aカタログ外だがフックとして実装): 1階層につき許された回数だけ、
                // 消失マスへの進入による敗北を無効化して継続する。Relics.None なら常に0のため既存挙動と同一。
                if (_disappearForgivenessRemainingThisFloor > 0)
                {
                    _disappearForgivenessRemainingThisFloor--;
                }
                else
                {
                    return Defeat(DefeatReason.CaughtByEnemy);
                }
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

                // #2 残像のヴェール: 斜め移動を消費したターンは、敵の移動をEasy相当に強制する。
                // #9 幻惑の粉 (5aカタログ外だがフックとして実装): 実効AIがHardのときのみNormalへ格下げ
                // (表示上の effective 自体は書き換えない)。両方満たす場合はヴェールを優先する。
                AILevel aiLevelForThisCall;
                if (Relics.ThiefResidualVeil && usedDiagonalSkillThisTurn)
                {
                    aiLevelForThisCall = AILevel.Easy;
                }
                else if (Relics.NeutralizeHardPrediction && effective == AILevel.Hard)
                {
                    aiLevelForThisCall = AILevel.Normal;
                }
                else
                {
                    aiLevelForThisCall = effective;
                }

                nextEnemy = _ai.CalculateNextMove(EnemyPosition, PlayerPosition, aiLevelForThisCall);
            }

            PlayerPosition = next;
            EnemyPosition = nextEnemy;
            PendingPlayerMove = null;

            // すれ違い (位置入れ替え) 判定
            bool isCrossing = (previousPlayer == nextEnemy && previousEnemy == next);
            bool isCollision = (PlayerPosition == EnemyPosition) || isCrossing;

            if (isCollision)
            {
                // #5 不死鳥の残り火 (5aカタログ外だがフックとして実装): 衝突による敗北そのものを無効化して継続する。
                // #12 二段構えの盾: 盾スキルを持たないキャラでも、レリックのチャージ分だけ即席の盾を発動する。
                // Relics.None (両方0) のときはどちらの分岐にも入らず、既存の透明化/盾判定にそのまま進む。
                if (Relics.ReviveCharges > 0)
                {
                    Relics.ReviveCharges--;
                }
                else if (Relics.GenericShieldCharges > 0)
                {
                    Relics.GenericShieldCharges--;
                    ComboCount = 0; // 既存の盾ガード(Shield)と同様、無効化時はコンボをリセットする
                }
                else if (Skill.Type == SkillType.Invisible && RemainingSkillUses > 0)
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
            if (grade == TimingGrade.Just || grade == TimingGrade.Good)
            {
                ComboCount++;
            }
            else if (Relics.ComboMissShieldCharges > 0)
            {
                // #6 コンボの守り (5aカタログ外だがフックとして実装、Tier2): Missでもコンボを維持する。
                Relics.ComboMissShieldCharges--;
            }
            else
            {
                ComboCount = 0;
            }
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

        /// <summary>敵を拘束。Swift: bindEnemy()
        /// #17 心話の絆: 拘束スキルを持たないキャラでも、レリックの専用チャージ分だけ疑似的に拘束できる
        /// (キャラのスキル残数(SkillUsageCount)は消費しない、専用チャージのみ減らす)。</summary>
        public void BindEnemy()
        {
            if (Status != GameStatus.Playing) return;

            if (Skill.Type == SkillType.Bind)
            {
                if (RemainingSkillUses <= 0) return;
                EnemyStoppedTurns = GameConfig.BindDurationTurns;
                SkillUsageCount++;
                return;
            }

            if (Relics.PseudoBindCharges > 0)
            {
                EnemyStoppedTurns = GameConfig.BindDurationTurns;
                Relics.PseudoBindCharges--;
            }
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

        /// <summary>階層に応じた消失マス数 (段階的)。Swift: getNumberOfDisappearingCells(for:)
        /// #7 地固めの護符: Relics.DisappearCellReduction を減算する (0未満は0にクランプ)。</summary>
        private int GetNumberOfDisappearingCells(int floor)
        {
            int baseCount = 1;
            foreach (var stage in GameConfig.DisappearCellStages)
            {
                if (floor >= stage.Floor)
                {
                    baseCount = stage.Count;
                    break;
                }
            }
            return Math.Max(0, baseCount - Relics.DisappearCellReduction);
        }

        /// <summary>霧マップ: プレイヤーから見えるマスか。Swift: isCellVisible(_:)
        /// #8 灯火の指輪: Relics.FogVisibilityRadiusBonus を視界半径 (既定1) に加算する。</summary>
        public bool IsCellVisible(int position)
        {
            if (DisappearedCells.Contains(position)) return true; // 消失マスは常に見える

            if (CurrentSpecialRule == SpecialRule.Fog || CurrentSpecialRule == SpecialRule.FogDisappear)
            {
                int playerRow = GameConfig.RowFromPosition(PlayerPosition);
                int playerCol = GameConfig.ColumnFromPosition(PlayerPosition);
                int cellRow = GameConfig.RowFromPosition(position);
                int cellCol = GameConfig.ColumnFromPosition(position);
                int radius = 1 + Relics.FogVisibilityRadiusBonus;
                return Math.Abs(cellRow - playerRow) <= radius && Math.Abs(cellCol - playerCol) <= radius;
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

        /// <summary>#10 護りの起点: reference から Chebyshev距離 minDistance 以上のマスを抽選する。
        /// 満たすマスが存在しない場合 (3x3では起こり得ないが安全側) は既存の RandomPositionExcluding にフォールバックする。</summary>
        private int RandomPositionSatisfyingDistance(int reference, int minDistance)
        {
            var candidates = new List<int>();
            for (int i = 1; i <= GameConfig.GridSize; i++)
            {
                if (i != reference && ChebyshevDistance(i, reference) >= minDistance) candidates.Add(i);
            }
            if (candidates.Count == 0) return RandomPositionExcluding(reference);
            return candidates[_rng.NextInt(candidates.Count)];
        }

        private int RandomPositionExcluding(int exclude)
        {
            int p;
            int guard = 0;
            do { p = RandomPosition(); } while (p == exclude && ++guard < 100);
            return p;
        }
    }
}
