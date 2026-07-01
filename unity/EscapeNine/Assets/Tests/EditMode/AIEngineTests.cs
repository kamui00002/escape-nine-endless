// AIEngineTests.cs
// Swift 正本 (Services/AIEngine.swift) と同じ判断になることを担保する回帰テスト。
// 乱数は FakeRandom で固定し、決定論的に分岐を検証する。
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
    /// <summary>テスト用の決定論的乱数源。用意した値を順に返し、尽きたら最後の値を返す。</summary>
    internal sealed class FakeRandom : IRandomSource
    {
        private readonly Queue<double> _doubles;
        private readonly Queue<int> _ints;
        private double _lastDouble;
        private int _lastInt;

        public FakeRandom(double[] doubles = null, int[] ints = null)
        {
            _doubles = new Queue<double>(doubles ?? new double[0]);
            _ints = new Queue<int>(ints ?? new int[0]);
        }

        public double NextDouble()
        {
            if (_doubles.Count > 0) _lastDouble = _doubles.Dequeue();
            return _lastDouble;
        }

        public int NextInt(int maxExclusive)
        {
            if (_ints.Count > 0) _lastInt = _ints.Dequeue();
            return _lastInt % (maxExclusive <= 0 ? 1 : maxExclusive);
        }
    }

    public class AIEngineTests
    {
        [Test]
        public void NormalAI_MovesTowardPlayer()
        {
            var ai = new AIEngine(new FakeRandom());
            // enemy=9, player=1: available {6,8}, 両方距離3 → 先頭 6
            Assert.AreEqual(6, ai.CalculateNextMove(9, 1, AILevel.Normal));
            // enemy=2, player=8: available {5,1,3}, 5 が最短 (距離1)
            Assert.AreEqual(5, ai.CalculateNextMove(2, 8, AILevel.Normal));
        }

        [Test]
        public void BossAI_ChasesWhenRollBelowThreshold()
        {
            var ai = new AIEngine(new FakeRandom(doubles: new[] { 0.0 })); // roll < 0.95 → 追跡
            Assert.AreEqual(6, ai.CalculateNextMove(9, 1, AILevel.Boss));
        }

        [Test]
        public void BossAI_RandomWhenRollAboveThreshold()
        {
            // roll >= 0.95 → ランダム。NextInt=1 → available {6,8} の index1 = 8
            var ai = new AIEngine(new FakeRandom(doubles: new[] { 0.99 }, ints: new[] { 1 }));
            Assert.AreEqual(8, ai.CalculateNextMove(9, 1, AILevel.Boss));
        }

        [Test]
        public void EasyAI_ChaseBranch()
        {
            var ai = new AIEngine(new FakeRandom(doubles: new[] { 0.0 })); // < 0.15 → 追跡
            Assert.AreEqual(6, ai.CalculateNextMove(9, 1, AILevel.Easy));
        }

        [Test]
        public void EasyAI_FleeBranch()
        {
            // 0.15 <= roll < 0.35 → 逃走。enemy=5, player=9: available {2,8,4,6}
            // 距離(→9): 2=3, 8=1, 4=3, 6=1 → 最大3の先頭 = 2
            var ai = new AIEngine(new FakeRandom(doubles: new[] { 0.25 }));
            Assert.AreEqual(2, ai.CalculateNextMove(5, 9, AILevel.Easy));
        }

        [Test]
        public void EasyAI_RandomBranch()
        {
            // roll >= 0.35 → ランダム。enemy=5 available {2,8,4,6}, NextInt=2 → index2 = 4
            var ai = new AIEngine(new FakeRandom(doubles: new[] { 0.5 }, ints: new[] { 2 }));
            Assert.AreEqual(4, ai.CalculateNextMove(5, 9, AILevel.Easy));
        }

        [Test]
        public void HardAI_MovesTowardPredictedPlayerPosition()
        {
            var ai = new AIEngine(new FakeRandom());
            // enemy=9, player=1: 予測=プレイヤーが鬼から最も遠ざかる手。
            // player の手 {4,2} は両方 enemy(9) から距離3 → 先頭 4 を予測。
            // enemy の手 {6,8} で 4 に最も近いのは両方距離2 → 先頭 6。
            Assert.AreEqual(6, ai.CalculateNextMove(9, 1, AILevel.Hard));
        }
    }
}
