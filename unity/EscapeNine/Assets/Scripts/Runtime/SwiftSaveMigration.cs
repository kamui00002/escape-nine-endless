// SwiftSaveMigration.cs
// Swift 版セーブデータの一回限り移行 (B-1、リリースブロッカー)。
// アップデート差し替え配信 (unity-migration-plan.md §リスク) では、Unity iOS の PlayerPrefs は
// Swift 版と同一 NSUserDefaults を読むため、既存 Swift ユーザーの進行・課金データを Unity 形式へ
// 一回だけ変換する。対象は進行・課金に直結する3キーのみ
// (unlockedCharacters / purchasedProductIDs / unlockedAchievements)。
// localRankings / dailyChallengeHistory は意図的にスキップ (.claude/rules/save-compat-ledger.md 参照)。
//
// 変換ロジックは Core/SwiftSaveConverter.cs (UnityEngine 非依存)、ネイティブ読み取りは
// Plugins/iOS/EscapeNineSaveMigration.mm が担当し、本ファイルは両者を結線する薄い Runtime 層。
//
// 「Unity 形式の値が既に存在する場合は上書きしない」の判定について:
// PlayerPrefs.HasKey は iOS では [NSUserDefaults objectForKey:] の非 nil 判定に相当するため、
// unlockedCharacters (Swift: NSArray) / unlockedAchievements (Swift: Data) のように Swift が
// 同名キーに別型の値を書き込んでいる場合、既存 Swift ユーザー全員で HasKey が true になり
// 移行が始まる前にスキップされてしまう (地雷5キー問題そのもの)。
// 一方 PlayerPrefs.GetString は内部で stringForKey: を使うため、型不一致の値には既定値
// (センチネル) を返す (save-compat-ledger.md: 「stringForKey が nil → Hero のみに戻る」の裏返し)。
// そのため「Unity 形式の文字列値が存在するか」は HasKey ではなく GetString+センチネル判定で行う。

using System;
using System.Collections.Generic;
using UnityEngine;
using EscapeNine.Core;

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace EscapeNine.Runtime
{
    public static class SwiftSaveMigration
    {
        // Swift 対応なし。移行が完了済みかどうかのガード (Unity 独自キー)。
        private const string MigrationDoneKey = "swiftMigrationDone";

        // 移行対象3キー (Swift/Unity 共通のキー名。詳細は save-compat-ledger.md 🚨節)
        private const string UnlockedCharactersKey = "unlockedCharacters";
        private const string PurchasedProductsKey = "purchasedProductIDs";
        private const string UnlockedAchievementsKey = "unlockedAchievements";

        // Swift: StoreKitService.keychainService / keychainAccount
        private const string KeychainService = "com.escapenine.purchases";
        private const string KeychainAccount = "purchasedProductIDs";

        // GetString の第2引数に渡す既定値。実際の保存値がこれと一致することは
        // まず無い前提の任意文字列 (「Unity 形式の値が無い」ことの判定用センチネル)。
        private const string AbsentSentinel = "__e9_swift_migration_absent__";

        /// <summary>
        /// 移行本体。App.cs から PlayerState 生成より前に一度だけ呼ぶこと
        /// (PlayerState.Load() が PlayerPrefs を読み込む前に Swift データを Unity 形式へ変換しておく必要があるため)。
        /// swiftMigrationDone が既に立っていれば即 return。
        /// </summary>
        public static void RunOnce()
        {
            if (PlayerPrefs.GetInt(MigrationDoneKey, 0) == 1) return;

#if UNITY_IOS && !UNITY_EDITOR
            MigrateUnlockedCharacters();
            MigratePurchasedProducts();
            MigrateUnlockedAchievements();
#endif

            PlayerPrefs.SetInt(MigrationDoneKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Unity 形式の文字列値が既に存在するか (HasKey ではなく GetString+センチネルで判定する理由はファイル冒頭コメント参照)。</summary>
        private static bool UnityStringValueExists(string key) =>
            PlayerPrefs.GetString(key, AbsentSentinel) != AbsentSentinel;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal", EntryPoint = "_e9MigStringArrayCsv")]
        private static extern IntPtr NativeStringArrayCsv(string key);

        [DllImport("__Internal", EntryPoint = "_e9MigDataUtf8")]
        private static extern IntPtr NativeDataUtf8(string key);

        [DllImport("__Internal", EntryPoint = "_e9MigKeychainUtf8")]
        private static extern IntPtr NativeKeychainUtf8(string service, string account);

        private static void MigrateUnlockedCharacters()
        {
            if (UnityStringValueExists(UnlockedCharactersKey)) return; // 既存 Unity ユーザー保護

            string raw = PtrToStringUtf8(NativeStringArrayCsv(UnlockedCharactersKey));
            if (string.IsNullOrEmpty(raw)) return;

            string csv = SwiftSaveConverter.NormalizeCharactersCsv(raw);
            if (string.IsNullOrEmpty(csv)) return;

            PlayerPrefs.SetString(UnlockedCharactersKey, csv);
            Debug.Log($"[SwiftSaveMigration] unlockedCharacters を移行しました ({csv.Split(',').Length}件)");
        }

        private static void MigratePurchasedProducts()
        {
            if (UnityStringValueExists(PurchasedProductsKey)) return; // 既存 Unity ユーザー保護

            string json = PtrToStringUtf8(NativeKeychainUtf8(KeychainService, KeychainAccount));
            if (string.IsNullOrEmpty(json)) return;

            List<string> parsed = SwiftSaveConverter.ParseJsonStringArray(json);
            string csv = SwiftSaveConverter.NormalizeProductIdsCsv(parsed);
            if (string.IsNullOrEmpty(csv)) return;

            PlayerPrefs.SetString(PurchasedProductsKey, csv);
            // 値そのもの (商品ID) はログに出さない (課金情報)。件数のみ。
            Debug.Log($"[SwiftSaveMigration] purchasedProductIDs を移行しました ({csv.Split(',').Length}件)");
        }

        private static void MigrateUnlockedAchievements()
        {
            if (UnityStringValueExists(UnlockedAchievementsKey)) return; // 既存 Unity ユーザー保護

            string json = PtrToStringUtf8(NativeDataUtf8(UnlockedAchievementsKey));
            if (string.IsNullOrEmpty(json)) return;

            List<string> parsed = SwiftSaveConverter.ParseJsonStringArray(json);
            string csv = SwiftSaveConverter.MapAchievementsToEnumCsv(parsed);
            if (string.IsNullOrEmpty(csv)) return;

            PlayerPrefs.SetString(UnlockedAchievementsKey, csv);
            Debug.Log($"[SwiftSaveMigration] unlockedAchievements を移行しました ({csv.Split(',').Length}件)");
        }

        /// <summary>
        /// ネイティブ側が strdup で返す UTF-8 の char* を手動デコードする。
        /// Marshal.PtrToStringUTF8 は .NET Framework API 互換レベルに含まれず未対応の可能性があり、
        /// 既定の文字列マーシャリング (ANSI/UTF-16 想定) は実績名の日本語 rawValue を含む UTF-8
        /// バイト列を静かに破損させるため使わない (手動デコードでリスクを排除する)。
        /// </summary>
        private static string PtrToStringUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;

            int len = 0;
            while (Marshal.ReadByte(ptr, len) != 0) len++;
            if (len == 0) return string.Empty;

            var buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return System.Text.Encoding.UTF8.GetString(buf);
        }
#endif
    }
}
