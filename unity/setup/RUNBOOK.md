# Escape Nine — Unity 立ち上げ 完全自動化ランブック ⭐️

**実行場所**: ローカル Mac の **Ghostty 上の Claude Code**（Unity インストール済の環境）
**この repo の役割**: プラン確定。本ランブックをローカルで実行して Unity 版を立ち上げる。
親プラン: `../../docs/unity-migration-plan.md` / 足場の説明: `../README.md`

> このランブックは「手作業のインポート・クリックをゼロ」にする設計です。
> `bootstrap.sh`（ヘッドレス）で**作成〜テスト〜Phase 0 シーン生成**まで自動、
> MCP は**その後の対話的な Editor 反復**（シーン/UI/プレハブ）に使います。

---

## 0. 全体フロー

```
[前提確認] → [§2 bootstrap.sh 一発] → [§3 MCP設定] → [§4 Phase0 実機検証 = GO/NO-GO]
                     │                                         │
          作成/テスト/Phase0シーン (headless)          リズム精度OK? → Phase1〜へ
                                                        └ NGなら移行方針を再検討
```

---

## 1. 前提確認（ローカル Claude Code が最初に実行）

```bash
# Unity 検出 (Hub 既定パス)
ls -1 /Applications/Unity/Hub/Editor 2>/dev/null || echo "Unity Hub 版が見つからない → UNITY_BIN を後で指定"

# repo を最新化 (このブランチに足場一式がある)
git fetch origin claude/ultraplan-growth-features-vqozes
git checkout claude/ultraplan-growth-features-vqozes
git pull --ff-only

# Node (MCP セットアップCLIで使用する場合)
node -v || echo "Node 未導入なら §3 の npx 系は nvm/homebrew で入れる"
```

---

## 2. ワンショット・ブートストラップ（headless・完全自動）

```bash
bash unity/setup/bootstrap.sh
# 変えたい場合:
# PROJECT_PATH=~/EscapeNineUnity UNITY_BIN='/Applications/Unity/Hub/Editor/<ver>/Unity.app/Contents/MacOS/Unity' \
#   bash unity/setup/bootstrap.sh
```

これが自動で行うこと:
1. Unity Editor バイナリを自動検出
2. `~/EscapeNineUnity` に空プロジェクトを `-createProject`（既存ならスキップ）
3. `unity/EscapeNine/Assets`（Core/Runtime/Editor/Tests）をプロジェクトへコピー
4. `-runTests -testPlatform editmode` で **EditMode テストを実行** → 結果 XML を集計表示
5. `-executeMethod EscapeNine.EditorTools.Phase0SceneBuilder.Build` で **Phase 0 シーンを生成**

**検証ゲート①**: EditMode テストが全 pass すること（＝Swift→C# 移植が忠実である一次証明）。
`failed=0` を確認。失敗があれば §6 トラブルシュート。

