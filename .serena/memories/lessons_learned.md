# Escape Nine: Endless - 失敗と学び

このドキュメントは、開発中に遭遇した問題、失敗、バグ、およびそれらから得た教訓を記録します。
次回同じミスを繰り返さないため、また次のプロジェクトでも活かせるようにまとめています。

## 🐛 バグ修正の記録

### 1. ダッシュスキルの範囲判定バグ (2025-11-28)

**問題**:
- 下2マスへの移動判定: `row <= 0` → 常にfalse（下端でのみtrue）
- 右2マスへの移動判定: `col <= 0` → 常にfalse（右端でのみtrue）

**原因**:
```swift
// ❌ 間違い
if row <= 0 { ... }  // 下2マス移動
if col <= 0 { ... }  // 右2マス移動
```

**修正**:
```swift
// ✅ 正解
if row <= Constants.gridRows - 3 { ... }  // 下2マス移動
if col <= Constants.gridColumns - 3 { ... }  // 右2マス移動
```

**教訓**:
- ✅ 範囲判定は必ずテストケースを書いて確認すること
- ✅ グリッドの端を扱う場合は `>= gridSize - n` または `<= gridSize - n` を使う
- ✅ `<= 0` は「左端・上端」、`>= gridSize - 1` は「右端・下端」
- ✅ 不等号の向きを間違えやすいので、コメントで方向を明記する

**ファイル**: `GameViewModel.swift:342-354`

---

### 2. スキル使用回数の仕様ミス (過去)

**問題**:
- 要件定義書では全キャラのスキル使用回数は5回と記載
- 実装時に魔法使いとエルフのスキル回数を間違えて設定

**修正コミット**: `4e6e407`

**教訓**:
- ✅ 実装前に必ず要件定義書を確認する
- ✅ 定数は `Constants.swift` にまとめて管理する
- ✅ キャラクターごとの差異がある場合は enum で型安全に管理

---

### 3. グリッドタップ時のブレ問題 (過去)

**問題**:
- タップ座標の計算が不安定でブレが発生
- レイアウトの再計算がタイミングによって異なる結果を返す

**修正コミット**: `1868113`

**教訓**:
- ✅ SwiftUIのGeometryReaderは適切に使う
- ✅ タップ判定はローカル座標系で行う
- ✅ グリッドのサイズ計算は1箇所で一元管理

---

### 4. 待機（同じマスにとどまる）の扱い (過去)

**問題**:
- 最初の実装では待機を有効な移動として扱っていなかった
- プレイヤーが追い詰められたときに逃げ場がなくなる

**修正コミット**: `e7c677f`

**教訓**:
- ✅ ゲームバランスを考慮して、待機は有効な選択肢として実装すべき
- ✅ ただし、連続待機（3回以上）はゲームオーバーにしてバランスを取る

---

## 🎮 ゲームバランス調整の記録

### 1. AI難易度の調整 (2025-11-28)

**問題**:
- Easy AIが弱すぎる（プレイヤーに近づく確率30%）
- 初心者がつまらないと感じる可能性

**修正**:
```swift
// Before: 30%の確率でプレイヤーに近づく
if Double.random(in: 0...1) < 0.3 { ... }

// After: 50%の確率でプレイヤーに近づく（初心者向けバランス調整）
if Double.random(in: 0...1) < 0.5 { ... }
```

**教訓**:
- ✅ 初心者向けのバランス調整は実際にプレイして確認
- ✅ Easy: 50%, Normal: 70%, Hard: 100%が適切なバランス
- ✅ ゲーム難易度は段階的に上げる（階層ごとのBPM加速など）

**ファイル**: `AIEngine.swift:35-46`

---

## 🎵 音声システムの設計ミス

### 1. BGM OFF時にビートエンジンを停止してしまう問題 (2025-11-28)

**問題**:
- BGMをOFFにすると `beatEngine` も停止してしまう
- ゲームロジックがビート同期に依存しているため、ゲームが正常に動作しない

**修正**:
```swift
// ❌ 間違い: BGM OFFでビートエンジンを停止
if isBGMEnabled {
    resumeBGM()
} else {
    pauseBGM()  // これがゲームロジックを壊す
}

// ✅ 正解: BGM OFFでも音量だけを0にする
if isBGMEnabled {
    beatEngine.setVolume(bgmVolume)
} else {
    beatEngine.setVolume(0.0)  // ビートエンジンは動き続ける
}
```

**教訓**:
- ✅ ゲームロジックと音声システムを分離する
- ✅ BGM ON/OFFは「音量の変更」として扱う
- ✅ ビートエンジンは常に動作させ、ゲームの進行を制御
- ✅ 音声の有効/無効とエンジンの動作は別物として設計

