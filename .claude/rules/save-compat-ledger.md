# セーブ互換台帳（PlayerPrefs / UserDefaults）⭐️

Unity 版は「**既存 iOS アプリのアップデート差し替え**」で配信する計画（`docs/unity-migration-plan.md` §リスク）。iOS の Unity PlayerPrefs は Swift 版と**同一の NSUserDefaults を読む**ため、キー名・型の互換が既存ユーザーの進行データの生死を決める。

## 🚨 リリース前ゲート（アップデート差し替え前に必須）

以下 5 キーは**同名なのに型/保存場所が不一致**。移行コードなしで出荷すると既存 iOS ユーザーの進行・課金状態が**静かに消える**:

| キー | iOS の実体 | Unity の実体 | 影響 |
|---|---|---|---|
| `purchasedProductIDs` | **Keychain**（`com.escapenine.purchases`、旧 UserDefaults キーは削除済み。StoreKitService.swift:259-289） | PlayerPrefs CSV 文字列 | **課金キャラ・広告削除が未購入扱いに**（最重要。IAP レシート復元で救済されるかは未検証） |
| `unlockedCharacters` | UserDefaults **NSArray** | PlayerPrefs CSV 文字列 | `stringForKey` が nil → Hero のみに戻る |
| `unlockedAchievements` | Codable **Data blob**・日本語 rawValue（"初勝利"） | CSV・英語 enum 名（"FirstWin"） | 実績全消失 |
| `localRankings` | Codable Data blob | JsonUtility JSON 文字列 | ローカル履歴消失（軽微） |
| `dailyChallengeHistory` | **辞書** [日付: Record] の Data blob | **リスト**の JSON 文字列 | デイリー履歴消失 |

→ 出荷前に「Swift 形式を読めたら Unity 形式へ一回変換する」migration を `PlayerState.Load` 系に実装し、実データ（Swift 版でプレイした端末/シミュレータ）でハッピーパスを 1 回通すこと（`mvp-prelaunch.md` の精神）。

🚨 移行実装済み（`SwiftSaveMigration`、対象3キー: `purchasedProductIDs` / `unlockedCharacters` / `unlockedAchievements`。`localRankings` / `dailyChallengeHistory` は実害軽微・スキーマ変換が重いため意図的にスキップ）。**実機ハッピーパス確認（Swift 版でプレイした端末で Unity 版起動）は未実施の人間ゲート**。

## 互換キー一覧（Swift キー名を踏襲・型も一致）

`highestFloor`(int) / `selectedCharacter`(string) / `adRemoved` / `bgmVolume` / `seVolume` / `isBGMEnabled` / `isSFXEnabled` / `hasSeenTutorial` / `hasSeenTutorialV1_1` / `oneTapRetryEnabled` / `hapticsEnabled`

※ iOS の `sfxVolume`（AudioManager 側）と `hasLaunchedBefore` は Unity 非採用（`seVolume` へ統一済み = Swift 二重経路負債の解消、unity-migration-plan.md 記載）。

## Unity 独自キー（Swift 対応なし・コメント明記が既存慣行）

`hasSeenRelicIntro` / `reduceMotionEnabled` / `stageQualityTier` / `aiLevel`（iOS は非永続の @State、意図的差分）/ `metaCurrency` / `unlockedRelicIds` / `unlockedCosmeticIds` / `starterPerkRelicId` / `lifetimeRelicsCollected` / `analyticsDistinctId`（AnalyticsService、PostHog 匿名ID）/ `firebaseLocalId`・`firebaseRefreshToken`・`onlineDisplayName`（OnlineRankingService、Firebase Auth REST。※iOS は Firebase SDK 管理なので対応キー無し）/ `hasSeenRankingRenewalNotice`（HomeScreen、ランキング刷新お知らせの一回表示）

Debug 系（`#if UNITY_EDITOR || DEVELOPMENT_BUILD` 限定）: `debugStartFloor` / `debugAILevel` / `debugUnlockAllCharacters` / `debugBPMOverride` / `debugTurnCountdownBeats` / `debugSkipStartCountdown`

`swiftMigrationDone`（int、`SwiftSaveMigration.RunOnce()` の一回限りガード。Swift 正本に対応キー無し）

## 新キー追加時のルール

1. **この台帳に追記する**（hook `post_write_cs_check.py` が台帳に無いキーを警告する）
2. 読み込みは安全側フォールバック（壊れ値・欠落 → 既定値。クラッシュさせない。StageQualityTier の範囲外ガードが先例）
3. Swift 対応が無いキーは定義行コメントに「Swift 正本に対応キー無し」と書く（PlayerState.cs の既存慣行）
4. 保存は即時 `Save()`（オーバーレイ中離脱でスコアが消えた 830e1a6 の再発防止）
5. キー定義は `PlayerState.cs` の `*Key` const に集約（文字列直書き散在の禁止）
