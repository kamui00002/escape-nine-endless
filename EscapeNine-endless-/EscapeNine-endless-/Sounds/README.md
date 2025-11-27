# Sounds フォルダ

このフォルダには、ゲームで使用するBGMと効果音を配置します。

## 📂 フォルダ構成

```
Sounds/
├── BGM/           # 背景音楽
│   ├── bgm_60.mp3
│   ├── bgm_120.mp3
│   ├── bgm_180.mp3
│   └── bgm_240.mp3
└── SFX/           # 効果音
    ├── move.wav
    ├── skill.wav
    ├── gameover.wav
    ├── floor_clear.wav
    ├── button_tap.wav
    └── warning.wav
```

## 🎵 BGMファイル仕様

| ファイル名 | BPM | 階層 | 用途 |
|-----------|-----|------|------|
| bgm_60.mp3 | 60 | 1-20 | 序盤（不気味・導入） |
| bgm_120.mp3 | 120 | 21-40 | 中盤（緊迫感上昇） |
| bgm_180.mp3 | 180 | 41-60 | 後半（高速追跡） |
| bgm_240.mp3 | 240 | 61-100 | 終盤（極限スピード） |

### BGM要件
- **形式**: MP3またはM4A
- **ループ**: シームレスにループ可能であること
- **長さ**: 30秒〜2分程度
- **ボーカル**: なし（インストゥルメンタル）
- **正確なBPM**: ファイル名のBPMと完全一致すること

## 🔊 効果音ファイル仕様

| ファイル名 | 用途 | タイミング |
|-----------|------|-----------|
| move.wav | 移動音 | プレイヤーが移動した時 |
| skill.wav | スキル使用音 | スキル発動時 |
| gameover.wav | ゲームオーバー | 鬼に捕まった時 |
| floor_clear.wav | フロアクリア | 階層クリア時 |
| button_tap.wav | ボタンタップ | UI操作時 |
| warning.wav | 警告音 | 危険な状況 |

### 効果音要件
- **形式**: WAV（推奨）またはMP3
- **長さ**: 0.5秒〜2秒
- **音量**: 統一感のある音量レベル
- **品質**: 44.1kHz, 16bit以上

## 📥 ファイル配置手順

### Xcodeで追加する場合
1. Xcodeでプロジェクトを開く
2. 左サイドバーの `EscapeNine-endless-` グループを右クリック
3. `Add Files to "EscapeNine-endless-"...` を選択
4. `Sounds` フォルダを選択
5. ✅ "Copy items if needed" にチェック
6. ✅ "Create groups" を選択
7. `Add` をクリック

### 注意事項
- ファイル名は **完全一致** である必要があります
- BGMファイルが存在しない場合でも、ゲームは動作します（BGMなしでビート検出のみ）
- 効果音ファイルが存在しない場合は、該当の効果音のみ再生されません

## 🎨 音楽生成のヒント

### Suno AIで生成する場合
- プロンプトに正確なBPMを指定してください
- "no vocals", "loopable", "seamless loop" を含めてください
- ゲームの雰囲気（ダークファンタジー、テクノなど）を明記してください

### 音楽編集が必要な場合
- **Audacity** (無料): ループポイント調整、フェード処理
- **GarageBand** (Mac): トリミング、エフェクト追加
- **Logic Pro** (有料): プロフェッショナル編集

## ✅ 実装状況

- [x] AudioManager作成完了
- [x] Soundsフォルダ作成
- [x] 効果音ファイル生成完了（8ファイル）
- [ ] BGMファイル追加（4ファイル）
- [ ] Xcodeプロジェクトに効果音を登録（手動作業が必要）

---

## 🎉 生成済みファイル

以下の効果音ファイルが生成されています：

```
EscapeNine-endless-/EscapeNine-endless-/Sounds/SFX/
├── button_tap.wav (6.9KB) - ボタンタップ音
├── countdown.wav (26KB) - カウントダウン音
├── floor_clear.wav (86KB) - フロアクリア音
├── game_start.wav (103KB) - ゲームスタート音
├── gameover.wav (103KB) - ゲームオーバー音
├── move.wav (8.7KB) - 移動音
├── skill.wav (69KB) - スキル使用音
└── warning.wav (52KB) - 警告音
```

## 🔧 Xcodeプロジェクトへの追加手順

効果音ファイルをXcodeプロジェクトに追加するには、以下の手順を実行してください：

1. Xcodeでプロジェクトを開く
2. **プロジェクトナビゲーター**（左サイドバー）で `EscapeNine-endless-` グループを右クリック
3. **Add Files to "EscapeNine-endless-"...** を選択
4. `Sounds` フォルダ全体を選択
5. **Options** セクションで以下を確認：
   - ✅ **Copy items if needed** にチェック
   - ✅ **Create groups** を選択（Create folder referencesではない）
   - ✅ **Add to targets** で `EscapeNine-endless-` にチェック
6. **Add** をクリック

これにより、Soundsフォルダとその中のすべての効果音ファイルがプロジェクトに追加されます。

## 🧪 動作確認

プロジェクトにファイルを追加したら、以下を確認してください：

1. **ビルドエラーがないことを確認**
   ```bash
   xcodebuild -scheme EscapeNine-endless- -destination 'id=5B405E6E-0F9F-4715-A97C-D2E85987CB53' build
   ```

2. **効果音が再生されることを確認**
   - ゲームを起動
   - ボタンをタップ → ボタンタップ音が鳴る
   - ゲームを開始 → ゲームスタート音が鳴る
   - 移動する → 移動音が鳴る
   - スキルを使用 → スキル音が鳴る
   - フロアをクリア → フロアクリア音が鳴る
   - ゲームオーバー → ゲームオーバー音が鳴る

---

**準備が整ったら**: 上記の手順に従ってXcodeでファイルを追加し、ビルドして動作確認してください。