> 📗 前段検証済み (2026-07-01): Core + 全60テストは `unity/verify/Core.Tests` の `dotnet test` で
> .NET 8 (C# 9) 上で **60/60 green** を確認済み。Editor で失敗する場合は Unity 側の設定
> (Test Framework 未導入 / asmdef 解決) を疑うのが先。

---

## 3. MCP セットアップ（対話フェーズ用）

MCP は Editor を **起動した状態**で Claude Code から Editor を操作するための橋。
以降の Phase（シーン配置・UI・プレハブ・演出）を Claude Code が直接いじれるようになる。

> ⚠️ Unity MCP サーバは複数あり導入手順が異なる。**ローカル Claude Code は Web にアクセスできるので、選んだサーバの README で最新のインストール文字列を必ず確認**してから実行すること（下記コマンドは 2026-07 時点の目安）。

### 案A（推奨・最も自動化しやすい）: IvanMurzak/Unity-MCP
CLI が UPM パッケージ導入 + Claude Code の MCP 設定を自動で書き込む。
```bash
# 例 (正確なパッケージ名は repo README で確認)
npx unity-mcp-cli setup-skills claude-code "$HOME/EscapeNineUnity"
```
- repo: https://github.com/IvanMurzak/Unity-MCP

### 案B: 公式 Unity MCP（Unity サブスク要 / AI Assistant 同梱）
Unity Editor を開き **Project Settings → (Unity) MCP → Integrations → Claude Code → Configure**。
自動でクライアント設定が書き込まれる。
- doc: https://unity.com/blog/unity-ai-mcp-how-to-get-started

### 案C: CoplayDev/unity-mcp（UPM git URL）
Editor → Window → Package Manager → **+ → Add package from git URL**:
```
https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main
```
その後パッケージ内の設定から Claude Code を Configure。
- repo: https://github.com/CoplayDev/unity-mcp

### 手動フォールバック（どの案でも `.mcp.json` を直接書く場合）
`unity/setup/mcp.example.json` を repo ルートの `.mcp.json` にマージし、`command`/`args` を選んだサーバの実コマンドに置換。
または:
```bash
claude mcp add unity -- <サーバ起動コマンド>
claude mcp list   # 接続確認
```

**検証ゲート②**: `claude mcp list` で `unity` が connected。Editor 起動中に Claude Code から
MCP ツール（シーン/GameObject 操作）が呼べること。

---

## 4. Phase 0 実機検証（GO/NO-GO の要）★最重要

リズムゲームの生命線 = タイミング精度。ここを Swift 版と比較して**先に**判定する。

1. Editor で Phase 0 シーンを開く: `Assets/Scenes/Phase0.unity`
2. （任意）既存 BGM を使う場合: `bgm_early.*` を `Assets/Resources/Sounds/BGM/` に置く
   （置かなくても無音でビート判定は動作。ログで拍と HIT/MISS を確認可能）
3. **iOS 実機ビルド**して、拍に合わせタップ → Console の `HIT/MISS` と体感を確認
   - Active Input が New のみの場合は §6 参照（Player Settings で "Both" 推奨）
4. **判定**（親プラン §6）:
   - ✅ Swift 版と同等以上のタイミング体感 → **GO**。Phase 1〜（ロジックは移植済）を Editor + MCP で結線して本格化
   - ❌ 明確に劣る/ずれる → **NO-GO**。`Conductor` の `scheduleAheadSeconds` / `firstBeatOffsetSeconds` 調整、
     それでもダメなら移行方針（ハイブリッド等）を再検討

---

## 5. Phase 0 通過後（MCP で反復）

Editor を起動したまま、Claude Code に MCP 経由で以下を依頼していく（親プラン §3〜§5）:
- Phase 2: 3×3 盤面 UI・Home/Result 等の再構築（`GameEngine`/`Floor`/`AIEngine` は移植済なので結線が中心）
- Phase 3: Unity IAP / 広告 / Firebase Unity / PostHog(REST) / Game Center・Google Play Games
- Phase 4: ゲームフィール（パーティクル/シェイク/音楽反応）
- Phase 5: ローグライク深化（レリック/メタ進行/分岐）
- Phase 6: Android + Steam 体験版

---

## 6. トラブルシュート

| 症状 | 対処 |
|---|---|
| `bootstrap.sh` が Unity を見つけない | `UNITY_BIN=...` を明示。Hub のバージョンは `ls /Applications/Unity/Hub/Editor` |
| EditMode テストが Test Runner に出ない / XML 無し | Package Manager で **Test Framework** を追加（新規プロジェクトに通常含まれるが未導入なら手動）→ 再実行 |
| `Phase0Harness` でタップが効かない | Player Settings → **Active Input Handling = Both**（旧Input有効化）。または新Input対応を追加 |
| `-executeMethod` が走らない | Editor にコンパイルエラーがある。Console/ログを確認して修正後に再実行 |
| MCP が connected にならない | Editor を起動しているか（bridge は Editor 内で動く）、`claude mcp list`、サーバ README の最新手順を確認 |
| Conductor の拍がずれる | `scheduleAheadSeconds`（既定0.1）を 0.05〜0.2 で調整、`firstBeatOffsetSeconds` を BGM の無音導入に合わせる |

---

## 付録: ローカル Claude Code に貼るワンショット・プロンプト

> 以下をそのまま Ghostty の Claude Code に貼れば自走します。

```
このリポジトリの unity/setup/RUNBOOK.md に従って Unity 版を立ち上げて。
手順:
1. §1 前提確認を実行 (Unity 検出 / このブランチに checkout)
2. §2 `bash unity/setup/bootstrap.sh` を実行し、EditMode テストが全 pass することを確認（検証ゲート①）。失敗したら §6 で直す。
3. §3 で Unity MCP を導入。まず案A(IvanMurzak)の最新インストール手順を repo README で確認してから実行し、`claude mcp list` で connected を確認（検証ゲート②）。
4. §4 の Phase 0 シーンを開き、iOS 実機でリズム精度を確認して GO/NO-GO を報告。
各ゲートの結果（テスト数・pass/fail、MCP接続、Phase0体感）を都度報告して。未検証を「できた」と言わないこと。
```
