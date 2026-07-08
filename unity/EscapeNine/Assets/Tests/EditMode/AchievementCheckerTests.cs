// AchievementCheckerTests.cs
// Swift 正本 (AchievementManager.checkAchievements) と同じ解除条件を担保。

using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class AchievementCheckerTests
    {
        [Test]
        public void Floor9_OnlyFirstWin()
        {
            var r = AchievementChecker.CheckAchievements(floor: 9, skillUsed: true, currentBPM: 100, gameWon: true);
            Assert.IsTrue(r.Contains(Achievement.FirstWin));
            Assert.IsFalse(r.Contains(Achievement.Floor10));
        }

        [Test]
        public void Floor10_NoSkill_UnlocksNoSkillWin()
        {
            var r = AchievementChecker.CheckAchievements(floor: 10, skillUsed: false, currentBPM: 100, gameWon: true);
            Assert.IsTrue(r.Contains(Achievement.Floor10));
            Assert.IsTrue(r.Contains(Achievement.NoSkillWin));
        }

        [Test]
        public void Floor10_WithSkill_NoNoSkillWin()
        {
            var r = AchievementChecker.CheckAchievements(floor: 10, skillUsed: true, currentBPM: 100, gameWon: true);
            Assert.IsFalse(r.Contains(Achievement.NoSkillWin));
        }

        [Test]
        public void SpeedRunner_RequiresBpm180AndFloor20()
        {
            var yes = AchievementChecker.CheckAchievements(floor: 20, skillUsed: true, currentBPM: 180, gameWon: true);
            Assert.IsTrue(yes.Contains(Achievement.SpeedRunner));

            var no = AchievementChecker.CheckAchievements(floor: 20, skillUsed: true, currentBPM: 179, gameWon: true);
            Assert.IsFalse(no.Contains(Achievement.SpeedRunner));
        }

        [Test]
        public void Survivor_RequiresFloor30AndWin()
        {
            var won = AchievementChecker.CheckAchievements(floor: 30, skillUsed: true, currentBPM: 100, gameWon: true);
            Assert.IsTrue(won.Contains(Achievement.Survivor));
        }

        [Test]
        public void Lost_DoesNotUnlockWinGatedAchievements()
        {
            // 敗北時: 到達系は付くが、勝利ゲート系 (FirstWin/NoSkillWin/SpeedRunner/Survivor) は付かない
            var r = AchievementChecker.CheckAchievements(floor: 100, skillUsed: false, currentBPM: 200, gameWon: false);
            Assert.IsTrue(r.Contains(Achievement.Floor100));
            Assert.IsFalse(r.Contains(Achievement.FirstWin), "FirstWin(ゲームクリア)は勝利限定 — 旧実装は floor>=1 で誤って敗北でも付いていた");
            Assert.IsFalse(r.Contains(Achievement.NoSkillWin));
            Assert.IsFalse(r.Contains(Achievement.SpeedRunner));
            Assert.IsFalse(r.Contains(Achievement.Survivor));
        }

        [Test]
        public void Lost_AtFloor50_UnlocksFloorReachUpTo50()
        {
            // 敗北でも死亡階層に応じて到達実績が解除される (「死んだら実績が付かない」= 呼び出し側が
            // 勝利時しか CheckAchievements を呼んでいなかったバグ の修正点を担保)。
            var r = AchievementChecker.CheckAchievements(floor: 50, skillUsed: true, currentBPM: 100, gameWon: false);
            Assert.IsTrue(r.Contains(Achievement.Floor10));
            Assert.IsTrue(r.Contains(Achievement.Floor25));
            Assert.IsTrue(r.Contains(Achievement.Floor50));
            Assert.IsFalse(r.Contains(Achievement.Floor75));
            Assert.IsFalse(r.Contains(Achievement.FirstWin));
        }

        [Test]
        public void Won_UnlocksFirstWin()
        {
            var r = AchievementChecker.CheckAchievements(floor: 100, skillUsed: true, currentBPM: 200, gameWon: true);
            Assert.IsTrue(r.Contains(Achievement.FirstWin));
        }
    }
}
