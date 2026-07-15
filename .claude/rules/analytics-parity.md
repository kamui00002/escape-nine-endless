# 分析イベント規約（iOS↔Unity）⭐️

グローバル規約 `~/.claude/rules/analytics-instrumentation.md` の 5 原則（リリース前計装・単一ファサード・命名固定・PII 禁止・重複禁止）を本 repo に適用したもの。

## 送信経路（単一ファサード厳守）

- iOS: `Services/AnalyticsEvents.swift` の `AnalyticsLogger.log()` のみ（Firebase + PostHog 二重送信の choke point）
- Unity: `Runtime/Analytics/AnalyticsService.cs` のみ（PostHog REST 直 POST）。**画面や Controller から直接 HTTP を叩かない**
- 反面教師（iOS の負債・Unity に持ち込まない）: `ConversionService.trackTutorialComplete()` が `eg_tutorial_complete` と重複した第2経路を作った。**現役で発火中**（HomeView「遊び方」ボタン → TutorialOverlayView 完了で到達。unity/PARITY_GAPS.md B-7、修正は別タスク）

## イベント一覧（2026-07-13 時点・両実装 1:1 を確認済み）

`eg_game_started` / `eg_floor_cleared` / `eg_game_over_shown`（**lose 時のみ**送信、ガード条件も両実装一致）/ `eg_retry_tapped` / `eg_home_tapped` / `eg_tutorial_started` / `eg_tutorial_step_completed` / `eg_tutorial_complete` / `purchase`

- prefix は **`eg_`**（Firebase 予約イベント・サードパーティ SDK との衝突回避。AnalyticsEvents.swift:34-36）
- `purchase` だけは GA4 予約名を**意図的に無 prefix**で使用（コンバージョンインポート経路のため）
- iOS のみのイベント（意図的）: `unity/PARITY_GAPS.md` §A 参照

## 新イベント追加のルール

1. **iOS（AnalyticsEvent enum）と Unity（AnalyticsService 定数）にペアで追加**。片側だけなら PARITY_GAPS.md に理由を書く（hook `check_analytics_parity.py` が台帳に無い片側イベントを警告）
2. 命名: `eg_` + snake_case + 動詞過去形
3. 同じユーザー行動に 2 つの名前を作らない（役割の違いはパラメータで表現）
4. **二重発火に注意**: 未確定注文の再配信・画面再表示で同一イベントが再送されないこと。先例: `UnityIapService.LogPurchase` は `wasOwned`（IsPurchased 事前捕捉）で新規付与時のみ送信（69e974d #3 — 水増しの修正）
5. PII（email / 実名 / トークン）をパラメータに入れない
