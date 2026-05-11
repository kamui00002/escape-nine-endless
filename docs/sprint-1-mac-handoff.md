# Sprint 1 Mac セッション Handoff

Discord Bot (Claude Code Channels) で完了した Sprint 1 着手作業を、
Mac セッションで仕上げるための最小限の手順書。

| Key | Value |
|---|---|
| ブランチ | `feature/sprint-1-foundation` (push 前) |
| 累計 commits | **14** (atomic commits、Conventional Commits) |
| 行数変化 | **+4,108 行 / -463 行** (実装 + 削除されたチュートリアル旧版) |
| 発行済 GitHub Issues | **#8 - #12** (5 個、`sprint-1` label 付き) |

---

## ✅ Discord 側で完了済 (もう触らなくて OK)

### コード
- [x] GameOver 画面刷新 (ResultView.swift / GameViewModel.swift / ShareSheet.swift)
- [x] AnalyticsEvents.swift で 5 カスタムイベント定義
- [x] GameView.swift から ResultView に新引数を渡す
- [x] GameViewModel に AnalyticsLogger 計装 (3 箇所)
- [x] OneTap retry 永続化 (@AppStorage in ResultView + SettingsView)
- [x] Tutorial 「初回のみ + 3 画面」改修 (TutorialOverlayView + HomeView)

### テスト
- [x] XCTest 28 個 (`EscapeNine-endless-Tests/Sprint1/`)

### ドキュメント
- [x] `.kiro/sprint-1-research.md` 調査メモ
- [x] `.github/ISSUE_TEMPLATE/sprint-1/*.md` Issue テンプレ 5 個
- [x] `docs/analytics/sprint-1-events.md` KPI 設計
- [x] `docs/aso/sprint-1-improvements.md` ASO 改善 4 言語
- [x] `docs/lore/text-package-v1.md` 迷宮図書館世界観
- [x] `docs/pr/press-release-v1-draft.md` プレスリリース
- [x] `docs/pr/sprint-1-pr-template.md` PR テンプレ
- [x] `docs/legal/checklist-sprint-7.md` 法務チェックリスト

### GitHub
- [x] Issue #8: GameOver 画面刷新
- [x] Issue #9: ワンタップリトライ
- [x] Issue #10: チュートリアル 3 画面
- [x] Issue #11: ASO クイック改善
- [x] Issue #12: Firebase Analytics KPI

→ <https://github.com/kamui00002/escape-nine-endless/issues?q=is%3Aissue+label%3Asprint-1>

---

## 🔧 Mac で実施するタスク (順序付き)

### 0. ブランチ確認 (1 分)

```bash
cd ~/Documents/GitHub/escape-nine-endless
git status
git branch --show-current  # → feature/sprint-1-foundation
git log --oneline -15      # 14 commits 確認
```

事前変更で M (modified) のものがあれば判断:
- `.serena/project.yml` / `project.pbxproj` / `ad-assets/generation-manifest.json`
- 必要なら `git stash` または別ブランチで commit

### 1. Xcode で新規 Swift ファイルを project に登録 (5 分、最重要)

新規 Swift ファイル 4 つは pbxproj に未登録 = Xcode が認識しない:
- `EscapeNine-endless-/EscapeNine-endless-/Services/AnalyticsEvents.swift`
- `EscapeNine-endless-/EscapeNine-endless-/Views/Components/ShareSheet.swift`
- `EscapeNine-endless-Tests/Sprint1/GameViewModelSprint1Tests.swift`
- `EscapeNine-endless-Tests/Sprint1/AnalyticsEventsTests.swift`
- `EscapeNine-endless-Tests/Sprint1/ShareTextBuilderTests.swift`

#### Xcode 手動登録 (推奨、最も安全)

1. Xcode で `EscapeNine-endless-.xcodeproj` を開く
2. Project Navigator で `Services` グループを右クリック → **Add Files to "EscapeNine-endless-"...**
3. `AnalyticsEvents.swift` を選択 → **Add**
4. `Components` グループ (なければ新規作成) を右クリック → **Add Files...** → `ShareSheet.swift`
5. **テストターゲット**: `EscapeNine-endless-Tests` グループ (なければ新規作成、Unit Test Target が必要) を右クリック → **Add Files...** → `Sprint1/` フォルダごと追加
   - **Target Membership** で `EscapeNine-endless-Tests` を選択 (本体ターゲットには追加しない)
6. ⌘B で Build → 「No such file or directory」エラーが消える

#### 代替: ruby xcodeproj 自動化 (リスク中、Mac で時間ない時)

```bash
sudo gem install xcodeproj
ruby <<'EOF'
require 'xcodeproj'
project_path = 'EscapeNine-endless-/EscapeNine-endless-.xcodeproj'
project = Xcodeproj::Project.open(project_path)
target = project.targets.find { |t| t.name == 'EscapeNine-endless-' }

# Services/AnalyticsEvents.swift
services_group = project.main_group['EscapeNine-endless-']['Services']
file_ref = services_group.new_reference('AnalyticsEvents.swift')
target.add_file_references([file_ref])

# Views/Components/ShareSheet.swift
components_group = project.main_group['EscapeNine-endless-']['Views']['Components']
file_ref = components_group.new_reference('ShareSheet.swift')
target.add_file_references([file_ref])

project.save
puts "Done"
EOF
```

