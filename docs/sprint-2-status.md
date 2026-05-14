# Sprint 2 ステータス棚卸し ⭐️

作成日: 2026-05-15
ステータス: **棚卸し完了 (実装ギャップ可視化済)**
依存: `docs/onboarding-v1.1-design.md` §10 ゲート条件 3「Sprint 2 完了済」の判断材料
背景: コードベース実調査の結果、**F1/F3 はすでに実装完了**、未着手は F2 のみと判明

---

## 1. 棚卸し結果サマリ

| Feature | 当初見積もり | 実態 | 実装率 | 残工数 |
|---|---|---|---|---|
| F1: デイリーチャレンジ | 6-7 日 | **完了** (Model 99 + View 170 + Service 146 行) | 100% | 0 日 (動作確認のみ) |
| F2: 抜かれ通知 | 8-9 日 | **完全未着手** (`UNUserNotification` ゼロ) | 0% | 設計次第で 1-9 日 |
| F3: シェア拡張 (Wordle 風) | 6 日 | **完了** (ShareTextBuilder + ResultView 統合済) | 100% | 0 日 (動作確認のみ) |

> **Sprint 2 ゲート達成 = F2 着手 / 完了の決定のみで足りる**。F1/F3 は既に「完了」と認定可能。

---

## 2. F1: デイリーチャレンジ — 詳細監査

### 実装済ファイル

| ファイル | 行数 | 役割 |
|---|---|---|
| `EscapeNine-endless-/EscapeNine-endless-/Models/DailyChallenge.swift` | 99 | `ChallengeCondition` enum (4 種類) + `DailyChallenge` struct, Codable 対応 |
| `EscapeNine-endless-/EscapeNine-endless-/Services/DailyChallengeService.swift` | 146 | UTC 日付ベースのシード生成 (LCG) + 30 日履歴保存 + 完了記録 |
| `EscapeNine-endless-/EscapeNine-endless-/Views/DailyChallenge/DailyChallengeView.swift` | 170 | 条件表示 + チャレンジ開始 + 完了バッジ |

### 既存統合

| 統合先 | 場所 | 確認結果 |
|---|---|---|
| GameViewModel | `ViewModels/GameViewModel.swift:69-70, 449-453, 859-861, 950-956` | `pendingChallenge` 読み取り → 条件適用 → 完了時 markCompleted 呼び出し |
| HomeView 動線 | `Views/Home/HomeView.swift:11, 17, 64-65, 150, 185-193` | `dailyChallengeButton` から `DailyChallengeView` へ遷移、`isCompleted` でラベル切替 |
| Analytics 計装 | `Services/AnalyticsEvents.swift:66, 121, 125, 130` + `GameViewModel.swift:498` | `eg_game_started` に `is_daily_challenge: Bool` パラメータ送信 |

### チャレンジ条件 (4 種)

1. `characterLock(CharacterType)` — 指定キャラで挑戦
2. `noSkillAllowed` — スキル使用禁止
3. `forcedAI(AILevel)` — AI 難易度固定 (Easy/Normal のみ、Hard 除外)
4. `startFloor(Int)` — 開始フロア指定 (5/10/15/20/25/30/35/40 から選択)

1 日あたり 1〜2 個の条件がランダム生成。シードは UTC 日付文字列の Unicode scalar 値合計。

### ギャップ (実装済の中で要確認)

- [ ] 動作確認: シミュレータでデイリーチャレンジを 1 回完走、`markCompleted` が呼ばれて翌日まで再挑戦できないことを確認
- [ ] エッジケース: 日付変更線跨ぎでチャレンジが切り替わるか (UTC ベースで実装、JST と差異あり)
- [ ] AnalyticsLogger 連携: `is_daily_challenge: true` で発火している記録が Firebase Console に届くか確認

### 結論

**実装完了として認定可能**。動作確認だけ別タスクで実施。

---

## 3. F3: シェア拡張 (Wordle 風) — 詳細監査

### 実装済ファイル

| ファイル | 行数 | 役割 |
|---|---|---|
| `EscapeNine-endless-/EscapeNine-endless-/Views/Components/ShareSheet.swift` | 122 | `UIActivityViewController` の SwiftUI wrapper + `ShareTextBuilder` (絵文字 + URL 組み立て) |

### 出力フォーマット

```
Escape9 #138 → 9階クリア (38秒)
⬛🟩⬛
⬛⬛⬛
⬛⬛🟧
escape9.app
```

