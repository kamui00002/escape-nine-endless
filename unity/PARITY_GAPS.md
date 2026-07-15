# iOS↔Unity パリティギャップ台帳 ⭐️

**用途**: (1) 意図的差分の記録（レビュー・hook の誤検知防止 allowlist） (2) 未移植＝要対応の追跡。
新しい差分を作ったら必ずここへ追記。`check_analytics_parity.py` hook はこのファイルに載っているイベント名を警告対象から除外する。

最終監査: 2026-07-13（回顧監査。churn/コミット精査 + Explore 3 並列）

## A. 意図的差分（対応不要・理由付き）

| 項目 | 内容 | 理由 |
|---|---|---|
| `eg_app_init_ok` | Unity 未送信 | Firebase Analytics の無音故障検知 sentinel。Unity は Firebase 自体を持たない |
| `app_open` | Unity 未送信 | Firebase 専用（GA4→Google Ads コンバージョン経路）。PostHog には元々流れていない |
| `tutorial_complete`（無 prefix 版） | Unity 未送信 | iOS の GA4→Google Ads コンバージョン専用イベント（2026-07-15 に発火場所を初回オンボーディング完了時のみへ是正済み）。Unity は PostHog のみで対応不要 |
| `aiLevel` の永続化 | Unity は PlayerPrefs 保存 | iOS は GameView の @State（画面間受け渡しのため。PlayerState.cs コメント明記） |
| Reduce Motion | Unity はアプリ内トグル（既定 OFF） | iOS は OS 設定 `UIAccessibility.isReduceMotionEnabled` を自動検出。Unity 独自設定として文書化済み — ただし体験としては後退（B-4 も参照） |
| ローグライク拡張（レリック/残光/分岐ルート/ボスパターン） | Unity のみ | Phase 5 の意図的な深化。Swift 正本に対応物なし |
| ナイトキャラ | 両方に実装あり | 要件定義書に未記載だが両実装一致（差分ではない、記録のみ） |
| SFX 音量キー | Unity は `seVolume` のみ | Swift の `sfxVolume`/`seVolume` 二重経路負債を統一（unity-migration-plan.md） |
| ATT/consent の非パーソナライズ化方式 | Unity は `npa=1`（AdRequest Extras）方式 | Swift は ATT 完了後に `MobileAds.shared.start()` を呼ぶ「ad load 遅延」方式。Unity は `App.cs` の呼び出し順序が `Initialize()`→`RequestTrackingAuthorization()` 固定のため、ATT 完了前は全 `AdRequest` に `npa=1` を付与し、ATT `Authorized` 完了時のみ personalized 許可へ切替（`AdMobService.cs` ヘッダ／`BuildAdRequest()`）。「init=denied → ATT 後切替」の原則自体は両実装で担保 |

## B. 未移植・未実装（要対応。勝手に「完了」扱いしない）

| # | 項目 | 状態 | 備考 |
|---|---|---|---|
| B-1 | **セーブ互換の移行コード** | **実装済み・実機検証待ち**（2026-07-15） | `SwiftSaveMigration.RunOnce()` が進行/課金直結の3キー（`purchasedProductIDs`=Keychain / `unlockedCharacters`=NSArray / `unlockedAchievements`=日本語blob）を一回限り変換。`localRankings`/`dailyChallengeHistory` は実害軽微のため意図的スキップ。**残る出荷ゲート: Swift 版でプレイした実機で Unity 版を起動するハッピーパス確認（人間ゲート）**。詳細は `.claude/rules/save-compat-ledger.md` |
| B-2 | **ハプティクス実発火** | **未実装のまま Phase 4 完了申告と矛盾** | `PlayerState.HapticsEnabled` と SettingsScreen のトグル UI だけ存在し、振動 API 呼び出しゼロ（SettingsScreen.cs:196 に「Phase 4/juice 送り」コメント残存）。トグルが機能しない騙し UI 状態 |
| B-3 | **抜かれ通知**（`eg_overtaken_notification_shown`） | 機能ごと未移植 | iOS Sprint2 F2 機能。移植するか捨てるか判断が必要（捨てるなら A 表へ移動） |
| B-4 | VoiceOver / スクリーンリーダー | Unity ゼロ | iOS は GridCellView に盤面読み上げ実装あり。uGUI はネイティブ連携が薄くプラグイン等の検討が必要 |
| B-5 | Dynamic Type / フォントスケール | Unity ゼロ | iOS も本編は固定 pt（チュートリアルのみセマンティック）なので優先度低 |
| B-6 | 色覚多様性配慮（斜線+アイコン） | Unity ゼロ | iOS も実質オンボーディング Step2 限定。両版とも本編の危険表示は色依存 — 対応するなら両版同時に |

※ B-7（iOS: `tutorial_complete` のファサード迂回）は 2026-07-15 解消: GA4 コンバージョン発火を v1.1 オンボーディング初回完了時（OnboardingTutorialView）へ移設し、「遊び方」再視聴経路からは削除。A 表の `tutorial_complete` 行も参照。

## 運用

- 新規の意図的差分: A 表へ 1 行追加（項目・内容・理由）
- B 表の解消: 該当行を削除し、対応コミットを Git メッセージに記録
- 四半期ごと（または大型リリース前）に `/parity-check` skill で再監査
