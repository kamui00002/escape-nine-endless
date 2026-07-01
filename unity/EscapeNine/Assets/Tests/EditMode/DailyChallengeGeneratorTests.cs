// DailyChallengeGeneratorTests.cs
// Swift 正本 (DailyChallengeService.buildChallenge) と同じく、日付から決定論的に
// 1〜2個の重複しない条件が生成されることを担保。

using System.Collections.Generic;
using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class DailyChallengeGeneratorTests
    {
        [Test]
        public void SameDate_ProducesIdenticalChallenge()
        {
            var a = DailyChallengeGenerator.BuildChallenge("2026-01-01");
            var b = DailyChallengeGenerator.BuildChallenge("2026-01-01");

            Assert.AreEqual(a.Conditions.Count, b.Conditions.Count);
            for (int i = 0; i < a.Conditions.Count; i++)
            {
                Assert.AreEqual(a.Conditions[i].Kind, b.Conditions[i].Kind);
                Assert.AreEqual(a.Conditions[i].Character, b.Conditions[i].Character);
                Assert.AreEqual(a.Conditions[i].AILevel, b.Conditions[i].AILevel);
                Assert.AreEqual(a.Conditions[i].Floor, b.Conditions[i].Floor);
            }
        }

        [Test]
        public void ConditionCount_IsOneOrTwo()
        {
            foreach (var date in new[] { "2026-01-01", "2026-03-17", "2026-07-01", "2026-12-31" })
            {
                var c = DailyChallengeGenerator.BuildChallenge(date);
                Assert.GreaterOrEqual(c.Conditions.Count, 1);
                Assert.LessOrEqual(c.Conditions.Count, 2);
            }
        }

        [Test]
        public void ConditionKinds_AreDistinct()
        {
            foreach (var date in new[] { "2026-01-01", "2026-03-17", "2026-07-01", "2026-12-31" })
            {
                var c = DailyChallengeGenerator.BuildChallenge(date);
                var kinds = new HashSet<ChallengeConditionKind>();
                foreach (var cond in c.Conditions)
                {
                    Assert.IsTrue(kinds.Add(cond.Kind), $"重複した条件種別: {cond.Kind} ({date})");
                }
            }
        }

        [Test]
        public void ForcedAI_IsEasyOrNormalOnly()
        {
            var floorOptions = new HashSet<int> { 5, 10, 15, 20, 25, 30, 35, 40 };
            for (int d = 1; d <= 28; d++)
            {
                var date = $"2026-02-{d:00}";
                var c = DailyChallengeGenerator.BuildChallenge(date);
                foreach (var cond in c.Conditions)
                {
                    if (cond.Kind == ChallengeConditionKind.ForcedAI)
                    {
                        Assert.IsTrue(cond.AILevel == AILevel.Easy || cond.AILevel == AILevel.Normal,
                            $"ForcedAI に Hard/Boss が混入: {cond.AILevel} ({date})");
                    }
                    if (cond.Kind == ChallengeConditionKind.StartFloor)
                    {
                        Assert.IsTrue(floorOptions.Contains(cond.Floor), $"想定外の startFloor: {cond.Floor} ({date})");
                    }
                }
            }
        }
    }
}
