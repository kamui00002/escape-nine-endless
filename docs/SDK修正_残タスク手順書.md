# SDK修正 残タスク手順書 ☀️

**作成日**: 2026-04-15
**対象プロジェクト**: Escape Nine: Endless / ParkPedia

---

## ステータス一覧

| タスク | サブタスク | ステータス | 備考 |
|--------|-----------|-----------|------|
| Task 2: ParkPedia ATT対応 | 2-1. app.config.js に NSUserTrackingUsageDescription 追加 | [x] 完了 | |
| | 2-2. expo-tracking-transparency 追加 | [x] 完了 | |
| | 2-3. plugins に追加 | [x] 完了 | |
| | 2-4. ATTダイアログ実装 | [x] 完了 | |
| | 2-5. ビルド・テスト | [x] 完了 | |
| Task 3: Facebook SDK導入 | 3-1. Escape Nine: Facebook SDK追加（SPM） | [x] 完了 | Info.plistにプレースホルダー設定済み |
| | 3-2. Escape Nine: Info.plist に追加 | [x] 設定済み | `{FB_APP_ID}` / `{FB_CLIENT_TOKEN}` をMeta Business Suiteの値に置換が必要 |
| | 3-3. Escape Nine: SKAdNetworkItems に Meta用ID追加 | [x] 設定済み | `v9wttpbfk9` / `n38lu8286q` 追加済み |
| | 3-4. Escape Nine: App初期化コード追加 | [x] 実装済み | `EscapeNine_endless_App.swift` に `#if canImport(FacebookCore)` ブロック追加済み |
| | 3-5. ParkPedia: react-native-fbsdk-next 追加 | [x] 完了 | |
| | 3-6. ParkPedia: app.config.js に追加 | [x] 完了 | |
| | 3-7. ParkPedia: ビルド | [x] 完了 | |
| Task 5: プロモーション動画作成 | 動画撮影・編集・書き出し | [x] 完了 | |

---

## 前提条件チェックリスト（実施前に確認）

- [ ] Meta Business Suite でアプリが作成済みか → `FB_APP_ID` と `FB_CLIENT_TOKEN` を取得
- [ ] Google Ads アカウントでキャンペーン設定済みか
- [ ] ParkPedia リポジトリのローカルパスを確認（以下 `{PARKPEDIA_ROOT}` と表記）
- [ ] Expo CLI がインストール済みか（`npx expo --version` で確認）
- [ ] EAS CLI がインストール済みか（`eas --version` で確認、なければ `npm install -g eas-cli`）

---

## Task 2: ParkPedia — ATT対応（Expo/React Native）

### 2-1. app.config.js に NSUserTrackingUsageDescription 追加

**ファイル**: `{PARKPEDIA_ROOT}/app.config.js`

**手順**:

1. `app.config.js` を開く

2. `ios` セクション内に `infoPlist` がなければ追加し、`NSUserTrackingUsageDescription` を設定する

```js
// app.config.js の ios セクション内
ios: {
  // ...既存設定...
  infoPlist: {
    // ...既存の infoPlist 設定があればそのまま残す...
    NSUserTrackingUsageDescription: '広告の最適化のためトラッキング許可をお願いします。',
  },
},
```

**確認方法**: `app.config.js` を保存し、`npx expo config` で出力される JSON の `ios.infoPlist.NSUserTrackingUsageDescription` に値が入っていることを確認。

```bash
cd {PARKPEDIA_ROOT}
npx expo config | grep -A2 NSUserTrackingUsageDescription
```

---

### 2-2. expo-tracking-transparency パッケージ追加

**手順**:

```bash
cd {PARKPEDIA_ROOT}
npx expo install expo-tracking-transparency
```

**確認方法**: `package.json` の `dependencies` に `expo-tracking-transparency` が追加されていること。

```bash
cat package.json | grep expo-tracking-transparency
```

期待出力:
```
"expo-tracking-transparency": "~X.X.X"
```

---

### 2-3. plugins に expo-tracking-transparency を追加

**ファイル**: `{PARKPEDIA_ROOT}/app.config.js`

**手順**:

1. `app.config.js` の `plugins` 配列に以下を追加

```js
// app.config.js
plugins: [
  // ...既存のプラグイン...
  [
    'expo-tracking-transparency',
    {
      userTrackingPermission: '広告の最適化のためトラッキング許可をお願いします。',
    },
  ],
],
```

**注意**: `userTrackingPermission` は手順 2-1 の `NSUserTrackingUsageDescription` と同じ文言にすること。plugin 側の設定が `Info.plist` の値を上書きするため、こちらが優先される。

