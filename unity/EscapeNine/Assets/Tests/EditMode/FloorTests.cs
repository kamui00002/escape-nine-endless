// FloorTests.cs
// Swift 正本 (Models/Floor.swift) と同じ入出力になることを担保する回帰テスト。
// 期待値は docs/game-spec.md の BPM 表 (Floor 1=70, 25=88, 50=119, 75=156, 100=200) と一致。

using System;
using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class FloorTests
    {
        [Test]
        public void CalculateBPM_Endpoints_AreExact()
        {
            Assert.AreEqual(70.0, Floor.CalculateBPM(1), 1e-9);
            Assert.AreEqual(200.0, Floor.CalculateBPM(100), 1e-9);
        }

        [Test]
        public void CalculateBPM_Prologue_Is60()
        {
            Assert.AreEqual(60.0, Floor.CalculateBPM(0), 1e-9);
        }

        [Test]
        public void CalculateBPM_MatchesSpecTable()
        {
            Assert.AreEqual(88, Math.Round(Floor.CalculateBPM(25)));
            Assert.AreEqual(119, Math.Round(Floor.CalculateBPM(50)));
            Assert.AreEqual(156, Math.Round(Floor.CalculateBPM(75)));
        }

        [Test]
        public void CalculateBPM_ClampsOutOfRange()
        {
            Assert.AreEqual(200.0, Floor.CalculateBPM(999), 1e-9); // 上限 clamp
            Assert.AreEqual(70.0, Floor.CalculateBPM(1), 1e-9);
        }

        [Test]
        public void IsBossFloor_MultiplesOfTen_ExceptZero()
        {
            Assert.IsTrue(Floor.IsBossFloor(10));
            Assert.IsTrue(Floor.IsBossFloor(20));
            Assert.IsTrue(Floor.IsBossFloor(100));
            Assert.IsFalse(Floor.IsBossFloor(0)); // プロローグは boss にしない
            Assert.IsFalse(Floor.IsBossFloor(5));
            Assert.IsFalse(Floor.IsBossFloor(1));
        }

        [Test]
        public void GetSpecialRule_RangesMatchSpec()
        {
            Assert.AreEqual(SpecialRule.None, Floor.GetSpecialRule(20));
            Assert.AreEqual(SpecialRule.Fog, Floor.GetSpecialRule(21));
            Assert.AreEqual(SpecialRule.Fog, Floor.GetSpecialRule(40));
            Assert.AreEqual(SpecialRule.Disappear, Floor.GetSpecialRule(41));
            Assert.AreEqual(SpecialRule.Disappear, Floor.GetSpecialRule(60));
            Assert.AreEqual(SpecialRule.FogDisappear, Floor.GetSpecialRule(61));
            Assert.AreEqual(SpecialRule.FogDisappear, Floor.GetSpecialRule(100));
        }

        [Test]
        public void GetNaturalDifficulty_Thresholds()
        {
            Assert.AreEqual(AILevel.Easy, Floor.GetNaturalDifficulty(1));
            Assert.AreEqual(AILevel.Easy, Floor.GetNaturalDifficulty(15));
            Assert.AreEqual(AILevel.Normal, Floor.GetNaturalDifficulty(16));
            Assert.AreEqual(AILevel.Normal, Floor.GetNaturalDifficulty(35));
            Assert.AreEqual(AILevel.Hard, Floor.GetNaturalDifficulty(36));
            Assert.AreEqual(AILevel.Hard, Floor.GetNaturalDifficulty(100));
        }

        [Test]
        public void GetEffectiveAILevel_BossFloorOverridesSelection()
        {
            Assert.AreEqual(AILevel.Boss, Floor.GetEffectiveAILevel(10, AILevel.Easy));
            Assert.AreEqual(AILevel.Boss, Floor.GetEffectiveAILevel(10, AILevel.Normal));
        }

        [Test]
        public void GetEffectiveAILevel_SelectionAdjustsByOneStep()
        {
            // Floor 1: natural Easy
            Assert.AreEqual(AILevel.Easy, Floor.GetEffectiveAILevel(1, AILevel.Easy));
            Assert.AreEqual(AILevel.Easy, Floor.GetEffectiveAILevel(1, AILevel.Normal));
            Assert.AreEqual(AILevel.Normal, Floor.GetEffectiveAILevel(1, AILevel.Hard));

            // Floor 16: natural Normal
            Assert.AreEqual(AILevel.Easy, Floor.GetEffectiveAILevel(16, AILevel.Easy));
            Assert.AreEqual(AILevel.Normal, Floor.GetEffectiveAILevel(16, AILevel.Normal));
            Assert.AreEqual(AILevel.Hard, Floor.GetEffectiveAILevel(16, AILevel.Hard));

            // Floor 36: natural Hard
            Assert.AreEqual(AILevel.Normal, Floor.GetEffectiveAILevel(36, AILevel.Easy));
            Assert.AreEqual(AILevel.Hard, Floor.GetEffectiveAILevel(36, AILevel.Normal));
            Assert.AreEqual(AILevel.Hard, Floor.GetEffectiveAILevel(36, AILevel.Hard));
        }

        [Test]
        public void GetMaxTurns_IncrementsEveryTenFloors()
        {
            Assert.AreEqual(5, GameConfig.GetMaxTurns(1));
            Assert.AreEqual(5, GameConfig.GetMaxTurns(10));
            Assert.AreEqual(6, GameConfig.GetMaxTurns(11));
            Assert.AreEqual(14, GameConfig.GetMaxTurns(100));
        }

        [Test]
        public void GetEnemySprite_RangesMatchSpec()
        {
            Assert.AreEqual("red_oni", Floor.GetEnemySprite(1));
            Assert.AreEqual("red_oni", Floor.GetEnemySprite(25));
            Assert.AreEqual("blue_oni", Floor.GetEnemySprite(26));
            Assert.AreEqual("skeleton", Floor.GetEnemySprite(51));
            Assert.AreEqual("dragon", Floor.GetEnemySprite(76));
            Assert.AreEqual("dragon", Floor.GetEnemySprite(100));
        }
    }
}
