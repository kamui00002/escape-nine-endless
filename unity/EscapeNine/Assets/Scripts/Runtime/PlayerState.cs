// PlayerState.cs
// Swift 正本からの忠実移植: ViewModels/PlayerViewModel.swift (+ AudioManager.swift の音量系 /
// HomeView.swift の @AppStorage / StoreKitService.swift の購入永続化)。
//
// キー名は Swift の UserDefaults キーをそのまま踏襲する (将来 iOS 版とのセーブ互換・
// 移行スクリプトを書きやすくするため)。
// PlayerPrefs は配列を保存できないため、unlockedCharacters / purchasedProductIDs は
// カンマ区切り文字列にシリアライズする (rawValue に ',' は含まれない前提)。
//
// Swift 同様「明示的 Save()」方式: プロパティ変更はメモリ上のみで、Save() 呼び出しで
// PlayerPrefs へ書き込む (PlayerViewModel.saveData() 相当)。

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EscapeNine.Core;

namespace EscapeNine.Runtime
{
    public sealed class PlayerState
    {
        // MARK: - UserDefaults Keys (Swift: PlayerViewModel / AudioManager / @AppStorage を踏襲)
        private const string HighestFloorKey = "highestFloor";
        private const string UnlockedCharactersKey = "unlockedCharacters";
        private const string SelectedCharacterKey = "selectedCharacter";
        private const string AdRemovedKey = "adRemoved";
        private const string BgmVolumeKey = "bgmVolume";
        private const string SeVolumeKey = "seVolume"; // Swift: PlayerViewModel は "seVolume" (AudioManager 側の "sfxVolume" とは別キーだった負債を PlayerViewModel 側に統一)
        private const string IsBgmEnabledKey = "isBGMEnabled";
        private const string IsSfxEnabledKey = "isSFXEnabled";
        private const string HasSeenTutorialKey = "hasSeenTutorial";
        private const string HasSeenTutorialV11Key = "hasSeenTutorialV1_1";
        private const string OneTapRetryEnabledKey = "oneTapRetryEnabled";
        private const string HapticsEnabledKey = "hapticsEnabled"; // Swift: HapticsHelper.storageKey
        private const string PurchasedProductsKey = "purchasedProductIDs"; // Swift は Keychain 保存。Unity は PlayerPrefs (Phase 3 で StoreKit/IAP 導入時にセキュア化を検討)
        private const string AILevelKey = "aiLevel"; // Swift は非永続 (GameView の @State)。Unity では画面間受け渡しのため永続化 (意図的差分)

        // Debug 系 (Swift: #if DEBUG の debug* プロパティ。Unity では Editor / Development Build から使う)
        private const string DebugStartFloorKey = "debugStartFloor";
        private const string DebugAILevelKey = "debugAILevel";
        private const string DebugUnlockAllCharactersKey = "debugUnlockAllCharacters";
        private const string DebugBPMOverrideKey = "debugBPMOverride";
        private const string DebugTurnCountdownBeatsKey = "debugTurnCountdownBeats";
        private const string DebugSkipStartCountdownKey = "debugSkipStartCountdown";

        // MARK: - Product IDs (Swift: StoreKitService.ProductID)
        public const string ProductWizard = "com.escapenine.endless.character.wizard";
        public const string ProductElf = "com.escapenine.endless.character.elf";
        public const string ProductKnight = "com.escapenine.endless.character.knight";
        public const string ProductRemoveAds = "com.escapenine.endless.removeads";

        // MARK: - Properties (Swift: @Published 相当。全て読み書き可、永続化は Save())

        /// <summary>最高到達階層。「クリア済み階層」の正でもある (Swift に別キーは存在しない)。</summary>
        public int HighestFloor { get; set; }

        public List<CharacterType> UnlockedCharacters { get; set; } = new List<CharacterType> { CharacterType.Hero };

        public CharacterType SelectedCharacter { get; set; } = CharacterType.Hero;

        public bool AdRemoved { get; set; }

        /// <summary>BGM 音量 0..1。デフォルトは GameConfig.DefaultVolume (Swift: Constants.defaultVolume)。</summary>
        public float BgmVolume { get; set; } = (float)GameConfig.DefaultVolume;

        /// <summary>効果音音量 0..1。</summary>
        public float SfxVolume { get; set; } = (float)GameConfig.DefaultVolume;

        public bool IsBgmEnabled { get; set; } = true;
        public bool IsSfxEnabled { get; set; } = true;

        public bool HasSeenTutorial { get; set; }
        public bool HasSeenTutorialV11 { get; set; }
        public bool OneTapRetryEnabled { get; set; } = true;
        public bool HapticsEnabled { get; set; } = true;

        /// <summary>選択中の AI 難易度 (Home/Game 画面間の受け渡し用)。</summary>
        public AILevel SelectedAILevel { get; set; } = AILevel.Easy;

        /// <summary>購入済み商品 ID (広告削除含む)。Phase 3 で StoreKit/IAP 検証と接続する。</summary>
        public HashSet<string> PurchasedProducts { get; set; } = new HashSet<string>();

