// PlayerStateTests.cs
// PlayerState (Swift: PlayerViewModel の PlayerPrefs 移植) の読み書き・デフォルト値・
// キャラ解放ロジックのテスト。
// SetUp/TearDown で PlayerPrefs.DeleteAll を行い、既存キーに依存しない・残さない。

using NUnit.Framework;
using UnityEngine;
using EscapeNine.Core;
using EscapeNine.Runtime;

namespace EscapeNine.Tests.EditMode
{
    public class PlayerStateTests
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

        // MARK: - デフォルト値 (Swift の初期値と一致すること)

        [Test]
        public void Defaults_MatchSwiftInitialValues()
        {
            var state = new PlayerState();

            Assert.AreEqual(0, state.HighestFloor);
            CollectionAssert.AreEqual(new[] { CharacterType.Hero }, state.UnlockedCharacters);
            Assert.AreEqual(CharacterType.Hero, state.SelectedCharacter);
            Assert.IsFalse(state.AdRemoved);
            Assert.AreEqual((float)GameConfig.DefaultVolume, state.BgmVolume, 1e-4f);
            Assert.AreEqual((float)GameConfig.DefaultVolume, state.SfxVolume, 1e-4f);
            Assert.IsTrue(state.IsBgmEnabled);
            Assert.IsTrue(state.IsSfxEnabled);
            Assert.IsFalse(state.HasSeenTutorial);
            Assert.IsFalse(state.HasSeenTutorialV11);
            Assert.IsTrue(state.OneTapRetryEnabled);
            Assert.IsTrue(state.HapticsEnabled);
            Assert.AreEqual(AILevel.Easy, state.SelectedAILevel);
            Assert.AreEqual(0, state.PurchasedProducts.Count);
        }

        [Test]
        public void Defaults_DebugValuesMatchSwift()
        {
            var state = new PlayerState();

            Assert.AreEqual(1, state.DebugStartFloor);
            Assert.AreEqual(AILevel.Normal, state.DebugAILevel);
            Assert.IsFalse(state.DebugUnlockAllCharacters);
            Assert.AreEqual(0f, state.DebugBPMOverride, 1e-4f);
            Assert.AreEqual(GameConfig.TurnCountdownBeats, state.DebugTurnCountdownBeats);
            Assert.IsFalse(state.DebugSkipStartCountdown);
        }

        // MARK: - 保存 → 再読込 (ラウンドトリップ)

        [Test]
        public void SaveAndReload_RoundTripsAllValues()
        {
            var state = new PlayerState();
            state.HighestFloor = 42;
            state.UnlockedCharacters.Add(CharacterType.Thief);
            state.SelectedCharacter = CharacterType.Thief;
            state.AdRemoved = true;
            state.BgmVolume = 0.3f;
            state.SfxVolume = 0.9f;
            state.HasSeenTutorial = true;
            state.HasSeenTutorialV11 = true;
            state.SelectedAILevel = AILevel.Hard;
            state.Save();

            var reloaded = new PlayerState(); // 新インスタンスが PlayerPrefs から復元すること

            Assert.AreEqual(42, reloaded.HighestFloor);
            CollectionAssert.AreEqual(
                new[] { CharacterType.Hero, CharacterType.Thief },
                reloaded.UnlockedCharacters);
            Assert.AreEqual(CharacterType.Thief, reloaded.SelectedCharacter);
            Assert.IsTrue(reloaded.AdRemoved);
            Assert.AreEqual(0.3f, reloaded.BgmVolume, 1e-4f);
            Assert.AreEqual(0.9f, reloaded.SfxVolume, 1e-4f);
            Assert.IsTrue(reloaded.HasSeenTutorial);
            Assert.IsTrue(reloaded.HasSeenTutorialV11);
            Assert.AreEqual(AILevel.Hard, reloaded.SelectedAILevel);
        }

        // MARK: - 最高階層更新とキャラ解放 (Swift: updateHighestFloor)

