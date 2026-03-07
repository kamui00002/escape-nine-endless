# 開発状況

## 完了した機能

### ✅ 基本システム
- [x] プロジェクトセットアップ
- [x] 3×3グリッドUI実装
- [x] 基本移動システム
- [x] 当たり判定システム
- [x] ビート同期システムの基礎実装
- [x] カウントダウンUI実装（2025-11-28）

### ✅ キャラクターシステム
- [x] キャラクター4種定義（勇者、盗賊、魔法使い、エルフ）
- [x] キャラクター選択画面
- [x] スキルシステム実装
- [x] スキル使用回数管理

### ✅ AIシステム
- [x] AI難易度3段階（Easy/Normal/Hard）
- [x] AIエンジン実装
- [x] 鬼キャラクター4種（赤鬼、青鬼、骸骨、ドラゴン）

### ✅ 階層・特殊ルールシステム
- [x] 階層システム（1-100階層）
- [x] 特殊ルール2種（霧マップ、マス消失）
- [x] BPM加速システム
- [x] 階層ごとの鬼切り替え

### ✅ UI/UX
- [x] ホーム画面
- [x] ゲーム画面
- [x] リザルト画面
- [x] ランキング画面
- [x] 設定画面（BGM/効果音音量分離）
- [x] レスポンシブレイアウト
- [x] カラーパレット統一

## 未実装の機能（優先度順）

### 🔴 高優先度

#### BGM・効果音（最重要）
- [x] 効果音ファイル生成完了（8ファイル、460KB）（2025-11-28）
  - button_tap.wav (6.9KB), countdown.wav (26KB)
  - floor_clear.wav (86KB), game_start.wav (103KB)
  - gameover.wav (103KB), move.wav (8.7KB)
  - skill.wav (69KB), warning.wav (52KB)
- [x] 効果音再生の統合（全Viewに実装）（2025-11-28）
- [x] AVFoundation実装（効果音のみ）（2025-11-28）
- [ ] Xcodeプロジェクトに効果音ファイルを手動追加
- [ ] Suno AIでBGM作成（4ファイル）
- [ ] 階層別BPMトラック（80→240）
- [ ] ビート音（メトロノーム音）

#### ビート同期システム（最重要）
- [x] BeatEngineの基礎実装（2025-11-28）
- [x] ビートカウントダウンUI実装（2025-11-28）
- [x] 次のビートまでの残り時間計算機能（2025-11-28）
- [x] ビート判定システムの基礎（2025-11-28）
- [ ] タイミング判定の最適化（±15%誤差）
- [ ] BGMとの完全同期
- [ ] ビートインジケーターの最終調整

### 🟡 中優先度

#### Firebase連携
- [ ] Firebase Authentication（認証）
- [ ] Firestore（ランキングデータ保存）
- [ ] Firebase Analytics（分析）
- [ ] ランキングのクラウド同期

#### 広告システム
- [ ] Google Mobile Ads SDK統合
- [ ] バナー広告（ホーム画面下部）
- [ ] インタースティシャル広告（リトライ時）
- [ ] 広告削除課金との連携

#### アプリ内課金
- [ ] StoreKit統合
- [ ] 魔法使いキャラ購入（¥240）
- [ ] エルフキャラ購入（¥240）
- [ ] 2キャラセット購入（¥480）
- [ ] 広告削除課金（¥240）
- [ ] PurchaseManagerの完成

#### Game Center連携
- [ ] ランキング統合
- [ ] 世界ランキング
- [ ] フレンドランキング
- [ ] リーダーボード実装

### 🟢 低優先度

#### プラクティスモード
- [ ] AI難易度選択
- [ ] 特殊ルール選択
- [ ] 階層選択（クリア済みのみ）
- [ ] 練習モードUI

#### その他
- [ ] オンライン対戦（将来的）
- [ ] 追加キャラクター（将来的）
- [ ] 新特殊ルール（将来的）
- [ ] ショップ画面（課金実装後）

## ファイル別実装状況

### Models（ほぼ完成）
- Character.swift: ✅ 完成
- GameState.swift: ✅ 完成
- Floor.swift: ✅ 完成
- Skill.swift: ✅ 完成

### Views（ほぼ完成）
- HomeView.swift: ✅ 完成
- GameView.swift: ✅ 完成
- CharacterSelectionView.swift: ✅ 完成
- RankingView.swift: ✅ UI完成（Firebase連携待ち）
- SettingsView.swift: ✅ 完成
- ResultView.swift: ✅ 完成
- GridBoardView.swift: ✅ 完成
- GridCellView.swift: ✅ 完成
- BeatIndicatorView.swift: ⚠️ UI完成（ビート同期待ち）
- BPMInfoView.swift: ✅ 完成

