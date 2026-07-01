// GameSessionTests.cs
// Swift 正本 (GameViewModel の onTurnDeadline / nextFloor / getAvailableMoves 等) と
// 同じターン進行になることを担保。
//
// 決定論化: 効果AIが Normal (100%追跡) になる floor1 + selected=Hard を使い、
// 配置は明示指定して乱数を排除。FakeRandom は AIEngineTests.cs のものを再利用。
//
// 盤面:
//   1 2 3
//   4 5 6
//   7 8 9

using System.Collections.Generic;
using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class GameSessionTests
    {
        // floor1 + Hard → 効果AI Normal (決定論的に最短追跡)
        private static GameSession NewSession(CharacterType type = CharacterType.Hero)
        {
            return new GameSession(
                Character.GetCharacter(type),
                AILevel.Hard,
                new AIEngine(new FakeRandom()),
                new FakeRandom());
        }

        [Test]
        public void Timeout_WhenNoPendingMove()
        {
            var s = NewSession();
            s.StartGame(1, 1, 9);
            s.PendingPlayerMove = null;

            Assert.AreEqual(TurnResult.Defeated, s.ResolveTurn());
            Assert.AreEqual(DefeatReason.TimeOut, s.LastDefeatReason);
            Assert.AreEqual(GameStatus.Lose, s.Status);
        }

        [Test]
        public void SimultaneousMove_NoCollision_Continues()
        {
            var s = NewSession();
            s.StartGame(1, 1, 9);
            s.PendingPlayerMove = 2;

            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn());
            Assert.AreEqual(2, s.PlayerPosition);
            Assert.AreEqual(6, s.EnemyPosition); // Normal: 9 → 6 (最短追跡)
            Assert.AreEqual(1, s.TurnCount);
        }

        [Test]
        public void Collision_DefeatsHero()
        {
            var s = NewSession();
            s.StartGame(1, 4, 6);
            s.PendingPlayerMove = 5; // 敵も 6→5 に来て同マス衝突

            Assert.AreEqual(TurnResult.Defeated, s.ResolveTurn());
            Assert.AreEqual(DefeatReason.CaughtByEnemy, s.LastDefeatReason);
        }

        [Test]
        public void Crossing_IsCollision()
        {
            var s = NewSession();
            s.StartGame(1, 4, 5);
            s.PendingPlayerMove = 5; // player 4→5, enemy 5→4 で入れ替わり

            Assert.AreEqual(TurnResult.Defeated, s.ResolveTurn());
        }

        [Test]
        public void Shield_AbsorbsCollision()
        {
            var s = NewSession(CharacterType.Knight);
            s.StartGame(1, 4, 6);
            s.ShieldActive = true;
            s.PendingPlayerMove = 5;

            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn());
            Assert.IsFalse(s.ShieldActive);
            Assert.AreEqual(1, s.SkillUsageCount);
            Assert.AreEqual(GameStatus.Playing, s.Status);
        }

        [Test]
        public void Invisible_AbsorbsCollision()
        {
            var s = NewSession(CharacterType.Wizard);
            s.StartGame(1, 4, 6);
            s.PendingPlayerMove = 5;

            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn());
            Assert.AreEqual(1, s.SkillUsageCount); // 透明化を1消費
        }

        [Test]
        public void Bind_StopsEnemyForTurn()
        {
            var s = NewSession();
            s.StartGame(1, 1, 9);
            s.EnemyStoppedTurns = 1;
            s.PendingPlayerMove = 2;

            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn());
            Assert.AreEqual(9, s.EnemyPosition); // 拘束中は動かない
            Assert.AreEqual(0, s.EnemyStoppedTurns);
        }

        [Test]
        public void FloorCleared_WhenReachingMaxTurns()
        {
            var s = NewSession();
            s.StartGame(1, 1, 9);
            s.TurnCount = 4; // floor1 の maxTurns = 5
            s.PendingPlayerMove = 2;

            Assert.AreEqual(TurnResult.FloorCleared, s.ResolveTurn());
            Assert.AreEqual(5, s.TurnCount);
            Assert.AreEqual(GameStatus.Playing, s.Status);
        }

        [Test]
        public void NextFloor_ResetsSkillEveryTenFloors()
        {
            var s = NewSession();
            s.StartGame(1, 1, 9);
            s.CurrentFloor = 10;
            s.SkillUsageCount = 2;

            Assert.AreEqual(FloorAdvanceResult.Advanced, s.NextFloor());
            Assert.AreEqual(11, s.CurrentFloor);
            Assert.AreEqual(0, s.SkillUsageCount); // 11 % 10 == 1 でリセット
        }

        [Test]
        public void NextFloor_DoesNotResetSkillMidCycle()
        {
            var s = NewSession();
            s.StartGame(1, 1, 9);
            s.CurrentFloor = 1;
            s.SkillUsageCount = 2;

            s.NextFloor();
            Assert.AreEqual(2, s.CurrentFloor);
            Assert.AreEqual(2, s.SkillUsageCount); // リセットされない
        }

        [Test]
        public void NextFloor_GameWonBeyondMaxFloor()
        {
            var s = NewSession();
            s.StartGame(1, 1, 9);
            s.CurrentFloor = 100;

            Assert.AreEqual(FloorAdvanceResult.GameWon, s.NextFloor());
            Assert.AreEqual(GameStatus.Win, s.Status);
        }

        [Test]
        public void GetAvailableMoves_ExcludesCurrentPosition()
        {
            var s = NewSession();
            s.StartGame(1, 5, 1);
            var moves = s.GetAvailableMoves();

            Assert.AreEqual(4, moves.Count); // 中央から上下左右
            Assert.IsFalse(moves.Contains(5));
        }

        [Test]
        public void GetAvailableMoves_ThiefHasDiagonals()
        {
            var s = NewSession(CharacterType.Thief);
            s.StartGame(1, 5, 1);
            var moves = s.GetAvailableMoves();

            // 中央から上下左右(4) + 斜め(4) = 8
            Assert.AreEqual(8, moves.Count);
        }

        [Test]
        public void GetAvailableMoves_HeroDashFromCorner()
        {
            var s = NewSession();
            s.StartGame(1, 1, 9);
            s.IsSkillActive = true; // ダッシュ発動状態

            var moves = s.GetAvailableMoves();
            Assert.IsTrue(moves.Contains(3)); // 右2マス
            Assert.IsTrue(moves.Contains(7)); // 下2マス
            Assert.AreEqual(4, moves.Count);  // {2,4,3,7}
        }

        [Test]
        public void ChebyshevDistance_Values()
        {
            Assert.AreEqual(2, GameSession.ChebyshevDistance(1, 9));
            Assert.AreEqual(1, GameSession.ChebyshevDistance(1, 2));
            Assert.AreEqual(1, GameSession.ChebyshevDistance(1, 5));
            Assert.AreEqual(0, GameSession.ChebyshevDistance(5, 5));
        }

        [Test]
        public void ScoreMultiplier_Thresholds()
        {
            var s = NewSession();
            s.ComboCount = 0; Assert.AreEqual(1.0, s.ScoreMultiplier, 1e-9);
            s.ComboCount = 3; Assert.AreEqual(1.5, s.ScoreMultiplier, 1e-9);
            s.ComboCount = 5; Assert.AreEqual(2.0, s.ScoreMultiplier, 1e-9);
        }

        [Test]
        public void ApplyDailyChallenge_NoSkillAndForcedAI()
        {
            var s = NewSession();
            s.DailyChallengeConditions = new List<ChallengeCondition>
            {
                ChallengeCondition.NoSkillAllowed(),
                ChallengeCondition.ForcedAI(AILevel.Normal)
            };
            s.ApplyDailyChallengeConditions();

            Assert.AreEqual(s.Skill.MaxUsage, s.SkillUsageCount); // 使い切り
            Assert.AreEqual(AILevel.Normal, s.SelectedAILevel);
        }

        [Test]
        public void ApplyDailyChallenge_StartFloor()
        {
            var s = NewSession();
            s.DailyChallengeConditions = new List<ChallengeCondition> { ChallengeCondition.StartFloor(25) };
            s.ApplyDailyChallengeConditions();

            Assert.AreEqual(25, s.CurrentFloor);
        }
    }
}