- 🟩 = プレイヤー最終位置
- 🟧 = 敵最終位置 (敗北時は死亡マス)
- ⬛ = その他
- ヘッダー: 通常版 (`Escape9 →`) / Daily Challenge 版 (`Escape9 #N →`) 切替
- URL: `escape9.app` (LP 仮値、本番未差し替え)

### 既存統合

| 統合先 | 場所 | 確認結果 |
|---|---|---|
| ResultView | `Views/Result/ResultView.swift:42, 151-154, 412` | `showShareSheet` State, `.sheet(isPresented:)` で表示、`ShareTextBuilder.build(...)` 呼び出し |

### ギャップ (実装済の中で要確認)

- [ ] 動作確認: ゲームオーバー後にシェアボタンタップ → iOS の標準シェアシート起動 → メッセージ/Twitter/Slack で正しいテキスト送信
- [ ] LP 差し替え: `ShareSheet.swift:38` の `shareURL = "https://escape9.app"` は仮値。本番 LP 公開後に差し替え
- [ ] Daily Challenge ID 連携: 現状 `dailyChallengeId: Int?` パラメータがあるが、`ResultView` から渡しているかは未確認
- [ ] 画像シェア対応: 現状テキストのみ。Twitter ではテキストより画像の方がエンゲージメント高い (任意拡張)

### 結論

**実装完了として認定可能**。LP 差し替え + 画像シェア化は **Sprint 3 以降の任意拡張**として保留。

---

## 4. F2: 抜かれ通知 — 未着手、設計提案

### 機能定義 (推定)

ユーザーのリーダーボード記録 (最高到達階層) が他プレイヤーに抜かれた時、本人に通知する。

会議録 (Obsidian) では「リテンション向上」「競争心理利用」が目的とされている。

### 実装アプローチ比較

| アプローチ | 仕組み | 工数 | バックエンド要否 | 通知精度 |
|---|---|---|---|---|
| **A. ローカル通知 + Game Center 定期チェック** | 起動時/1日1回 GKLeaderboard で順位確認、抜かれたら `UNUserNotification` 発火 | **1-2 日** | なし | 低 (アプリ起動が必要) |
| **B. Firebase Functions + FCM** | Firestore の rankings collection を Function で監視 → 抜かれた user に Push 配信 | **5-7 日** | Firebase Functions + APNs 設定 | 高 (リアルタイム) |
| **C. Hybrid (A + B 段階的)** | Sprint 2 で A を出す → Sprint 3 で B にアップグレード | A: 1-2 日 / B: 5-7 日 | 段階的 | 段階的 |

### 推奨案: A (ローカル通知)

**理由**:
- 個人開発でバックエンド運用負担ゼロ
- Sprint 2 工数 1-2 日で完了 → v1.1 ゲート 3 即達成
- ユーザー体験的に「アプリ起動時に "誰々に抜かれた" を見せる」で十分競争心理は刺激できる
- B (FCM) への移行余地は残す (Sprint 3 以降の任意拡張)

### A 案の実装スコープ (1-2 日)

#### 新規ファイル

| ファイル | 行数 (見積) | 役割 |
|---|---|---|
| `Services/NotificationService.swift` | 80-120 | `UNUserNotification` 許可リクエスト + ローカル通知発火 |
| `Services/LeaderboardWatcher.swift` | 100-150 | GameKit の `GKLeaderboard.loadEntries` で自分の順位を取得、前回比較で「抜かれ」検出 |

#### 既存ファイル変更

| ファイル | 変更 | 行数 (見積) |
|---|---|---|
| `App/EscapeNine_endless_App.swift` | 起動時に `NotificationService.requestPermission()` 呼び出し | +5 |
| `Services/AnalyticsEvents.swift` | `eg_overtaken_notification_shown` イベント追加 | +15 |
| `Views/Home/HomeView.swift` | `.onAppear` で `LeaderboardWatcher.checkAndNotify()` 呼び出し | +3 |

#### Info.plist 追加 (権限)

```xml
<key>NSUserNotificationUsageDescription</key>
<string>リーダーボード抜かれ通知のためにローカル通知を使用します</string>
```

#### Acceptance Criteria

