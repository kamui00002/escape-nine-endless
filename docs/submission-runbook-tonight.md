# 提出実行ランブック（夜・実機セッション用）⭐️☀️

> **目的**: Unity 版 Escape Nine を `com.yoshidometoru.EscapeNine-endless-` の**アップデート差し替え**として App Store 審査に出すまでを、**上から順に実行するだけ**にした turnkey 手順。過去リジェクト対策は `docs/review-readiness-unity.md`（R1〜R11）と対応。
> 作成 2026-07-16（Phase3③広告 実機達成 + 審査対策 R1/R3/R5/R8 完了後）。
> 前提値: 実機 iPhone 17 Pro（devicectl UDID `87F3A267-5378-5F81-A2F5-2AA1DFE1A51A` / ハードUDID `00008150-001A11EA3441401C`）/ Team `B7F79FDM78` / ミラー `~/EscapeNineUnity`。

---

## 0. 事前準備（★これが無いと失敗する）

1. iPhone を **USB 接続**・**自動ロック → なし**・**ロック解除で放置**（il2cpp/xcodebuild 中の再ロックで launch が Locked 失敗）。
2. **メモリを空ける**: 不要な iOS シミュレータを落とす（`xcrun simctl shutdown all`）。空き < 1GB だと il2cpp コンパイルが OOM で `BUILD INTERRUPTED`（2026-07-15 に踏んだ）。`top -l1 | grep PhysMem` で確認。
3. **Unity Editor を閉じる**（batchmode とロック競合）。並行 finish-unity セッションがあれば止める（同一ミラーの Unity ビルド競合で kill される）。
4. **repo→ミラー同期**: `rsync -a unity/EscapeNine/Assets/ ~/EscapeNineUnity/Assets/`（`--delete` 禁止＝secrets/GMA設定/PostProcess を保護）。

---

## 1. bundle 差し替え（★不可逆の方向づけ・最初の一手）

`BuildScripts.cs:34` を編集:
```csharp
// 変更前
private const string IosBundleId = "com.yoshidometoru.escapenine.unity";
// 変更後（本番＝Swift 版と同一 bundle。既存アプリのアップデートとして配信される）
private const string IosBundleId = "com.yoshidometoru.EscapeNine-endless-";
```
→ repo で変更 → §0.4 の rsync でミラーへ反映。

**署名の注意**: 自動署名（`appleEnableAutomaticSigning=true` / Team `B7F79FDM78`）。`com.yoshidometoru.EscapeNine-endless-` の App ID は Swift 版で登録済み。ただし **旧 Swift App ID に紐づく capability（Sign in with Apple / Game Center / Push 等）が Developer Portal で有効だと、entitlements 不一致で署名警告/失敗**の可能性。Unity ビルドは entitlements クリーン（R3 確認済）なので、xcodebuild `-allowProvisioningUpdates` が合わないときは **Portal 側で該当 App ID の不要 capability を外す** or **一致する provisioning profile を用意**（この場で対処）。

---

## 2. B-1 セーブ移行の実機検証（本番bundle・Development ビルド）

**なぜ必須**: Keychain/NSUserDefaults は bundle 単位。移行の実地確認は本番bundleビルドでしかできない。**コードは敵対レビュー済み（課金ID完全一致・Keychain クエリ一致・実績9/9）** なのでこれは確認。

**検証セットアップ（Swift 形式データを実機に用意する）**:
- 確実な方法: **Swift 版アプリを dev 署名でビルド → 実機に入れて 1プレイ**（Sandbox で課金1つ購入 + 数階クリア + 実績解錠）→ その後 **Unity 版（同 bundle・dev 署名）を上書きインストール**（両方 dev 署名・同 bundle/team ならデータコンテナ・Keychain 保持）。
- ※ App Store 版（配布署名）の上に dev 署名の Unity を直接は入れられない（署名不一致で要削除→データ消失）。**Swift を dev ビルドしてデータを作る**のが安全。

**手順**:
1. BuildOptions は **Development のまま**（console ログ・デバッグ確認のため。§3 でまだ None にしない）。
2. `bash unity/setup/ios-device-deploy.sh`（workspace 自動・OOM 対策で必要なら xcodebuild を `-jobs 4`）。
3. console 起動: `xcrun devicectl device process launch --console --terminate-existing --device 87F3A267-5378-5F81-A2F5-2AA1DFE1A51A com.yoshidometoru.EscapeNine-endless- > ~/console-mig.log 2>&1 &`
4. **目視確認（人間ゲート）**:
   - 起動ログに `[SwiftSaveMigration] ... を移行しました`（characters/purchasedProductIDs/achievements の件数）
   - **課金キャラ（購入済のもの）が解放されたまま**か
   - **広告削除購入済なら広告が消えてる**か（`adRemoved` 直接互換キー）
   - **実績が消えてない**か・**最高到達階層が引き継がれてる**か
