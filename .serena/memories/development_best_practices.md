# 開発ベストプラクティス（汎用）

このドキュメントは、Escape Nine: Endless の開発から得た汎用的なベストプラクティスをまとめています。
次のプロジェクトでも活用できる知見を集約しています。

## 🏗️ アーキテクチャ設計

### MVVM パターン

**原則**:
- ✅ View: UIのみ担当、ロジックを含めない
- ✅ ViewModel: ビジネスロジック、状態管理
- ✅ Model: データ構造、永続化

**実装のポイント**:
```swift
// ✅ Good: ViewModelで状態管理
class GameViewModel: ObservableObject {
    @Published var playerPosition: Int = 0
    @Published var gameState: GameState = .notStarted
    
    func movePlayer(to position: Int) {
        // ビジネスロジック
    }
}

// ❌ Bad: Viewにロジックを書く
struct GameView: View {
    var body: some View {
        Button("Move") {
            // ここにゲームロジックを書かない！
        }
    }
}
```

---

### サービス層の分離

**原則**:
- AIEngine: AI専用
- AudioManager: 音声専用
- GameEngine: ゲームロジック専用

**メリット**:
- ✅ テスタビリティ向上
- ✅ 再利用性向上
- ✅ 責任の明確化

---

## 🎮 ゲーム開発のベストプラクティス

### 1. 定数管理

**原則**: マジックナンバーを使わない

```swift
// ✅ Good: 定数で管理
struct Constants {
    static let gridRows = 3
    static let gridColumns = 3
    static let skillResetInterval = 10
}

// ❌ Bad: マジックナンバー
if currentFloor % 10 == 0 {  // 10って何？
    ...
}
```

**ファイル構成**:
- `Constants.swift`: ゲーム設定
- `GameColors.swift`: カラーパレット
- `Fonts.swift`: フォント定義

---

### 2. グリッド・座標系の扱い

**範囲判定の鉄則**:

```swift
// 3×3グリッド（0-2のインデックス）の場合

// 上端判定
if row >= 1 { ... }  // 上に移動可能

// 下端判定
if row <= gridRows - 2 { ... }  // 下に移動可能

// 左端判定
if col >= 1 { ... }  // 左に移動可能

// 右端判定
if col <= gridColumns - 2 { ... }  // 右に移動可能

// 2マス移動の場合
if row >= 2 { ... }  // 上2マス移動可能
if row <= gridRows - 3 { ... }  // 下2マス移動可能
```

**テストケース**:
```swift
// 必ず端のケースをテスト
- (0, 0): 左上
- (0, 2): 右上
- (2, 0): 左下
- (2, 2): 右下
- (1, 1): 中央
```

---

### 3. ゲームバランス調整

**AI難易度の推奨値**:
```swift
enum AILevel {
    case easy   // 50%の確率で追跡
    case normal // 70%の確率で追跡
    case hard   // 100%追跡 + 予測
}
```

**段階的難易度上昇**:
- ✅ 階層1-20: Easy相当
- ✅ 階層21-40: Normal相当
- ✅ 階層41-100: Hard相当
- ✅ BPMも同時に加速（60→240）

---

### 4. スキルシステム設計

**原則**:
- ✅ 使用回数は明確に表示
- ✅ リセットタイミングを視覚的に通知
- ✅ スキル効果は1ターンまたは即時

**実装例**:
```swift
func useSkill(_ skill: Skill) {
    guard skillUsageCount < Constants.maxSkillUsage else { return }
    
    skillUsageCount += 1
    
    switch skill {
    case .dash: 
        isSkillActive = true  // 今回の移動のみ有効
    case .invisible:
        isInvisible = true    // 1ターン無敵
    case .freeze:
        enemyStopped = true   // 1ターン敵停止
    }
}
```

---

## 🎵 音声システム設計

### ゲームロジックと音声の分離

**重要な原則**:
- ✅ ゲームロジックは音声に依存しない
- ✅ BGM ON/OFFは「音量調整」として実装
- ✅ ビートエンジンは常に動作

**実装パターン**:
```swift
class AudioManager: ObservableObject {
    @Published var isBGMEnabled: Bool = true {
        didSet {
            // ❌ Bad: エンジンを停止
            // beatEngine.pause()
            
            // ✅ Good: 音量だけ調整
            let volume = isBGMEnabled ? bgmVolume : 0.0
            beatEngine.setVolume(volume)
        }
    }
}
```

**理由**:
- ゲームの進行タイミングはビートエンジンで管理
- BGMをOFFにしてもゲームは正常に進行すべき

---

### UserDefaultsの初期値管理

**問題**: `UserDefaults.standard.double(forKey:)` は存在しない場合0を返す

**解決策**:
```swift
// ✅ Good: 初回起動フラグを使う
let isFirstLaunch = !UserDefaults.standard.bool(forKey: "hasLaunchedBefore")

if isFirstLaunch {
    // デフォルト値を設定
    bgmVolume = 0.7
    sfxVolume = 0.8
    isBGMEnabled = true
    isSFXEnabled = true
    
    UserDefaults.standard.set(true, forKey: "hasLaunchedBefore")
    saveUserPreferences()
} else {
    // 保存された設定を読み込み
    bgmVolume = UserDefaults.standard.double(forKey: "bgmVolume")
    sfxVolume = UserDefaults.standard.double(forKey: "sfxVolume")
}
```

---

## 🎨 UI/UX設計

### 1. ユーザーフィードバック

**原則**: すべてのアクションに視覚的フィードバック

