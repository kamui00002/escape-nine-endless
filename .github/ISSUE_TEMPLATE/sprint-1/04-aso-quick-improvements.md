---
name: "Sprint 1 — ASO クイック改善"
about: "アプリ名・サブタイトル・スクショキャプション・キーワードを刷新し、検索流入と CV 率を改善"
title: "[Sprint 1] ASO クイック改善（アプリ名 + サブタイトル + スクショ + キーワード）"
labels: ["sprint-1", "aso", "marketing", "priority-medium"]
assignees: ""
---

## 背景

Sprint 1（緊急止血フェーズ）における**獲得側**の重要施策。

会議録より、現状の課題:
- App Store 検索で「脱出」「ローグライク」「9 マス」関連ワードでの可視性が低い
- スクショからゲーム性が伝わらず、ストアページ訪問 → DL 率（CVR）が低い
- アプリ名・サブタイトルがゲーム性を端的に表現していない

App Store の文言・スクショは**ビルド提出を伴わない更新**が可能（メタデータのみの更新）なため、Sprint 1 の早い段階でリリース反映できる。

元会議録: `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)`

詳細案は別途作成される `docs/aso/sprint-1-improvements.md` を参照（Subagent D が作成）。

## ゴール / Definition of Done

- [ ] 新アプリ名「**Escape Nine - 9マス脱出ローグライク**」に変更（30 文字以内 = 60 byte 以内 ✅）
- [ ] サブタイトル変更（30 文字以内）— ジャンル + 1 行特徴
- [ ] スクショ 5 枚にキャプション（短文 1〜2 行 + アイコン）追加
- [ ] キーワードフィールド見直し（100 文字制限内、半角カンマ区切り）
- [ ] App Store Connect に変更を反映（メタデータ提出 → 審査 → リリース）
- [ ] 変更前の現行値を記録（ロールバック用）
- [ ] 多言語対応するか方針決定（Sprint 1 は日本語のみで OK の判断 OK）

## 文字数チェックリスト（日本語 1 文字 = 2 byte 換算）

- アプリ名「Escape Nine - 9マス脱出ローグライク」
  - 表示文字数: 21 文字（全角 13 + 半角 8）
  - byte 換算: 13×2 + 8×1 = **34 byte**（App Store 30 文字制限内 ✅）
- サブタイトル: 30 文字以内に収める（案は `docs/aso/sprint-1-improvements.md` 参照）
- スクショキャプション: 各 1〜2 行、視認性優先

## 実装タスク

1. 別 Subagent D 作成の `docs/aso/sprint-1-improvements.md` を確認・レビュー
2. App Store Connect での文言更新権限確認
3. 現行メタデータ（アプリ名・サブタイトル・キーワード・説明文）のバックアップ取得
4. 新アプリ名の登録（サブミット）
5. サブタイトル更新
6. キーワード更新（重複・低 CTR ワード削除、ターゲットワード追加）
7. スクショ 5 枚のキャプション付き版を作成（Figma or 専用ツール）
   - 1 枚目: ゲームコア体験（「9 マスから脱出せよ」）
   - 2 枚目: バトル・戦闘（「敵を倒して進め」）
   - 3 枚目: ローグライク要素（「装備で強くなる」）
   - 4 枚目: 達成・スコア（「自己ベストを更新」）
   - 5 枚目: ループ性（「何度でも挑戦」）
8. スクショアップロード（iPhone 6.7 inch / 6.5 inch / 5.5 inch サイズ）
9. プロモーションテキスト（170 文字以内、ビルド不要で更新可）の更新
10. 説明文（4000 文字）の見直し（最初 3 行が「もっと見る」前に表示されることを意識）
11. メタデータのみ提出 → 審査 → リリース
12. Firebase Analytics ＋ App Store Connect 解析で前後比較できるよう KPI 記録（#5 と連動）

## 関連ファイル

- `docs/aso/sprint-1-improvements.md`（別 Subagent D が作成、文言詳細）
- App Store Connect（管理画面、コードリポジトリ外）
- `EscapeNine-endless-/EscapeNine-endless-/Resources/InfoPlist.strings`（任意、ローカライズ対応時）
- スクショ素材ディレクトリ（`docs/aso/screenshots/` 想定）

## 想定工数

- 文言案レビュー・確定: 2 時間
- App Store Connect 操作（メタデータ更新）: 1 時間
- スクショ作成（キャプション付き 5 枚）: 4 時間
- スクショアップロード・プレビュー確認: 1 時間
- 審査待ち（実時間: 24〜48h、作業時間外）
- **合計: 8 時間（審査待ち除く）**

## 完了基準（Sprint 1 完了基準）

- TestFlight に v1.1 を提出可能な状態（メタデータ更新は v1.1 ビルドと別途 or 同時提出 OK）
- App Store ページが新メタデータで公開されている
- 前後比較用の KPI（インプレッション・タップ率・CVR）の取得経路が決まっている

## 関連 Issue / Vault

- 親 Sprint Issue: `[Sprint 1 親 Issue へのリンク placeholder]`
- 関連 Issue: #5（Firebase Analytics KPI 計測 - 流入/CV 関連）
- 文言詳細: `docs/aso/sprint-1-improvements.md`（別 Subagent D 作成）
- 元会議録: `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)`
- 関連スキル: `app-store-submit`, `app-store-screenshots`
