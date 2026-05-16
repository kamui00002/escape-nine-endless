# Sprint 1 KPI ベースライン — Escape Nine: Endless ⭐️

作成日: 2026-05-15
最終更新: 2026-05-17 (暫定値で §4 記入、Sprint 3 ゲート 2「暫定達成」扱い)
ステータス: **⚠️ 暫定 / サンプル数 100 セッション未達のため再採取要 (v1.1 リリース後 7-14 日で再取得)**
対象: Sprint 1 (TestFlight v1.0.0 / 配信中) リリース後 7 日間の Analytics 実測値
依存: `docs/onboarding-v1.1-design.md` §10 着手判断 ゲート条件 2 (本書の main マージが v1.1 着手の必要条件)

---

## 1. 目的

Sprint 1 の KPI **現状値**を Firebase Analytics で測定し、v1.1 動的オンボーディング着手後の効果検証用ベースラインとして固定する。

「やってみたら良くなった気がする」を排除し、**数値で前後比較できる状態**を作るのが本書の唯一の目的。

### Sprint 3 着手の前提

`docs/onboarding-v1.1-design.md` §10 によると v1.1 着手の 4 条件のうち 2 番目が「**Sprint 1 KPI ベースライン取得済 (本書が main にマージ済み)**」。つまり本書のテーブル §4 が埋まらない限り Sprint 3 は開始できない。

---

## 2. 計測対象 KPI 一覧

`EscapeNine-endless-/EscapeNine-endless-/Services/AnalyticsEvents.swift:46-50` で Sprint 1 に計装済の 5 イベントを母数に算出する。

| KPI | 算出式 | データソース | 計測単位 |
|---|---|---|---|
| **1 階離脱率** | `1 - (count(eg_floor_cleared where floor==1) / count(eg_game_started))` | Firebase Analytics > イベント | % |
| **平均到達階層** | `avg(max(eg_floor_cleared.floor) per session)` | Firebase Analytics > エクスポート | 階 |
| **Game Over → リトライ率** | `count(eg_retry_tapped) / count(eg_game_over_shown)` | Firebase Analytics > 漏斗 | % |
| **平均セッション秒数** | `avg(eg_game_over_shown.elapsed_seconds)` | Firebase Analytics > イベント パラメータ | 秒 |
| **平均ニアミス距離** | `avg(eg_game_over_shown.near_miss_distance)` | Firebase Analytics > イベント パラメータ | マス |
| **Day 1 リテンション** | Firebase 標準レポート | Firebase Analytics > リテンション | % |
| **Day 7 リテンション** | Firebase 標準レポート | Firebase Analytics > リテンション | % |

### KPI ごとの v1.1 改善目標値 (参照のみ、本書では計測しない)

`docs/onboarding-v1.1-design.md` §1 KPI テーブルから:

- 1 階離脱率: 50%+ → **30% 以下** (-20pp)
- Day 1 リテンション: 不明 → **+10pp**
- チュートリアル完了率: 不明 → **80%+** (v1.1 で `eg_tutorial_*` 新規追加後計測)

---

## 3. 計測期間と方法

### 期間

- **開始**: v1.0.0 App Store 公開日 (2026-03-31 公開済) を起点とした任意の連続 7 日間
- **推奨**: 直近 7 日間 (最新の挙動を反映、ASO 効果も含む)
- **除外条件**: TestFlight 配信日と通常配信日が混在する期間は避ける (内部テスター挙動が混ざる)

### サンプル数下限

`eg_game_started` の合計 **100 セッション以上** を最低ラインとする (信頼区間 ±5% を担保)。

- 7 日で 100 に届かない場合 → 計測期間を 14 日間まで延長
- 14 日でも届かない場合 → v1.1 KPI 設計を「相対値ベース (vs 自身)」に切り替え、絶対値ベースは保留

### 手順