**確認方法**:
```bash
cd {PARKPEDIA_ROOT}
npx expo config | grep -A5 expo-tracking-transparency
```

---

### 2-4. ATTダイアログ実装

**ファイル**: `{PARKPEDIA_ROOT}/App.js`（または `App.tsx`、ルートコンポーネント）

**手順**:

1. ファイル先頭に import 追加

```js
import { useEffect } from 'react';
import { requestTrackingPermissionsAsync } from 'expo-tracking-transparency';
```

2. ルートコンポーネントの関数本体に `useEffect` を追加

```js
export default function App() {
  useEffect(() => {
    (async () => {
      const { status } = await requestTrackingPermissionsAsync();
      console.log('[ATT] Tracking status:', status);
      // status は 'granted' | 'denied' | 'undetermined' | 'restricted'
    })();
  }, []);

  // ...既存の return 文...
}
```

**注意事項**:
- ATT ダイアログは iOS 14.5+ でのみ表示される
- シミュレーターでは「設定 → プライバシーとセキュリティ → トラッキング」をリセットしないと再表示されない
- `requestTrackingPermissionsAsync()` は1回だけ呼ぶこと（2回目以降はダイアログが出ず即座にステータスを返す）

**確認方法**: 実機またはシミュレーターで起動し、ATT ダイアログが表示されることを確認。Console に `[ATT] Tracking status: granted` または `denied` が出力される。

---

### 2-5. ビルド・テスト

**手順**:

1. ローカルでビルド確認（型エラー等がないか）

```bash
cd {PARKPEDIA_ROOT}
npx expo prebuild --clean
```

2. EAS ビルド（preview プロファイル）

```bash
cd {PARKPEDIA_ROOT}
eas build --platform ios --profile preview
```

3. ビルド成功後、TestFlight またはシミュレーターで以下を確認:
   - [ ] アプリ起動直後に ATT ダイアログが表示される
   - [ ] 「許可」選択後、Console に `status: granted` が出る
   - [ ] 「許可しない」選択後、Console に `status: denied` が出る
   - [ ] 2回目起動時にはダイアログが再表示されない

---

## Task 3: Meta広告用 Facebook SDK導入

### Escape Nine (Swift) — Task 3-1〜3-4

> **現状確認**: Info.plist にプレースホルダー（`{FB_APP_ID}` / `{FB_CLIENT_TOKEN}`）が設定済み。SKAdNetworkItems に Meta 用 ID（`v9wttpbfk9` / `n38lu8286q`）追加済み。App初期化コード（`#if canImport(FacebookCore)` ブロック）も `EscapeNine_endless_App.swift` に実装済み。

#### 3-1. Facebook SDK 追加（SPM）

**手順**:

1. Xcode でプロジェクトを開く

```bash
open /Users/yoshidometoru/Documents/GitHub/escape-nine-endless/EscapeNine-endless-/EscapeNine-endless-.xcodeproj
```

2. メニュー: **File → Add Package Dependencies...**

3. 検索バーに以下の URL を入力:
```
https://github.com/facebook/facebook-ios-sdk
```

4. バージョン設定:
   - **Dependency Rule**: `Up to Next Major Version`
   - **Version**: `17.0.0` 以上（最新安定版を選択）

5. **Add Package** をクリック

6. プロダクト選択画面で **FacebookCore** のみにチェックを入れる（他は不要）
   - [x] FacebookCore
   - [ ] FacebookLogin（不要）
   - [ ] FacebookShare（不要）
   - [ ] FacebookGamingServices（不要）

7. **Add Package** をクリックして完了

**確認方法**: Xcode 左パネルの **Package Dependencies** に `facebook-ios-sdk` が表示されること。

```bash
# Package.resolved で確認
cat /Users/yoshidometoru/Documents/GitHub/escape-nine-endless/EscapeNine-endless-/EscapeNine-endless-.xcodeproj/project.xcworkspace/xcshareddata/swiftpm/Package.resolved | grep facebook
```

8. ビルドして `#if canImport(FacebookCore)` ブロックが有効になることを確認

```bash
cd /Users/yoshidometoru/Documents/GitHub/escape-nine-endless/EscapeNine-endless-
xcodebuild -scheme EscapeNine-endless- \
  -destination 'id=5B405E6E-0F9F-4715-A97C-D2E85987CB53' \
  build 2>&1 | tail -20
```

期待: `[App] Facebook SDK初期化完了` が起動時のログに出る。

---

