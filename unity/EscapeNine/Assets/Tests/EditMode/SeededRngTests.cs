// SeededRngTests.cs
// Swift 正本 (DailyChallengeService.SeededRNG) と同一の LCG 系列・シード算出を担保。

using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class SeededRngTests
    {
        [Test]
        public void SeedFromDateString_SumsCharCodes()
        {
            // "2026-01-01" の文字コード総和 = 486
            Assert.AreEqual(486, SeededRng.SeedFromDateString("2026-01-01"));
        }

        [Test]
        public void NextInt_FirstValueForSeed486()
        {
            var rng = new SeededRng(486);
            // (486 * 1664525 + 1013904223) & 0x7fffffff = 1822863373
            Assert.AreEqual(1822863373, rng.NextInt());
        }

        [Test]
        public void NextInt_IsDeterministicForSameSeed()
        {
            var a = new SeededRng(486);
            var b = new SeededRng(486);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(a.NextInt(), b.NextInt());
            }
        }

        [Test]
        public void NextInt_AlwaysNonNegativeAndBounded()
        {
            var rng = new SeededRng(1);
            for (int i = 0; i < 100; i++)
            {
                int v = rng.NextInt();
                Assert.GreaterOrEqual(v, 0);
                Assert.LessOrEqual(v, 0x7fffffff);
            }
        }
    }
}
