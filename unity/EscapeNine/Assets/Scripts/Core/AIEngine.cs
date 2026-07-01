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