- [ ] 初回起動時に通知許可ダイアログ表示 (Reduce Motion 等の独立 opt-out 不要、通知は OS で個別 OFF 可能)
- [ ] 「設定 → 通知 → Escape Nine」で OFF にしたユーザーには表示しない
- [ ] 抜かれ検出: 前回チェック時の順位 (UserDefaults 保存) と今回順位を比較、順位下落 = 抜かれと判定
- [ ] 通知文面: 「○○ さんに抜かれました! 現在 N 位」(GameKit の displayName 利用、PII 配慮不要 = Game Center 公開情報)
- [ ] チェック頻度: アプリ起動時 (毎回) + バックグラウンドフェッチ (任意、`UIBackgroundFetchInterval.minimum` で省電力)
- [ ] Analytics: `eg_overtaken_notification_shown` (タップ時) / `eg_overtaken_notification_tapped` (タップ後の起動時) を発火

### B 案 (FCM) は Sprint 3 以降に保留

理由: Firebase Functions のメンテナンス負担、APNs 証明書管理、Firestore セキュリティルール拡張 = **個人開発で運用継続が現実的でない**。

---

## 5. Sprint 2 ゲート判定

`docs/onboarding-v1.1-design.md` §10 着手判断:

```
[ ] Sprint 1 が TestFlight に提出済 (Phase 6 完了)               → ✅ 達成 (v1.0.0 配信中)
[ ] Sprint 1 KPI ベースライン取得済                              → 🟡 雛形 main 済 (PR #25)、数値採取は別 PR
[ ] Sprint 2 (デイリー / 抜かれ通知 / シェア拡張) 完了済          → 🟡 F1/F3 完了、F2 未着手
[ ] Obsidian 「Sprint 2 計画書 draft」を「Sprint 2 完了報告」に昇格  → ❌ ユーザー手動タスク
```

### Sprint 2 ゲート達成のための残作業

1. **F2 抜かれ通知 — A 案で実装** (1-2 日)
2. **F1/F3 動作確認** (各 30 分、シミュレータで実行)
3. **本書の PR を main マージ** → Sprint 2 のステータスを公式化
4. **Obsidian の Sprint 2 計画書を完了報告に昇格** (ユーザー手動)

### Sprint 3 (v1.1 オンボーディング) 着手可能タイミング

```
ベースライン PR #25 マージ済 (今日)
  ↓
ベースライン数値採取 (Firebase Console、15 分)
  ↓
F2 抜かれ通知 A 案実装 (1-2 日)
  ↓
F1/F3 動作確認 (各 30 分)
  ↓
Sprint 2 完了報告 (Obsidian)
  ↓
Sprint 3 = v1.1 オンボーディング 7-8 日着手
```

合計: ベースライン採取〜v1.1 着手まで **最短 2-3 日** で到達可能。

---

## 6. リスクと対策

| リスク | 対策 |
|---|---|
| F1/F3 が動作確認で不具合発見 | 本書では「完了として認定可能」とするが、実機テストで NG なら別 PR で修正 |
| F2 ローカル通知が iOS のサイレントモードで届かない | バナー通知 + サウンドをセットで指定、ユーザー側設定変更は OS 任せ |
| GKLeaderboard.loadEntries の rate limit | 起動時のみのチェックなら問題なし、background fetch は任意 |
| Game Center の displayName が null/匿名 | "誰かに" のフォールバック文言を用意 |
| ベースライン採取と F2 実装が並走で時間取られる | 採取はユーザー手動 15 分なので並走可、F2 実装と独立 |

---

## 7. 関連ドキュメント

- v1.1 オンボーディング設計書: `docs/onboarding-v1.1-design.md` (§10 ゲート条件で本書を要求)
- Sprint 1 KPI ベースライン: `docs/aso/sprint-1-baseline.md` (PR #25 で main マージ済、数値採取は次の PR)
- Sprint 1 ASO 改善案: `docs/aso/sprint-1-improvements.md`
- Analytics 計装ファサード: `EscapeNine-endless-/EscapeNine-endless-/Services/AnalyticsEvents.swift`
- 会議録: Obsidian `2026-05-09 [会議録] Escape-Nine 戦略会議 統合` (Sprint 2 機能定義の典拠)
- Sprint 2 計画書 (draft): Obsidian `2026-05-11 Escape Nine Sprint 2 計画書 draft`

---

## 8. 本書の更新ルール

- F2 実装着手時に PR で本書の §4 内チェックリストを更新
- F1/F3 動作確認結果は §2/§3 の「ギャップ」セクションに ✓/✗ で追記
- Sprint 2 全完了時に §5 のゲート判定をすべて ✅ に更新 → Obsidian 同期 → Sprint 3 着手
