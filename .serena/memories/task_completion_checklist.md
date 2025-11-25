# タスク完了時のチェックリスト

## コード変更後の必須手順

### 1. ビルド確認
コード変更後は必ずビルドが通ることを確認:
```bash
xcodebuild -scheme EscapeNine-endless- \
  -destination 'id=5B405E6E-0F9F-4715-A97C-D2E85987CB53' \
  build
```

### 2. Lint（導入されている場合）
SwiftLintが導入されている場合は実行:
```bash
swiftlint
```

自動修正可能な問題があれば:
```bash
swiftlint --fix
```

### 3. テスト実行（テストが存在する場合）
```bash
xcodebuild test -scheme EscapeNine-endless- \
  -destination 'platform=iOS Simulator,name=iPhone 15'
```

### 4. コードレビュー
- 命名規則が守られているか
- 日本語コメントが適切に記載されているか
- MVVMアーキテクチャに沿っているか
- 定数は`Constants`または`GameColors`に定義されているか
- enumで型安全性が確保されているか

### 5. 動作確認
- シミュレータまたは実機で実際に動作確認
- 特に以下を確認:
  - レイアウトが崩れていないか
  - タップ・スワイプなどの操作が正常か
  - 画面遷移が正常か
  - エラーハンドリングが適切か

## Git コミット前

### 1. 変更内容の確認
```bash
git status
git diff
```

### 2. 不要なファイルを含めない
- ビルド成果物
- 一時ファイル
- `.DS_Store`
- IDE設定ファイル（`.xcuserstate`など）

### 3. コミットメッセージ
わかりやすいメッセージを記載:
```bash
git commit -m "feat: 新機能の説明"
git commit -m "fix: バグ修正の説明"
git commit -m "refactor: リファクタリングの説明"
git commit -m "docs: ドキュメント更新"
git commit -m "chore: その他の変更"
```

## 新機能追加時

### 1. ドキュメント更新
- CLAUDE.md の更新（必要に応じて）
- README.md の更新（必要に応じて）
- コード内コメントの追加

### 2. 定数の確認
- 新しい定数は `Constants` または `GameColors` に追加
- マジックナンバーを避ける

### 3. レスポンシブ対応
- 様々な画面サイズでの動作確認
- `ResponsiveLayout.swift` の活用

## リリース前

### 1. バージョン番号の更新
Info.plist または Xcode プロジェクト設定でバージョンを更新

### 2. 要件定義書との整合性確認
`docs/要件定義書_EscapeNine.md` と実装内容が一致しているか確認

### 3. 全画面テスト
- ホーム画面
- キャラクター選択画面
- ゲーム画面
- リザルト画面
- ランキング画面
- 設定画面
- ショップ画面（実装されている場合）

### 4. 各機能のテスト
- ゲームプレイ（各階層、各キャラ）
- スキル使用
- AI難易度（Easy/Normal/Hard）
- 特殊ルール（霧、マス消失）
- 広告表示（実装されている場合）
- 課金システム（実装されている場合）

### 5. パフォーマンス確認
- メモリリーク
- CPU使用率
- バッテリー消費
- フレームレート

## 注意事項
- 現在、一部の機能（BGM、ビート同期、Firebase、AdMob、StoreKit）は未実装の可能性があります
- 実装状況に応じてチェックリストを調整してください
