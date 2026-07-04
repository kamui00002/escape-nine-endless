// AIEngine.cs
// Swift 正本からの忠実移植: Services/AIEngine.swift
// 鬼AI (Easy/Normal/Hard/Boss)。乱数源は IRandomSource で注入 (テスト決定論化のため)。

using System;
using System.Collections.Generic;

namespace EscapeNine.Core
{
    public sealed class AIEngine
    {
        private readonly IRandomSource _rng;

        /// <param name="rng">省略時は既定の System 乱数 (Swift のグローバル乱数相当)。</param>
        public AIEngine(IRandomSource rng = null)
        {
            _rng = rng ?? new SystemRandomSource();
        }

        /// <summary>次の鬼の移動先。Swift: calculateNextMove(from:target:level:)</summary>
        public int CalculateNextMove(int enemyPosition, int playerPosition, AILevel level)
        {
            switch (level)
            {
                case AILevel.Easy: return EasyAI(enemyPosition, playerPosition);
                case AILevel.Normal: return NormalAI(enemyPosition, playerPosition);
                case AILevel.Hard: return HardAI(enemyPosition, playerPosition);
                case AILevel.Boss: return BossAI(enemyPosition, playerPosition);
                default: return NormalAI(enemyPosition, playerPosition);
            }
        }

        // MARK: - Easy AI (追跡15% / 逃走20% / 残りランダム)
        private int EasyAI(int enemyPosition, int playerPosition)
        {
            var availableMoves = GameEngine.GetAvailableMoves(enemyPosition);

            double roll = _rng.NextDouble();
            if (roll < GameConfig.EasyAIChaseChance)
            {
                int? towards = GetMoveTowardsPlayer(playerPosition, availableMoves);
                if (towards.HasValue) return towards.Value;
            }
            else if (roll < GameConfig.EasyAIChaseChance + GameConfig.EasyAIFleeChance)
            {
                int? away = GetMoveAwayFromPlayer(playerPosition, availableMoves);
                if (away.HasValue) return away.Value;
            }

            return RandomElement(availableMoves, enemyPosition);
        }

        // MARK: - Normal AI (常に最短距離で追跡)
        private int NormalAI(int enemyPosition, int playerPosition)
        {
            var availableMoves = GameEngine.GetAvailableMoves(enemyPosition);
            int? towards = GetMoveTowardsPlayer(playerPosition, availableMoves);
            if (towards.HasValue) return towards.Value;
            return availableMoves.Count > 0 ? availableMoves[0] : enemyPosition;
        }

        // MARK: - Boss AI (95%追跡 / 5%ランダム)
        private int BossAI(int enemyPosition, int playerPosition)
        {
            var availableMoves = GameEngine.GetAvailableMoves(enemyPosition);

            double roll = _rng.NextDouble();
            if (roll < GameConfig.BossAIChaseChance)
            {
                int? towards = GetMoveTowardsPlayer(playerPosition, availableMoves);
                if (towards.HasValue) return towards.Value;
            }

            return RandomElement(availableMoves, enemyPosition);
        }

        // MARK: - Hard AI (プレイヤーの次手を予測して先回り)
        private int HardAI(int enemyPosition, int playerPosition)
        {
            var availableMoves = GameEngine.GetAvailableMoves(enemyPosition);

            int predicted = PredictPlayerMove(playerPosition, enemyPosition);

            int? towardsPrediction = GetMoveTowardsPlayer(predicted, availableMoves);
            if (towardsPrediction.HasValue) return towardsPrediction.Value;

            int? towardsActual = GetMoveTowardsPlayer(playerPosition, availableMoves);
            if (towardsActual.HasValue) return towardsActual.Value;
            return availableMoves.Count > 0 ? availableMoves[0] : enemyPosition;
        }

        /// <summary>プレイヤーは鬼から最も遠ざかると仮定して次手を予測。Swift: predictPlayerMove</summary>
        private int PredictPlayerMove(int playerPosition, int enemyPosition)
        {
            var playerMoves = GameEngine.GetAvailableMoves(playerPosition);

            int maxDistance = -1;
            int predictedMove = playerPosition;

            foreach (int move in playerMoves)
            {
                int moveRow = (move - 1) / 3;
                int moveCol = (move - 1) % 3;
                int enemyRow = (enemyPosition - 1) / 3;
                int enemyCol = (enemyPosition - 1) % 3;

                int distance = Math.Abs(moveRow - enemyRow) + Math.Abs(moveCol - enemyCol);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    predictedMove = move;
                }
            }

            return predictedMove;
        }

