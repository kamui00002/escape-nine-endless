// RelicDraftServiceTests.cs
// Unity Phase 5a「ローグライク深化」設計文書 §2.1 (ドラフトの仕組み) / §6.4 に基づくテスト。
// 固定シードでの決定性、重複禁止、スタック可・上限あり、プール枯渇時のフォールバックを検証する。
//
// FakeRandom は AIEngineTests.cs で定義されたもの (internal, 同一アセンブリ内で再利用可能)。

using System.Collections.Generic;
using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class RelicDraftServiceTests
    {
        [Test]
        public void DraftCandidates_FixedSeed_IsDeterministic()
        {
            // Definitions5a のカタログ内部順 (RelicCatalog.All): shadow_footwork, afterimage_veil,
            // veteran_stance, grounding_charm, lantern_ring, safe_start, improvised_shield, heartbound_pact
            var service1 = new RelicDraftService(new FakeRandom(ints: new[] { 2, 0, 3 }));
            var result1 = service1.DraftCandidates(new List<string>(), CharacterType.Hero, count: 3);

            var service2 = new RelicDraftService(new FakeRandom(ints: new[] { 2, 0, 3 }));
            var result2 = service2.DraftCandidates(new List<string>(), CharacterType.Hero, count: 3);

            Assert.AreEqual(3, result1.Count);
            for (int i = 0; i < result1.Count; i++)
            {
                Assert.AreEqual(result1[i].Id, result2[i].Id, "同じ乱数列なら同じ候補が同じ順で出るはず");
            }

            // 手計算: eligible=[shadow_footwork(0),afterimage_veil(1),veteran_stance(2),grounding_charm(3),
            // lantern_ring(4),safe_start(5),improvised_shield(6),heartbound_pact(7)]
            // draw1: idx=2 -> veteran_stance (残り7件から afterimage_veil 以降のインデックスが1つ詰める)
            // draw2: idx=0 -> shadow_footwork
            // draw3: idx=3 -> safe_start
            Assert.AreEqual(RelicCatalog.VeteranStanceId, result1[0].Id);
            Assert.AreEqual(RelicCatalog.ShadowFootworkId, result1[1].Id);
            Assert.AreEqual(RelicCatalog.SafeStartId, result1[2].Id);
        }

        [Test]
        public void DraftCandidates_NoDuplicatesWithinSingleDraft()
        {
            var service = new RelicDraftService(new FakeRandom(ints: new[] { 0, 0, 0, 0, 0, 0, 0, 0 }));
            var result = service.DraftCandidates(new List<string>(), CharacterType.Hero, count: 8);

            var seen = new HashSet<string>();
            foreach (var def in result)
            {
                Assert.IsTrue(seen.Add(def.Id), $"{def.Id} が同一ドラフト内で重複した");
            }
            Assert.AreEqual(8, result.Count);
        }

        [Test]
        public void DraftCandidates_ExcludesNonStackableRelicOnceOwned()
        {
            var owned = new List<string> { RelicCatalog.VeteranStanceId }; // stackLimit=1
            var service = new RelicDraftService(new FakeRandom(ints: new[] { 0, 0, 0, 0, 0, 0, 0 }));
            var result = service.DraftCandidates(owned, CharacterType.Hero, count: 8);

            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.VeteranStanceId), "スタック不可レリックは1回所持したら候補から除外される");
            Assert.AreEqual(7, result.Count, "残り7種は引き続き候補になる");
        }

        [Test]
        public void DraftCandidates_StackableRelic_EligibleBelowCap_ExcludedAtCap()
        {
            // #8 灯火の指輪 (lantern_ring) は stackLimit=3
            var owned2 = new List<string> { RelicCatalog.LanternRingId, RelicCatalog.LanternRingId };
            var serviceBelowCap = new RelicDraftService(new FakeRandom(ints: new[] { 0, 0, 0, 0, 0, 0, 0, 0 }));
            var resultBelowCap = serviceBelowCap.DraftCandidates(owned2, CharacterType.Hero, count: 8);
            Assert.IsTrue(resultBelowCap.Exists(d => d.Id == RelicCatalog.LanternRingId), "2個所持 (上限3未満) ではまだ候補に入る");

            var owned3 = new List<string> { RelicCatalog.LanternRingId, RelicCatalog.LanternRingId, RelicCatalog.LanternRingId };
            var serviceAtCap = new RelicDraftService(new FakeRandom(ints: new[] { 0, 0, 0, 0, 0, 0, 0 }));
            var resultAtCap = serviceAtCap.DraftCandidates(owned3, CharacterType.Hero, count: 8);
            Assert.IsFalse(resultAtCap.Exists(d => d.Id == RelicCatalog.LanternRingId), "3個所持 (上限到達) では候補から除外される");
            Assert.AreEqual(7, resultAtCap.Count);
        }

        [Test]
        public void DraftCandidates_PoolExhaustion_ReturnsFewerThanRequested()
        {
            var pool = new List<RelicDefinition>
            {
                new RelicDefinition("a", "A", "", RelicRarity.Common, RelicTag.General, stackLimit: 1, applyDelta: e => { }),
                new RelicDefinition("b", "B", "", RelicRarity.Common, RelicTag.General, stackLimit: 1, applyDelta: e => { }),
            };
            var service = new RelicDraftService(new FakeRandom(), pool);

            // どちらも既に上限まで所持 -> 候補は0件
            var owned = new List<string> { "a", "b" };
            var result = service.DraftCandidates(owned, CharacterType.Hero, count: 3);
            Assert.AreEqual(0, result.Count, "プールが完全に枯渇した場合は空リストを返す");
        }

        [Test]
        public void DraftCandidates_PoolPartialExhaustion_ReturnsOnlyRemainingEligible()
        {
            var pool = new List<RelicDefinition>
            {
                new RelicDefinition("a", "A", "", RelicRarity.Common, RelicTag.General, stackLimit: 1, applyDelta: e => { }),
                new RelicDefinition("b", "B", "", RelicRarity.Common, RelicTag.General, stackLimit: 1, applyDelta: e => { }),
            };
            var service = new RelicDraftService(new FakeRandom(ints: new[] { 0 }), pool);

            var owned = new List<string> { "a" }; // aは上限到達、bのみ残る
            var result = service.DraftCandidates(owned, CharacterType.Hero, count: 3);

            Assert.AreEqual(1, result.Count, "候補数が3未満でも要求数まで待たず、残っている分だけ返す");
            Assert.AreEqual("b", result[0].Id);
        }

        [Test]
        public void DraftCandidates_DefaultsToRelicCatalogPool()
        {
            var service = new RelicDraftService(new FakeRandom());
            var result = service.DraftCandidates(new List<string>(), CharacterType.Hero, count: 3);

            Assert.AreEqual(3, result.Count);
            foreach (var def in result)
            {
                Assert.IsNotNull(RelicCatalog.Find(def.Id), "既定プールは RelicCatalog.All (5aの8種) であるはず");
            }
        }
    }
}
