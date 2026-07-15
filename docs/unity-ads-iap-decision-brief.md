# Unity 版 広告 / IAP 統合 決定ブリーフ ⭐️

作成: 2026-07-07（オーナー要望「広告SDK導入とか」を受け、advisor 助言に基づき**コードでなく判断材料**を先に整備）。
実コード（ネイティブSDK追加）は**オーナー在席＆実機テスト可能時**に着手する。理由: manifest への
ネイティブ広告/IAP パッケージ追加は現在動作中の iOS ビルド（UI/傾き/リング一式が乗っている）を壊し得る
唯一の操作であり、広告/consent/ATT フローは実機＋広告アカウントでの検証が必須のため。

関連: `docs/収益化設定ガイド.md`（Swift 版の本番手順）/ Obsidian `unity_ads_mediation.md`（メディエーションの罠）/
`docs/unity-migration-plan.md` §Phase 3。

---

## 0. 前提: 移行プランは Phase 3 を後ろ倒し

プラン §207 の判断: 「実プレイヤー約2名の現状で IAP/広告の結線は価値が低い。人が入ってから結線しても遅くない」。
→ 本ブリーフは**やるなら何を選び何を再利用するか**を確定させ、着手時に迷わないようにするもの。急ぐ必要はない。

---

## 1. 最重要の未決事項: メディエーションの選択（オーナー判断）

**配信中の iOS(Swift) 版は AdMob を主軸 + Unity Ads メディエーション**で動いている。これを Unity 版でどう踏襲するか。

| | A. AdMob Unity plugin + Unity Ads メディエーション（継続性） | B. LevelPlay（Unity 純正・旧 ironSource） |
|---|---|---|
| 統合の楽さ | △ 重い（ネイティブ依存・アダプタ版固定の罠あり） | ◎ Package Manager で楽（プラン §53「Unity 純正で統合が楽」） |
| 既存資産の再利用 | ◎ **同じ AdMob App/ユニットID・同じ Unity Ads Game ID・最適化済みウォーターフォールをそのまま**流用 | ✗ 新しいメディエーションスタック。ダッシュボード/アカウント/最適化を新規に |
| 収益の連続性 | ◎ 配信中アプリと同条件＝eCPM 実績が引き継げる | △ 立ち上げ直後は最適化前 |
| 既知の罠 | Unity Ads アダプタは Exact 4.16.500 固定（4.16.601+ は GMA 13 必須）。Obsidian `unity_ads_mediation.md` 参照 | ネットワークアダプタを LevelPlay 側で別途構成 |
| dashboard | AdMob（既存） | Unity/LevelPlay（新規） |

**推奨: A（AdMob Unity plugin で継続）。** 理由: 既に収益が出ている構成（AdMob+Unity Ads、Game ID `800002603`、
最適化済み）を**そっくり再利用**できる＝ID もウォーターフォールも作り直し不要。LevelPlay は統合は楽だが
「動いている収益構成を捨てて別スタックを新規に立てる」コストの方が大きい。
**ただし**「Unity で完結させたい/将来 Android と統合したい」なら B も合理的 → **オーナー決定事項**。

---

## 2. 再利用できる既存 ID（Swift 版 = 配信中アプリと同一に揃える）

> AdMob の App ID / 広告ユニット ID は**公開識別子**（アプリバイナリに埋め込まれ秘匿性なし）。IAP 商品 ID も同様。
> よって本ドキュメントに記載可（`feedback_no_creds_in_docs` が禁じるのは審査用デモ垢/API鍵であり、これらは該当しない）。

### 2.1 IAP 商品（非消耗型 4 種、App Store Connect で承認済み・そのまま流用）
| 商品 | Product ID | 価格 |
|---|---|---|
| 魔法使い | `com.escapenine.endless.character.wizard` | ¥240 |
| エルフ | `com.escapenine.endless.character.elf` | ¥240 |
| ナイト | `com.escapenine.endless.character.knight` | ¥240 |
| 広告削除 | `com.escapenine.endless.removeads` | ¥480 |

（出典: Swift `Services/StoreKitService.swift` の ProductID enum）

### 2.2 AdMob（本番。出典: Swift `Services/AdMobService.swift` / `Info.plist`）
| 項目 | 値 |
|---|---|
| AdMob App ID (GADApplicationIdentifier) | `ca-app-pub-5237930968754753~9585848266` |
| バナー広告ユニット（本番） | `ca-app-pub-5237930968754753/3156438181` |
| インタースティシャル広告ユニット（本番） | `ca-app-pub-5237930968754753/7861969950` |
| Google テストバナー | `ca-app-pub-3940256099942544/2934735716` |
| Google テストインタースティシャル | `ca-app-pub-3940256099942544/4411468910` |

### 2.3 Unity Ads メディエーション（Obsidian `unity_ads_mediation.md`）
- Unity Ads Game ID: `800002603`（AdMob サーバー配信）
- アダプタ版: **Exact 4.16.500 固定**（4.16.601+ は GMA 13 必須になるため上げない）