        // MARK: - Boss patterns (Phase 5c, Swift正本には存在しない)
        // docs/unity-phase5-roguelike-design.md §5.1・§5.3。既存 CalculateNextMove は変更しない (原則1)。

        /// <summary>
        /// ボス階専用の移動計算。パターンごとにアルゴリズムを切り替える (§5.1)。
        /// 呼び出し元 (GameSession.ResolveTurn) は、ボス階では effective (Floor.GetEffectiveAILevel)
        /// を経由するこの既存 CalculateNextMove ではなく、必ずこちらを経由する (§5.3)。
        /// </summary>
        /// <param name="turnIndexInFloor">ボス階内の経過ターン数 (0始まり)。①②③いずれの移動計算自体も
        /// この値に依存しないが、③威圧パターンの対象マス選定 (CalculateIntimidationZone) と揃えるため
        /// シグネチャに含める。</param>
        public int CalculateBossMove(int enemyPosition, int playerPosition, BossPattern pattern, int turnIndexInFloor)
        {
            if (pattern == BossPattern.Foresight)
            {
                // §5.1②: HardAI (PredictPlayerMove を使った決定論的先読み) をそのまま流用。
                return HardAI(enemyPosition, playerPosition);
            }

            // §5.1①・③: 移動アルゴリズムは既存 BossAI (追跡95%/ランダム5%) を両パターン共通で流用する。
            // ③威圧は「移動を止めて隣接マスを警告する」のではなく「追跡を続けたまま隣接マスにも威圧を
            // 上乗せする」加算的な効果として実装した — §5.2 が③をFloor40+で解禁する理由を「既存の
            // 難易度ランプ哲学と整合させる」と明記しているため、移動を止める実装だと最新解禁パターンの
            // ターンがむしろ休憩ターンになり、意図しているはずのランプが逆転してしまう。
            // どのマスを1ターン進入不可にするかは CalculateIntimidationZone / GameSession.TemporaryBossZone
            // が別途・独立に決定する。
            return BossAI(enemyPosition, playerPosition);
        }

        /// <summary>
        /// ③威圧パターン: 敵の隣接マス (上下左右) のうち、1ターンだけ進入不可にする対象を
        /// 決定論的に選ぶ (turnIndexInFloor による周期選択、乱数を消費しない)。
        /// ヘッドレスsim/テストで決定論的に検証できる (Tier1、§5.3)。隣接マスが存在しない場合は null。
        /// </summary>
        public int? CalculateIntimidationZone(int enemyPosition, int turnIndexInFloor)
        {
            var adjacent = GameEngine.GetAvailableMoves(enemyPosition);
            if (adjacent.Count == 0) return null;
            int index = ((turnIndexInFloor % adjacent.Count) + adjacent.Count) % adjacent.Count;
            return adjacent[index];
        }

        // MARK: - Helpers

        /// <summary>プレイヤーから最も離れる移動先 (マンハッタン距離最大)。Swift: getMoveAwayFromPlayer</summary>
        private int? GetMoveAwayFromPlayer(int playerPosition, List<int> availableMoves)
        {
            int playerRow = (playerPosition - 1) / 3;
            int playerCol = (playerPosition - 1) % 3;

            int? bestMove = null;
            int maxDistance = -1;

            foreach (int move in availableMoves)
            {
                int moveRow = (move - 1) / 3;
                int moveCol = (move - 1) % 3;
                int distance = Math.Abs(moveRow - playerRow) + Math.Abs(moveCol - playerCol);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    bestMove = move;
                }
            }

            return bestMove;
        }

        /// <summary>プレイヤーに最も近づく移動先 (マンハッタン距離最小)。Swift: getMoveTowardsPlayer</summary>
        private int? GetMoveTowardsPlayer(int playerPosition, List<int> availableMoves)
        {
            int playerRow = (playerPosition - 1) / 3;
            int playerCol = (playerPosition - 1) % 3;

            int? bestMove = null;
            int minDistance = int.MaxValue;

            foreach (int move in availableMoves)
            {
                int moveRow = (move - 1) / 3;
                int moveCol = (move - 1) % 3;
                int distance = Math.Abs(moveRow - playerRow) + Math.Abs(moveCol - playerCol);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestMove = move;
                }
            }

            return bestMove;
        }

        /// <summary>Swift の availableMoves.randomElement() ?? enemyPosition 相当。</summary>
        private int RandomElement(List<int> moves, int fallback)
        {
            if (moves == null || moves.Count == 0) return fallback;
            return moves[_rng.NextInt(moves.Count)];
        }
    }
}
