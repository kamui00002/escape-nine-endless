# Unity 版 提出前 審査レディネス（過去リジェクト履歴との照合）⭐️

Escape Nine を **Swift → Unity 版に差し替え**て `com.souatou.escapenine` で再提出する前に、**過去に実際に落ちた指摘**を先回りで潰すためのチェックリスト。
出典: Obsidian 学びログ（phc / Escape Nine の審査リジェクト実例）。各項目に **過去の該当リジェクト** と **Unity 版での状態 / 対策** を対応づけた。

最終更新: 2026-07-16（Phase 3 収益化③広告 実機達成後、提出準備フェーズ）。

---

## 🔴 自動却下（submit 時 ~20秒で Invalid Binary。審査に到達すらしない）

### R1. ITMS-91064 — NSPrivacyTracking + NSPrivacyTrackingDomains
- **過去**: phc build22/23/24 で3回焼き直し（[[2026-06-17 ITMS-91064 空のNSPrivacyTrackingDomains]]）。ルール = **`NSPrivacyTracking=true`（＝ATT/IDFAで追跡・ASCで「Device ID→トラッキングに使用=はい」申告）なら、`NSPrivacyTrackingDomains` に実在ドメイン≥1件必須**。空配列もキー削除も「ドメイン0」で同じ却下。
- **Unity 版の現状（2026-07-16 確認）**: ❌ **app レベル `PrivacyInfo.xcprivacy` が存在しない**。UnityFramework のは `NSPrivacyTracking=false`、GMA pod のは `tracking=None`。**AdMob で実際に追跡（ATT提示・personalized 有効を実機確認済）してるのに追跡宣言マニフェストが無い** = ASC ラベル（追跡=はい）とバイナリが矛盾 → R1 と 5.1.2 の両方に該当。
- **対策**: **app レベル `PrivacyInfo.xcprivacy` を PostProcessBuild で生成**し、メインApp ターゲットに追加する:
  ```xml
  <key>NSPrivacyTracking</key><true/>
  <key>NSPrivacyTrackingDomains</key>
  <array>
    <string>googleads.g.doubleclick.net</string>  <!-- AdMob 追跡/配信 -->
  </array>
  <key>NSPrivacyCollectedDataTypes</key> <!-- Device ID を Tracking+ThirdPartyAdvertising 目的で収集 -->
  <key>NSPrivacyAccessedAPITypes</key>   <!-- UserDefaults(CA92.1) 等の required-reason API -->
  ```
  ※ PostHog(`us.i.posthog.com`)/Firebase Auth は ATT の「トラッキング」ではない（匿名分析・認証）ので追跡ドメインに入れない。Unity Ads メディエーションは未導入なので Unity ドメインも不要。ドメインは1件（AdMob）で規則充足。
  → **実装: `Editor/AdsBuild/` に PrivacyManifest 用 PostProcessBuild を追加（本タスクで着手）**。
- **検証**: 提出前に `plutil -lint PrivacyInfo.xcprivacy`。upload 成功 ≠ 検証通過（submit 時にもう一段自動検証が走る）。無効化された build は再利用不可 → build 番号 +1。

---

## 🟠 本審査リジェクト（審査官が指摘）

### R2. 5.1.2(i) — ATT ラベル ⇔ バイナリ整合
- **過去**: phc 第2R（[[2026-06-09 App審査リジェクト第2R]]）。追跡ラベルを出すなら ATT 提示必須。逆に `NSUserTrackingUsageDescription` がバイナリにあると「追跡しない」は選べない。
- **Unity 版**: ✅ **Route B（追跡する）で整合**。`NSUserTrackingUsageDescription` あり（GMA注入）＋ ATT ダイアログ実提示（実機で Authorized 確認済）＋ AdMob で実追跡。ASC App Privacy で **「Device ID / 広告データ → トラッキングに使用=はい」** を宣言する（Swift 版と同 bundle なので既存申告が引き継がれる想定 → **ASC で現行の App Privacy を確認**）。R1 のマニフェストと矛盾しないこと。
- **対策**: ASC の App Privacy 質問票が「追跡=はい」になっているか確認（xcprivacy と別管理・手動）。

