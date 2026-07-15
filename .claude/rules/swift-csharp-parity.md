# Swift↔C# 1:1 パリティ規約 ⭐️

**正本は Swift**（`EscapeNine-endless-/EscapeNine-endless-/`）。Unity 版は挙動をこれに一致させる。意図的に変える場合は `unity/PARITY_GAPS.md` に記録してから。

根拠コミット: 830e1a6（敵対的検証で CONFIRMED 差分 9 件）/ 8d214d9（機能矛盾 10 件）/ 49fc52d・24af24c・0083946（「Swift+C# 1:1」ペア修正 3 連）— **片側だけ直して壊れるのが本移植で最も多かった欠陥**。

## ファイル対応表（片側を触ったら必ず対応先を確認）

| iOS (Swift) | Unity (C#) |
|---|---|
| `ViewModels/GameViewModel.swift` | `Core/GameSession.cs` + `Runtime/GameController.cs` |
| `Utilities/Constants.swift` | `Core/GameConfig.cs` |
| `Services/GameEngine.swift` | `Core/GameEngine.cs` |
| `Services/AIEngine.swift` | `Core/AIEngine.cs` |
| `Models/Floor.swift`（BPM曲線） | `Core/Floor.cs` |
| `Models/Skill.swift` / `Models/Character.swift` | `Core/Skill.cs` / `Core/Character.cs` |
| `Models/Achievement.swift` | `Core/Achievement.cs` |
| `Services/DailyChallengeService.swift` | `Core/SeededRng.cs` + `Core/DailyChallengeGenerator.cs` + `Runtime/DailyChallengeStore.cs` |
| `Services/BeatEngine.swift`（拍・ターンCD） | `Runtime/Conductor.cs` |
| `Services/AnalyticsEvents.swift` | `Runtime/Analytics/AnalyticsService.cs` |
| `ViewModels/PlayerViewModel.swift` + `Services/AudioManager.swift`（永続化） | `Runtime/PlayerState.cs` |
| `Services/RankingService.swift` | `Runtime/RankingStore.cs` |

## ルール

1. **ゲームロジック（ルール・バランス・判定・タイミング）の変更は両実装ペアで修正**し、EditMode テストを追加する。コミットメッセージ末尾に `(Swift+C# 1:1)` を付ける（既存慣行）
2. **bit 一致必須の領域**: バランス定数・BPM 曲線（70→200 べき 1.4）・AI 分岐確率・**タイブレーク順**（`GetAvailableMoves` の上→下→左→右構築順 × 厳密不等号。ここが変わると同距離候補の選択が変わる）・デイリーシード系列（`daily-seed-spec.md`）
3. **移植・修正の手順**（finish-unity.md と同じ）: Swift 正本を読む → C# へ反映 → EditMode テストで同一入出力を検証。大きめの変更は敵対的検証（別エージェントが差分・欠落を探し、CONFIRMED のみ採用）
4. 過去に繰り返しズレた箇所（レビュー時の重点）: ポーズ→再開の拍タイミング / GO! 直後の入力ブロック中のターンカウント / 実績チェックの勝敗両パス結線 / スコア・自己ベストの**即時**永続化（オーバーレイ中離脱で消えない）
5. 定期監査は `/parity-check` skill を使う

## 仕様書との食い違い

数値・挙動の正本は**コード**（Constants.swift / GameConfig.cs）。`docs/要件定義書_EscapeNine.md` は初版値のまま（例: 移動「8方向」→実装は基本4方向+スキル）なので根拠にしない。`docs/game-spec.md` は概ね現行だが最終確認はコード。