**ファイル**: `AudioManager.swift:15-22, 130-151`

---

### 2. UserDefaultsの初期値問題 (2025-11-28)

**問題**:
- 初回起動時に `UserDefaults.standard.double(forKey: "bgmVolume")` が0を返す
- デフォルト値（0.7）が設定されない

**修正**:
```swift
// ❌ 間違い: 常に0と比較してしまう
if bgmVolume == 0 && sfxVolume == 0 {
    // 初回起動とは限らない
}

// ✅ 正解: 初回起動フラグを使う
let isFirstLaunch = !UserDefaults.standard.bool(forKey: "hasLaunchedBefore")
if isFirstLaunch {
    // デフォルト値を設定
    UserDefaults.standard.set(true, forKey: "hasLaunchedBefore")
}
```

**教訓**:
- ✅ UserDefaultsの初回判定には専用フラグを使う
- ✅ `double(forKey:)` は存在しない場合0.0を返すので注意
- ✅ デフォルト値は明示的に設定する

**ファイル**: `AudioManager.swift:70-90`

---

## 🎨 UX/UI改善の記録

### 1. 霧マップで消失マスが見えない問題 (2025-11-28)

**問題**:
- 霧マップでは周囲しか見えない
- 消失マスも見えないため、プレイヤーが避けられない（理不尽）

**修正**:
```swift
func isCellVisible(_ position: Int) -> Bool {
    // 消失マスは常に見える（プレイヤーが避けられるように）
    if disappearedCells.contains(position) {
        return true
    }
    
    // 霧マップの処理...
}
```

**教訓**:
- ✅ ゲームデザインは「難しい」と「理不尽」を区別する
- ✅ プレイヤーが避けられない障害は不公平
- ✅ UXを最優先し、仕様通りでも問題があれば調整する

**ファイル**: `GameViewModel.swift:535-541`

---

### 2. スキルリセット通知の追加 (2025-11-28)

**改善内容**:
- 10階層ごとにスキル使用回数がリセットされる
- しかし、プレイヤーに通知がないため気づかない可能性

**追加機能**:
```swift
if currentFloor % Constants.skillResetInterval == 1 {
    skillUsageCount = 0
    showSkillReset = true
    // 3秒後に通知を非表示
    DispatchQueue.main.asyncAfter(deadline: .now() + 3.0) { [weak self] in
        self?.showSkillReset = false
    }
}
```

**教訓**:
- ✅ 重要なゲームイベントは必ず視覚的にフィードバック
- ✅ 通知は3秒程度表示して自動的に消す
- ✅ アニメーションで目立たせる（glow、bounceInなど）

**ファイル**: `GameViewModel.swift:441-448`, `GameView.swift:337-367`

---

### 3. ゲーム開始時の猶予時間 (過去)

**改善内容**:
- ゲーム開始直後にすぐに敵が動き出すと、プレイヤーが準備できない
- スタートボタンを追加して、プレイヤーが準備できてから開始

**修正コミット**: `d114c48`

**教訓**:
- ✅ ゲーム開始時は必ず猶予時間を設ける
- ✅ スタートボタンやカウントダウンで準備時間を与える
- ✅ UXテストは実際に自分でプレイして確認

---

## 📱 iOS開発の落とし穴

### 1. SwiftUIのビルド時間
**問題**: 複雑なViewビルダーは型推論に時間がかかる

**対策**:
- ✅ Viewを小さく分割
- ✅ `some View` を活用
- ✅ 型を明示的に指定

---

### 2. Combineの購読解除
**問題**: メモリリークの原因になりやすい

**対策**:
- ✅ `[weak self]` を使う
- ✅ `cancellables` を適切に管理
- ✅ `onDisappear` で購読解除

---

## 🚀 次のプロジェクトへの提言

以下のことを最初から実装すべき：

1. **エラーログシステム**
   - Crashlytics などの導入
   - デバッグビルドでの詳細ログ

2. **ユニットテスト**
   - ゲームロジックは必ずテスト
   - 特に範囲判定、移動処理

3. **CI/CD**
   - GitHub Actions でビルド自動化
   - SwiftLint の自動実行

4. **ベータテスト**
   - TestFlight で早期フィードバック
   - ゲームバランス調整に活用

5. **設計ドキュメント**
   - アーキテクチャ図
   - データフロー図
   - 状態遷移図

---

## 🎮 移動選択の問題 (2025-11-28)

### 1. マスのぷかぷかエフェクト問題

**問題**:
- スタート時に移動可能なマスがぷかぷか浮かぶエフェクトが表示される
- 視覚的に気が散る、意図しないアニメーション