#### 3-2. Info.plist のプレースホルダーを実際の値に置換

**ファイル**: `/Users/yoshidometoru/Documents/GitHub/escape-nine-endless/EscapeNine-endless-/EscapeNine-endless-/Info.plist`

**現在の状態**: プレースホルダー `{FB_APP_ID}` / `{FB_CLIENT_TOKEN}` が設定済み。

**手順**:

1. Meta Business Suite（https://business.facebook.com/）にログイン

2. **アプリ設定 → ベーシック** から以下を取得:
   - **アプリID**: 数字の文字列（例: `1234567890123456`）
   - **クライアントトークン**: **アプリ設定 → 詳細** から取得（例: `abcdef1234567890abcdef1234567890`）

3. Info.plist の以下の3箇所を置換:

```xml
<!-- 置換前 -->
<key>FacebookAppID</key>
<string>{FB_APP_ID}</string>
<key>FacebookClientToken</key>
<string>{FB_CLIENT_TOKEN}</string>
...
<key>CFBundleURLSchemes</key>
<array>
    <string>fb{FB_APP_ID}</string>
</array>

<!-- 置換後（例: アプリID が 1234567890123456 の場合） -->
<key>FacebookAppID</key>
<string>1234567890123456</string>
<key>FacebookClientToken</key>
<string>abcdef1234567890abcdef1234567890</string>
...
<key>CFBundleURLSchemes</key>
<array>
    <string>fb1234567890123456</string>
</array>
```

**注意**: `CFBundleURLSchemes` の値は `fb` + アプリID（スペースなし）。

**確認方法**: Xcode で Info.plist を開き、値がプレースホルダーでないことを確認。

---

#### 3-3. SKAdNetworkItems に Meta用ID追加（設定済み）

**ステータス**: 完了済み

Info.plist に以下の2つの Meta 用 SKAdNetwork ID が既に追加されている:
- `v9wttpbfk9.skadnetwork`
- `n38lu8286q.skadnetwork`

**確認方法**:
```bash
grep -A1 'v9wttpbfk9\|n38lu8286q' /Users/yoshidometoru/Documents/GitHub/escape-nine-endless/EscapeNine-endless-/EscapeNine-endless-/Info.plist
```

---

#### 3-4. App初期化コード追加（実装済み）

**ステータス**: 完了済み

**ファイル**: `/Users/yoshidometoru/Documents/GitHub/escape-nine-endless/EscapeNine-endless-/EscapeNine-endless-/EscapeNine_endless_App.swift`

以下のコードが既に実装されている:

```swift
#if canImport(FacebookCore)
import FacebookCore
#endif

// init() 内:
#if canImport(FacebookCore)
ApplicationDelegate.shared.application(
    UIApplication.shared,
    didFinishLaunchingWithOptions: nil
)
print("[App] Facebook SDK初期化完了")
#endif
```

**注意**: SPM で FacebookCore パッケージを追加（3-1）するまで `#if canImport(FacebookCore)` は false となり、このブロックはコンパイルされない。パッケージ追加後に自動的に有効になる。

---

#### Escape Nine 最終ビルド確認

3-1〜3-2 完了後に実行:

```bash
cd /Users/yoshidometoru/Documents/GitHub/escape-nine-endless/EscapeNine-endless-
xcodebuild -scheme EscapeNine-endless- \
  -destination 'id=5B405E6E-0F9F-4715-A97C-D2E85987CB53' \
  build 2>&1 | tail -20
```

確認項目:
- [ ] ビルドが `BUILD SUCCEEDED` で完了する
- [ ] 実行時ログに `[App] Facebook SDK初期化完了` が出力される
- [ ] ATT ダイアログが引き続き正常に表示される

---

### ParkPedia (Expo) — Task 3-5〜3-7

#### 3-5. react-native-fbsdk-next パッケージ追加

**手順**:

```bash
cd {PARKPEDIA_ROOT}
npx expo install react-native-fbsdk-next
```

**確認方法**:
```bash
cat {PARKPEDIA_ROOT}/package.json | grep react-native-fbsdk-next
```

期待出力:
```
"react-native-fbsdk-next": "~X.X.X"
```

---

#### 3-6. app.config.js に Facebook SDK 設定追加

**ファイル**: `{PARKPEDIA_ROOT}/app.config.js`

**手順**:

1. Meta Business Suite から取得した `FB_APP_ID` と `FB_CLIENT_TOKEN` を用意

2. `plugins` 配列に以下を追加（Task 2-3 の `expo-tracking-transparency` の後ろに追加）:

```js
// app.config.js
plugins: [
  // ...既存のプラグイン...
  [
    'expo-tracking-transparency',
    {
      userTrackingPermission: '広告の最適化のためトラッキング許可をお願いします。',
    },
  ],
  // ↓ 追加
  [
    'react-native-fbsdk-next',
    {
      appID: '{FB_APP_ID}',           // 例: '1234567890123456'
      clientToken: '{FB_CLIENT_TOKEN}', // 例: 'abcdef1234567890abcdef1234567890'
      displayName: 'ParkPedia',
      advertiserIDCollectionEnabled: true,
      autoLogAppEventsEnabled: true,
      isAutoInitEnabled: true,
    },
  ],
],
```

**注意**:
- `appID` と `clientToken` は文字列（シングルクォートで囲む）
- `advertiserIDCollectionEnabled: true` — Meta 広告の計測に必要
- `autoLogAppEventsEnabled: true` — アプリイベントの自動ログ
- `isAutoInitEnabled: true` — アプリ起動時に自動初期化（別途初期化コード不要）

**確認方法**:
```bash
cd {PARKPEDIA_ROOT}
npx expo config | grep -A10 react-native-fbsdk-next
```

---

#### 3-7. ParkPedia ビルド

**手順**:

1. prebuild でネイティブプロジェクトを再生成

```bash
cd {PARKPEDIA_ROOT}
npx expo prebuild --clean
```

2. EAS ビルド

```bash
cd {PARKPEDIA_ROOT}
eas build --platform ios --profile preview
```

3. ビルド成功後の確認項目:
   - [ ] ビルドが正常に完了する
   - [ ] TestFlight でインストール後、ATT ダイアログが表示される
   - [ ] Facebook SDK がクラッシュなく初期化される（Console にエラーなし）
   - [ ] Meta Business Suite の「イベントマネージャ」でアプリイベントが受信されている

---

## Task 5: プロモーション動画作成

### 動画仕様

#### Google Ads（App Campaign）

| 項目 | 要件 |
|------|------|
| アスペクト比 | 横長 16:9 / 縦長 9:16 / 正方形 1:1 |
| 解像度 | 1920x1080 / 1080x1920 / 1080x1080 |
| 長さ | 10秒〜60秒 |
| 形式 | MP4 |
| 最大サイズ | 100MB |

#### Meta広告

| 項目 | 要件 |
|------|------|
| アスペクト比 | 1:1 / 4:5 / 9:16 |
| 解像度 | 1080x1080 / 1080x1350 / 1080x1920 |
| 長さ | 15秒〜60秒推奨 |
| 形式 | MP4 / MOV |
| 最大サイズ | 4GB |

#### 共通で必要な動画バリエーション

| 用途 | アスペクト比 | 解像度 | 両プラットフォーム対応 |
|------|-------------|--------|----------------------|
| 正方形 | 1:1 | 1080x1080 | Google Ads + Meta |
| 縦長 | 9:16 | 1080x1920 | Google Ads + Meta |
| 横長 | 16:9 | 1920x1080 | Google Ads のみ |
| 縦長（4:5） | 4:5 | 1080x1350 | Meta のみ |

→ 最低4種類の書き出しが必要。素材は共通で使い回し、編集ツールでアスペクト比を変えて書き出す。

---

### 動画作成手順

#### Step 1: シミュレーター録画（ゲームプレイ素材）

```bash
# シミュレーターを起動
xcrun simctl boot 5B405E6E-0F9F-4715-A97C-D2E85987CB53

# アプリをビルド・実行
cd /Users/yoshidometoru/Documents/GitHub/escape-nine-endless/EscapeNine-endless-
xcodebuild -scheme EscapeNine-endless- \
  -destination 'id=5B405E6E-0F9F-4715-A97C-D2E85987CB53' \
  build

# 録画開始（Ctrl+C で停止）
xcrun simctl io booted recordVideo ~/Desktop/escape_nine_gameplay.mp4
```

**撮影するシーン**（推奨）:
1. ホーム画面 → キャラクター選択（2〜3秒）
2. ゲーム開始カウントダウン 3→2→1→GO!（3〜4秒）
3. 実際のゲームプレイ（移動・スキル使用）（10〜15秒）
4. 階層クリア演出（3秒）
5. BPM加速した高難度プレイ（5〜10秒）
6. ゲームオーバー → リザルト画面（3秒）

#### Step 2: 実機プレイ動画撮影（オプション、より高品質）

