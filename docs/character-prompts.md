# キャラクター画像生成プロンプト集 ⭐️

最終更新: 2026-05-14

> **用途**: ChatGPT image-2 / Gemini 2.5 Flash Image / DALL-E 等で「Escape Nine」のキャラクタースプライト 9 体を**スタイル統一して**生成するためのプロンプト集。
>
> **対象キャラ**: 味方 5 体 (勇者 / 盗賊 / 魔法使い / エルフ / ナイト) + 敵 4 体 (赤鬼 / 青鬼 / 骸骨 / ドラゴン) = **計 9 体**。
>
> **既存生成パイプライン**: `ads/generate-images.py` (Gemini 2.5 Flash Image) でも流用可能。`BRIEFS` 配列に追記する形で対応。

---

## 🎯 全体戦略

ChatGPT image-2 (および類似モデル) は**同一スレッド内**で**直前の生成スタイルを参照する**性質がある。
**9 キャラを 1 つのスレッドで連続生成**するとスタイル統一が劇的に楽になる。

### 推奨フロー

```
[Step 1] スレッド先頭で §A の「スタイルガイド」を宣言してコピペ
   ↓
[Step 2] 1 キャラ目 (勇者) を §B-1 のプロンプトで生成 → 基準として OK か確認
   ↓
[Step 3] 「同じスタイルで次は盗賊を作って」と会話で続ける
   ↓
[Step 4] 全 9 キャラを生成 → 不満なものは §C の修正フレーズで再生成
   ↓
[Step 5] PNG ダウンロード → §D の sips でダウンサンプル → §E で Assets.xcassets に配置
```

---

## §A スレッド先頭の「スタイルガイド」宣言文 (コピペ用)

```
これから iOS ゲーム「Escape Nine」用のキャラクタースプライトを 9 体、同じスタイルで生成します。
以下のスタイルガイドを全キャラに適用してください:

【スタイル】
- 16-bit レトロ RPG ピクセルアート (Final Fantasy IV / Chrono Trigger / Secret of Mana の質感)
- 正面向きフルボディ、キャラが画面の 80% を占める
- 背景は完全透過 (透明な PNG / alpha channel)
- アンチエイリアスなし、シャープなピクセルエッジ
- 1024x1024 で出力 (あとで 64x64 にダウンサンプルする)
- パレットは 16 色以内、暖色系の冒険ファンタジー (sandy brown / goldenrod / dark brown / beige / gold accent)
- 頭身は 5:1 (英雄的プロポーション、チビキャラ NG)
- 影なし、ハイライトは上から弱め

【避けること】
- 写実的なレンダリング、3D 風、なめらかなグラデーション
- 現代アニメ風 (もっとレトロドット絵に寄せる)
- 複数キャラを 1 枚に詰める、テキスト・透かし・署名
- 背景に何か描く (完全透過のみ)

ここからキャラを 1 体ずつ指示します。OK ですか?
```

---

## §B キャラ別プロンプト

### 👤 味方 (Players)

#### B-1. 勇者 (hero.imageset)

```
1 体目: 勇者を作って。
- 若い男性の戦士、ボサッとしたブロンドの髪、青いチュニックに金トリム
- 短い革のブーツ、片手に小ぶりな剣 (camera 側に向ける)、青いマント
- 表情は決意に満ちた微笑、希望に満ちた雰囲気
- カラー: 鮮やかな青 #3b7dd8、金 #ffd700、肌色は健康的なタン
- ポーズ: アラート、剣をわずかに上げ構える
```

#### B-2. 盗賊 (thief.imageset)

```
2 体目: 盗賊を作って。
- 黒いフードで目元が隠れた俊敏なローグ、緑のフード付きクローク
- 腰に交差した 2 本のダガー、軽い革鎧、ニヤリと笑う口元
- カラー: フォレストグリーン #3d7841、ダークグレー #4a4a4a、紫のサッシュ
- ポーズ: 低く屈み、斜め方向にダッシュ準備
```

#### B-3. 魔法使い (wizard.imageset)

```
3 体目: 魔法使いを作って。
- 白い長いひげの老賢者、深紫のローブに星の模様
- とがった魔法使い帽、ねじれた木の杖の先端に光るクリスタル玉
- 体の周りに半透明の魔法オーラ (うっすら、完全透明にはしない)
- カラー: 深紫 #5a2a7a、銀白 #dcdcdc、シアンの光 #7ad9ff
- ポーズ: 杖を上げて呪文を唱える
```

#### B-4. エルフ (elf.imageset)

```
4 体目: エルフを作って。
- 長く尖った耳の優雅な森のエルフ、流れるエメラルドグリーンのドレス
- 三つ編みの銀髪に葉を編み込み、手首に蔦のブレスレット
- 両手から光る蔦のツルが伸びる
- カラー: エメラルドグリーン #1c8d4a、シルバー #cfcfcf、髪のピンクの花 #ffaecf
- ポーズ: 両腕を広げて蔦の呪文を放つ
```