---

## 3. 配置ルール（Swift 版と同じ挙動に）
- **バナー**: ホーム画面下部のみ（常時）。広告削除購入で非表示。
- **インタースティシャル**: ゲームオーバー→リトライ時。広告削除購入で非表示。
- **ATT**: iOS 14.5+ で起動時にトラッキング許可ダイアログ（consent は ATT 前に granted 禁止＝init=denied→ATT後切替。`feedback_consent_auth_antipatterns` 厳守）。
- Unity 側の結線先: バナー = HomeScreen、インタースティシャル = リザルト/ゲームオーバー導線、広告削除 = PlayerState/購入状態でゲート。

---

## 4. 着手時ランブック（オーナー在席＆実機接続時に実行。★=ビルドを壊し得る要注意手順）

### 4.1 IAP（Unity IAP）
1. ★ Package Manager で `com.unity.purchasing`（Unity IAP）を追加。→ manifest 解決・再インポート。**この直後に iOS ビルドが通るか実機まで確認**（壊れやすい最初の関門）。
2. `IAPService`（Runtime）を新規作成: 4 Product ID を登録、購入/復元、購入状態を PlayerState へ反映（`RemoveAds` フラグ・キャラ解放）。Swift `StoreKitService`/`PurchaseManager` の挙動に 1:1。
3. ShopScreen / MetaShop / CharacterScreen の購入 UI から呼ぶ。StoreKit Configuration file（`Products.storekit`）でローカルテスト。
4. Sandbox テスターで購入/復元を実機確認。

### 4.2 広告（推奨 A の場合）
1. ★ Google Mobile Ads Unity plugin を追加（Package/OpenUPM or unitypackage）。→ **iOS ビルド通過を実機まで確認**。
2. ★ Unity Ads メディエーションアダプタを **4.16.500 固定**で追加（版を上げない）。
3. `AdService`（Runtime）を新規作成: バナー（ホーム下部）+ インタースティシャル（リザルト）。App ID/ユニット ID は §2.2。DEBUG はテスト ID、Release は本番 ID（Swift `AdMobConfig` の `#if DEBUG` と同型）。
4. Info.plist 相当（iOS Player Settings/Post-process build）に GADApplicationIdentifier・SKAdNetworkItems・NSUserTrackingUsageDescription を注入（Swift Info.plist の値を流用）。
5. ATT ダイアログ + consent を結線（アンチパターン厳守）。
6. 実機 + 実広告アカウントでテスト広告→本番広告の順に検証。

### 4.3 検証ゲート（各 ★ の後で必ず）
- `console-get-logs Error` = 0 / EditMode 回帰 green / **iOS 実機ビルド Succeeded + 起動**。1 つでも欠けたらそのパッケージ追加を切り戻す。

---

## 5. まとめ / 進捗

- **メディエーション決定: A（AdMob 継続）** ✅ 2026-07-08 オーナー確定。
- **アプリ側 groundwork 実装済み** ✅ 2026-07-08（電話無しで安全な範囲）:
  - `Runtime/Ads/AdConfig.cs`（ID定数）/ `IAdService.cs`（継ぎ目）/ `StubAdService.cs`（no-op、AdRemoved は PlayerState 既存フラグ参照）。
  - 結線: `App.cs`（Ads 生成 + Initialize + ATT フック）/ `HomeScreen`（バナー show/hide）/ `ResultScreen.TriggerRetry`（リトライ前にインタースティシャル、全リトライ経路が通る）。
  - **ネイティブ SDK 未導入・manifest 不可触**。GMA/Unity Ads の実 API は一切呼んでいない（推測実装を避けた）。
  - 検証: Unity EditMode green（コンパイル + 回帰なし）。
- **残り（実機接続時に §4 ランブックで実行）**: ★ GMA Unity plugin + Unity Ads アダプタ(4.16.500) を manifest へ追加 → `AdMobService : IAdService` を1クラス実装 → `App.cs` の `new StubAdService(Player)` を1行差し替え → ATT/consent 実結線 → Info.plist 相当注入 → 実機 + 実広告アカウントで検証。**★ごとに iOS 実機ビルド通過を確認**（ここが今動作中のビルドを壊し得る唯一の危険手のため電話が要る）。

---

## 6. 実行ログ / 環境運用メモ

