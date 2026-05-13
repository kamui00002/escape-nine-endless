# App Store メタデータ ⭐️

最終更新: 2026-05-14 (Sprint 1 ASO 改善案を反映)

> このファイルは App Store Connect 入力時のコピペ元。ASO 戦略の根拠は `docs/aso/sprint-1-improvements.md` (4 言語の Name/Subtitle/Keywords 算出ロジック)、`Obsidian: 2026-05-09 [会議録] Escape-Nine 戦略会議 統合` 第 8 回 (ASO) と第 12 回 (結論) を参照。

---

## 1. アプリ名 (App Name, 30 文字以内)

```
Escape Nine
```

> 旧 (1.4.x 系まで): `Escape Nine: Endless`。Sprint 1 で「Endless」を外し、サブタイトル側にジャンル訴求を移譲。

---

## 2. サブタイトル (Subtitle, 30 文字以内)

```
9マス脱出ローグライク
```

> 旧: `9マスの鬼ごっこ音ゲー`。「鬼ごっこ音ゲー」→「ローグライク」へ転換 (会議録 第 8 回 + 第 12 回結論)。「9マス」「脱出」「ローグライク」が **3 主要キーワードをサブタイトルに集中** させ、Keywords フィールドは別ワードで補完する戦略。

---

## 3. 説明文 (Description)

```
9マスの結界で、あなたを追う影から何階まで逃げ切れるか?

【ゲームコンセプト】
9マス (3×3) の盤面でターン制で逃げ続けるローグライク脱出ゲーム。
10ターン耐え切れば次の階層へ。階層が上がるごとに敵 AI が賢くなり、霧・マス消失などの特殊ルールが発動。
100 階層クリアを目指す、究極のエンドレスチャレンジ。

【特徴】
- 一手ですべて変わる: 9マスのターン制脱出、シンプルだが奥深い読み合い
- 発射台型 Game Over: 死んでも即リトライ、自己ベスト誘発演出 +「あと1マスで生存」表示
- 4 キャラ × 固有スキル: 勇者ダッシュ / 盗賊斜め移動 / 魔法使い透明化 / エルフ拘束
- 特殊ルール: 階層 21+ で霧マップ、41+ でマス消失、61+ で複合
- 世界ランキング: Game Center 連携、最高到達階層で世界と競う
- シェア機能: 9マス絵文字でクリア記録を Wordle 風シェア
- リズム連動: BPM 70 → 200 へ加速するビートに合わせた緊張感

【操作】
カウントダウンに合わせてタップで移動するだけ。誰でもすぐに遊べる超シンプル操作。

【キャラクター】
- 勇者: ダッシュで 2 マス移動 (3 回 / 階層)
- 盗賊: 斜め方向に移動可能 (5 回 / 階層)
- 魔法使い: 透明化で衝突を回避 (7 回 / 階層)
- エルフ: 鬼を 2 ターン拘束 (4 回 / 階層)

【こんな人におすすめ】
- ローグライク / ダンジョン系が好き
- 短時間で完結する一手詰め系パズル
- 自己ベスト更新の挑戦欲が湧くゲーム
- 世界ランキングで競いたい
```

---

## 4. キーワード (Keywords, 100 文字以内、半角コンマ区切り)

```
ローグ,暇つぶし,中毒性,一手,リトライ,高難度,迷宮,ダンジョン,カジュアル,ピクセル,神ゲー,放置,やり込み,記録,世界戦,挑戦,ランキング,シンプル
```

実カウント: 約 95 文字 (ASC 入力時に再確認推奨、バッファ 5 文字)

> サブタイトルに既出の「9マス」「脱出」「ローグライク」は重複除外。会議録 第 8 回 Emily 初期案からの圧縮ロジックは `docs/aso/sprint-1-improvements.md` §2.2 参照。

---

## 5. 4 言語サマリ (ASC 入力コピペ用)

### Name / Subtitle

| 言語 | Name | Subtitle |
|---|---|---|
| ja | Escape Nine | 9マス脱出ローグライク |
| en-US | Escape Nine | 9-Tile Roguelike Escape |
| ko-KR | Escape Nine | 9칸 탈출 로그라이크 |
| zh-TW | Escape Nine | 9宮格逃脫 Roguelike 地下城 |