#### B-5. ナイト (knight.imageset) ⚠️ 未生成、新規追加候補

> `Models/Character.swift` / `docs/game-spec.md` には登場するが、`Assets.xcassets/knight.imageset` は未作成。

```
5 体目: ナイトを作って (これは新キャラです)。
- 全身銀色のプレートアーマー、金のライオン紋章付き丸盾を胸前に構える
- 赤い羽根飾り付きヘルメット、腰に鞘に収めた短剣
- カラー: 磨かれた銀 #b0b0b0、金 #daa520、深紅の羽根 #c8252a
- ポーズ: 盾を胸前に立てて防御
```

### 👹 敵 (Enemies)

#### B-6. 赤鬼 (red_oni.imageset, 階層 1-25)

```
6 体目: 赤鬼を作って。
- 凶悪な日本の赤鬼、燃えるような赤い肌、短い金色の角 2 本
- 黄色い怒りの目、鋭い牙、虎柄のふんどし、トゲ付き鉄棍棒 (金棒)
- カラー: 血のような赤 #c8252a、金 #daa520、角の付け根が黒
- ポーズ: 棍棒を肩に担いで歯を剥く
```

#### B-7. 青鬼 (blue_oni.imageset, 階層 26-50)

```
7 体目: 青鬼を作って。
- 不気味な日本の青鬼、深いコバルトブルーの肌、長い銀色の角 2 本
- 冷たく計算高い紫の目、大きな牙、暗いふんどしに鎖を巻く
- 黒い湾曲した刀
- カラー: コバルトブルー #1e3a8a、シルバー #cfcfcf、紫のハイライト
- ポーズ: 刀を抜いて低く構え、襲いかかる直前
```

#### B-8. 骸骨 (skeleton.imageset, 階層 51-75)

```
8 体目: 骸骨戦士を作って。
- アンデッドの骨格、骨色の体、紫に光る眼窩
- ぼろぼろの黒いフード付きマント、錆びた曲がった鎌
- 足元に紫のエーテルなオーラ
- カラー: 骨のアイボリー #e8e0c8、深紫オーラ #5a2a7a、錆びた鎌 #8a4a1a
- ポーズ: 鎌を斜めに構え、頭を傾ける
```

#### B-9. ドラゴン (dragon.imageset, 階層 76-100)

```
9 体目: ドラゴンを作って (これがラスボス相当です、最も威圧的に)。
- 小型だが凶暴な子ドラゴン、深紅の鱗、金色の腹
- 黒い革のような翼を半分広げる、小さな角 2 本、オレンジに光る目
- 口の周りと尻尾の先に火の粉
- カラー: 深紅 #a01818、金 #daa520、黒い翼、オレンジの炎 #ff7a00
- ポーズ: 翼を広げ、口を開けて火を吐く構え、爪を出す
```

---

## §C ChatGPT への追い込みフレーズ集 (うまくいかない時)

会話で重ねて指示することで品質を上げる:

| 症状 | 修正フレーズ |
|---|---|
| ぼやけたピクセル | 「ピクセル感がぼやけている。アンチエイリアスを完全に切って、シャープなドットエッジで再生成」 |
| キャラが小さすぎる | 「キャラが画面の真ん中に小さく配置されている。画面の 80% 高さで大きく」 |
| 背景が透明じゃない | 「背景が灰色のチェッカーになっている。完全透明な PNG (alpha channel) で出力して」 |
| 現代アニメ風 | 「現代アニメ風になっている。もっと SNES Chrono Trigger / FF4 のスプライト風に寄せて」 |
| 色数が多すぎ | 「色数が多すぎる。16 色パレットに減らして、Genesis/Mega Drive 風の限定パレットに」 |
| ポーズが硬い | 「ポーズが正面すぎる。少し動きをつけて (剣を振り上げる、杖を掲げる等)」 |
| 頭身が低い (チビ) | 「頭身が低すぎる (チビ風)。5:1 の英雄的プロポーションに、頭を小さく体を長く」 |

---

## §D 生成後の Mac 処理 (sips でダウンサンプル)

```bash
# ダウンロードした 1024x1024 PNG (例: ~/Downloads/hero.png) を縮小
cd ~/Downloads

# 64x64 (1x) - Nearest Neighbor 補間でピクセル感を保つ
sips -z 64 64 --interpolation nearest hero.png --out hero_64.png

# 128x128 (@2x, Retina 用)
sips -z 128 128 --interpolation nearest hero.png --out hero@2x.png

# 192x192 (@3x, Retina HD 用)
sips -z 192 192 --interpolation nearest hero.png --out hero@3x.png

# 透過確認 ("hasAlpha: yes" であれば OK)
sips -g hasAlpha hero_64.png
```

> ⚠️ `--interpolation nearest` は必須。これを忘れると 64×64 にした時にピクセル感が消えてぼやける。