### R3. 5.1.1(v) — アカウント削除（ログイン/アカウント作成がある場合）
- **過去**: phc（[[2026-06-06 SIWAは削除機能とセット]]）。SIWA=アカウント作成 → アプリ内アカウント削除必須。任意ログインでも対象。自動解析がバイナリの SIWA を検出しデモアカウント要求（2.1）。
- **Unity 版**: ⚠️ **要確認**。Unity は **Firebase 匿名認証のみ（SIWA/ログインUI 無し）** で世界ランキング用に匿名UIDでデータ保存。匿名のみなら 5.1.1(v) 対象外の可能性が高いが、**確認2点**: ①Unity ビルドの entitlements に **Sign in with Apple が入っていない**こと（Swift 版は SIWA 持ち。Unity 差し替えで継承しないこと）②ログインUIをApple自動解析が検出しないこと。→ 匿名UID でも Firestore に displayName/floor を保存するため、**「アカウント削除/データ削除」導線を用意するか、ASC で説明**するのが安全（グレーゾーン）。
- **対策**: entitlements 確認（次項参照）＋ 必要なら設定にデータ削除導線 or Resolution Center 説明。

### R4. 2.1 — デモアカウント / 審査ノート
- **過去**: phc（ログインありでデモ垢未提供 → 情報要求）。
- **Unity 版**: ログイン無し（匿名）なのでデモ垢不要。ただし **審査ノートに** ①ログイン不要で全機能プレイ可 ②IAP テスト手順（Sandbox）③特殊操作（ビート同期・スキル）を明記。IAP は審査官が実際に購入フローをテストするため要動作。

### R5. 1.5 — Support URL に実サポート情報
- **過去**: phc（Support URL がマーケLPでサポート情報無し → 却下）。
- **Unity 版**: ⚠️ 現行 Support URL = `github.com/kamui00002/escape-nine-endless`（リポジトリ）。**サポート窓口/連絡先/FAQ が明確でないと 1.5 リスク**。プライバシーポリシーには連絡先 `yoshidometoru@gmail.com` あり。
- **対策**: サポートページ（連絡先・簡単なFAQ・ポリシーリンク）を用意して Support URL に設定（GitHub Pages でホスト可。privacy-policy.html と同じ場所）。

---

## 🟡 メタデータ（新ビルド不要・ASC で直せる）

### R6. 2.3.4 — App プレビュー動画に端末フレーム/モック枠は不可
- **過去**: phc（Seedance/マーケ動画の枠合成で却下）。App プレビューは**実機の素の画面録画のみ**。
- **Unity 版**: Seedance 生成のプロモ動画は**広告キャンペーン用**であり、**ストアの App プレビュー枠には使わない**こと。App プレビューは (a) 実機素録画を撮る or (b) 枠として**設定しない**（任意）。混同注意。

### R7. 2.3.2 — 促進IAP画像 ≠ アプリアイコン
- **過去**: phc（IAP画像がアイコンと同一で却下）。
- **Unity 版**: IAP の「画像（任意）」を設定するならアイコンと別に。未使用なら**設定しない**。

### R8. メタデータ正確性 — 「10ターン」修正
- **過去（本 repo）**: `appstore-metadata.md` §未処理。実装は 5〜14ターン（BaseTurns=5+(floor-1)/10）で「10ターン」は不正確。
- **Unity 版**: ja 説明文は doc 修正済 → **ASC の ja 説明文を更新**。**en-US/ko-KR/zh-TW の説明文にも「10 turns / 10턴 / 10回合」が残っていれば修正**（翻訳文言案は別途ドラフト）。
- 加えて **Unity 版の新機能**（HD-2D ビジュアル・ローグライク レリック・世界ランキング）にメタデータ/スクショを更新（Swift 版から見た目が別物）。

---

## 🔧 提出ビルド設定（コード側・提出前必須）

### R9. BuildOptions.Development → None
- `BuildScripts.BuildIOS` の `BuildOptions.Development` を **`None`（Release）** に戻す。デバッグパネル（DangerZone・全キャラ解放等）を出荷しない。本番広告ID有効化（`#if UNITY_EDITOR || DEVELOPMENT_BUILD` が false になり本番IDへ）。**恒久ATT framework リンク（`7b325c2`）もこのクリーンビルドで初めて実地検証される**。

