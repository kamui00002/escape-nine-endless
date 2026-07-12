# Unity 実機セッション ランブック ⭐️☀️

> **目的**: 次に iPhone を繋いだ時、これを**上から順に実行するだけ**で「溜まった実機検証 + 広告実装 + 提出準備」を片付けられるようにした turnkey 手順。
> 作成 2026-07-12（Phase 3 コード側 2/3 完了時点。分析・世界ランキングは実装+コンパイル+REST/パーサ検証済、実機発火のみ未確認）。
> 前提値: iPhone 17 Pro UDID `87F3A267-5378-5F81-A2F5-2AA1DFE1A51A`（devicectl）/ ハードUDID `00008150-001A11EA3441401C`（xcodebuild -destination）/ テスト bundle `com.yoshidometoru.escapenine.unity` / ミラー `~/EscapeNineUnity` / deploy: `unity/setup/ios-device-deploy.sh`。

---

## 0. 事前準備（デバイス）★これが無いと全部失敗する

1. iPhone を **USB 接続**
2. **設定 → 画面表示と明るさ → 自動ロック → 「なし」**（ビルド中に再ロックされると launch が `Locked` で失敗する。今セッションで2回踏んだ罠）
3. iPhone を**ロック解除したまま**放置
4. 確認: `xcrun devicectl list devices | grep -i 17pro` が **`available`** になっているか（`unavailable` なら接続/解除やり直し）

---

## 1. 分析・ランキングの実機発火確認（広告より先・既存ビルドで可）

コードは検証済み（分析=PostHog REST 契約実証 / ランキング=REST全往復 curl 実証 + パーサ device-free 検証）。残りは「実プレイでアプリが実際に送るか」。

1. デプロイ: `bash unity/setup/ios-device-deploy.sh`（Unityビルド→xcodebuild→install→launch、〜15分）
2. **console 添付でログ捕捉**（Unity Debug.Log を拾える。要ロック解除）:
   ```bash
   xcrun devicectl device process launch --console --terminate-existing \
     --device 87F3A267-5378-5F81-A2F5-2AA1DFE1A51A \
     com.yoshidometoru.escapenine.unity > ~/console-verify.log 2>&1 &
   ```
3. アプリで **1ゲームプレイ**（ゲーム開始→1〜2階クリア→死亡→もう一回タップ）。ランキング画面も開く。
4. ログ確認（別ターミナル/grep）:
   - 分析: `[AnalyticsService] 送信成功: eg_game_started` / `eg_floor_cleared` / `eg_game_over_shown` / `eg_retry_tapped`
   - ランキング認証: `[OnlineRankingService]`（signUp/refresh の往復・エラーが出ていないこと）
   - ランキング送信: `[OnlineRankingService] スコア送信成功: floor=N`（自己ベスト更新時のみ発火）
5. **客観裏取り**（アプリ報告に頼らない）:
   - 分析: PostHog MCP で project 467042 を `distinct_id`=端末の `analyticsDistinctId`（plist吸い出し: `xcrun devicectl device copy from --domain-type appDataContainer --domain-identifier com.yoshidometoru.escapenine.unity --source "Library/Preferences/com.yoshidometoru.escapenine.unity.plist"` → `plutil -p`）で照会し `eg_*` 着弾を確認
   - ランキング: Firebase MCP で `firestore_query_collection rankings`（floor降順）に**端末UID（plist の `firebaseLocalId`）の新規doc**が出たか確認。※テスト行を残したくない場合は `firestore_delete_document rankings/<そのUID>` で消す
   - RankingScreen: Cloud タブに世界順位が出て、自分の行がハイライトされるか目視

---

## 2. 広告（③）実装 ★重いネイティブSDK・ビルド破壊リスク大なので慎重に

**調査済みの具体手順**（版・依存確定済み）:

1. **manifest 追加**（ミラー `~/EscapeNineUnity/Packages/manifest.json`）:
   - dependencies に2行:
     ```json
     "com.google.ads.mobile": "11.2.0",
     "com.google.ads.mobile.mediation.unity": "3.18.1",
     ```
   - scopedRegistries の openupm scope 配列に `"com.google"` を追加（EDM4U `com.google.external-dependency-manager` も openupm 経由で解決）
