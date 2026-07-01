// GameEngine.cs
// Swift 正本からの忠実移植: Services/GameEngine.swift
// 移動バリデーション・移動可能マス列挙・勝敗判定。純ロジックのため static class として提供。

using System;
using System.Collections.Generic;

namespace EscapeNine.Core
{
    public static class GameEngine
    {
        // MARK: - Move Validation

        /// <summary>通常移動 (上下左右の隣接1マス)。同マス待機は有効。Swift: isValidMove</summary>
        public static bool IsValidMove(int currentPosition, int newPosition)
        {
            if (newPosition < 1 || newPosition > GameConfig.GridSize) return false;
            if (currentPosition == newPosition) return true; // 待機は有効な選択肢

            int currentRow = (currentPosition - 1) / 3;
            int currentCol = (currentPosition - 1) % 3;
            int newRow = (newPosition - 1) / 3;
            int newCol = (newPosition - 1) % 3;

            int rowDiff = Math.Abs(newRow - currentRow);
            int colDiff = Math.Abs(newCol - currentCol);

            return (rowDiff == 1 && colDiff == 0) || (rowDiff == 0 && colDiff == 1);
        }

        /// <summary>斜め移動 (盗賊スキル)。Swift: isValidDiagonalMove</summary>
        public static bool IsValidDiagonalMove(int currentPosition, int newPosition)
        {
            if (newPosition < 1 || newPosition > GameConfig.GridSize) return false;
            if (currentPosition == newPosition) return false;

            int currentRow = (currentPosition - 1) / 3;
            int currentCol = (currentPosition - 1) % 3;
            int newRow = (newPosition - 1) / 3;
            int newCol = (newPosition - 1) % 3;

            int rowDiff = Math.Abs(newRow - currentRow);
            int colDiff = Math.Abs(newCol - currentCol);

            return rowDiff == 1 && colDiff == 1;
        }

        /// <summary>ダッシュ移動 (勇者スキル: 上下左右2マス先)。Swift: isValidDashMove</summary>
        public static bool IsValidDashMove(int currentPosition, int newPosition)
        {
            if (newPosition < 1 || newPosition > GameConfig.GridSize) return false;
            if (currentPosition == newPosition) return false;

            int currentRow = (currentPosition - 1) / 3;
            int currentCol = (currentPosition - 1) % 3;
            int newRow = (newPosition - 1) / 3;
            int newCol = (newPosition - 1) % 3;

            int rowDiff = Math.Abs(newRow - currentRow);
            int colDiff = Math.Abs(newCol - currentCol);

            return (rowDiff == 2 && colDiff == 0) || (rowDiff == 0 && colDiff == 2);
        }

        // MARK: - Available Moves

        /// <summary>現在位置から移動可能な (上下左右) マス一覧。Swift: getAvailableMoves</summary>
        public static List<int> GetAvailableMoves(int position)
        {
            var moves = new List<int>();
            int row = (position - 1) / 3;
            int col = (position - 1) % 3;

            if (row > 0) moves.Add((row - 1) * 3 + col + 1); // 上
            if (row < 2) moves.Add((row + 1) * 3 + col + 1); // 下
            if (col > 0) moves.Add(row * 3 + (col - 1) + 1); // 左
            if (col < 2) moves.Add(row * 3 + (col + 1) + 1); // 右

            return moves;
        }

        // MARK: - Game Result

        /// <summary>勝敗判定。同マス=Lose / ターン上限到達=Win / それ以外=Playing。Swift: checkGameResult</summary>
        public static GameStatus CheckGameResult(int playerPosition, int enemyPosition, int turnCount, int maxTurns)
        {
            if (playerPosition == enemyPosition) return GameStatus.Lose;
            if (turnCount >= maxTurns) return GameStatus.Win;
            return GameStatus.Playing;
        }
    }
}
