# Escape Nine — Unity 移行 足場 (Phase 0 + Phase 1 コア)

親プラン: `../docs/unity-migration-plan.md`

---

## ⚠️ 重要 — このフォルダの正直な状態

このフォルダの C# は **Linux コンテナ上でテキストとして書かれたもので、Unity Editor でのビルド・実行・テストは未実施 (未検証)** です。
Swift 正本からの **忠実移植** ですが、コンパイル確認はあなたの Unity 環境で行う必要があります。

- ✅ **移植済 (コードとして完成、要コンパイル確認)**: Phase 1 の純ロジック + Phase 0 の Conductor + EditMode テスト
- ❌ **未実施 (Editor が必要なため本環境では作れない)**: Unity プロジェクト本体の生成、`.meta` 生成、SDK パッケージ導入、シーン/プレハブ、各プラットフォームのビルド

「作ったフリ」を避けるため、Editor 依存の作業は下の §手順 に人間タスクとして明記しています。

---

## 収録物

```
unity/EscapeNine/Assets/
├── Scripts/
│   ├── Core/           # 純 .NET (noEngineReferences)。UnityEngine 非依存 = 他エンジンにも流用可
│   │   ├── GameEnums.cs        ← Models/GameState.swift, Character.swift, Skill.swift
│   │   ├── GameConfig.cs       ← Utilities/Constants.swift (Constants / TutorialConstants)
│   │   ├── Floor.cs            ← Models/Floor.swift (BPM曲線・特殊ルール・AI階層)
│   │   ├── GameEngine.cs       ← Services/GameEngine.swift (移動判定・勝敗)
│   │   ├── AIEngine.cs         ← Services/AIEngine.swift (Easy/Normal/Hard/Boss)
│   │   ├── IRandomSource.cs    ← 乱数注入 (テスト決定論化のための意図的追加)
│   │   ├── Skill.cs            ← Models/Skill.swift
│   │   ├── Character.cs        ← Models/Character.swift
│   │   └── GameStateData.cs    ← Models/GameState.swift (struct GameState)
│   └── Runtime/        # UnityEngine 依存
│       └── Conductor.cs        ← Services/BeatEngine.swift を dspTime ベースに再設計 (Phase 0 の核)
└── Tests/
    └── EditMode/       # NUnit 回帰テスト (Swift と同一入出力を担保)
        ├── FloorTests.cs
        ├── GameEngineTests.cs
        └── AIEngineTests.cs
```

## 移植方針・意図的な差分

- **Core は UnityEngine 非依存** (`noEngineReferences: true`)。ロジックをエンジンから切り離し、テスト容易性と移植性を確保。
- **乱数を注入可能に** (`IRandomSource`)。Swift 版はグローバル乱数 (`Double.random` / `randomElement`) を直接使用していたが、C# では注入式にしてテストを決定論化。既定は `SystemRandomSource` でグローバル乱数相当。
- **`BeatEngine` → `Conductor`**: Timer ベースの拍検出を **`AudioSettings.dspTime` ベース**に置換。リズムゲームの iOS では `Time.time` は使わない (フレーム揺れ・音声レイテンシで拍がずれる)。
- 数値定数・BPM曲線・グリッド演算・AI判断は Swift と **1:1** で移植。期待値は `docs/game-spec.md` の BPM 表と一致 (Floor 1=70 / 25=88 / 50=119 / 75=156 / 100=200)。

---

## 手順 — あなたの Unity 環境でやること

### 1. プロジェクト作成 & スクリプト取り込み
1. Unity Hub で新規 2D プロジェクトを作成 (**Unity 6 LTS** or **2022 LTS** 推奨)
2. 本 `unity/EscapeNine/Assets/Scripts` と `Assets/Tests` を、作成した Unity プロジェクトの `Assets/` 配下へコピー
   - `.meta` は同梱していない → **Editor 初回 import 時に自動生成**される (GUID 衝突回避のため意図的に未同梱)
3. Editor がコンパイルを通すことを確認 (Console にエラーが出ないか)

### 2. EditMode テスト実行 (最優先の検証)
1. **Window → General → Test Runner → EditMode**
2. `FloorTests` / `GameEngineTests` / `AIEngineTests` が **全 green** になることを確認
   - ここが通れば「Swift ロジックの C# 移植が忠実」であることの一次証明になる

### 3. Phase 0 プロトタイプ (リズム精度ゲート) ★最重要
1. 空シーンに空 GameObject を作り `Conductor` をアタッチ (AudioSource は自動追加)
2. `song` に既存 BGM (`Resources/Sounds/BGM/` の bgm_*.mp3 を流用) を割り当て
3. 最小の入力→`Conductor.CheckMoveTiming()` 判定→ヒット/ミス表示 の仮UIを作る
4. **iOS 実機ビルド**して、Swift 版と同等以上のタイミング体感が出るか確認
5. → **GO/NO-GO 判定** (親プラン §6)。OK なら Phase 1〜へ全力、NG なら移行方針を再検討

### 4. 以降 (親プラン §3 の Phase 順)
- Phase 2: UI 再構築 / Phase 3: 収益化・サービス再統合 (Unity IAP / 広告 / Firebase Unity / PostHog REST)
- Phase 4: ゲームフィール / Phase 5: ローグライク深化 / Phase 6: Android + Steam / Phase 7: 移行リリース

---

## 未同梱 (Editor 生成/人手が必要)

- `ProjectSettings/` `Packages/manifest.json` (Unity バージョン・依存に依存するため各自の環境で)
- 各 `.meta` (Editor が自動生成)
- シーン / プレハブ / インポート済アセット (音源・ドット絵は既存 repo から流用)
- SDK パッケージ (Unity IAP / Firebase Unity / 広告メディエーション / Facebook Unity)