### Services（一部未完成）
- AIEngine.swift: ✅ 完成
- GameEngine.swift: ✅ 完成
- StageManager.swift: ✅ 完成
- BeatEngine.swift: ⚠️ 未完成（BGM実装待ち）
- FirebaseService.swift: ⚠️ 未完成
- AdMobService.swift: ⚠️ 未完成
- StoreKitService.swift: ⚠️ 未完成
- PurchaseManager.swift: ⚠️ 未完成
- RankingService.swift: ⚠️ 未完成（Firebase待ち）

### ViewModels（ほぼ完成）
- GameViewModel.swift: ✅ 完成
- PlayerViewModel.swift: ✅ 完成
- RankingViewModel.swift: ⚠️ 未完成（Firebase待ち）

### Utilities（完成）
- Constants.swift: ✅ 完成
- Fonts.swift: ✅ 完成
- AnimationEffects.swift: ✅ 完成
- ResponsiveLayout.swift: ✅ 完成

## 次のステップ

1. **BGM・効果音作成**（Suno AI使用）
2. **BeatEngine完成**（ビート同期システム）
3. **Firebase連携**（認証・ランキング）
4. **AdMob統合**（広告表示）
5. **StoreKit統合**（課金システム）
6. **Game Center連携**（ランキング）
7. **総合テスト**
8. **App Storeリリース準備**

## 既知の問題
- BGMファイル未実装のため、音楽はまだ鳴らない（効果音のみ実装済み）
- 効果音ファイルをXcodeプロジェクトに手動追加する必要あり
- ビート同期システムの最適化が必要（BGM実装後）
- Firebase未連携のため、ランキングはローカルのみ
- 広告・課金システム未実装のため、収益化機能なし

## 最近の更新

### 2025-11-28 (初回更新)

#### UI/UXバグ修正
- ✅ マスのぷかぷかエフェクト削除（不要なアニメーション除去）
- ✅ 2回目以降の移動選択バグ修正（hasMovedThisBeatチェック削除）
- ✅ カウントダウンUI実装（円形プログレスバー + 数値表示）
- ✅ ビート発火のデバッグログ追加

#### 効果音システム実装
- ✅ 効果音8ファイル生成（generate_sound_effects.py使用）
- ✅ 全Viewに効果音再生統合
- ✅ Sounds/README.md更新（Xcode追加手順も記載）

#### ドキュメント追加
- ✅ lessons_learned.md - プロジェクト固有の失敗と学び
- ✅ development_best_practices.md - 汎用的なベストプラクティス

### 2025-11-28 (コードレビュー対応)

#### 🔴 重大な問題の修正
1. **メモリリーク修正 (BeatEngine.swift)**
   - ✅ Timerの適切な管理（RunLoopを明示的に指定）
   - ✅ 既存タイマーの確実な無効化処理追加

2. **スレッドセーフティ確保 (GameViewModel.swift)**
   - ✅ Combine購読に`.receive(on: DispatchQueue.main)`追加
   - ✅ UI更新がメインスレッドで実行されることを保証

3. **初回ビート判定バグ修正 (BeatEngine.swift)**
   - ✅ `play()`と`resume()`で異なる動作を実装
   - ✅ play()時のみ初回猶予時間を適用

#### 🟡 パフォーマンス最適化
1. **GridBoardViewのリファクタリング**
   - ✅ 手動記述していた9個のセルをForEachループに変更
   - ✅ コード量を約70%削減（100行→30行）
   - ✅ DRY原則を遵守

#### ⚖️ ゲームバランス調整
1. **BPM進行曲線の改善 (Floor.swift)**
   - ✅ 初期BPMを60→80に引き上げ（60は遅すぎる）
   - ✅ 最大BPMを240→200に抑制（240は速すぎる）
   - ✅ 段階的な加速：階層1-20（緩やか）、21-50（中程度）、51-100（急加速）

2. **スキルバランス調整 (Character.swift)**
   - ✅ ダッシュ：5回→3回（強力すぎるため減少）
   - ✅ 透明化：5回→7回（弱いため増加）
   - ✅ 拘束：1ターン→2ターン停止に強化、使用回数5→4回

3. **AI難易度調整 (AIEngine.swift)**
   - ✅ Easy: ランダム移動確率50%→70%（初心者に優しく）
   - ✅ 拘束スキルのロジック改善（enemyStoppedTurnsで管理）

#### 🛠️ コード品質向上
1. **デバッグコードの条件付きコンパイル化 (PlayerViewModel.swift)**
   - ✅ デバッグプロパティを`#if DEBUG`で囲む
   - ✅ リリースビルドではデバッグ機能が無効化
   - ✅ セキュリティとパフォーマンスの向上

#### ✨ 新機能実装
1. **実績システム (Achievement.swift + AchievementPopupView.swift)**
   - ✅ 9種類の実績定義
   - ✅ 実績解除時のポップアップ通知
   - ✅ 実績一覧画面（プログレスバー付き）
   - ✅ UserDefaultsで永続化

2. **ビジュアルフィードバック強化 (AnimationEffects.swift)**
   - ✅ スキルフラッシュエフェクト追加
   - ✅ 移動成功パーティクルエフェクト追加
   - ✅ ゲームオーバーシェイクエフェクト追加