        // Debug (Swift: #if DEBUG。Unity では常時読み書き可能だがデバッグ UI からのみ触る想定)
        public int DebugStartFloor { get; set; } = 1;
        public AILevel DebugAILevel { get; set; } = AILevel.Normal;
        public bool DebugUnlockAllCharacters { get; set; }
        public float DebugBPMOverride { get; set; } // 0 = フロア曲線を使用
        public int DebugTurnCountdownBeats { get; set; } = GameConfig.TurnCountdownBeats;
        public bool DebugSkipStartCountdown { get; set; }

        // MARK: - Initialization

        public PlayerState()
        {
            Load();
        }

        /// <summary>他所が PlayerPrefs を更新した後の再読込。Swift: PlayerViewModel.reload()</summary>
        public void Reload() => Load();

        // MARK: - Persistence

        private void Load()
        {
            HighestFloor = PlayerPrefs.GetInt(HighestFloorKey, 0);

            // unlockedCharacters: 未知の rawValue は読み飛ばす (Swift の compactMap 相当)
            string unlockedRaw = PlayerPrefs.GetString(UnlockedCharactersKey, "");
            if (!string.IsNullOrEmpty(unlockedRaw))
            {
                var parsed = new List<CharacterType>();
                foreach (var token in unlockedRaw.Split(','))
                {
                    if (TryParseCharacter(token, out var c) && !parsed.Contains(c)) parsed.Add(c);
                }
                if (parsed.Count > 0) UnlockedCharacters = parsed;
            }

            if (TryParseCharacter(PlayerPrefs.GetString(SelectedCharacterKey, ""), out var selected))
            {
                SelectedCharacter = selected;
            }

            AdRemoved = GetBool(AdRemovedKey, false);
            BgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, (float)GameConfig.DefaultVolume);
            SfxVolume = PlayerPrefs.GetFloat(SeVolumeKey, (float)GameConfig.DefaultVolume);
            IsBgmEnabled = GetBool(IsBgmEnabledKey, true);
            IsSfxEnabled = GetBool(IsSfxEnabledKey, true);
            HasSeenTutorial = GetBool(HasSeenTutorialKey, false);
            HasSeenTutorialV11 = GetBool(HasSeenTutorialV11Key, false);
            OneTapRetryEnabled = GetBool(OneTapRetryEnabledKey, true);
            HapticsEnabled = GetBool(HapticsEnabledKey, true);
            SelectedAILevel = ParseAILevel(PlayerPrefs.GetString(AILevelKey, "Easy"), AILevel.Easy);

            string purchasedRaw = PlayerPrefs.GetString(PurchasedProductsKey, "");
            PurchasedProducts = string.IsNullOrEmpty(purchasedRaw)
                ? new HashSet<string>()
                : new HashSet<string>(purchasedRaw.Split(',').Where(s => !string.IsNullOrEmpty(s)));

            // Debug (Swift: #if DEBUG の対称。リリースビルドでは端末に残った旧 PlayerPrefs 値や
            // 外部からの改変を読み込ませず、常にデフォルト(無効)値にする)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Swift 同様、0 はデフォルトへフォールバック
            DebugStartFloor = PlayerPrefs.GetInt(DebugStartFloorKey, 1);
            if (DebugStartFloor == 0) DebugStartFloor = 1;
            DebugAILevel = ParseAILevel(PlayerPrefs.GetString(DebugAILevelKey, "Normal"), AILevel.Normal);
            DebugUnlockAllCharacters = GetBool(DebugUnlockAllCharactersKey, false);
            DebugBPMOverride = PlayerPrefs.GetFloat(DebugBPMOverrideKey, 0f);
            DebugTurnCountdownBeats = PlayerPrefs.GetInt(DebugTurnCountdownBeatsKey, GameConfig.TurnCountdownBeats);
            if (DebugTurnCountdownBeats == 0) DebugTurnCountdownBeats = GameConfig.TurnCountdownBeats;
            DebugSkipStartCountdown = GetBool(DebugSkipStartCountdownKey, false);

            // 全キャラアンロックデバッグ (Swift: loadData 末尾と同じ)
            if (DebugUnlockAllCharacters)
            {
                UnlockedCharacters = new List<CharacterType>
                {
                    CharacterType.Hero, CharacterType.Thief, CharacterType.Wizard, CharacterType.Elf, CharacterType.Knight
                };
            }
#else
            DebugStartFloor = 1;
            DebugAILevel = AILevel.Normal;
            DebugUnlockAllCharacters = false;
            DebugBPMOverride = 0f;
            DebugTurnCountdownBeats = GameConfig.TurnCountdownBeats;
            DebugSkipStartCountdown = false;
#endif
        }