        [Test]
        public void UpdateHighestFloor_UnlocksThiefAtThreshold()
        {
            var state = new PlayerState();
            state.UpdateHighestFloor(GameConfig.ThiefUnlockFloor); // 10

            Assert.AreEqual(GameConfig.ThiefUnlockFloor, state.HighestFloor);
            Assert.IsTrue(state.IsCharacterUnlocked(CharacterType.Thief));

            // 永続化も確認
            var reloaded = new PlayerState();
            Assert.IsTrue(reloaded.IsCharacterUnlocked(CharacterType.Thief));
        }

        [Test]
        public void UpdateHighestFloor_BelowThreshold_DoesNotUnlockThief()
        {
            var state = new PlayerState();
            state.UpdateHighestFloor(GameConfig.ThiefUnlockFloor - 1); // 9

            Assert.IsFalse(state.IsCharacterUnlocked(CharacterType.Thief));
        }

        [Test]
        public void UpdateHighestFloor_IgnoresLowerFloor()
        {
            var state = new PlayerState();
            state.UpdateHighestFloor(30);
            state.UpdateHighestFloor(20); // 低い値では更新しない (Swift と同じ guard)

            Assert.AreEqual(30, state.HighestFloor);
        }

        // MARK: - キャラ選択 (Swift: selectCharacter — アンロック済みのみ)

        [Test]
        public void SelectCharacter_LockedCharacter_Fails()
        {
            var state = new PlayerState();
            bool ok = state.SelectCharacter(CharacterType.Wizard);

            Assert.IsFalse(ok);
            Assert.AreEqual(CharacterType.Hero, state.SelectedCharacter);
        }

        [Test]
        public void SelectCharacter_UnlockedCharacter_SucceedsAndPersists()
        {
            var state = new PlayerState();
            state.UnlockCharacter(CharacterType.Wizard);
            bool ok = state.SelectCharacter(CharacterType.Wizard);

            Assert.IsTrue(ok);
            Assert.AreEqual(CharacterType.Wizard, new PlayerState().SelectedCharacter);
        }

        // MARK: - 購入 (Swift: StoreKitService/PurchaseManager のローカル反映)

        [Test]
        public void AddPurchasedProduct_RemoveAds_SetsAdRemovedAndPersists()
        {
            var state = new PlayerState();
            state.AddPurchasedProduct(PlayerState.ProductRemoveAds);

            Assert.IsTrue(state.AdRemoved);
            Assert.IsTrue(state.IsPurchased(PlayerState.ProductRemoveAds));

            var reloaded = new PlayerState();
            Assert.IsTrue(reloaded.AdRemoved);
            Assert.IsTrue(reloaded.IsPurchased(PlayerState.ProductRemoveAds));
        }

        [Test]
        public void AddPurchasedProduct_CharacterProduct_UnlocksCharacter()
        {
            var state = new PlayerState();
            state.AddPurchasedProduct(PlayerState.ProductElf);

            Assert.IsTrue(state.IsCharacterUnlocked(CharacterType.Elf));
            Assert.IsTrue(new PlayerState().IsCharacterUnlocked(CharacterType.Elf));
        }

        [Test]
        public void PurchasedProducts_MultipleIds_RoundTrip()
        {
            var state = new PlayerState();
            state.AddPurchasedProduct(PlayerState.ProductWizard);
            state.AddPurchasedProduct(PlayerState.ProductKnight);

            var reloaded = new PlayerState();
            Assert.IsTrue(reloaded.IsPurchased(PlayerState.ProductWizard));
            Assert.IsTrue(reloaded.IsPurchased(PlayerState.ProductKnight));
            Assert.IsFalse(reloaded.IsPurchased(PlayerState.ProductElf));
        }

        // MARK: - 不正データ耐性

        [Test]
        public void Load_UnknownCharacterRawValue_IsSkipped()
        {
            // Swift の compactMap 相当: 未知 rawValue は読み飛ばす
            PlayerPrefs.SetString("unlockedCharacters", "hero,ninja,thief");
            var state = new PlayerState();

            CollectionAssert.AreEqual(
                new[] { CharacterType.Hero, CharacterType.Thief },
                state.UnlockedCharacters);
        }
    }
}
