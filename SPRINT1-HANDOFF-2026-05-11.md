# 2026-05-11 Sprint 1 完成ハンドオフ (右下セッション → 実行担当)

> 🟢 このドキュメントは **`~/Documents/GitHub/escape-nine-endless`** の Claude Code セッション (画面右下) 向けの作業引継ぎです。
> 左下の `~/discord-article-bot` セッションが調査・計画・Vault整理を担当しました。

---

## 🎯 1 行で

> Add Files は**不要**だった (PBXFileSystemSynchronized 自動同期方式)。`xcodebuild build` を実行 → 通れば即 push → `gh pr create` で PR 作成 → ultrareview → main マージ → ユーザーが Xcode Archive で TestFlight 提出。

---

## 🔍 重要発見 (左下セッションの調査結果)

### この project は **Xcode 16+ の PBXFileSystemSynchronizedRootGroup を採用**

- `EscapeNine-endless-/` フォルダがターゲット `EscapeNine-endless-` に**自動同期**
- exception (除外) は **`Info.plist`** のみ
- → **フォルダに置かれた .swift ファイルは自動的にビルド対象**

### 結果: Sprint1 ファイル 5 本のうち...

| ファイル | ビルド対象? | 補足 |
|---|---|---|
| `Services/AnalyticsEvents.swift` | ✅ 自動 | アプリターゲットに同期される |
| `Views/Components/ShareSheet.swift` | ✅ 自動 | 同上 |
| `EscapeNine-endless-Tests/Sprint1/GameViewModelSprint1Tests.swift` | ❌ **テストターゲット未存在** | pbxproj に Tests ターゲットなし |
| `EscapeNine-endless-Tests/Sprint1/ShareTextBuilderTests.swift` | ❌ 同上 | |
| `EscapeNine-endless-Tests/Sprint1/AnalyticsEventsTests.swift` | ❌ 同上 | |

### テストターゲット問題

- `EscapeNine-endless-Tests` という名前のターゲットが pbxproj に存在しない (PBXNativeTarget セクションには `EscapeNine-endless-` (app) のみ)
- Sprint1 テストファイルはコンパイル対象に入っていないので **xcodebuild test は実行できない**
- 🟡 対応案:
  - **A (推奨)**: 今回はテストを Sprint 1 完了基準から外す。Xcode で Cmd+U で手動実行 (Xcode が必要に応じて Test ターゲットを自動補完するか、後日テストターゲット作成 PR で対応)
  - B: 今回テストターゲットを `ruby xcodeproj` で新規作成 (リスク中、別 PR で対応推奨)

→ **A 推奨**で Sprint 1 PR をまず通す。

---

## 🛤️ 実行手順 (推奨フロー)

### Phase 1: ビルド検証 (5-10 分)

```bash
cd ~/Documents/GitHub/escape-nine-endless
git branch --show-current  # feature/sprint-1-foundation を確認
git log --oneline -5       # 1e670a5 (chore(version): bump 1.4.3) が HEAD のはず

# シミュレータビルド試行
xcodebuild -project EscapeNine-endless-/EscapeNine-endless-.xcodeproj \
  -scheme "EscapeNine-endless-" \
  -sdk iphonesimulator \
  -destination 'platform=iOS Simulator,name=iPhone 15 Pro' \
  build 2>&1 | tail -40
```

#### 期待結果
- `** BUILD SUCCEEDED **` で終わる
- AnalyticsEvents.swift / ShareSheet.swift が自動でコンパイルされる

#### エラーが出た場合
- `Cannot find type 'AnalyticsEvent' in scope` → 自動同期されていない可能性。`xcodebuild` の Targets/Sources セクションを `xcodebuild -showBuildSettings | grep SOURCE` で確認
- `Cannot find 'DefeatReason' in scope` → subagent A2 が触った enum の rawValue mapping。`Services/AnalyticsEvents.swift` 内の rawValue mapping を見る
- `Multiple commands produce` → ファイル重複登録 (古い手動 reference が残ってる可能性)

---

### Phase 2: push + PR 作成 (5 分)

ビルド成功したら、ASO/ASC 更新 (B3) より先に PR を出してしまう。

```bash
git push -u origin feature/sprint-1-foundation
gh pr create \
  --title "feat(sprint-1): foundation — Game Over redesign + Analytics + ASO + Lore + PR + Legal" \
  --body-file docs/pr/sprint-1-pr-template.md \
  --base main \
  --head feature/sprint-1-foundation
```

