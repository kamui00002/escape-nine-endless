// RankingStoreTests.cs
// RankingStore (Swift: RankingService のローカル部分) の挿入順・整列・上限・永続化テスト。
// SetUp/TearDown で PlayerPrefs.DeleteAll を行い、既存キーに依存しない・残さない。

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using EscapeNine.Runtime;

namespace EscapeNine.Tests.EditMode
{
    public class RankingStoreTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
        }

        [Test]
        public void GetTopScore_Empty_ReturnsZero()
        {
            var store = new RankingStore();
            Assert.AreEqual(0, store.GetTopScore());
            Assert.AreEqual(0, store.GetRankings().Count);
        }

        [Test]
        public void SubmitScore_SortsByFloorDescending()
        {
            var store = new RankingStore();
            store.SubmitScore(5);
            store.SubmitScore(20);
            store.SubmitScore(12);

            var floors = store.GetRankings().Select(e => e.floor).ToArray();
            CollectionAssert.AreEqual(new[] { 20, 12, 5 }, floors);
            Assert.AreEqual(20, store.GetTopScore());
        }

        [Test]
        public void SubmitScore_EqualFloors_PreserveInsertionOrder()
        {
            // 安定ソート担保: 同階層は先に投稿した記録が上位に残る
            var store = new RankingStore();
            store.SubmitScore(10, "hero");
            store.SubmitScore(10, "thief");
            store.SubmitScore(10, "wizard");

            var characters = store.GetRankings().Select(e => e.characterType).ToArray();
            CollectionAssert.AreEqual(new[] { "hero", "thief", "wizard" }, characters);
        }

        [Test]
        public void SubmitScore_DefaultCharacterIsHero_AndPlayerNameFixed()
        {
            var store = new RankingStore();
            store.SubmitScore(3);

            var entry = store.GetRankings()[0];
            Assert.AreEqual("hero", entry.characterType); // Swift: デフォルト引数 "hero"
            Assert.AreEqual("あなた", entry.playerName);   // Swift: 固定名
            Assert.IsFalse(string.IsNullOrEmpty(entry.id));
            Assert.IsFalse(string.IsNullOrEmpty(entry.timestamp));
        }

        [Test]
        public void SubmitScore_TrimsToMaxEntries_DroppingLowest()
        {
            var store = new RankingStore();
            for (int floor = 1; floor <= 101; floor++)
            {
                store.SubmitScore(floor);
            }

            var rankings = store.GetRankings();
            Assert.AreEqual(100, rankings.Count);         // Swift: maxEntries = 100
            Assert.AreEqual(101, rankings[0].floor);      // 最高スコアは残る
            Assert.AreEqual(2, rankings[99].floor);       // 最下位 floor=1 が切り捨てられる
        }

        [Test]
        public void Persistence_RoundTrip_NewInstanceLoadsSavedEntries()
        {
            var store = new RankingStore();
            store.SubmitScore(7, "elf");
            store.SubmitScore(15, "knight");

            var reloaded = new RankingStore(); // 新インスタンスが PlayerPrefs から復元すること
            var rankings = reloaded.GetRankings();

            Assert.AreEqual(2, rankings.Count);
            Assert.AreEqual(15, rankings[0].floor);
            Assert.AreEqual("knight", rankings[0].characterType);
            Assert.AreEqual(7, rankings[1].floor);
            Assert.AreEqual("elf", rankings[1].characterType);
            Assert.AreEqual(15, reloaded.GetTopScore());
        }

        [Test]
        public void ClearRankings_EmptiesListAndStorage()
        {
            var store = new RankingStore();
            store.SubmitScore(9);
            store.ClearRankings();

            Assert.AreEqual(0, store.GetRankings().Count);
            Assert.AreEqual(0, store.GetTopScore());
            // ストレージも消えていること (新インスタンスが空で立ち上がる)
            Assert.AreEqual(0, new RankingStore().GetRankings().Count);
        }

        [Test]
        public void Load_CorruptedJson_FallsBackToEmpty()
        {
            PlayerPrefs.SetString("localRankings", "{ this is not json ");
            var store = new RankingStore();

            Assert.AreEqual(0, store.GetRankings().Count); // 壊れたデータでも例外で落ちない
        }
    }
}
