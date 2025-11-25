# 推奨コマンド

## ビルド・実行

### シミュレータでビルド
```bash
xcodebuild -scheme EscapeNine-endless- \
  -destination 'id=5B405E6E-0F9F-4715-A97C-D2E85987CB53' \
  build
```

### Xcodeでプロジェクトを開く
```bash
open EscapeNine-endless-/EscapeNine-endless-.xcodeproj
```

### シミュレータ一覧を確認
```bash
xcrun simctl list devices
```

### 実機ビルド（開発証明書が必要）
```bash
xcodebuild -scheme EscapeNine-endless- \
  -destination 'generic/platform=iOS' \
  build
```

## Git操作

### ステータス確認
```bash
git status
```

### 変更をコミット
```bash
git add .
git commit -m "コミットメッセージ"
```

### プッシュ
```bash
git push origin main
```

### ブランチ作成
```bash
git checkout -b feature/新機能名
```

## macOS (Darwin) 特有コマンド

### ファイル検索
```bash
find . -name "*.swift"
```

### ファイル内容検索
```bash
grep -r "検索文字列" .
```

### ディレクトリ一覧
```bash
ls -la
```

### ディレクトリ移動
```bash
cd ディレクトリ名
```

### ファイルの内容表示
```bash
cat ファイル名
```

## テスト（未実装の場合もあり）

### ユニットテスト実行
```bash
xcodebuild test -scheme EscapeNine-endless- \
  -destination 'platform=iOS Simulator,name=iPhone 15'
```

## クリーンアップ

### ビルド成果物削除
```bash
xcodebuild clean -scheme EscapeNine-endless-
```

### DerivedData削除
```bash
rm -rf ~/Library/Developer/Xcode/DerivedData
```

## デバッグ

### ログ確認（シミュレータ実行中）
Xcodeのコンソールで確認するか、以下のコマンド:
```bash
xcrun simctl spawn booted log stream --predicate 'processImagePath endswith "EscapeNine-endless-"'
```

## SwiftLint（導入されている場合）

### Lint実行
```bash
swiftlint
```

### Lint自動修正
```bash
swiftlint --fix
```

## その他便利コマンド

### Xcodeバージョン確認
```bash
xcodebuild -version
```

### Swift バージョン確認
```bash
swift --version
```

### CocoaPods（使用している場合）
```bash
pod install
pod update
```

### 自動Gitプッシュスクリプト
プロジェクトルートに `auto-git-push.sh` があります。
