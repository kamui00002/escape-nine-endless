# DEVELOPMENT_SWIFT.md - Escape Nine: Endless (iOS Native)

## 📘 Swift開発版ドキュメント

このドキュメントはSwift(iOS Native)での開発用技術仕様書です。

---

## 目次
1. [技術スタック](#技術スタック)
2. [プロジェクト構成](#プロジェクト構成)
3. [セットアップ手順](#セットアップ手順)
4. [アーキテクチャ](#アーキテクチャ)
5. [ディレクトリ構造](#ディレクトリ構造)
6. [音楽同期システム](#音楽同期システム)
7. [実装優先順位](#実装優先順位)

---

## 技術スタック

### 開発環境
- **言語**: Swift 5.9+
- **最小iOS**: iOS 14.0
- **IDE**: Xcode 15.0+
- **UI Framework**: SwiftUI
- **アーキテクチャ**: MVVM + Combine

### フレームワーク・ライブラリ

#### Apple標準
- **SwiftUI**: UI構築
- **Combine**: リアクティブプログラミング
- **AVFoundation**: 音楽再生・同期(最重要)
- **GameKit**: Game Center連携
- **StoreKit**: アプリ内課金

#### サードパーティ
- **Firebase**:
  - FirebaseAuth
  - FirebaseFirestore (ランキング)
  - FirebaseAnalytics
- **Google Mobile Ads SDK**: AdMob広告

### パッケージ管理
- **Swift Package Manager** (推奨)
- CocoaPods (Firebase用に必要な場合)

---

## プロジェクト構成

### 基本情報
- **プロジェクト名**: EscapeNine
- **Bundle ID**: com.souatou.escapenine
- **最小iOS**: 14.0
- **対応デバイス**: iPhone専用

### ゲームの核心
- **音楽同期**: AVAudioEngineでビート検出(最重要)
- **60fps動作**: スムーズなアニメーション
- **低遅延**: タップからの反応速度

---

## セットアップ手順

### 1. Xcodeプロジェクト作成

```bash
# Xcodeで新規プロジェクト作成
# App Template
# Interface: SwiftUI
# Language: Swift
# Project Name: EscapeNine
# Bundle ID: com.souatou.escapenine
```

### 2. Swift Package Manager で依存関係追加

```
File > Add Package Dependencies

Firebase iOS SDK:
https://github.com/firebase/firebase-ios-sdk
- FirebaseAuth
- FirebaseFirestore
- FirebaseAnalytics

Google Mobile Ads:
https://github.com/googleads/swift-package-manager-google-mobile-ads
```

### 3. Info.plist設定

```xml
<!-- 音楽再生許可 -->
<key>UIBackgroundModes</key>
<array>
    <string>audio</string>
</array>

<!-- Game Center -->
<key>UIRequiredDeviceCapabilities</key>
<array>
    <string>gamekit</string>
</array>
```

---

## アーキテクチャ

### MVVM + Combine パターン

```
View (SwiftUI)
    ↓ User Action
ViewModel (ObservableObject)
    ↓ Business Logic
Model (Struct/Class)
    ↓ Data
Service Layer (API/Database)
```

### レイヤー構成

```
EscapeNine/
├── App/
│   ├── EscapeNineApp.swift          # エントリーポイント
│   └── AppDelegate.swift            # Firebase初期化
│
├── Views/                           # SwiftUI Views
│   ├── Home/
│   │   └── HomeView.swift
│   ├── Game/
│   │   ├── GameView.swift           # メインゲーム画面
│   │   ├── GridBoardView.swift      # 9マス盤面
│   │   ├── BeatIndicatorView.swift  # ビート表示
│   │   └── CharacterSpriteView.swift
│   ├── Result/
│   │   └── ResultView.swift
│   ├── Ranking/
│   │   └── RankingView.swift
│   ├── Shop/
│   │   └── ShopView.swift
│   ├── Practice/
│   │   └── PracticeView.swift
│   └── Settings/
│       └── SettingsView.swift
│
├── ViewModels/
│   ├── GameViewModel.swift          # ゲームロジック制御
│   ├── PlayerViewModel.swift        # プレイヤーデータ
│   └── RankingViewModel.swift       # ランキング
│
├── Models/
│   ├── Character.swift
│   ├── GameState.swift
│   ├── Floor.swift
│   └── Skill.swift
│
├── Services/                        # ビジネスロジック
│   ├── BeatEngine.swift            # 音楽同期エンジン(最重要)
│   ├── GameEngine.swift            # ゲームロジック
│   ├── AIEngine.swift              # AI制御
│   ├── StageManager.swift          # 階層管理
│   ├── SkillManager.swift          # スキル管理
│   └── RankingService.swift        # ランキング
│
├── Utilities/
│   ├── Constants.swift
│   ├── Extensions.swift
│   └── Helpers.swift
│
├── Config/
│   ├── FirebaseConfig.swift
│   ├── AdMobConfig.swift
│   └── GameConfig.swift
│
└── Resources/
    ├── Assets.xcassets/            # 画像・アイコン
    ├── Sounds/                     # BGM・効果音
    │   ├── BGM/
    │   └── SFX/
    └── Fonts/
```

---

## 音楽同期システム

### BeatEngine.swift (最重要)

```swift
import AVFoundation
import Combine

class BeatEngine: ObservableObject {
    // MARK: - Published Properties
    @Published var currentBeat: Int = 0
    @Published var isPlaying: Bool = false
    
    // MARK: - Private Properties
    private var audioPlayer: AVAudioPlayer?
    private var bpm: Double = 60
    private var beatInterval: TimeInterval = 1.0
    private var timer: Timer?
    private var lastBeatTime: Date = Date()
    
    // MARK: - Constants
    private let timingTolerance: Double = 0.15 // ±15%の誤差許容
    
    // MARK: - Initialization
    init() {
        setupAudioSession()
    }
    
    // MARK: - Audio Session Setup
    private func setupAudioSession() {
        do {
            let audioSession = AVAudioSession.sharedInstance()
            try audioSession.setCategory(.playback, mode: .default)
            try audioSession.setActive(true)
        } catch {
            print("Audio Session setup failed: \(error)")
        }
    }
    
    // MARK: - Load Music
    func loadMusic(bpm: Double) {
        self.bpm = bpm
        self.beatInterval = 60.0 / bpm
        
        // BGMファイル読み込み
        guard let url = Bundle.main.url(
            forResource: "bgm_\(Int(bpm))",
            withExtension: "mp3"
        ) else {
            print("BGM file not found")
            return
        }
        
        do {
            audioPlayer = try AVAudioPlayer(contentsOf: url)
            audioPlayer?.prepareToPlay()
            audioPlayer?.numberOfLoops = -1 // 無限ループ
        } catch {
            print("Failed to load BGM: \(error)")
        }
    }
    
    // MARK: - Playback Control
    func play() {
        audioPlayer?.play()
        isPlaying = true
        startBeatDetection()
    }
    
    func stop() {
        audioPlayer?.stop()
        isPlaying = false
        timer?.invalidate()
        timer = nil
    }
    
    func pause() {
        audioPlayer?.pause()
        isPlaying = false
        timer?.invalidate()
    }
    
    func resume() {
        audioPlayer?.play()
        isPlaying = true
        startBeatDetection()
    }
    
    // MARK: - Beat Detection
    private func startBeatDetection() {
        lastBeatTime = Date()
        
        // 高精度タイマー(10msごとにチェック)
        timer = Timer.scheduledTimer(
            withTimeInterval: 0.01,
            repeats: true
        ) { [weak self] _ in
            self?.checkBeat()
        }
    }
    
    private func checkBeat() {
        let now = Date()
        let elapsed = now.timeIntervalSince(lastBeatTime)
        
        if elapsed >= beatInterval {
            onBeat()
            lastBeatTime = now
        }
    }
    
    private func onBeat() {
        DispatchQueue.main.async {
            self.currentBeat += 1
        }
        
        // 触覚フィードバック
        let generator = UIImpactFeedbackGenerator(style: .light)
        generator.impactOccurred()
    }
    
    // MARK: - Timing Check
    func checkMoveTiming() -> Bool {
        let now = Date()
        let timeDiff = abs(now.timeIntervalSince(lastBeatTime))
        let tolerance = beatInterval * timingTolerance
        
        return timeDiff <= tolerance
    }
    
    // MARK: - BPM Change
    func changeBPM(_ newBPM: Double) {
        stop()
        loadMusic(bpm: newBPM)
        play()
    }
    
    // MARK: - Cleanup
    deinit {
        stop()
    }
}
```

### GameViewModel.swift

```swift
import SwiftUI
import Combine

class GameViewModel: ObservableObject {
    // MARK: - Published Properties
    @Published var currentFloor: Int = 1
    @Published var turnCount: Int = 0
    @Published var playerPosition: Int = 1
    @Published var enemyPosition: Int = 9
    @Published var gameStatus: GameStatus = .playing
    @Published var skillUsageCount: Int = 0
    
    // MARK: - Dependencies
    private let beatEngine = BeatEngine()
    private let gameEngine = GameEngine()
    private let aiEngine = AIEngine()
    
    // MARK: - Constants
    private let maxTurns = 10
    private let maxSkillUsage = 5
    
    // MARK: - Combine
    private var cancellables = Set<AnyCancellable>()
    
    // MARK: - Initialization
    init() {
        setupBeatObserver()
    }
    
    // MARK: - Setup
    private func setupBeatObserver() {
        beatEngine.$currentBeat
            .sink { [weak self] beat in
                self?.onBeat(beat)
            }
            .store(in: &cancellables)
    }
    
    // MARK: - Game Control
    func startGame(aiLevel: AILevel) {
        currentFloor = 1
        turnCount = 0
        gameStatus = .playing
        skillUsageCount = 0
        
        // ランダム配置
        playerPosition = Int.random(in: 1...9)
        enemyPosition = Int.random(in: 1...9)
        
        // BPM設定
        let bpm = calculateBPM(floor: currentFloor)
        beatEngine.loadMusic(bpm: bpm)
        beatEngine.play()
    }
    
    func movePlayer(to position: Int) {
        // タイミングチェック
        guard beatEngine.checkMoveTiming() else {
            endGame(result: .lose)
            return
        }
        
        // 移動可能かチェック
        guard gameEngine.isValidMove(
            from: playerPosition,
            to: position
        ) else {
            return
        }
        
        // 移動実行
        playerPosition = position
        
        // 衝突チェック
        if playerPosition == enemyPosition {
            endGame(result: .lose)
            return
        }
        
        // ターン進行
        turnCount += 1
        
        // 敵の移動
        moveEnemy()
        
        // 10ターンで階層クリア
        if turnCount >= maxTurns {
            nextFloor()
        }
    }
    
    private func moveEnemy() {
        enemyPosition = aiEngine.calculateNextMove(
            from: enemyPosition,
            target: playerPosition,
            level: .normal
        )
        
        if enemyPosition == playerPosition {
            endGame(result: .lose)
        }
    }
    
    func nextFloor() {
        currentFloor += 1
        turnCount = 0
        skillUsageCount = 0
        
        // 100階層でクリア
        if currentFloor > 100 {
            endGame(result: .win)
            return
        }
        
        // BPM変更
        let newBPM = calculateBPM(floor: currentFloor)
        beatEngine.changeBPM(newBPM)
        
        // ランダム配置
        playerPosition = Int.random(in: 1...9)
        enemyPosition = Int.random(in: 1...9)
    }
    
    func endGame(result: GameStatus) {
        gameStatus = result
        beatEngine.stop()
        
        // スコア送信
        if result == .win || result == .lose {
            RankingService.shared.submitScore(floor: currentFloor)
        }
    }
    
    // MARK: - Beat Handler
    private func onBeat(_ beat: Int) {
        // ビートごとの処理(アニメーション等)
    }
    
    // MARK: - Helpers
    private func calculateBPM(floor: Int) -> Double {
        switch floor {
        case 1...10: return 60
        case 11...20: return 80
        case 21...30: return 100
        case 31...40: return 120
        case 41...50: return 140
        case 51...60: return 160
        case 61...70: return 180
        case 71...80: return 200
        case 81...90: return 220
        default: return 240
        }
    }
}

// MARK: - Game Status
enum GameStatus {
    case playing
    case paused
    case win
    case lose
}
```

---

## 実装優先順位

### Phase 1: プロジェクトセットアップ (1日)
- [ ] Xcodeプロジェクト作成
- [ ] Swift Package Manager でFirebase追加
- [ ] AdMob SDK追加
- [ ] Info.plist設定
- [ ] Assets.xcassets準備

### Phase 2: 音楽同期システム (3-4日) ⚠️ 最重要
- [ ] BeatEngine.swift実装
- [ ] AVAudioPlayer設定
- [ ] ビート検出ロジック
- [ ] タイミング判定システム
- [ ] テスト用BGM準備

### Phase 3: 基本ゲームロジック (3-4日)
- [ ] GameEngine.swift実装
- [ ] 9マス移動ロジック
- [ ] AIEngine.swift実装 (Easy/Normal/Hard)
- [ ] StageManager.swift実装
- [ ] 勝敗判定

### Phase 4: UI実装 (5-7日)
- [ ] GameView.swift (メイン画面)
- [ ] GridBoardView.swift (9マス盤面)
- [ ] BeatIndicatorView.swift (ビート表示)
- [ ] HomeView.swift
- [ ] ResultView.swift
- [ ] RankingView.swift
- [ ] ShopView.swift

### Phase 5: キャラクター・スキル (3-4日)
- [ ] Character.swift モデル
- [ ] SkillManager.swift
- [ ] 4種のスキル実装
- [ ] キャラクター切り替え

### Phase 6: 特殊ルール (2-3日)
- [ ] 霧マップ実装
- [ ] マス消失実装
- [ ] 階層別ルール適用

### Phase 7: 収益化 (3-4日)
- [ ] Firebase連携
- [ ] AdMob統合 (バナー・インタースティシャル)
- [ ] StoreKit課金実装
- [ ] Game Center連携

### Phase 8: テスト・調整 (1-2週間)
- [ ] バグ修正
- [ ] BPM調整
- [ ] UI/UX改善
- [ ] TestFlight配信
- [ ] ベータテスト

### Phase 9: リリース準備 (1週間)
- [ ] App Store申請資料
- [ ] スクリーンショット
- [ ] プロモ動画
- [ ] 審査申請

---

## 開発Tips

### Xcode便利機能
- **Cmd + B**: ビルド
- **Cmd + R**: 実行
- **Cmd + .**: 実行停止
- **Cmd + Shift + K**: クリーンビルド

### デバッグ
```swift
// Print文
print("Debug: \(variable)")

// Breakpoint設定
// エディタ左側をクリック

// LLDB
po variable
```

### SwiftUI Preview
```swift
#Preview {
    GameView()
        .environmentObject(GameViewModel())
}
```

---

## 次のステップ

1. **Xcodeプロジェクト作成**
2. **BeatEngine.swift実装** (最優先)
3. **基本的なGameView実装**
4. **テストプレイ**

---

**Escape Nine: Endless**
Swift Native版開発開始: 2025-11-13
開発者: Souatou

Let's Build! 🚀🎮
