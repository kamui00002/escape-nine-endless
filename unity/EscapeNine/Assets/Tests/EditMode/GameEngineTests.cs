// GameEngineTests.cs
// Swift 正本 (Services/GameEngine.swift) と同じ入出力になることを担保する回帰テスト。
//
// 盤面の 1-indexed 配置:
//   1 2 3
//   4 5 6
//   7 8 9

using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class GameEngineTests
    {
        [Test]
        public void IsValidMove_AdjacentAndWait()
        {
            Assert.IsTrue(GameEngine.IsValidMove(1, 2));  // 右
            Assert.IsTrue(GameEngine.IsValidMove(1, 4));  // 下
            Assert.IsTrue(GameEngine.IsValidMove(1, 1));  // 待機は有効
            Assert.IsFalse(GameEngine.IsValidMove(1, 5)); // 斜めは不可
            Assert.IsFalse(GameEngine.IsValidMove(1, 3)); // 2マス離れは不可
        }

        [Test]
        public void IsValidMove_OutOfRange()
        {
            Assert.IsFalse(GameEngine.IsValidMove(1, 0));
            Assert.IsFalse(GameEngine.IsValidMove(1, 10));
        }

        [Test]
        public void IsValidDiagonalMove()
        {
            Assert.IsTrue(GameEngine.IsValidDiagonalMove(1, 5));  // 斜め
            Assert.IsTrue(GameEngine.IsValidDiagonalMove(5, 1));
            Assert.IsFalse(GameEngine.IsValidDiagonalMove(1, 2)); // 直線は不可
            Assert.IsFalse(GameEngine.IsValidDiagonalMove(1, 1)); // 待機は不可
        }

        [Test]
        public void IsValidDashMove()
        {
            Assert.IsTrue(GameEngine.IsValidDashMove(1, 3));  // 横2マス
            Assert.IsTrue(GameEngine.IsValidDashMove(1, 7));  // 縦2マス
            Assert.IsFalse(GameEngine.IsValidDashMove(1, 2)); // 1マスは不可
            Assert.IsFalse(GameEngine.IsValidDashMove(1, 9)); // 斜め2マスは不可
            Assert.IsFalse(GameEngine.IsValidDashMove(1, 1)); // 待機は不可
        }

        [Test]
        public void GetAvailableMoves_Counts()
        {
            Assert.AreEqual(2, GameEngine.GetAvailableMoves(1).Count); // 角
            Assert.AreEqual(3, GameEngine.GetAvailableMoves(2).Count); // 辺
            Assert.AreEqual(4, GameEngine.GetAvailableMoves(5).Count); // 中央
        }

        [Test]
        public void GetAvailableMoves_CornerContents()
        {
            var moves = GameEngine.GetAvailableMoves(1);
            Assert.Contains(2, moves); // 右
            Assert.Contains(4, moves); // 下
        }

        [Test]
        public void GetAvailableMoves_CenterContents()
        {
            var moves = GameEngine.GetAvailableMoves(5);
            Assert.Contains(2, moves);
            Assert.Contains(8, moves);
            Assert.Contains(4, moves);
            Assert.Contains(6, moves);
        }

        [Test]
        public void CheckGameResult()
        {
            Assert.AreEqual(GameStatus.Lose, GameEngine.CheckGameResult(3, 3, 0, 10));   // 同マス
            Assert.AreEqual(GameStatus.Win, GameEngine.CheckGameResult(1, 9, 10, 10));   // ターン上限
            Assert.AreEqual(GameStatus.Playing, GameEngine.CheckGameResult(1, 9, 5, 10)); // 継続
        }
    }
}
