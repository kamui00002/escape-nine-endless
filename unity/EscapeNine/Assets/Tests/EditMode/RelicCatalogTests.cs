// RelicCatalogTests.cs
// Unity Phase 5b「ローグライク深化」設計文書 §2.3 (レリック一覧18種) との 1:1 対応を検証する。
// 各レリックの ID / レアリティ / 弱点タグ / スタック上限 / ApplyTo の効果デルタが
// 設計書の表と一致していることをアサートする (表がスペック、テストはその写し)。

using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class RelicCatalogTests
    {
        [Test]
        public void Catalog_HasExactly18UniqueDefinitions()
        {
            Assert.AreEqual(18, RelicCatalog.All.Count, "§2.3 の18種と1:1のはず");

            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var def in RelicCatalog.All)
            {
                Assert.IsTrue(seen.Add(def.Id), $"ID重複: {def.Id}");
            }
        }

        [Test]
        public void Catalog_Find_ReturnsDefinitionForEveryId_AndNullForUnknown()
        {
            foreach (var def in RelicCatalog.All)
            {
                var found = RelicCatalog.Find(def.Id);
                Assert.IsNotNull(found);
                Assert.AreEqual(def.Id, found.Value.Id);
            }
            Assert.IsNull(RelicCatalog.Find("no_such_relic"));
        }

        private static RelicEffects Apply(string id)
        {
            var def = RelicCatalog.Find(id);
            Assert.IsNotNull(def, $"カタログに {id} が存在しない");
            var effects = RelicEffects.None;
            def.Value.ApplyTo(effects);
            return effects;
        }

        private static void AssertMeta(string id, RelicRarity rarity, RelicTag tags, int stackLimit = 1)
        {
            var def = RelicCatalog.Find(id).Value;
            Assert.AreEqual(rarity, def.Rarity, $"{id}: レアリティが§2.3の表と不一致");
            Assert.AreEqual(tags, def.Tags, $"{id}: 弱点タグが§2.3の表と不一致");
            Assert.AreEqual(stackLimit, def.StackLimit, $"{id}: スタック上限が不一致");
        }

        // --- §2.3 の表と1:1 (メタ情報 + 効果デルタ) ---

        [Test]
        public void Relic01_ShadowFootwork()
        {
            AssertMeta(RelicCatalog.ShadowFootworkId, RelicRarity.Common, RelicTag.ThiefRescue);
            Assert.AreEqual(0.25, Apply(RelicCatalog.ShadowFootworkId).ThiefDiagonalSkillSaveChance, 1e-9);
        }

        [Test]
        public void Relic02_AfterimageVeil()
        {
            AssertMeta(RelicCatalog.AfterimageVeilId, RelicRarity.Rare, RelicTag.ThiefRescue | RelicTag.HardAICounter);
            Assert.IsTrue(Apply(RelicCatalog.AfterimageVeilId).ThiefResidualVeil);
        }

        [Test]
        public void Relic03_ShadowCloneArt()
        {
            AssertMeta(RelicCatalog.ShadowCloneArtId, RelicRarity.Epic, RelicTag.ThiefRescue);
            Assert.AreEqual(3, Apply(RelicCatalog.ShadowCloneArtId).ThiefSkillMaxUsageBonus);
        }

        [Test]
        public void Relic04_VeteranStance()
        {
            AssertMeta(RelicCatalog.VeteranStanceId, RelicRarity.Uncommon, RelicTag.General | RelicTag.Safety);
            Assert.AreEqual(1, Apply(RelicCatalog.VeteranStanceId).TurnCountReduction);
        }

        [Test]
        public void Relic05_PhoenixEmber()
        {
            AssertMeta(RelicCatalog.PhoenixEmberId, RelicRarity.Epic, RelicTag.Safety);
            Assert.AreEqual(1, Apply(RelicCatalog.PhoenixEmberId).ReviveCharges);
        }

        [Test]
        public void Relic06_ComboWard()
        {
            AssertMeta(RelicCatalog.ComboWardId, RelicRarity.Rare, RelicTag.Score);
            Assert.AreEqual(1, Apply(RelicCatalog.ComboWardId).ComboMissShieldCharges);
        }

        [Test]
        public void Relic07_GroundingCharm()
        {
            AssertMeta(RelicCatalog.GroundingCharmId, RelicRarity.Uncommon, RelicTag.LateGame);
            Assert.AreEqual(1, Apply(RelicCatalog.GroundingCharmId).DisappearCellReduction);
        }

        [Test]
        public void Relic08_LanternRing_Stackable3()
        {
            AssertMeta(RelicCatalog.LanternRingId, RelicRarity.Common, RelicTag.LateGame, stackLimit: 3);
            Assert.AreEqual(1, Apply(RelicCatalog.LanternRingId).FogVisibilityRadiusBonus);
        }

        [Test]
        public void Relic09_BewilderingDust()
        {
            AssertMeta(RelicCatalog.BewilderingDustId, RelicRarity.Epic, RelicTag.HardAICounter);
            Assert.IsTrue(Apply(RelicCatalog.BewilderingDustId).NeutralizeHardPrediction);
        }

        [Test]
        public void Relic10_SafeStart()
        {
            AssertMeta(RelicCatalog.SafeStartId, RelicRarity.Common, RelicTag.General | RelicTag.Safety);
            Assert.AreEqual(2, Apply(RelicCatalog.SafeStartId).MinStartDistance);
        }

        [Test]
        public void Relic11_ShadowPassage()
        {
            AssertMeta(RelicCatalog.ShadowPassageId, RelicRarity.Rare, RelicTag.LateGame | RelicTag.Safety);
            Assert.AreEqual(1, Apply(RelicCatalog.ShadowPassageId).DisappearForgivenessPerFloor);
        }

        [Test]
        public void Relic12_ImprovisedShield()
        {
            AssertMeta(RelicCatalog.ImprovisedShieldId, RelicRarity.Rare, RelicTag.Safety);
            Assert.AreEqual(1, Apply(RelicCatalog.ImprovisedShieldId).GenericShieldCharges);
        }

        [Test]
        public void Relic13_ReserveBreath()
        {
            AssertMeta(RelicCatalog.ReserveBreathId, RelicRarity.Uncommon, RelicTag.General);
            Assert.AreEqual(1, Apply(RelicCatalog.ReserveBreathId).SkillMaxUsageBonus);
        }

        [Test]
        public void Relic14_ChainProof()
        {
            AssertMeta(RelicCatalog.ChainProofId, RelicRarity.Common, RelicTag.Score);
            Assert.AreEqual(1, Apply(RelicCatalog.ChainProofId).ComboThresholdReduction);
        }

        [Test]
        public void Relic15_AccelProof_RiskReward()
        {
            AssertMeta(RelicCatalog.AccelProofId, RelicRarity.Uncommon, RelicTag.Score);
            var e = Apply(RelicCatalog.AccelProofId);
            Assert.AreEqual(0.08, e.BpmMultiplierBonus, 1e-9, "BPM +8% (リスク)");
            Assert.AreEqual(0.5, e.ComboThresholdBonusMultiplier, 1e-9, "しきい値到達時の倍率 +0.5 (リワード)");
        }

        [Test]
        public void Relic16_GraceOfTime()
        {
            AssertMeta(RelicCatalog.GraceOfTimeId, RelicRarity.Rare, RelicTag.LateGame);
            Assert.AreEqual(1, Apply(RelicCatalog.GraceOfTimeId).TurnCountdownBonus);
        }

        [Test]
        public void Relic17_HeartboundPact()
        {
            AssertMeta(RelicCatalog.HeartboundPactId, RelicRarity.Rare, RelicTag.HardAICounter | RelicTag.General);
            Assert.AreEqual(2, Apply(RelicCatalog.HeartboundPactId).PseudoBindCharges);
        }

        [Test]
        public void Relic18_CollectorsEye()
        {
            AssertMeta(RelicCatalog.CollectorsEyeId, RelicRarity.Legendary, RelicTag.General);
            Assert.AreEqual(3, Apply(RelicCatalog.CollectorsEyeId).DraftCandidateBonusFloorsRemaining);
        }

        [Test]
        public void EveryWeaknessTag_HasAtLeastOneRelic()
        {
            // §2.2 の全タグがプール内に代表を持つこと (重み付けの効き先が存在する保証)。
            foreach (RelicTag tag in new[]
                     {
                         RelicTag.ThiefRescue, RelicTag.HardAICounter, RelicTag.LateGame,
                         RelicTag.General, RelicTag.Safety, RelicTag.Score
                     })
            {
                bool found = false;
                foreach (var def in RelicCatalog.All)
                {
                    if ((def.Tags & tag) != 0) { found = true; break; }
                }
                Assert.IsTrue(found, $"タグ {tag} を持つレリックがカタログに1つも無い");
            }
        }
    }

    public class MetaProgressionCalculatorTests
    {
        [Test]
        public void CalculateGlow_MatchesSpecFormula()
        {
            // §3.1: 残光 = 到達階層 + floor(到達階層/5)*2 + (勝利+100) + (デイリー達成+20)
            Assert.AreEqual(1, MetaProgressionCalculator.CalculateGlow(1, false, false));      // 1 + 0
            Assert.AreEqual(5 + 2, MetaProgressionCalculator.CalculateGlow(5, false, false));  // 5 + 1*2
            Assert.AreEqual(9 + 2, MetaProgressionCalculator.CalculateGlow(9, false, false));  // 9 + 1*2
            Assert.AreEqual(50 + 20, MetaProgressionCalculator.CalculateGlow(50, false, false)); // 50 + 10*2
            // 勝利ラン: CurrentFloor=101 のまま渡す (PersistRunResult とのパリティ)
            Assert.AreEqual(101 + 40 + 100, MetaProgressionCalculator.CalculateGlow(101, true, false));
            Assert.AreEqual(101 + 40 + 100 + 20, MetaProgressionCalculator.CalculateGlow(101, true, true));
        }

        [Test]
        public void CalculateGlow_NegativeOrZeroFloor_ClampsToZeroBase()
        {
            Assert.AreEqual(0, MetaProgressionCalculator.CalculateGlow(0, false, false));
            Assert.AreEqual(0, MetaProgressionCalculator.CalculateGlow(-5, false, false));
        }
    }
}