5. **安全網**: 万一移行が課金を取りこぼしても、**設定→「購入を復元」で StoreKit が Apple ID の非消耗課金を再付与**する（4商品とも非消耗）。＝課金が永久に消えることはない（移行はシームレス化のため）。

---

## 3. Release ビルド（提出用・PostProcess 実地検証込み）

1. `BuildScripts.cs:135` を **`BuildOptions.Development` → `BuildOptions.None`**（デバッグパネル DangerZone を出荷しない・本番広告ID有効化）。→ rsync 反映。
2. `bash unity/setup/ios-device-deploy.sh`（or archive 用の手順）。**このクリーンビルドで PostProcessBuild 2件が実地検証される**:
   - `AdsBuildPostProcess` → `AppTrackingTransparency.framework` が UnityFramework にリンク（ATT リンクエラー再発しない）
   - `PrivacyManifestPostProcess` → app レベル `PrivacyInfo.xcprivacy` 生成（`NSPrivacyTracking=true` + `googleads.g.doubleclick.net`）
   - 確認: `plutil -lint ~/EscapeNineUnity/Builds/ios/PrivacyInfo.xcprivacy` が OK / `grep NSPrivacyTracking` で true / pbxproj に AppTrackingTransparency 参照
   ※ ここまでは 2026-07-16 に**テストbundleで Unity ビルド検証済**（下記「検証結果」参照）。本番bundle Release で再確認。
3. 本番広告が出るか（Release は `#if` が false → 本番広告ID）実機で軽く確認。デバッグパネルが**出ない**ことも確認。

---

## 4. アーカイブ → IPA → ASC アップロード（TestFlight）

- `CURRENT_PROJECT_VERSION`（build番号）を +1（無効化された build は再利用不可）。
- Release アーカイブ → `-exportArchive`（`app-store-connect` method / Team `B7F79FDM78`）→ `xcrun altool --upload-app`（or Transporter）。
- ※ Unity の Xcode プロジェクトは `make testflight`（Swift 版用）とは別系統。Unity 用の archive/export 手順を用意（Unity-iPhone.xcworkspace を Release で archive）。
- **upload 成功 ≠ 検証通過**。submit 時にもう一段の自動検証（ITMS-91064 等）が走る。

> **⚠️ 2026-07-17 に踏んだ罠: ERROR 91111「Missing app icon (1024 'Any Appearance')」で upload 失敗。**
> Unity iOS Player Settings に App Store 1024 アイコンが未設定だと、生成 `AppIcon.appiconset` に
> `ios-marketing`(1024) スロットが欠落し、**altool upload 自体が弾かれる**（submit 前・アップロード段階で失敗）。
> 対策は恒久化済み: `Editor/AdsBuild/IconPostProcess.cs`（PostProcessBuild 102）が同梱の
> `AppStoreIcon1024.png`（Swift 版と同一・アルファ無し）を appiconset へ注入し Contents.json に
> ios-marketing エントリを追加する（コミット `45cd0b9`）。→ **次のクリーン Unity ビルドで自動適用**。
> 検証: `unzip -p <ipa> Payload/*.app/... ` は不可なので、`Assets.car` を `assetutil --info` で見て
> AppIcon に 1024 が入っているか、または archive 後に actool ログで確認。upload が 91111 を返さなければ OK。

---

## 5. ASC メタデータ整備（提出前・R2/R5/R6/R7/R8）

- **R2 App Privacy**: 「Device ID / 広告データ → **トラッキングに使用=はい**」を確認（AdMob+ATT の実態と、§3 の PrivacyInfo と整合）。
- **R5 Support URL**: ✅ `docs/support.html` は main に追加済・GitHub Pages で**公開確認済** (`https://kamui00002.github.io/escape-nine-endless/support.html`, HTTP 200)。→ ASC の Support URL をこれに更新するだけ。
- **Export Compliance (輸出コンプライアンス)**: build22 の Info.plist に `ITSAppUsesNonExemptEncryption` が無いため、TestFlight で **「Missing Compliance / 輸出コンプライアンス情報がありません」**が出る。→ **「暗号化を使用していますか?」に "いいえ (標準的な暗号化のみ = 適用除外)"** で回答 (通信は Firebase/PostHog/AdMob すべて標準 HTTPS)。これを答えないと build を審査提出に添付できない。※次ビルド以降は `ExportCompliancePostProcess.cs` が自動で false を付与するのでこの質問は出ない。
- **R6**: App プレビュー動画に**端末フレーム付きの Seedance マーケ動画を使わない**（実機素録画のみ / 或いはプレビュー枠は未設定）。
- **R7**: 促進 IAP 画像がアイコンと同一なら**外す**。
- **R8**: 説明文の「10ターン / 10 turns / 10턴 / 10回合」を `docs/appstore-metadata.md §未処理` の置換文言に差し替え（ja/en/ko/zh-TW）。
- Unity 版の新機能（HD-2D・レリック・世界ランキング）にスクショ/説明を更新（別途素材）。

