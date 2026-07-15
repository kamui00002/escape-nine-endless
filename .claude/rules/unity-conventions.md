# Unity 開発規約 (Escape Nine) ⭐️

2026-07-13 の移植回顧監査で「実際に採用された規約」を凍結したもの。建前ではなくコードの実態が根拠。

## 正本の場所（最重要・二重コピー注意）

- **git 正本**: `unity/EscapeNine/Assets/`（この repo）。編集は必ずこちらに行う
- `~/EscapeNineUnity` は `unity/setup/bootstrap.sh` が rsync で生成した Unity プロジェクト実体（git 管理外）。**テスト・ビルドの実行場所**であり、直接編集したら必ず repo 側へ反映すること（"fix didn't take effect" 事故防止）

## レイヤリング（asmdef 4 層・依存一方向）

- `EscapeNine.Core`（**noEngineReferences: true**、参照ゼロ）← `EscapeNine.Runtime` ← `EscapeNine.EditorTools` / `EscapeNine.Tests.EditMode`
- **Core に `using UnityEngine` を書かない**。盤面座標・移動判定・勝敗・敵AI・PRNG・レリック抽選はすべて Core（`GameEngine.cs` / `GameSession.cs` / `AIEngine.cs` / `SeededRng.cs` / `RelicDraftService.cs`）
- 3D 表示（`Runtime/Stage/` の BoardStage / TileView / PawnView）は描画専任。判定ロジックを持たず `IBoardView` 経由で Core の結果を反映するだけ
- 乱数は `Core/IRandomSource.cs` 経由のみ。詳細は `daily-seed-spec.md`

## C# 言語バージョン

- **C# 9.0 固定**（Unity 2022/6 互換。`unity/verify/Core.Tests/Core.Tests.csproj` の `<LangVersion>9.0</LangVersion>` が強制）
- 禁止構文: file-scoped namespace（`namespace X;`）/ `global using` / `record struct` / `required`

## UI 構築

- **全 UI はコード生成**（`Runtime/UI/UIFactory.cs` + `UITheme.cs`）。プレハブ・`.unity` への UI 焼き込みは禁止（シーンは `Editor/MainSceneBuilder.cs` がプログラム生成）
- 固定 pt 禁止・親比率レイアウト（Swift `ResponsiveLayout` 思想の移植。UIFactory.cs ヘッダに明記）
- テキストは TextMeshPro 統一。色・フォントの供給源は `UITheme` のみ（Swift `Constants.swift` の GameColors と 1:1）

## 命名規約

| サフィックス | 役割 |
|---|---|
| `Screen`（ScreenBase 継承） | 画面単位 UI（`Runtime/UI/Screens/`） |
| `Widget` | 画面内部品 |
| `Service` / `I*Service` | 外部連携・ドメインサービス（Stub* はテスト/未結線用） |
| `Store` | PlayerPrefs 永続化ラッパー |
| `Config` | 定数集約 |
| `Director` | 演出制御（Swift の Manager 相当） |
| `View` | `Stage/` 配下の 3D 描画のみ |
| `Controller` | ゲーム進行 MonoBehaviour（Swift の ViewModel に対応） |

インタフェースは `I` prefix。テストは `<対象名>Tests.cs`（NUnit）。

## ログ・デバッグ・通信

- `Debug.Log($"[ClassName] メッセージ")` — 角括弧クラス名 prefix 必須（全 16 ファイルで踏襲済みの慣行）
- デバッグ機能（`Debug*` 系設定キー・全キャラ解放等）は **`#if UNITY_EDITOR || DEVELOPMENT_BUILD` ガード必須**（根拠: 830e1a6 — ガード漏れでリリースビルドから有料キャラ全開放できる穴があった）
- `UnityWebRequest` は **`.timeout` 設定必須**（根拠: 69e974d — 未設定 5 箇所が認証ラッチのデッドロックを起こした）
- 非同期応答で UI 状態を上書きする時は stale 応答対策（世代カウンタ。`OnlineRankingService` の先例）
- 秘密情報は `*Secrets.cs`（gitignore 済み）+ `*Secrets.cs.example` テンプレ方式（AnalyticsSecrets / RankingSecrets の先例）

## 検証コマンド（変更種別 → 実行するもの）

1. **Core のみの変更**: `cd unity/verify/Core.Tests && dotnet test`（CI `unity-core-tests.yml` と同一、Unity Editor 不要。※この Mac では `dotnet` は PATH に無く `~/.dotnet/dotnet` を使う）
2. **Runtime を含む変更**: Unity CLI EditMode テスト（`/unity-test` skill 参照。PlayerStateTests / RankingStoreTests はここでしか走らない）
3. **UI 変更**: `/ui-overlap-audit` skill（重なり・はみ出し監査）

## 関連 rule

- `swift-csharp-parity.md` — iOS↔Unity 1:1 規約とファイル対応表
- `save-compat-ledger.md` — PlayerPrefs キー台帳とセーブ互換
- `daily-seed-spec.md` — デイリーシード決定論仕様
- `analytics-parity.md` — 分析イベント規約
- `unity/PARITY_GAPS.md` — 意図的差分・未移植の台帳