1. iPhone を Mac に接続
2. QuickTime Player → **ファイル → 新規ムービー収録** → iPhone を選択
3. ゲームをプレイしながら録画
4. 録画ファイルを `~/Desktop/escape_nine_device.mov` に保存

#### Step 3: 動画編集

**推奨ツール**: Canva（無料プラン可）または CapCut（無料）

**編集内容**:

1. **構成案（30秒版）**:
   - 0〜3秒: フック（「9マスで逃げ切れ!」テキスト + ゲーム画面チラ見せ）
   - 3〜8秒: ゲームプレイ（通常速度）
   - 8〜15秒: スキル使用シーン（ダッシュ・透明化等）
   - 15〜22秒: BPM加速した高難度プレイ（緊張感）
   - 22〜27秒: クリア演出 or ゲームオーバー
   - 27〜30秒: アプリ名 + App Store ロゴ + CTA（「無料ダウンロード」）

2. **追加要素**:
   - テキストオーバーレイ（日本語、簡潔に）
   - BGM（ゲーム内 BGM またはフリー素材）
   - トランジション（カット or フェード、過剰なエフェクトは避ける）

#### Step 4: 各アスペクト比で書き出し

Canva の場合:
1. 1080x1080（正方形）で編集 → MP4 書き出し
2. **サイズ変更** → 1080x1920（縦長 9:16）→ レイアウト調整 → MP4 書き出し
3. **サイズ変更** → 1920x1080（横長 16:9）→ レイアウト調整 → MP4 書き出し
4. **サイズ変更** → 1080x1350（縦長 4:5）→ レイアウト調整 → MP4 書き出し

**ファイル命名規則**:
```
escape_nine_promo_1080x1080.mp4   # 正方形（Google Ads + Meta）
escape_nine_promo_1080x1920.mp4   # 縦長 9:16（Google Ads + Meta）
escape_nine_promo_1920x1080.mp4   # 横長 16:9（Google Ads）
escape_nine_promo_1080x1350.mp4   # 縦長 4:5（Meta）
```

**書き出し設定**:
- コーデック: H.264
- ビットレート: 8〜15 Mbps
- フレームレート: 30fps
- 音声: AAC 128kbps 以上

#### Step 5: アップロード

**Google Ads**:
1. Google Ads → キャンペーン → アセット → 動画追加
2. YouTube にアップロード（限定公開）→ URL を Google Ads に入力
3. 横長・縦長・正方形の3本をそれぞれ追加

**Meta広告**:
1. Meta Ads Manager → 広告セット → クリエイティブ
2. 動画を直接アップロード（YouTube 不要）
3. 正方形・4:5・9:16 の3本をそれぞれ追加

---

## 実行順序（推奨）

1. **前提条件チェックリスト** を全て完了させる（特に `FB_APP_ID` / `FB_CLIENT_TOKEN` の取得）
2. **Task 3-1〜3-2**: Escape Nine に Facebook SDK を SPM で追加 + Info.plist のプレースホルダー置換 → ビルド確認
3. **Task 2**: ParkPedia の ATT 対応（2-1〜2-5）
4. **Task 3-5〜3-7**: ParkPedia に Facebook SDK 導入 → ビルド確認
5. **Task 5**: プロモーション動画作成（並行作業可能）

---

## トラブルシューティング

### Facebook SDK ビルドエラー（Escape Nine）

**症状**: `No such module 'FacebookCore'`

**原因**: SPM パッケージが正しく追加されていない

**対処**:
1. Xcode → **File → Packages → Reset Package Caches**
2. Xcode → **File → Packages → Resolve Package Versions**
3. クリーンビルド: **Product → Clean Build Folder** (Cmd+Shift+K)
4. 再ビルド

### ATT ダイアログが表示されない（ParkPedia）

**症状**: アプリ起動時に ATT ダイアログが出ない

**原因候補**:
- シミュレーターの場合: 設定 → プライバシーとセキュリティ → トラッキング が既に許可/拒否済み
- `expo-tracking-transparency` の plugin 設定が抜けている
- `prebuild --clean` を実行していない

**対処**:
1. シミュレーターの場合: `xcrun simctl privacy booted reset all` でリセット
2. `npx expo prebuild --clean` を再実行
3. 再ビルド・再インストール

### EAS ビルド失敗

**症状**: `eas build` がエラーで終了

**対処**:
1. ビルドログを確認: `eas build:view`
2. `npx expo-doctor` で依存関係の問題を診断
3. `node_modules` を削除して再インストール:
```bash
rm -rf node_modules
npm install
npx expo prebuild --clean
eas build --platform ios --profile preview
```