### Keywords (ASC 入力時に文字数再確認)

**ja-JP** (約 95 文字)
```
ローグ,暇つぶし,中毒性,一手,リトライ,高難度,迷宮,ダンジョン,カジュアル,ピクセル,神ゲー,放置,やり込み,記録,世界戦,挑戦,ランキング,シンプル
```

**en-US** (99 文字、ASO 元案 145 文字から圧縮)
```
dungeon,puzzle,grid,retry,addictive,one tap,casual,indie,minimalist,hardcore,survive,ranking,pixel,permadeath,maze,roguelite,9x9,leaderboard,solo
```

**ko-KR** (約 70 文字)
```
던전,퍼즐,중독,한손,재도전,로그라이트,미로,픽셀,인디,미니멀,순위표,챌린지,9x9,9칸,세계랭킹,스코어,몰입,캐주얼,영구사망
```

**zh-TW** (約 66 文字)
```
地牢,益智,上癮,單手,重試,類銀河戰士惡魔城,迷宮,像素,獨立遊戲,極簡,排行榜,挑戰,九宮,世界排名,得分,沉浸,休閒,永久死亡
```

> Keywords 詳細ロジック (各ワード採用理由・削除理由・競合分析) は `docs/aso/sprint-1-improvements.md` §2 (ja/en/ko/zh-TW 各セクション) 参照。

---

## 6. カテゴリ

- プライマリ: ゲーム - パズル
- セカンダリ: ゲーム - ストラテジー

> 旧 (1.4.x まで): プライマリ ミュージック / セカンダリ パズル。「ローグライク」訴求への転換に伴い、「ミュージック」カテゴリは外す。「ストラテジー」は読み合い要素を補強。

---

## 7. 年齢レーティング

```
4+ (暴力的 / 性的コンテンツなし)
```

---

## 8. プロモーションテキスト (Promotional Text, 170 文字以内)

```
Sprint 1 リリース: Game Over 画面を「発射台」へリニューアル!
巨大リトライ + ワンタップリトライ + 自己ベスト誘発演出 + 9マス絵文字シェア機能を追加しました。
```

> プロモーションテキストは審査なしで即時反映できる唯一のフリーテキスト枠。リリースごとに更新推奨。

---

## 9. URL

| 種別 | URL |
|---|---|
| プライバシーポリシー | https://kamui00002.github.io/escape-nine-endless/privacy-policy.html |
| サポート | https://github.com/kamui00002/escape-nine-endless |
| マーケティング (任意) | (未設定) |

---

## 10. ASC 入力チェックリスト

App Store Connect → App 情報 → ローカライズ可能な情報 で 4 言語ごとに入力:

- [ ] 🇯🇵 ja: Name / Subtitle / Keywords / Description / Promotional Text → 保存
- [ ] 🇺🇸 en-US: Name / Subtitle / Keywords / Description (要英訳) / Promotional Text (要英訳) → 保存
- [ ] 🇰🇷 ko-KR: Name / Subtitle / Keywords / Description (要韓訳) / Promotional Text (要韓訳) → 保存
- [ ] 🇹🇼 zh-TW: Name / Subtitle / Keywords / Description (要中訳) / Promotional Text (要中訳) → 保存

> ⚠️ **en-US Keywords は元 ASO 文書の 145 文字版だと「使用できない文字」エラーで弾かれる**。必ず本ファイル §5 の圧縮版 99 文字を使用すること (Obsidian チェックリスト Phase B-3 §E-3 既知の罠)。

> ⚠️ **絵文字・矢印・記号 (→ ☀️ ⭐️ 等) は ASC で「無効な文字」として弾かれる**。本ファイルから ASC へコピペする際は装飾記号が混入していないか確認。

---

## 11. 関連ドキュメント

- ASO 戦略元案: `docs/aso/sprint-1-improvements.md`
- 会議録 (戦略の典拠): Obsidian `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)` 第 8 回 / 第 12 回
- TestFlight 提出後チェックリスト: Obsidian `2026-05-11 Escape Nine v1.5.1 TestFlight 提出後チェックリスト` Phase B-3
- Sprint 2 計画 (アイコン A/B テスト含む): Obsidian `2026-05-11 Escape Nine Sprint 2 計画書 draft`
