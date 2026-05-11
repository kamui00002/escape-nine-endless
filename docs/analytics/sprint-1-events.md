# Sprint 1 KPI 計測イベント設計

> 対象期間: Sprint 1 (2026-05-09 〜)
> 関連会議録: 2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26 名 35 ラウンド)
> 関連: `~/Documents/GitHub/escape-nine-endless/.kiro/sprint-1-research.md`

## 0. 背景・前提

EscapeNine は Firebase Analytics SDK 12.10.0 を導入済み。`EscapeNine_endless_App.swift` で
`Analytics.setAnalyticsCollectionEnabled(true)` の有効化、Consent Mode の
`analyticsStorage` / `adStorage` / `adUserData` / `adPersonalization` を `granted` に設定済。
ATT (App Tracking Transparency) prompt の実装も完了している。

→ **Sprint 1 で追加するのはアプリ固有のカスタムイベント定義のみ**。SDK の初期化や Consent
には触らない。

## 1. KPI と計測手段の対応表

### 🔴 最重要 (週次レビュー、6 ヶ月後ゴール)

| KPI | 計測方法 | 6 ヶ月後ゴール | Sprint 1 対応 |
|---|---|---|---|
| DAU | Firebase Analytics 自動 (`first_open`, `session_start`) | 1,000+ | ✅ 既存で計測可能 |
| Day 1 Retention | Firebase Analytics 自動 | 40%+ | ✅ 既存で計測可能 |
| Day 7 Retention | Firebase Analytics 自動 | 15%+ | ✅ 既存で計測可能 |
| **1 階離脱率** | カスタムイベント (`eg_game_started` / `eg_floor_cleared`) | 30% 以下 | 🆕 Sprint 1 で実装 |
| **Game Over → リトライ率** | カスタムイベント (`eg_game_over_shown` / `eg_retry_tapped`) | 70%+ | 🆕 Sprint 1 で実装 |
| デイリーチャレンジ参加率 | カスタムイベント (`eg_daily_challenge_started`) | 60%+ | ⏳ Sprint 2 で実装 |

## 2. イベント命名規則

| 規則 | 採用方針 |
|---|---|
| 名前空間 prefix | `eg_` (Escape-Nine の略。Firebase 予約イベント・第三者 SDK との衝突回避) |
| 文体 | スネークケース (例: `eg_game_over_shown`) |
| 動詞 | 過去形 (Firebase 推奨。`shown`, `tapped`, `cleared`, `started`) |
| パラメータ名 | スネークケース。1 イベントあたり最大 25 パラメータ (Firebase 制限) |
| 値の型 | 数値は `Int` / `Double`、Bool は Firebase が `Int64` (0/1) として保存 |

## 3. Sprint 1 で追加するカスタムイベント (5 個)

### 3.1 `eg_game_started`

| 項目 | 内容 |
|---|---|
| トリガー | ゲーム開始時 (1 階を表示したフレーム) |
| 用途 | ゲーム開始の母数。DAU に対してプレイ開始したユーザーの割合 |

| パラメータ | 型 | 例 | 用途 |
|---|---|---|---|
| `floor` | Int | `1` | 開始階層 (通常は 1。コンティニュー復帰時のみ別値) |
| `is_daily_challenge` | Bool | `false` | デイリーチャレンジモードかどうか |
| `character_id` | String | `"default"` | プレイヤーキャラ識別子 (将来のキャラ別離脱分析用) |

### 3.2 `eg_game_over_shown`

| 項目 | 内容 |
|---|---|
| トリガー | Game Over 画面の表示完了時 (UI が見えるフレーム) |
| 用途 | 離脱階層分布、惜しさメーター発火率、プレイ時間分布 |

| パラメータ | 型 | 例 | 用途 |
|---|---|---|---|
| `floor` | Int | `3` | 死亡階層 |
| `defeat_reason` | String | `"trap"` / `"timeout"` / `"enemy"` | 死亡原因の分類 |
| `near_miss_distance` | Int | `1` | 「あと N マスで生存」の N 値 (惜しさメーター発火条件) |
| `elapsed_seconds` | Double | `42.7` | このゲームで経過した秒数 (60 秒問題分析) |

### 3.3 `eg_retry_tapped`

| 項目 | 内容 |
|---|---|
| トリガー | Game Over 画面の「もう一回」ボタンタップ時 |
| 用途 | Game Over → リトライ率の分子 |

| パラメータ | 型 | 例 | 用途 |
|---|---|---|---|
| `from_floor` | Int | `3` | 直前の死亡階層 |
| `seconds_until_tap` | Double | `2.4` | Game Over 表示〜タップまでの遅延 (即リトライ率分析) |

### 3.4 `eg_home_tapped`