### R10. bundle 差し替え（★不可逆・オーナー決定）
- `BuildScripts.cs` の `IosBundleId` を `com.yoshidometoru.escapenine.unity` → 本番 `com.souatou.escapenine`。紐づく provisioning/entitlements も本番App IDへ。**B-1 セーブ移行検証はこの本番bundleビルドでしかできない**（Keychain/NSUserDefaults は bundle 単位。移行コードは正しいと敵対レビュー済 = 課金ID完全一致・Keychain クエリ一致）。
- **entitlements 確認**: Sign in with Apple / Game Center の要否。Unity 版は匿名認証・Game Center 未結線なら不要（R3 参照）。

### R11. SKAdNetwork
- ✅ GMA が Info.plist に SKAdNetworkItems 50件を自動注入済（実機ビルドで確認）。Unity Ads メディエーション導入時は Unity の SKAN ID を追加（今は未導入）。

---

## 提出手順（推奨順序）

1. **R1 PrivacyInfo.xcprivacy 実装**（自動却下 #1 潰し・本タスクで着手）
2. **R9 Release ビルド化** + **R10 bundle 差し替え判断**（オーナー）
3. **本番bundle Development ビルドで B-1 セーブ移行を実機検証**（Swift版データのある端末）
4. **Release ビルドで R1/R9 が効いてるか確認**（`plutil -lint` + デバッグパネル非表示）
5. **ASC**: R2(App Privacy追跡) / R5(Support URL) / R6(プレビュー) / R7(IAP画像) / R8(10ターン翻訳) を整備
6. **審査ノート**（R4）を書いて TestFlight → 審査提出
7. submit 直後20秒の自動却下メール（account holder 宛）が来ないか確認（R1 の関門）

---

## 進捗 (2026-07-16 device 不要タスク)

- ✅ **R1**: PrivacyInfo.xcprivacy 生成 PostProcessBuild 実装済 (`71bfd00`)。次のクリーン Unity ビルドで実地検証。
- ✅ **R3**: **クリア確認 — 対応不要**。Unity は SIWA/ログインUI/実 Apple-auth API (ASAuthorization 等) がゼロ、生成 entitlements もクリーン (capability 未設定)。→ 5.1.1(v)「SIWA→アカウント削除」は **N/A**。※匿名UIDで世界ランキングを保存する点は 5.1.1(v) グレーだが「ユーザーによるアカウント作成」が無く低リスク。防御したい場合は設定に「ランキングデータ削除」導線 (Firestore rules の `allow delete` 変更 + OnlineRankingService に DELETE 追加) = **任意・オーナー判断**。
- ✅ **R5**: サポートページ草案 `docs/support.html` 作成 (連絡先 `yoshidometoru@gmail.com`・FAQ・ポリシーリンク、ダークモード対応)。→ **GitHub Pages 等でホストして ASC の Support URL を `.../support.html` に設定**すれば 1.5 解消。
- ✅ **R8**: 「10ターン」の各言語置換文言 (ja/en/ko/zh-TW) を `docs/appstore-metadata.md` §未処理タスクに用意。ASC で該当文を差し替えるだけ。
- ⬜ **残 (オーナー主導)**: R2 (ASC App Privacy 追跡=はい確認) / R9 (BuildOptions.None) / **R10 (bundle 差し替え判断・不可逆)** / B-1 実機検証 (本番bundleビルド)。

## 関連
- 過去リジェクト: [[2026-06-17 ITMS-91064]] / [[2026-06-09 第2R]] / [[2026-06-06 SIWA]] / [[2026-03-31 round2 ATT-IAP-buildnumber]] / [[2026-03-24 IAP-support-url]]
- メタデータ: `docs/appstore-metadata.md` / 収益化: `docs/収益化設定ガイド.md`
- 移行台帳: `.claude/rules/save-compat-ledger.md`（B-1）/ `docs/unity-ads-iap-decision-brief.md`（広告）