---

## 6. 審査ノート（R4）→ 提出 → 自動却下監視

**審査ノート（ASC「App Review Information > Notes」に貼る）**:
```
このアプリはログイン不要で全機能をプレイできます（アカウント作成なし・匿名）。デモアカウントは不要です。

【遊び方】9マスの盤面で、カウントダウンに合わせて画面タップでプレイヤーを移動し、追ってくる敵から規定ターン逃げ切ると次の階層へ進みます。階層が上がると敵AIが強化され、霧・マス消失などの特殊ルールが加わります。

【アプリ内課金のテスト】Sandbox アカウントで以下の非消耗課金をテストできます:
- 魔法使い / エルフ / ナイト（各キャラクター解放）
- 広告削除
「設定 > 購入を復元」で復元も動作します。

【広告】無料提供のため AdMob 広告を表示します（バナー / インタースティシャル）。起動時に App Tracking Transparency の許可ダイアログを表示します。
```

- **提出後 ~20秒**、account holder（Apple Developer 登録メール）に **`ITMS-90xx` 自動却下メールが来ないか**確認（R1 の関門）。来たら本文の ITMS コードが唯一の正 → 該当を直して build +1 で再提出。
- 本審査（1〜3日）の指摘は Resolution Center で対応。

---

## 検証結果ログ（このランブック作成時点で済んでいること）

- ✅ 広告③（AdMob）実機E2E: バナー/インタースティシャル/ATT 描画確認済（2026-07-15）。
- ✅ 分析①・ランキング②認証 実機確認済。
- ✅ B-1 移行コード 敵対レビュー済（課金ID完全一致・Keychain クエリ一致・実績9/9マッピング）。実機ハッピーパスのみ残（§2）。
- ✅ **PostProcess 2件 検証済み（2026-07-16 クリーン Unity ビルド）**:
  - `[AdsBuildPostProcess]` 実行 → `AppTrackingTransparency.framework` を UnityFramework へ weak リンク（pbxproj 参照4件）＝ATT リンクエラー再発しない。
  - `[PrivacyManifestPostProcess]` 実行 → app レベル `PrivacyInfo.xcprivacy` 生成（`plutil -lint` OK / `NSPrivacyTracking=true` / `googleads.g.doubleclick.net`）を App ターゲットへ追加（pbxproj 参照8件）＝ITMS-91064 対策が効く。
  - CS エラー0・asmdef 参照（`UnityEditor.iOS.Extensions.Xcode.dll`）正常。→ **§3 の本番bundle Release でも同じ PostProcess が自動適用される（確証あり）**。
- ✅ R1/R3/R5/R8 の審査対策コミット済（`71bfd00` / `0f1f8b5`）。
- ✅ **2026-07-17 build22 (1.5.8) を ASC へ upload 成功**（Delivery UUID `ed0a37f2`）。バイナリ客観検証: bundle=`com.yoshidometoru.EscapeNine-endless-` / ver 1.5.8 / build 22 / PrivacyInfo(Tracking=true+`googleads.g.doubleclick.net`) / GADApplicationIdentifier(本番) / Assets.car に 1024 アイコン。
- ✅ **build22 = Release ビルド確定（objective）**: `Builds/ios/Data/boot.config` に development フラグ（`player-connection-debug` 等）**無し** + 生成元 `build-release.log`(BuildScripts.BuildIOS 経由・`result=Succeeded`) の repo/ミラー両 BuildScripts.cs が `BuildOptions.None` → `DEVELOPMENT_BUILD` 未定義 = **DangerZone デバッグパネル非同梱・本番広告ユニットID**。※最終ダメ押しは TestFlight で実機起動して (a)デバッグパネルが出ない (b)本番広告が描画される を確認（§4 の #1/#4）。
- ⬜ **screenshots 未更新（2.3.3 リスク）**: 現ストアのスクショは Swift 版。Unity 版は HD-2D で別物 → **提出前に**実機スクショ差し替え（R8 と同枠・オーナー作業）。

---

## 関連
- 審査対策台帳: `docs/review-readiness-unity.md`（R1〜R11）
- メタデータ/翻訳: `docs/appstore-metadata.md`
- セーブ移行: `.claude/rules/save-compat-ledger.md`（B-1）/ 広告: `docs/unity-ads-iap-decision-brief.md`
- 実機デプロイ: `unity/setup/ios-device-deploy.sh`