**原因**:
```swift
// ❌ GridCellView.swift: 移動可能なマスに常時pulseエフェクト
.if(isAvailable) { view in
    view.pulse(minScale: 0.98, maxScale: 1.02, duration: 0.8)
}
```

**修正**:
```swift
// ✅ pulseエフェクトを削除
// 移動可能なマスはglowエフェクトのみで十分
.glow(
    color: isAvailable ? Color(hex: GameColors.available) : .clear,
    radius: isAvailable ? 15 : 0,
    intensity: isAvailable ? 0.8 : 0
)
```

**教訓**:
- ✅ アニメーションは控えめに。過度なエフェクトはUXを悪化させる
- ✅ 移動可能マスはglowと枠線で十分に視認可能
- ✅ 常時アニメーションはバッテリー消費も増加するため避ける

**ファイル**: `GridCellView.swift:66-70`

---

### 2. 2回目以降の移動が失敗する問題

**問題**:
- 1回目の移動は普通に動作
- 2回目以降のマスタップが無視される（移動先が選択できない）

**原因**:
```swift
// ❌ GameViewModel.swift: selectMove()でhasMovedThisBeatをチェック
func selectMove(to position: Int) {
    guard gameStatus == .playing else { return }
    guard !hasMovedThisBeat else { return }  // これが問題！
    ...
}
```

**問題の詳細**:
1. ビート発火 → `hasMovedThisBeat = false`
2. 移動実行 → `hasMovedThisBeat = true`
3. プレイヤーがマスをタップ → `hasMovedThisBeat == true` なので `return`
4. 次のビートまで移動先を変更できない

しかし、仕様として「プレイヤーは次のビートが来るまで何度でも移動先を変更できる」べき！

**修正**:
```swift
// ✅ hasMovedThisBeatチェックを削除
func selectMove(to position: Int) {
    guard gameStatus == .playing else { return }
    // プレイヤーは次のビートまで何度でも移動先を変更できる
    
    let availableMoves = getAvailableMoves()
    guard availableMoves.contains(position) else { return }
    guard !disappearedCells.contains(position) else { return }
    
    pendingPlayerMove = position  // 何度でも更新可能
}
```

**教訓**:
- ✅ `hasMovedThisBeat`は「移動が実行されたか」のフラグ
- ✅ `pendingPlayerMove`は「次のビートで移動する位置」の保持
- ✅ 移動先の選択（`selectMove`）と移動の実行（`onBeat`）は別のタイミング
- ✅ プレイヤーは次のビートまで何度でも移動先を変更できるべき

**ファイル**: `GameViewModel.swift:275-294`

---

### 3. カウントダウンUIの追加

**改善内容**:
- 次のビートまでの残り時間が視覚的にわからない
- プレイヤーがタイミングを掴めない

**追加機能**:
1. **BeatEngineに残り時間計算メソッド追加**:
```swift
/// 次のビートまでの残り時間（0.0〜1.0の割合）
func timeUntilNextBeat() -> Double {
    let now = Date()
    let elapsed = now.timeIntervalSince(lastBeatTime)
    let requiredInterval = isFirstBeat ? (beatInterval * 2.0) : beatInterval
    let remaining = max(0, requiredInterval - elapsed)
    return remaining / requiredInterval
}
```

2. **AudioManagerでメソッド公開**:
```swift
func timeUntilNextBeat() -> Double {
    beatEngine.timeUntilNextBeat()
}
```

3. **BeatIndicatorViewに円形プログレスバー追加**:
```swift
// プログレスバー（カウントダウン）
Circle()
    .trim(from: 0, to: progress)
    .stroke(
        LinearGradient(...),
        style: StrokeStyle(lineWidth: 6, lineCap: .round)
    )
    .frame(width: 80, height: 80)
    .rotationEffect(.degrees(-90))
    .animation(.linear(duration: 0.05), value: progress)
```

**教訓**:
- ✅ タイミングゲームでは必ず視覚的なカウントダウンを提供
- ✅ 円形プログレスバーは直感的でわかりやすい
- ✅ 0.05秒ごとに更新することで滑らかなアニメーション
- ✅ ビート時（progress == 1.0）にはパルスエフェクトで強調

**ファイル**: 
- `BeatEngine.swift:155-162`
- `AudioManager.swift:204-207`
- `BeatIndicatorView.swift` (全体的に更新)

---

## 📝 更新履歴

- 2025-11-28 (2回目): マスのエフェクト問題、移動選択バグ、カウントダウンUI追加
- 2025-11-28: 初版作成（ダッシュバグ、音声システム、UX改善を追加）

- 2025-11-28 (2回目): マスのエフェクト問題、移動選択バグ、カウントダウンUI追加
- 2025-11-28: 初版作成（ダッシュバグ、音声システム、UX改善を追加）