| 項目 | 内容 |
|---|---|
| トリガー | Game Over 画面の「ホームへ」ボタンタップ時 |
| 用途 | リトライしなかった離脱パスの可視化 |

| パラメータ | 型 | 例 | 用途 |
|---|---|---|---|
| `from_floor` | Int | `3` | 直前の死亡階層 (どの階層で諦めたか) |

### 3.5 `eg_floor_cleared`

| 項目 | 内容 |
|---|---|
| トリガー | 階層クリア時 (次階層に進む直前) |
| 用途 | 階層別クリア率、1 階離脱率の補完、難易度カーブの検証 |

| パラメータ | 型 | 例 | 用途 |
|---|---|---|---|
| `floor` | Int | `1` | クリアした階層番号 |
| `clear_seconds` | Double | `18.3` | この階層のクリア所要時間 |

## 4. 派生 KPI 計算式 (BigQuery クエリ例)

Firebase → BigQuery export を有効化した前提。`your-project.analytics_xxx.events_*` は実際の
プロジェクト ID に置換すること。

### 4.1 1 階離脱率

「ゲーム開始したが 2 階に到達しなかったユーザーの割合」。

```sql
WITH base AS (
  SELECT
    user_pseudo_id,
    event_name,
    (SELECT value.int_value FROM UNNEST(event_params) WHERE key = 'floor') AS floor
  FROM `your-project.analytics_xxx.events_*`
  WHERE _TABLE_SUFFIX BETWEEN
        FORMAT_DATE('%Y%m%d', DATE_SUB(CURRENT_DATE(), INTERVAL 7 DAY))
        AND FORMAT_DATE('%Y%m%d', CURRENT_DATE())
)
SELECT
  COUNT(DISTINCT IF(event_name = 'eg_game_started', user_pseudo_id, NULL)) AS started,
  COUNT(DISTINCT IF(event_name = 'eg_floor_cleared' AND floor >= 1, user_pseudo_id, NULL)) AS cleared_floor1,
  ROUND(
    SAFE_DIVIDE(
      COUNT(DISTINCT IF(event_name = 'eg_game_started', user_pseudo_id, NULL))
      - COUNT(DISTINCT IF(event_name = 'eg_floor_cleared' AND floor >= 1, user_pseudo_id, NULL)),
      COUNT(DISTINCT IF(event_name = 'eg_game_started', user_pseudo_id, NULL))
    ) * 100,
    2
  ) AS floor1_dropoff_rate_pct
FROM base;
```

### 4.2 Game Over → リトライ率

「Game Over を見たユーザーのうち、もう一度プレイしたユーザーの割合」。

```sql
SELECT
  COUNT(DISTINCT IF(event_name = 'eg_game_over_shown', user_pseudo_id, NULL)) AS gameover_seen,
  COUNT(DISTINCT IF(event_name = 'eg_retry_tapped', user_pseudo_id, NULL)) AS retry_tapped,
  ROUND(
    SAFE_DIVIDE(
      COUNT(DISTINCT IF(event_name = 'eg_retry_tapped', user_pseudo_id, NULL)),
      COUNT(DISTINCT IF(event_name = 'eg_game_over_shown', user_pseudo_id, NULL))
    ) * 100,
    2
  ) AS retry_rate_pct
FROM `your-project.analytics_xxx.events_*`
WHERE _TABLE_SUFFIX BETWEEN
      FORMAT_DATE('%Y%m%d', DATE_SUB(CURRENT_DATE(), INTERVAL 7 DAY))
      AND FORMAT_DATE('%Y%m%d', CURRENT_DATE())
  AND event_name IN ('eg_game_over_shown', 'eg_retry_tapped');
```

### 4.3 階層別クリア率 (補助指標)

```sql
SELECT
  floor,
  COUNT(*) AS clear_count,
  AVG(clear_seconds) AS avg_clear_seconds
FROM (
  SELECT
    (SELECT value.int_value FROM UNNEST(event_params) WHERE key = 'floor') AS floor,
    (SELECT value.double_value FROM UNNEST(event_params) WHERE key = 'clear_seconds') AS clear_seconds
  FROM `your-project.analytics_xxx.events_*`
  WHERE event_name = 'eg_floor_cleared'
    AND _TABLE_SUFFIX BETWEEN
        FORMAT_DATE('%Y%m%d', DATE_SUB(CURRENT_DATE(), INTERVAL 7 DAY))
        AND FORMAT_DATE('%Y%m%d', CURRENT_DATE())
)
GROUP BY floor
ORDER BY floor;
```

## 5. パラメータ設計の意図