1. Firebase Console (https://console.firebase.google.com) を開く
2. プロジェクト: `escapenine-endless` (本番) を選択
3. 左メニュー: 分析 → イベント
4. 期間セレクタで対象 7 日間を設定
5. 各イベント (`eg_game_started` / `eg_floor_cleared` / `eg_game_over_shown` / `eg_retry_tapped` / `eg_home_tapped`) のカウントを記録
6. 漏斗 (Funnels) で `eg_game_started → eg_floor_cleared(floor==1)` を作成
7. リテンションレポートで D1 / D7 値を取得
8. 結果を §4 のテーブルに記入し、PR を出して main マージ

---

## 4. 実測値 (取得後に埋める)

### 計測条件

| 項目 | 値 |
|---|---|
| 計測開始日 | 2026-05-10 |
| 計測終了日 | 2026-05-16 |
| 計測日数 | 7 日間 |
| 対象アプリバージョン | v1.0.0 (App Store 配信中) |
| プラットフォーム | iOS のみ |
| 地域 | 日本 (`Japan` のみイベント検出、他国 0) |
| 採取者 | kamui00002 (Firebase Console「分析 → Events」画面) |
| データソース | Firebase Analytics Console |
| **信頼性ステータス** | ⚠️ **暫定値 / サンプル数 21 セッション (下限 100 未達)** |

### イベントカウント (Sprint 1 計装 5 イベント)

| イベント | 7 日間カウント | 備考 |
|---|---|---|
| `eg_game_started` | **21** | 母数 (総ユーザー 2 名) |
| `eg_floor_cleared` (全 floor) | **3** | クリア総数 (総ユーザー 2 名) |
| `eg_floor_cleared` (floor==1) | _取得不能_ | Firebase Console 単独ではパラメータ別過去集計 UI 無し (GA4 探索 or カスタム ディメンション登録が必要) |
| `eg_game_over_shown` | _未取得_ | (再採取時に追加) |
| `eg_retry_tapped` | _未取得_ | (再採取時に追加) |
| `eg_home_tapped` | _未取得_ | (再採取時に追加) |

### 算出 KPI

| KPI | 実測値 | 目標値 (v1.1 後) | 改善幅 |
|---|---|---|---|
| 1 階離脱率 | _算出不能_ (floor=1 単独カウントが取れず) | 30% 以下 | _未算出_ |
| 全体クリア率 (参考値) | 3 / 21 = **14.3%** (= ゲーム開始したセッションのうちクリアまで到達した割合、暫定) | — | — |
| 平均到達階層 | _未取得_ (再採取時に追加) | (会議録に記載なし) | — |
| Game Over → リトライ率 | _未取得_ | (Sprint 1 既知の改善対象) | — |
| 平均セッション秒数 | _未取得_ | (60 秒問題分析用) | — |
| 平均ニアミス距離 | _未取得_ | (惜しさメーター検証用) | — |
| Day 1 リテンション | _未取得_ (Dashboard 「ユーザー継続率」カードから再採取要) | +10pp | _未算出_ |
| Day 7 リテンション | _未取得_ (同上) | (参考、目標値未設定) | — |

### ⚠️ サンプル数不足の影響と暫定扱いとした理由

- `eg_game_started=21 セッション` は信頼区間 ±5% を担保する下限 100 セッションを大きく下回る (1/5 以下)。
- floor 別の内訳は Firebase Console の「Events」画面では **過去集計のパラメータ別フィルタ UI が無い** (リアルタイム 30 分のみ対応)。GA4 探索を使えば出るが、サンプル 21 のうち floor=1 が何回かを精度高く算出してもベースラインとして意味を持たないため、本書では算出不能とした。
- それでも本書を main マージする理由: `docs/onboarding-v1.1-design.md` §10 ゲート 2 を「**暫定達成**」扱いとして v1.1 (Sprint 3 実装済み) を **TestFlight 配信して実ユーザー数を増やす方が ROI が高い** という判断 (advisor 推奨)。
- **v1.1 配信後の再採取** で本書 §4 を上書きせず、別ファイル `docs/aso/v1.1-post-launch-baseline.md` で v1.0.0 (本書) ↔ v1.1 の前後比較を新規に開始する想定 (§9 更新ルール準拠)。

---

## 5. v1.1 で新規追加予定イベント (本書では未計装)

`docs/onboarding-v1.1-design.md` §1 で要求されている 3 イベントは **v1.1 実装と同 PR で `AnalyticsEvents.swift` に追加**する (本書のスコープ外)。

| イベント | 発火タイミング | 主なパラメータ |
|---|---|---|
| `eg_tutorial_started` | チュートリアル Step 1 開始時 | (なし) |
| `eg_tutorial_step_completed` | 各 Step 通過時 | `step_number: Int`, `skipped: Bool` |
| `eg_tutorial_complete` | Step 4 完了時 | `elapsed_seconds: Double` |

これらの計装後に再度ベースライン取得 (Sprint 3 直後 7 日間) を行い、本書とは別ファイル `docs/aso/v1.1-post-launch-baseline.md` で v1.0 → v1.1 の差分を記録する想定。

---

## 6. リスクと対策

| リスク | 対策 |
|---|---|
| サンプル数 100 セッション未達 | 期間を 14 日間まで延長、それでも未達なら相対値ベースに切り替え |
| 計測期間中に v1.0.0 → v1.0.1 等のアプデが入った | アプリバージョンでフィルタし、最新版のみ採用 |
| Firebase Analytics の DebugView と本番 ID が混在 | 本番デバイス (TestFlight Phase B 通過後の実機) のみ採用、シミュレータ除外 |
| Sprint 1 計装に漏れ・バグがある | `audit-analytics` skill で計装棚卸し → 漏れ発見時は別 PR で修正してから再計測 |
| 7 日間に外れ値イベント (TwitterX 突発バズ等) が含まれる | 一日ごとの内訳もテーブル化し、極端な日を除外できるよう生データも保存 |

---

## 7. 取得後のアクションフロー

```
本書 main マージ
  ↓
Firebase Analytics で 7 日間取得 (§3 手順)
  ↓
本書 §4 テーブル記入 → PR 出して main マージ
  ↓
docs/onboarding-v1.1-design.md §10 ゲート 2 達成
  ↓
Sprint 2 完了を待つ (ゲート 3)
  ↓
Sprint 3 着手 → feature/onboarding-v1.1 ブランチで v1.1 動的オンボーディング実装
  ↓
v1.1 リリース後、再度 7 日間計測 → v1.1-post-launch-baseline.md で前後比較
```

---

## 8. 関連ドキュメント

- v1.1 動的オンボーディング設計書: `docs/onboarding-v1.1-design.md` (本書はその §10 ゲート条件 2 を満たすために存在する)
- Sprint 1 ASO クイック改善案: `docs/aso/sprint-1-improvements.md`
- Analytics 計装ファサード: `EscapeNine-endless-/EscapeNine-endless-/Services/AnalyticsEvents.swift`
- 計装の根拠会議録: Obsidian `2026-05-09 [会議録] Escape-Nine 戦略会議 統合` 第 12 回 結論 (Sprint 1 計装 5 イベント決定)
- グローバル計装ルール: `~/.claude/rules/analytics-instrumentation.md` (PII sanitize / 命名規則 / ファサード集約)

---

## 9. 本書の更新ルール

- §4 テーブルが埋まった時点で本書は「データ済」状態に昇格
- 以降の修正は **誤記訂正のみ** に限定 (数値の事後変更禁止、変更が必要な場合は別ファイル `sprint-1-baseline-revision-YYYYMMDD.md` を新設)
- v1.1 リリース後の前後比較は別ファイル `v1.1-post-launch-baseline.md` で実施 (本書を上書きしない)