2. **★ isolated build-green 確認を先に**（manifest 変更だけで Unity ビルドが通るか。壊れたら即2行を消して切り戻し=IAPと同じ規律。1変更ずつ、attributable に）:
   ```bash
   cd ~/EscapeNineUnity && /Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity \
     -batchmode -quit -projectPath ~/EscapeNineUnity -buildTarget iOS \
     -executeMethod EscapeNine.EditorTools.BuildScripts.BuildIOS -logFile ~/build-ads.log
   cat ~/EscapeNineUnity/build-ios-result.txt
   ```
   ※ EDM4U がネイティブ pod を Podfile に足す。Unityビルド green でも pod install/xcodebuild は別=**実機 deploy まで通して初めて広告SDKが本当にリンクできたと言える**。
3. **AdMobService 実装**（implementer, model sonnet）: 既存 stub 継ぎ目 `Runtime/Ads/IAdService.cs`/`StubAdService.cs`/`AdConfig.cs`（本番広告ID入り）に対し `AdMobService : IAdService` を1クラス実装。GMA Unity API（`MobileAds.Initialize` / `BannerView` / `InterstitialAd`）で。App.cs の `new StubAdService(Player)` を1行差し替え。※実キー無しの stub と違い本物 API を叩くので、パッケージ導入後（実型が見える）に実装＝IAP/分析と同じ順序。
4. **iOS Player Settings 注入**（PostProcessBuild or Info.plist相当）: `GADApplicationIdentifier`（AdConfig.AppId）・`SKAdNetworkItems`・`NSUserTrackingUsageDescription`・**`NSPrivacyTrackingDomains`（ITMS-91064 対策・空だと自動却下、別アプリで実例あり）**。
5. **ATT/consent 実結線**: init=denied → ATTダイアログ許可後に granted（`feedback_consent_auth_antipatterns` 厳守。既存 StubAdService の状態機械を踏襲）。
6. **実機 + 実広告アカウント検証**: テスト広告ID→本番の順。バナー（ホーム下部）・インタースティシャル（ゲームオーバー→リトライ）が出るか。広告削除購入で消えるか。

---

## 3. スクショ/動画の新調（提出直前・要オーナーの目）

Unity版は HD-2D で見た目が別物＝ストア画像の新調必須。撮る候補シーン（新ビジュアル訴求）:
- ホーム画面（新UI・世界最高階表示）
- ゲームプレイ中（3D舞台・奥行き・ビート演出）
- ボス階（専用ボス絵4体・足元アオーラ）
- 世界ランキング画面（今回実装・"世界と競う"訴求）
- ローグライク要素（レリックドラフト）
- キャラ選択（AI再生成した9体）

必要: 6.7"/6.5"/5.5" 解像度 × 4言語（ja/en/ko/zh-TW、`docs/appstore-metadata.md` §5）。動画は Seedance プロンプト（`marketing/seedance-prompts/`）を新ビジュアルへ更新。**実機/シミュレータで実フレーム撮影→編集**。オーナー在席で素材撮りから一緒に。

---

## 4. 提出手続き（全部の後・不可逆判断あり）

1. **★ `BuildScripts.BuildIOS` の `BuildOptions.Development` → `BuildOptions.None`（Release）に戻す**（デバッグパネル DangerZone を出荷しない。`BuildScripts.cs:135`）。Phase 0 リズム精度・60fps 実測も Release で。
2. **bundle 差し替え判断（不可逆・オーナー最終決定）**: `BuildScripts.cs` の `IosBundleId` を `com.yoshidometoru.escapenine.unity` → 本番 `com.souatou.escapenine` に。紐づく provisioning/entitlements（Sign in with Apple・Game Center）も本番App IDへ。→ 既存アプリのアップデートとして配信（レビュー/順位資産維持）。※初回起動で「ランキング刷新」お知らせが出る（実装済 e7f34e5）。
3. **ASC**: テスト bundle は ASC アプリ登録が無い。本番 bundle で配布用プロビジョニング + Release アーカイブ + TestFlight upload（`make testflight` 相当）→ 審査。ASC「10ターン」文言の en/ko/zh-TW 翻訳修正も（`docs/appstore-metadata.md` §ASC TODO）。

---

## 現状サマリ（2026-07-12 時点）
- Phase 3 収益化: ①分析 ✅ / ②世界ランキング+お知らせ ✅（コード・REST/パーサ検証済） / ③広告 ⬜（上記手順で・実機必須）
- 全コミット push 済み（branch `claude/ultraplan-growth-features-vqozes`）。詳細はメモリ [[unity-migration-phase-status]]。