⚠️ 失敗したら `git checkout -- EscapeNine-endless-.xcodeproj/project.pbxproj` で revert。

### 2. ビルド試行 (5 分)

```bash
xcodebuild -project EscapeNine-endless-/EscapeNine-endless-.xcodeproj \
  -scheme "EscapeNine-endless-" \
  -sdk iphonesimulator \
  -destination 'platform=iOS Simulator,name=iPhone 15 Pro' \
  build 2>&1 | tail -30
```

または Xcode で ⌘B。

エラーが出る可能性:
- `'AnalyticsEvents' (or ShareTextBuilder) cannot be found` → pbxproj 未登録
- `DefeatReason has no rawValue` → enum に `: String` がない場合 (subagent A2 が該当 case を確認しているはず)
- `Character.id` が存在しない → 既存の Character struct に id プロパティがあるか確認

### 3. テスト実行 (10 分、optional)

```bash
xcodebuild test \
  -project EscapeNine-endless-/EscapeNine-endless-.xcodeproj \
  -scheme "EscapeNine-endless-" \
  -destination 'platform=iOS Simulator,name=iPhone 15 Pro' \
  -only-testing:EscapeNine-endless-Tests/Sprint1
```

28 個のテストが PASS することを確認。
失敗したら subagent A5 が指摘した「テスト書きづらい箇所」(private プロパティアクセス等) の影響かを判断。

### 4. App Store Connect 更新 (10 分、Sprint 1 完了基準)

`docs/aso/sprint-1-improvements.md` を参照:

1. アプリ名: 「Escape Nine」
2. サブタイトル: 「9マス脱出ローグライク」
3. キーワード: 同 docs の「日本」セクションのキーワード文字列を貼り付け
4. スクショ 5 枚にキャプション追加 (本格刷新は Sprint 3)

### 5. push + PR 作成 (5 分)

```bash
git push -u origin feature/sprint-1-foundation
gh pr create \
  --title "feat(sprint-1): foundation — Game Over redesign + Analytics + ASO + Lore + PR + Legal" \
  --body-file docs/pr/sprint-1-pr-template.md \
  --base main \
  --head feature/sprint-1-foundation
```

### 6. ultrareview 実行 (15-30 分、optional)

```bash
/ultrareview
```

または GitHub PR で自動レビュアー走査。

### 7. TestFlight 提出 (10-20 分、Sprint 1 完了基準)

archive + altool または Xcode Organizer で。
詳細手順は既存の Apple App Store CLI スクリプト / `expo-build-submit` skill を参照。

---

## 🚨 既知の懸念事項 (Mac で要確認)

### 1. SourceKit 警告 (環境固有、無視可)
- `'UIKit' module not found` 等
- → 単独パース時の問題、Xcode build では問題なし

### 2. AnalyticsLogger 計装の DefeatReason mapping
- subagent A2 が `switch defeatReason` で `AnalyticsDefeatReason` (enemy/timeout/unknown) にマッピング
- → 既存の DefeatReason enum cases (caughtByEnemy, timeOut) と一致しているか確認

### 3. Character.id プロパティ
- subagent A2 が `currentCharacter.id` を使用
- → `Character` struct に `id` プロパティがあるか確認 (subagent は既存実装で確認済と報告したが念のため)

### 4. AppStorage キー変更による既存ユーザー影響
- 旧: `tutorialCompleted` → 新: `hasSeenTutorial`
- 既存ユーザーは初回起動時に新チュートリアルを 1 回見る (会議録の合意通り)

### 5. Tutorial の旧 6 ページ実装が削除された
- 「ビート同期」「スキル」「特殊ルール」の説明が消えた
- → Sprint 1 の意図 (3 画面で簡潔に) に従ったが、別途 Help セクションで保持するか判断

---

## 📞 サポート

何か詰まったら Discord に投げる。
- "Mac で xxx エラーが出た" → Discord Bot (Claude Code Channels) が並行で調査
- 「進捗どうなった?」 → 即対応

Sprint 1 のコードベースは `feature/sprint-1-foundation` ブランチに完全保存済。
revert したい時は `git checkout main` で元に戻せる。

---

## 📋 完了チェックリスト

- [ ] 0. ブランチ確認、事前変更の判断
- [ ] 1. Xcode Add Files (4 ファイル)
- [ ] 2. ⌘B でビルド成功
- [ ] 3. テスト実行 (optional)
- [ ] 4. App Store Connect 更新 (アプリ名/サブタイトル/キーワード)
- [ ] 5. push + PR 作成
- [ ] 6. ultrareview (optional)
- [ ] 7. TestFlight 提出
- [ ] **Sprint 1 完了 → Sprint 2 へ**

---

## 🌟 最後に (専門家からの言葉、会議録より)

> 田中: Sprint 1 を完璧にやれば後はリズム。最初の 2 週間に集中投資を。
>
> Soumatou さんへ — 26 人全員から:
> 本業を続けながら、自分のペースで。私たちは伴走者です。

Discord Bot 側でできることは全部やりました。
Mac セッションで上記チェックリストを埋めれば、**Sprint 1 完成**です。