---

## §E Assets.xcassets への配置

```bash
cd /Users/yoshidometoru/Documents/GitHub/escape-nine-endless/EscapeNine-endless-/EscapeNine-endless-/Assets.xcassets/hero.imageset

# 既存ファイルをバックアップ
mv hero.png hero.png.bak

# 新しいファイルを配置
mv ~/Downloads/hero_64.png hero.png
mv ~/Downloads/hero@2x.png .
mv ~/Downloads/hero@3x.png .
```

`Contents.json` の参考形式 (3 解像度入れる場合):

```json
{
  "images" : [
    { "idiom" : "universal", "filename" : "hero.png", "scale" : "1x" },
    { "idiom" : "universal", "filename" : "hero@2x.png", "scale" : "2x" },
    { "idiom" : "universal", "filename" : "hero@3x.png", "scale" : "3x" }
  ],
  "info" : { "version" : 1, "author" : "xcode" }
}
```

> 新規 imageset 作成 (ナイト等) 時は、既存の `hero.imageset/` をコピーして `knight.imageset/` にリネーム、内部の Contents.json と png ファイル名を編集するのが最速。

---

## §F 進捗トラッカー (任意で使う)

| # | キャラ | 既存画像 | 新画像生成 | sips 処理 | Assets 配置 | 確認 |
|---|---|---|---|---|---|---|
| 1 | hero | ✅ | ⬜ | ⬜ | ⬜ | ⬜ |
| 2 | thief | ✅ | ⬜ | ⬜ | ⬜ | ⬜ |
| 3 | wizard | ✅ | ⬜ | ⬜ | ⬜ | ⬜ |
| 4 | elf | ✅ | ⬜ | ⬜ | ⬜ | ⬜ |
| 5 | knight | ❌ (未作成) | ⬜ | ⬜ | ⬜ (新規 imageset 要) | ⬜ |
| 6 | red_oni | ✅ | ⬜ | ⬜ | ⬜ | ⬜ |
| 7 | blue_oni | ✅ | ⬜ | ⬜ | ⬜ | ⬜ |
| 8 | skeleton | ✅ | ⬜ | ⬜ | ⬜ | ⬜ |
| 9 | dragon | ✅ | ⬜ | ⬜ | ⬜ | ⬜ |

---

## §G カラーパレット (`docs/game-spec.md` 整合)

`Utilities/Constants.swift` の `GameColors` と整合する冒険ファンタジー系:

| 名前 | Hex | 用途 |
|---|---|---|
| サンディブラウン | `#f4a460` | メイン |
| ゴールデンロッド | `#daa520` | アクセント |
| ダークブラウン | `#2c1810` | 背景 |
| ベージュ | `#f5deb3` | テキスト |
| ゴールド | `#ffd700` | 金テキスト・宝物 |
| トマトレッド | `#ff6347` | 警告・敵 |
| ライトグリーン | `#90ee90` | 成功・エルフ |

キャラのアクセント色は上記の **隣接色** (青系・紫系・銀系) を 1-2 色加える程度に抑えると、UI 全体との調和が取れる。

---

## §H 既存 `ads/generate-images.py` で自動化する場合 (参考)

```python
# ads/generate-images.py の BRIEFS 配列に追加
BRIEFS = [
    # ... 既存の広告画像 ...
    {
        "concept": "character_hero",
        "filename": "hero.png",
        "out_dir": "generated_imgs/characters",
        "aspect": "1:1",
        "prompt": (
            "A 64x64 pixel art sprite, retro 16-bit RPG style (Final Fantasy IV / Chrono Trigger), "
            "front-facing character portrait centered on transparent background, "
            "a brave young warrior boy with messy blond hair, blue tunic with gold trim, "
            "short leather boots, holding a small sword facing camera, blue cape, "
            "Color accents: bright blue (#3b7dd8), gold (#ffd700), tan skin, "
            "Pose: standing alert, sword raised slightly, heroic 5:1 proportions, "
            "limited 16-color palette, no anti-aliasing, sharp pixel edges, alpha channel transparent."
        ),
    },
    # ... 他 8 体も同様 ...
]
```

実行: `python3 ads/generate-images.py` で 9 枚一括生成。
生成後は §D の sips ダウンサンプルと §E の Assets 配置を実施。

---

## §I 関連ドキュメント

- ゲーム仕様 (キャラ一覧 / カラーパレット / ディレクトリ構造): `docs/game-spec.md`
- 既存生成パイプライン: `ads/generate-images.py`
- キャラモデル定義: `EscapeNine-endless-/EscapeNine-endless-/Models/Character.swift`
- カラー定数: `EscapeNine-endless-/EscapeNine-endless-/Utilities/Constants.swift` の `GameColors`
- 会議録 (キャラ戦略の典拠): Obsidian `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)` 第 14 回 ビジュアルアイデンティティ