        /// <summary>全プロパティを PlayerPrefs へ書き込む。Swift: PlayerViewModel.saveData()</summary>
        public void Save()
        {
            PlayerPrefs.SetInt(HighestFloorKey, HighestFloor);
            PlayerPrefs.SetString(UnlockedCharactersKey, string.Join(",", UnlockedCharacters.Select(c => c.RawValue())));
            PlayerPrefs.SetString(SelectedCharacterKey, SelectedCharacter.RawValue());
            SetBool(AdRemovedKey, AdRemoved);
            PlayerPrefs.SetFloat(BgmVolumeKey, BgmVolume);
            PlayerPrefs.SetFloat(SeVolumeKey, SfxVolume);
            SetBool(IsBgmEnabledKey, IsBgmEnabled);
            SetBool(IsSfxEnabledKey, IsSfxEnabled);
            SetBool(HasSeenTutorialKey, HasSeenTutorial);
            SetBool(HasSeenTutorialV11Key, HasSeenTutorialV11);
            SetBool(OneTapRetryEnabledKey, OneTapRetryEnabled);
            SetBool(HapticsEnabledKey, HapticsEnabled);
            PlayerPrefs.SetString(AILevelKey, SelectedAILevel.RawValue());
            PlayerPrefs.SetString(PurchasedProductsKey, string.Join(",", PurchasedProducts));

            PlayerPrefs.SetInt(DebugStartFloorKey, DebugStartFloor);
            PlayerPrefs.SetString(DebugAILevelKey, DebugAILevel.RawValue());
            SetBool(DebugUnlockAllCharactersKey, DebugUnlockAllCharacters);
            PlayerPrefs.SetFloat(DebugBPMOverrideKey, DebugBPMOverride);
            PlayerPrefs.SetInt(DebugTurnCountdownBeatsKey, DebugTurnCountdownBeats);
            SetBool(DebugSkipStartCountdownKey, DebugSkipStartCountdown);

            PlayerPrefs.Save();
        }

        // MARK: - Character Management (Swift: PlayerViewModel と同名の操作)

        public bool IsCharacterUnlocked(CharacterType character) => UnlockedCharacters.Contains(character);

        public void UnlockCharacter(CharacterType character)
        {
            if (!UnlockedCharacters.Contains(character))
            {
                UnlockedCharacters.Add(character);
                Save();
            }
        }

        /// <summary>
        /// アンロック済み (またはデバッグ全開放) の場合のみ選択。Swift: selectCharacter(_:)
        /// Swift は #if DEBUG / #else でデバッグ全開放バイパスの有無を切り替えており、
        /// リリースビルドではその分岐自体が存在しない。Load() で DebugUnlockAllCharacters が
        /// 常に false になる防御と合わせ、こちらも同じ #if で対称に絶つ (二重の安全策)。
        /// </summary>
        public bool SelectCharacter(CharacterType character)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugUnlockAllCharacters || UnlockedCharacters.Contains(character))
#else
            if (UnlockedCharacters.Contains(character))
#endif
            {
                SelectedCharacter = character;
                Save();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 最高到達階層を更新。盗賊は階層10到達で自動解放 (Swift: updateHighestFloor(_:))。
        /// </summary>
        public void UpdateHighestFloor(int floor)
        {
            if (floor <= HighestFloor) return;
            HighestFloor = floor;
            Save();

            if (floor >= GameConfig.ThiefUnlockFloor && !UnlockedCharacters.Contains(CharacterType.Thief))
            {
                UnlockCharacter(CharacterType.Thief);
            }
        }

        public void RemoveAds()
        {
            AdRemoved = true;
            Save();
        }

        // MARK: - Purchases (Swift: StoreKitService/PurchaseManager のローカル反映部分)

        public bool IsPurchased(string productId) => PurchasedProducts.Contains(productId);

        /// <summary>
        /// 購入済み商品を記録し、対応する解放処理 (キャラ解放 / 広告削除) を行う。
        /// Swift では PurchaseManager が担当。IAP 決済そのものは Phase 3。
        /// </summary>
        public void AddPurchasedProduct(string productId)
        {
            PurchasedProducts.Add(productId);
            switch (productId)
            {
                case ProductWizard: UnlockCharacter(CharacterType.Wizard); break;
                case ProductElf: UnlockCharacter(CharacterType.Elf); break;
                case ProductKnight: UnlockCharacter(CharacterType.Knight); break;
                case ProductRemoveAds: AdRemoved = true; break;
            }
            Save();
        }

        // MARK: - Helpers

        // PlayerPrefs には bool が無いため int 0/1 で保存する
        private static bool GetBool(string key, bool defaultValue) =>
            PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;

        private static void SetBool(string key, bool value) =>
            PlayerPrefs.SetInt(key, value ? 1 : 0);

        private static bool TryParseCharacter(string raw, out CharacterType type)
        {
            switch (raw)
            {
                case "hero": type = CharacterType.Hero; return true;
                case "thief": type = CharacterType.Thief; return true;
                case "wizard": type = CharacterType.Wizard; return true;
                case "elf": type = CharacterType.Elf; return true;
                case "knight": type = CharacterType.Knight; return true;
                default: type = CharacterType.Hero; return false;
            }
        }

        private static AILevel ParseAILevel(string raw, AILevel fallback)
        {
            switch (raw)
            {
                case "Easy": return AILevel.Easy;
                case "Normal": return AILevel.Normal;
                case "Hard": return AILevel.Hard;
                case "Boss": return AILevel.Boss;
                default: return fallback;
            }
        }
    }
}