### 2026-07-10 セッション（着手前の環境整備）
- **Unity Editor は閉じている / MCP サーバ (pid 44824) は孤児化**（親=launchd）→ 対話 MCP は応答 null。**着手は CLI batchmode ルート**で行う（Editor 常駐不要、`unity/setup/ios-device-deploy.sh`）。次に Editor を GUI で開く時、孤児サーバがポート 26865 を掴んでいると MCP 再バインドが失敗し得る（その時は pid を kill してから起動）。
- **実機（iPhone 17 Pro, 87F3A267…）は devicectl から `unavailable`**（USB 未接続 / ロック中）。→ **ビルド通過確認までは実機不要でできるが、ATT / Sandbox 購入 / 実広告の目視検証は実機を available にしてから（次回）**。
- **deploy スクリプトを repo に恒久化**: `unity/setup/ios-device-deploy.sh`（前セッションの scratchpad にあった実物を移植、揮発対策 + 環境変数 override 対応）。
- **manifest 運用方針（確定）**: Unity 依存の唯一の真実源は **ミラー `~/EscapeNineUnity/Packages/manifest.json`**（rsync は `.cs` のみのため repo は `Packages/` を追跡しない）。**repo に manifest.json を置かない**（ミラーと乖離する「死んだ第2権威」を作らないため）。その代わり、**広告 / IAP で追加した依存行は必ず本節「追加依存ログ」に追記**して、ミラー消失時に手復元できるようにする。
- **baseline**: manifest を触る前に現ミラーの iOS ビルドが green か記録する（パッケージ追加で壊れた時に「元から/追加が原因か」を切り分けるため）。

### 追加依存ログ（manifest.json に足した行をここに記録）
- **2026-07-10 Unity IAP 導入**: ミラー `~/EscapeNineUnity/Packages/manifest.json` の dependencies に
  `"com.unity.purchasing": "5.4.1"` を追加（Unity 2022.3+ 対応 = 6000.3 で可）。依存 `com.unity.services.core 1.18.0`
  が自動解決される（packages-lock.json に記録・PackageCache に DL 済み）。
  **CLI batchmode iOS ビルド green**（result=Succeeded / errors=0 / IAP 由来の新規 CS 警告なし /
  sizeMB 1375→1501 = +126MB は IAP + services.core のネイティブ分）。既存コード・Stub 継ぎ目に影響なし。
  ※ ミラー消失時はこの1行を dependencies に足せば復元できる。
  **未了（次の実機セッションで一気通貫）**: (1) `UnityIapService : IIapService` を実装（PackageCache の
  `com.unity.purchasing@…` の実 API に合わせる。推測実装せず実型で検証）→ (2) `App.cs` の
  `new StubIapService(Player)` を差し替え → (3) StoreKit Configuration + Sandbox で実機購入/復元検証。
  広告（GMA + Unity Ads アダプタ 4.16.500）は **IAP が固まってから別 manifest 変更**で（1変更に2つ混ぜると
  ビルドが壊れた時に原因が切り分けられないため）。

- **2026-07-15 GMA 広告導入（GMA プラグイン単体・メディエーション後回し）**: ミラー `~/EscapeNineUnity/Packages/manifest.json` の
  dependencies に `"com.google.ads.mobile": "11.2.0"` と `"com.google.external-dependency-manager": "1.2.187"` を追加、
  scopedRegistries の openupm(`package.openupm.com`) の scopes に `"com.google"` を追加（GMA/EDM4U を openupm で解決）。
  **CLI batchmode iOS ビルド green**（result=Succeeded / **CS error 0**）。EDM4U の iOS Resolver が **batchmode でも自動実行**され、
  `Builds/ios/Podfile`（`pod 'Google-Mobile-Ads-SDK', '~> 13.4'` + `GoogleUserMessagingPlatform 3.1.0`）→ `pod install` →
  `Unity-iPhone.xcworkspace`（Pods 統合）まで生成された。
  - ★ **native GMA SDK は 13.4**（Unity plugin 11.2.0 が pull）。→ 将来 Unity Ads メディエーションアダプタを足す時は
    **4.16.601+（GMA 13 対応版）が必要**。上の「4.16.500 固定」は GMA 12 時代の記述で**今は不適用**（GMA 13 に上がったため）。
  - GMA 設定: `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` の `adMobIOSAppId` に App ID
    `ca-app-pub-5237930968754753~9585848266`、`userTrackingUsageDescription` に Swift 正本と同一の ATT 文言を設定
    （空だと GMA PListProcessor が `BuildMethodException: app ID is empty` で落ちる＝必須）。
  - deploy スクリプト `unity/setup/ios-device-deploy.sh` を **workspace 自動検出**に修正（Podfile 生成後は `-project` だと
    広告 SDK 未リンクのため `-workspace Unity-iPhone.xcworkspace` を使う。アドバイザー指摘）。
  - ※ ミラー消失時は上記 dependencies 2行 + `com.google` スコープ + GoogleMobileAdsSettings.asset の2値を復元すれば戻る。
  - **未了（本セッション継続）**: `AdMobService : IAdService` 実装（実機ビルド→テスト広告目視）→ App.cs 差し替え → deploy #2 →
    バナー(ホーム下部)+インタースティシャル(GO→リトライ) がテスト広告で出るか実機確認。Unity Ads メディエーション(4.16.601+)は後続。