| パラメータ | 設計意図 |
|---|---|
| `near_miss_distance` | 「あと 1 マスで生存」の発火率を測定。惜しさメーターのリテンション寄与を後で検証するため。 |
| `elapsed_seconds` | ハイパーカジュアルの 60 秒問題分析。短時間プレイヤー (< 30 秒) の離脱パターンを分離する。 |
| `seconds_until_tap` | 即リトライ (< 1 秒) と熟考リトライ (> 5 秒) の比率を分析。ボタン位置・色の改善判断材料。 |
| `defeat_reason` | 死亡原因別の難易度バランス調整。`trap` だけが突出して多ければ罠の警告を強化する等。 |
| `character_id` | Sprint 4 以降のキャラ追加時に、キャラ別離脱率を即座に分析できるよう先に計装。 |

## 6. プライバシー考慮

- 個人情報 (氏名・メール・位置情報) は一切送信しない
- すべてゲームプレイの統計値のみ (Firebase の "使用状況データ" 区分)
- Firebase Analytics のデフォルト設定 + Consent Mode で App Tracking Transparency 整合
- App Store プライバシーポリシーの「収集データ」セクションで「使用状況データ」「診断」を明示
- `user_pseudo_id` (Firebase 自動採番) のみで集計し、IDFA は使用しない

## 7. 実装上の注意

- **バッチログを避ける**: イベント発生時に即座に `Analytics.logEvent()` を呼ぶ
- **重複初期化禁止**: `EscapeNine_endless_App.swift` の Firebase 初期化を再実行しない
- **Logger との併用**: デバッグ時の確認のため `os.Logger` で同時出力 (本番ビルドでも軽量)
- **エラー時の握りつぶし**: ログ送信失敗はゲームプレイを止めない (Firebase SDK が内部リトライ)
- **イベント数の上限**: アプリ起動 1 回あたり 10 件程度を想定 (Firebase の無料枠で十分)
- **パラメータ命名**: スネークケース、Firebase の最大 25 個 / event 制限内 (現状最大 4)

## 8. Looker Studio ダッシュボード (推奨、optional)

週次レビューの自動化を目的として、以下を推奨。

1. Firebase Console → Project Settings → Integrations → BigQuery export を有効化 (無料枠で月 10GB)
2. Looker Studio で BigQuery データソースを追加
3. 上記 §4 のクエリでカスタムフィールドを定義
4. 主要 KPI (DAU / 1 階離脱率 / リトライ率) を 1 ダッシュボードに集約
5. 週次で URL を共有 (チームメンバー閲覧専用)

## 9. 将来の Sprint で追加予定のイベント (placeholder)

Sprint 1 では実装しないが、以下のイベントを将来追加する予定。命名と用途を先に決めておくこと
で、後の実装時に重複・命名揺れを避ける。

### Sprint 2: デイリーチャレンジ系

| イベント名 | トリガー | 主なパラメータ |
|---|---|---|
| `eg_daily_challenge_started` | デイリーチャレンジ開始 | `challenge_date`, `seed` |
| `eg_daily_challenge_completed` | デイリーチャレンジ完了 | `challenge_date`, `clear_floor`, `total_seconds` |
| `eg_daily_challenge_shared` | スコアシェア時 | `challenge_date`, `clear_floor`, `share_destination` |

### Sprint 3: 収益化系 (既存 `ConversionService.swift` と整合)

| イベント名 | トリガー | 主なパラメータ |
|---|---|---|
| `eg_ad_reward_requested` | リワード広告要求 | `placement` (continue / hint) |
| `eg_ad_reward_granted` | リワード広告完了 | `placement`, `floor` |
| `eg_iap_initiated` | IAP 購入開始 | `product_id` |
| `eg_iap_completed` | IAP 購入完了 | `product_id`, `price_local`, `currency` |

### Sprint 4: ソーシャル / ランキング系

| イベント名 | トリガー | 主なパラメータ |
|---|---|---|
| `eg_leaderboard_viewed` | ランキング画面表示 | `leaderboard_id` |
| `eg_score_submitted` | スコア提出 | `score`, `floor`, `leaderboard_id` |
| `eg_character_unlocked` | キャラ解放 | `character_id`, `unlock_method` |

## 10. 関連ファイル

- 実装: `~/Documents/GitHub/escape-nine-endless/EscapeNine-endless-/EscapeNine-endless-/Services/AnalyticsEvents.swift`
- Firebase 初期化: `~/Documents/GitHub/escape-nine-endless/EscapeNine-endless-/EscapeNine-endless-/EscapeNine_endless_App.swift`
- 既存サービス: `Services/FirebaseService.swift`, `Services/ConversionService.swift`
- Sprint 1 リサーチ: `~/Documents/GitHub/escape-nine-endless/.kiro/sprint-1-research.md`