```swift
// ✅ ボタンタップ
.onTapGesture {
    withAnimation(.spring()) {
        // アニメーション
    }
    audioManager.playSFX(.tap)
}

// ✅ 重要なイベント通知
if showNotification {
    NotificationView(message: "スキル回復！")
        .transition(.scale.combined(with: .opacity))
        .onAppear {
            DispatchQueue.main.asyncAfter(deadline: .now() + 3.0) {
                showNotification = false
            }
        }
}
```

---

### 2. ゲーム開始時の配慮

**必須要素**:
- ✅ スタートボタン（準備時間を与える）
- ✅ チュートリアル（初回のみ）
- ✅ 操作説明（常に確認可能に）

**実装例**:
```swift
if !isGameStarted {
    VStack {
        Text("準備はいいですか？")
        Button("スタート") {
            withAnimation {
                isGameStarted = true
            }
            audioManager.startBGM(bpm: currentBPM)
        }
    }
}
```

---

### 3. 難しさと理不尽の区別

**良い難しさ**:
- ✅ プレイヤーのスキルで克服可能
- ✅ パターンを学習できる
- ✅ 失敗から学べる

**悪い難しさ（理不尽）**:
- ❌ 避けられない障害
- ❌ 運だけで決まる
- ❌ 情報が不足している

**実装例**:
```swift
// ✅ Good: 霧マップでも消失マスは見える
if disappearedCells.contains(position) {
    return true  // 常に表示
}

// ❌ Bad: 霧マップで消失マスも見えない
// プレイヤーは避けられない → 理不尽
```

---

### 4. レスポンシブデザイン

**原則**: すべてのiPhoneサイズに対応

```swift
struct ResponsiveLayout {
    static func gridSize(for screenWidth: CGFloat) -> CGFloat {
        return screenWidth * 0.85
    }
    
    static func fontSize(for screenWidth: CGFloat) -> CGFloat {
        return screenWidth * 0.05
    }
}
```

**テスト対象**:
- iPhone SE (小)
- iPhone 14 Pro (中)
- iPhone 14 Pro Max (大)

---

## 🧪 テスト戦略

### 1. ユニットテスト

**優先順位**:
1. ゲームロジック（移動、当たり判定）
2. AIアルゴリズム
3. スキルシステム
4. スコア計算

**例**:
```swift
func testPlayerMovement() {
    let engine = GameEngine()
    let initialPosition = 4  // 中央
    
    // 上移動
    let newPosition = engine.move(from: initialPosition, direction: .up)
    XCTAssertEqual(newPosition, 1)
    
    // 範囲外
    let edgePosition = engine.move(from: 0, direction: .up)
    XCTAssertNil(edgePosition)
}
```

---

### 2. 実機テスト

**チェックリスト**:
- [ ] タップ・スワイプの反応速度
- [ ] アニメーションの滑らかさ
- [ ] 音声の同期
- [ ] バッテリー消費
- [ ] 発熱

---

## 📦 依存関係管理

### CocoaPods vs SPM

**推奨**: Swift Package Manager (SPM)

**理由**:
- ✅ Xcode統合
- ✅ 軽量
- ✅ Swiftネイティブ

**よく使うパッケージ**:
- Firebase: 認証、DB、Analytics
- Lottie: アニメーション
- Kingfisher: 画像キャッシュ

---

## 🚀 リリース前チェックリスト

### 1. コード品質
- [ ] SwiftLint エラー0
- [ ] ビルド警告0
- [ ] メモリリークなし

### 2. テスト
- [ ] ユニットテスト全通過
- [ ] 実機テスト（3機種以上）
- [ ] ベータテスト（TestFlight）

### 3. ドキュメント
- [ ] README.md 更新
- [ ] CHANGELOG 記載
- [ ] App Store説明文

### 4. App Store対応
- [ ] スクリーンショット（全サイズ）
- [ ] プレビュー動画
- [ ] プライバシーポリシー
- [ ] 利用規約

---

## 🔧 トラブルシューティング

### ビルドエラー

**よくある原因**:
1. 署名の問題 → Xcode設定確認
2. CocoaPods → `pod install` 再実行
3. キャッシュ → Clean Build Folder

---

### メモリリーク

**確認方法**:
1. Instruments → Leaks
2. `[weak self]` の使用
3. NotificationCenter の購読解除

---

### パフォーマンス問題

**最適化ポイント**:
1. 画像サイズ削減
2. アニメーション最適化
3. 不要な再描画を避ける

```swift
// ✅ Good: 必要な時だけ更新
.onChange(of: playerPosition) { newValue in
    updateUI()
}

// ❌ Bad: 毎フレーム更新
.onReceive(timer) { _ in
    updateUI()  // 重い！
}
```

---

## 📚 参考資料

### 公式ドキュメント
- [Swift.org](https://swift.org/)
- [SwiftUI Tutorials](https://developer.apple.com/tutorials/swiftui)
- [Human Interface Guidelines](https://developer.apple.com/design/human-interface-guidelines/)

### コミュニティ
- [Swift Forums](https://forums.swift.org/)
- [Stack Overflow](https://stackoverflow.com/questions/tagged/swift)
- [Reddit r/iOSProgramming](https://www.reddit.com/r/iOSProgramming/)

---

## 🎯 次のプロジェクトで最初にやること

1. **アーキテクチャ設計**
   - MVVM or VIPER
   - ディレクトリ構造

2. **CI/CD構築**
   - GitHub Actions
   - 自動ビルド・テスト

3. **ツール導入**
   - SwiftLint
   - SwiftFormat
   - Danger

4. **ドキュメント作成**
   - README.md
   - 要件定義書
   - アーキテクチャ図

5. **基盤実装**
   - ログシステム
   - エラーハンドリング
   - ネットワーク層

---

## 📝 更新履歴

- 2025-11-28: 初版作成（Escape Nine: Endless の知見をまとめ）