### Phase 3: ultrareview (15-30 分)

```bash
/ultrareview
```

- マルチエージェント型レビューで HIGH/MEDIUM/LOW スコア
- HIGH 全部 + MEDIUM 重要分のみ対応 → コミット追加

### Phase 4: PR マージ (5 分)

```bash
gh pr merge --squash  # または手動マージ
git checkout main
git pull origin main
```

### Phase 5: ASO 更新 (10 分)

```bash
cat docs/aso/sprint-1-improvements.md  # 内容確認
```

App Store Connect 手動入力:
- アプリ名「Escape Nine」
- サブタイトル「9マス脱出ローグライク」
- キーワード (docs の「日本」セクション最適化版)
- スクショキャプション 5 枚 (Sprint 3 で本格刷新)

### Phase 6: Archive → TestFlight 提出 (15-30 分、ユーザー手動)

ユーザーが Xcode GUI で:
1. Xcode → Product → Archive
2. Distribute App → App Store Connect → Upload
3. ビルド処理待ち (10-30 分)
4. TestFlight 内部テスター追加 + リリースノート (v1.4.3)

---

## 📋 Sprint 1 DoD チェック (会議録より)

完成判定基準:
- [x] MARKETING_VERSION 1.4.3 commit (左下セッション完了、`1e670a5`)
- [ ] xcodebuild build 成功
- [ ] PR 作成
- [ ] ultrareview HIGH 全対応
- [ ] main マージ
- [ ] ASO Sprint 1 反映 (ASC)
- [ ] AppIcon 更新 (B3.5、optional — 元画像未確定なら次 Sprint へ)
- [ ] Archive → TestFlight 提出

---

## 🚨 既知の懸念

### B3.5 AppIcon 元画像
- ChatGPT 生成の黒×金 Escape Nine ENDLESS アイコン
- 元画像の場所がユーザー不明 (Discord Bot 送付物)
- → **今 Sprint 1 では現アイコンのまま TestFlight 提出**、AppIcon は Sprint 1.1 で別 PR

### subagent A2 触った箇所
- `DefeatReason` の rawValue mapping
- `Character.id` プロパティ整合性
- → ビルドで型エラー出たら `docs/sprint-1-mac-handoff.md` の「既知の懸念事項」セクション参照

### テストターゲット未存在
- `EscapeNine-endless-Tests` ターゲットが pbxproj になし
- Sprint1 テスト 3 ファイルは現状走らない
- → ユーザーが Xcode で Cmd+U した時に Test ターゲット作成提案が出ればそれで対応、もしくは別 PR

---

## 🟢 並行で進行中 (左下セッションが担当)

| 進行 | 内容 |
|---|---|
| ✅ 済 | ParkPedia 8 commits push + P0-3 archive + `feature/admob-verify-2026-05-11` ブランチ |
| ✅ 済 | そらもよう `feature/reward-ad-2026-05-11` ブランチ (Reward 広告は後日) |
| ✅ 済 | Vault: 計画書 + Firebase A1-A4 監視シート + Sprint1 完了テンプレ + ParkPedia C1 ガイド |
| 🟡 待機 | Firebase App Store ID `6760906738` + Team ID 貼り付け (ユーザー手動) |

---

## 🔗 関連

- Vault: `📘 Claude/2026-05-11 [TODO] 今日の作業計画 + 手動作業ガイド.md`
- Vault: `📘 Claude/2026-05-11 [手動作業] EscapeNine Xcode Add Files 詳細手順.md` (→ 不要に！)
- Vault: `📘 Claude/2026-05-11 [手動作業] Firebase 計測修理 A1-A4 監視シート.md`
- Vault: `📘 Claude/2026-05-11 [テンプレ] Sprint 1 完了報告テンプレ.md`
- リポジトリ内: `docs/sprint-1-mac-handoff.md` (subagent A2 メモ)
- PR テンプレ: `docs/pr/sprint-1-pr-template.md`
- ASO 改善: `docs/aso/sprint-1-improvements.md`

---

## 📞 詰まったら

- 左下セッション (Discord Bot 側) に Discord で相談 → 並行調査可能
- subagent A2 触った箇所のエラー → `docs/sprint-1-mac-handoff.md`
- ultrareview の HIGH 大量指摘 → 致命的のみ修正、残りは Sprint 2

頑張ってください 🌅
