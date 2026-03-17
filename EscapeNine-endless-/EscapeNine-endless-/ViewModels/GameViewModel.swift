//
//  GameViewModel.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI
import UIKit
import Combine

@MainActor
class GameViewModel: ObservableObject {
    // MARK: - Published Properties
    @Published var currentFloor: Int = 1
    @Published var turnCount: Int = 0
    @Published var playerPosition: Int = 1
    @Published var enemyPosition: Int = 9
    @Published var gameStatus: GameStatus = .idle
    @Published var skillUsageCount: Int = 0
    @Published var pendingPlayerMove: Int? = nil // 次のターンで移動する位置
    @Published var hasMovedThisTurn: Bool = false // このターンで移動先を選択したか
    @Published var specialRule: SpecialRule = .none // 現在の特殊ルール
    @Published var disappearedCells: Set<Int> = [] // 消失したマス
    @Published var showFloorClear: Bool = false // 階層クリア表示
    @Published var isInvisible: Bool = false // 透明化状態
    @Published var enemyStoppedTurns: Int = 0 // 敵が停止している残りターン数
    @Published var isSkillActive: Bool = false // スキルがアクティブか（ダッシュ、斜め移動用）
    @Published var showSkillReset: Bool = false // スキルリセット通知

    // MARK: - Countdown Properties
    @Published var turnCountdown: Int = Constants.turnCountdownBeats // ターンカウントダウン(3→2→1)
    @Published var gameStartCountdown: Int = 0 // ゲーム開始カウントダウン(3→2→1→0)
    @Published var isGameStartCountdownActive: Bool = false // ゲーム開始カウントダウン中か

    // MARK: - Defeat Reason
    @Published var defeatReason: DefeatReason? = nil

    // MARK: - Game Over Overlay
    @Published var showGameOverOverlay: Bool = false

    // MARK: - Shield (Knight skill)
    @Published var shieldActive: Bool = false

    // MARK: - Boss Floor
    @Published var showBossWarning: Bool = false

    // MARK: - Combo System
    @Published var comboCount: Int = 0
    @Published var lastTimingGrade: TimingGrade? = nil

    // MARK: - Daily Challenge
    var dailyChallengeMode: Bool = false
    var dailyChallengeConditions: [ChallengeCondition] = []

    // MARK: - Game Settings
    private var selectedAILevel: AILevel = .easy // プレイヤーが選択したAI難易度（全階層で固定、初心者向けにEasyをデフォルト）

    // MARK: - Dependencies
    private let audioManager = AudioManager.shared
    private let gameEngine = GameEngine.shared
    private let aiEngine = AIEngine.shared
    private let stageManager = StageManager.shared

    // MARK: - Constants
    private let maxTurns = Constants.maxTurns
    private let maxSkillUsage = Constants.maxSkillUsage

    // MARK: - Character & Skill
    private weak var playerViewModelInstance: PlayerViewModel?

    var currentCharacter: Character {
        guard let vm = playerViewModelInstance else {
            print("Warning: PlayerViewModel is not set yet. Using default hero character.")
            return Character.getCharacter(for: .hero) // デフォルト値
        }
        return Character.getCharacter(for: vm.selectedCharacter)
    }

    var currentSkill: Skill {
        currentCharacter.skill
    }

    var remainingSkillUses: Int {
        currentSkill.maxUsage - skillUsageCount
    }

    /// コンボによるスコア倍率
    var scoreMultiplier: Double {
        if comboCount >= Constants.comboMultiplierThreshold2 {
            return 2.0
        } else if comboCount >= Constants.comboMultiplierThreshold1 {
            return 1.5
        }
        return 1.0
    }

    /// 現在のフロアがボスフロアか
    var isBossFloor: Bool {
        Floor.isBossFloor(currentFloor)
    }

    func setPlayerViewModel(_ viewModel: PlayerViewModel) {
        playerViewModelInstance = viewModel
    }

    // MARK: - Game Start Countdown Timer
    private var gameStartCountdownTimer: Timer?

    // MARK: - Combine
    private var cancellables = Set<AnyCancellable>()

    // MARK: - Computed Properties
    var currentBeat: Int {
        audioManager.currentBeat
    }

    var isPlaying: Bool {
        audioManager.isPlaying
    }

    // MARK: - Initialization
    init() {
        setupObservers()
        setupTurnDeadlineCallback()
    }

    deinit {
        gameStartCountdownTimer?.invalidate()
        gameStartCountdownTimer = nil
    }

    // MARK: - Setup
    private func setupObservers() {
        // ターンカウントダウンをUIに反映
        audioManager.turnCountdownPublisher
            .receive(on: DispatchQueue.main)
            .sink { [weak self] remaining in
                self?.turnCountdown = remaining
            }
            .store(in: &cancellables)
    }

    private func setupTurnDeadlineCallback() {
        audioManager.onTurnDeadline = { [weak self] in
            Task { @MainActor in
                self?.onTurnDeadline()
            }
        }
    }

    // MARK: - Turn Deadline Handler
    private func onTurnDeadline() {
        guard gameStatus == .playing else { return }
        guard !isGameStartCountdownActive else { return }

        // 移動先が選択されていない → 時間切れゲームオーバー
        guard let nextPosition = pendingPlayerMove else {
            defeatReason = .timeOut
            showGameOverOverlay = true
            audioManager.pauseBGM()
            // 1.5秒後にゲーム終了
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) { [weak self] in
                self?.showGameOverOverlay = false
                self?.endGame(result: .lose)
            }
            return
        }

        // 移動可能かチェック（スキルを考慮）
        let (isValid, shouldConsume) = validateMove(from: playerPosition, to: nextPosition)

        guard isValid else {
            defeatReason = .caughtByEnemy
            showGameOverOverlay = true
            audioManager.pauseBGM()
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) { [weak self] in
                self?.showGameOverOverlay = false
                self?.endGame(result: .lose)
            }
            return
        }

        // スキル使用回数を消費（ダッシュ、斜め移動の場合）
        if shouldConsume && remainingSkillUses > 0 {
            skillUsageCount += 1
        }
        isSkillActive = false

        // 消失したマスに入っていないかチェック
        if disappearedCells.contains(nextPosition) {
            defeatReason = .caughtByEnemy
            showGameOverOverlay = true
            audioManager.pauseBGM()
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) { [weak self] in
                self?.showGameOverOverlay = false
                self?.endGame(result: .lose)
            }
            return
        }

        // 【重要】同時移動の実装
        // 鬼の次の位置を「プレイヤー移動前」に計算
        let previousPlayerPosition = playerPosition
        let previousEnemyPosition = enemyPosition
        var nextEnemyPosition = enemyPosition

        if enemyStoppedTurns > 0 {
            // 拘束中は敵を移動させず、残りターン数を減らす
            enemyStoppedTurns -= 1
        } else {
            // 階層に応じたAI難易度を使用
            let effectiveAI = Floor.getEffectiveAILevel(for: currentFloor, playerSelection: selectedAILevel)
            nextEnemyPosition = aiEngine.calculateNextMove(
                from: enemyPosition,
                target: playerPosition,
                level: effectiveAI
            )
        }

        // 両方の位置を同時に更新
        playerPosition = nextPosition
        enemyPosition = nextEnemyPosition
        pendingPlayerMove = nil
        hasMovedThisTurn = false

        // 移動効果音
        audioManager.playSoundEffect(.move)

        // すれ違い判定（プレイヤーと鬼が位置を入れ替えた場合）
        let isCrossing = (previousPlayerPosition == nextEnemyPosition && previousEnemyPosition == nextPosition)

        // 衝突チェック（透明化スキルを考慮）
        let isCollision = (playerPosition == enemyPosition) || isCrossing

        if isCollision {
            if currentSkill.type == .invisible && remainingSkillUses > 0 {
                // 透明化スキル: 常時有効、衝突時に自動消費
                skillUsageCount += 1
                // 視覚効果用にフラグを一時的にON
                isInvisible = true
                DispatchQueue.main.asyncAfter(deadline: .now() + Constants.invisibilityDuration) { [weak self] in
                    self?.isInvisible = false
                }
            } else if currentSkill.type == .shield && shieldActive {
                // 盾ガード: シールドが有効な間の衝突を1回無効化
                shieldActive = false
                skillUsageCount += 1
                comboCount = 0 // 衝突でコンボリセット
            } else {
                defeatReason = .caughtByEnemy
                showGameOverOverlay = true
                audioManager.pauseBGM()
                DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) { [weak self] in
                    self?.showGameOverOverlay = false
                    self?.endGame(result: .lose)
                }
                return
            }
        }

        // ターン進行
        turnCount += 1

        // 10ターンで階層クリア
        if turnCount >= maxTurns {
            // 階層クリア表示
            showFloorClear = true
            audioManager.pauseBGM()

            // フロアクリア効果音
            audioManager.playSoundEffect(.floorClear)
        }
    }

    // MARK: - Skill Validation Helpers
    /// スキルを考慮した移動の有効性をチェック
    /// - Returns: (isValid: 移動が有効か, shouldConsumeSkill: スキルを消費すべきか)
    private func validateMove(from: Int, to: Int) -> (isValid: Bool, shouldConsumeSkill: Bool) {
        var isValid = false
        var shouldConsumeSkill = false

        if isSkillActive {
            // スキルがアクティブな場合
            switch currentSkill.type {
            case .dash:
                isValid = gameEngine.isValidDashMove(from: from, to: to)
                shouldConsumeSkill = isValid
            case .diagonal:
                let isDiagonal = gameEngine.isValidDiagonalMove(from: from, to: to)
                let isNormal = gameEngine.isValidMove(from: from, to: to)
                isValid = isDiagonal || isNormal
                shouldConsumeSkill = isDiagonal
            default:
                isValid = gameEngine.isValidMove(from: from, to: to)
            }
        } else if currentSkill.type == .diagonal && remainingSkillUses > 0 {
            // 盗賊の斜め移動は常時有効（スキル残数がある場合）
            let isDiagonal = gameEngine.isValidDiagonalMove(from: from, to: to)
            let isNormal = gameEngine.isValidMove(from: from, to: to)
            isValid = isDiagonal || isNormal
            shouldConsumeSkill = isDiagonal
        } else if currentSkill.type == .diagonal {
            // スキル切れの場合は通常移動のみ
            isValid = gameEngine.isValidMove(from: from, to: to)
        } else {
            isValid = gameEngine.isValidMove(from: from, to: to)
        }

        return (isValid, shouldConsumeSkill)
    }

    // MARK: - Game Start Countdown (独立タイマー、1秒間隔固定)
    private func startGameStartCountdown(completion: @escaping () -> Void) {
        #if DEBUG
        // デバッグ: カウントダウンスキップ
        if playerViewModelInstance?.debugSkipStartCountdown == true {
            isGameStartCountdownActive = false
            gameStartCountdown = 0
            completion()
            return
        }
        #endif

        isGameStartCountdownActive = true
        gameStartCountdown = Constants.gameStartCountdownBeats

        gameStartCountdownTimer?.invalidate()
        gameStartCountdownTimer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { [weak self] timer in
            guard let self = self else {
                timer.invalidate()
                return
            }
            DispatchQueue.main.async {
                self.gameStartCountdown -= 1
                // Haptic feedback
                let generator = UIImpactFeedbackGenerator(style: .heavy)
                generator.impactOccurred()

                if self.gameStartCountdown <= 0 {
                    timer.invalidate()
                    self.gameStartCountdownTimer = nil
                    // GO! 表示を0.5秒後に消す
                    DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) { [weak self] in
                        self?.isGameStartCountdownActive = false
                    }
                    completion()
                }
            }
        }
    }

    // MARK: - Game Control
    func startGame(aiLevel: AILevel) {
        // デバッグ用の開始階層を使用（設定されている場合）
        #if DEBUG
        let startFloor = playerViewModelInstance?.debugStartFloor ?? 1
        #else
        let startFloor = 1
        #endif
        currentFloor = max(1, min(startFloor, Constants.maxFloors))

        // AI難易度を保存（全階層で固定）
        // デバッグ用のAI難易度を使用（設定されている場合）
        #if DEBUG
        selectedAILevel = playerViewModelInstance?.debugAILevel ?? aiLevel
        #else
        selectedAILevel = aiLevel
        #endif

        turnCount = 0
        gameStatus = .playing
        skillUsageCount = 0
        hasMovedThisTurn = false
        isInvisible = false
        enemyStoppedTurns = 0
        isSkillActive = false
        defeatReason = nil
        showGameOverOverlay = false
        shieldActive = false
        comboCount = 0
        lastTimingGrade = nil
        showBossWarning = false

        // ターンカウントダウンを初期化
        turnCountdown = Constants.turnCountdownBeats

        // ランダム配置（同じ位置にならないように）
        let playerPos = Int.random(in: 1...9)
        var enemyPos = Int.random(in: 1...9)
        while playerPos == enemyPos {
            enemyPos = Int.random(in: 1...9)
        }
        playerPosition = playerPos
        enemyPosition = enemyPos

        // 初回ターンも移動必須（pendingPlayerMoveはnil）
        pendingPlayerMove = nil

        // デイリーチャレンジ：pending challenge があれば適用
        if let pending = DailyChallengeService.shared.pendingChallenge {
            DailyChallengeService.shared.pendingChallenge = nil
            setupDailyChallenge(pending)
            applyDailyChallengeConditions()
        } else if dailyChallengeMode {
            applyDailyChallengeConditions()
        }

        // ゲームスタート効果音
        audioManager.playSoundEffect(.gameStart)

        // 階層に応じたBGM再生
        audioManager.playBGMMusic(.forFloor(currentFloor))

        // BPM設定
        var bpm = stageManager.getBPM(for: currentFloor)
        #if DEBUG
        if let overrideBPM = playerViewModelInstance?.debugBPMOverride, overrideBPM > 0 {
            bpm = overrideBPM
        }
        // デバッグ用ターンカウントダウンビート数を適用
        let debugBeats = playerViewModelInstance?.debugTurnCountdownBeats ?? Constants.turnCountdownBeats
        audioManager.setTurnCountdownBeats(debugBeats)
        turnCountdown = debugBeats
        #endif

        // 特殊ルール設定
        specialRule = stageManager.getSpecialRule(for: currentFloor)
        updateDisappearedCells()

        // ボスフロア警告
        if Floor.isBossFloor(currentFloor) {
            showBossWarning = true
            DispatchQueue.main.asyncAfter(deadline: .now() + 2.0) { [weak self] in
                self?.showBossWarning = false
            }
        }

        // ゲーム開始カウントダウン → 完了後にBeatEngine開始
        startGameStartCountdown { [weak self] in
            guard let self = self else { return }
            self.audioManager.resetTurnCountdown()
            self.audioManager.startBGM(bpm: bpm)
        }
    }

    // 移動先を指定（次のターンで移動）
    func selectMove(to position: Int) {
        guard gameStatus == .playing else { return }
        guard !isGameStartCountdownActive else { return }

        // 移動可能な位置を取得（スキルを考慮）
        let availableMoves = getAvailableMoves()

        // 移動可能かチェック
        guard availableMoves.contains(position) else {
            return
        }

        // 消失したマスに入っていないかチェック
        guard !disappearedCells.contains(position) else {
            return
        }

        // 次のターンで移動する位置を設定
        pendingPlayerMove = position
        hasMovedThisTurn = true

        // コンボシステム：タイミング判定
        let grade = audioManager.checkMoveTimingGrade()
        lastTimingGrade = grade
        if grade == .just || grade == .good {
            comboCount += 1
        } else {
            comboCount = 0
        }
        // タイミング表示を一定時間後にリセット
        Task { @MainActor [weak self] in
            try? await Task.sleep(for: .seconds(0.8))
            self?.lastTimingGrade = nil
        }
    }

    // 移動可能な位置を取得（現在位置を除外 = 必須移動）
    func getAvailableMoves() -> [Int] {
        var moves: [Int] = []

        // 基本移動（現在位置は含めない = 必須移動）
        moves.append(contentsOf: gameEngine.getAvailableMoves(from: playerPosition))

        // スキルによる追加移動
        if isSkillActive {
            switch currentSkill.type {
            case .dash:
                // ダッシュ: 2マス移動
                let dashMoves = getDashMoves(from: playerPosition)
                moves.append(contentsOf: dashMoves)
            case .diagonal:
                // 斜め移動
                let diagonalMoves = getDiagonalMoves(from: playerPosition)
                moves.append(contentsOf: diagonalMoves)
            default:
                break
            }
        } else if currentSkill.type == .diagonal && remainingSkillUses > 0 {
            // 盗賊の斜め移動は常時有効
            let diagonalMoves = getDiagonalMoves(from: playerPosition)
            moves.append(contentsOf: diagonalMoves)
        }

        // 消失したマスを除外、現在位置を除外
        return Array(Set(moves)).filter { !disappearedCells.contains($0) && $0 != playerPosition }
    }

    private func getDashMoves(from position: Int) -> [Int] {
        var moves: [Int] = []
        let row = Constants.rowFromPosition(position)
        let col = Constants.columnFromPosition(position)

        // 上2マス
        if row >= 2 {
            moves.append(Constants.positionFromRowColumn(row: row - 2, column: col))
        }
        // 下2マス
        if row <= Constants.gridRows - 3 {
            moves.append(Constants.positionFromRowColumn(row: row + 2, column: col))
        }
        // 左2マス
        if col >= 2 {
            moves.append(Constants.positionFromRowColumn(row: row, column: col - 2))
        }
        // 右2マス
        if col <= Constants.gridColumns - 3 {
            moves.append(Constants.positionFromRowColumn(row: row, column: col + 2))
        }

        return moves
    }

    private func getDiagonalMoves(from position: Int) -> [Int] {
        var moves: [Int] = []
        let row = Constants.rowFromPosition(position)
        let col = Constants.columnFromPosition(position)

        // 左上
        if row > 0 && col > 0 {
            moves.append(Constants.positionFromRowColumn(row: row - 1, column: col - 1))
        }
        // 右上
        if row > 0 && col < Constants.gridColumns - 1 {
            moves.append(Constants.positionFromRowColumn(row: row - 1, column: col + 1))
        }
        // 左下
        if row < Constants.gridRows - 1 && col > 0 {
            moves.append(Constants.positionFromRowColumn(row: row + 1, column: col - 1))
        }
        // 右下
        if row < Constants.gridRows - 1 && col < Constants.gridColumns - 1 {
            moves.append(Constants.positionFromRowColumn(row: row + 1, column: col + 1))
        }

        return moves
    }

    // MARK: - Skill Actions
    func activateSkill() {
        guard gameStatus == .playing else { return }
        guard remainingSkillUses > 0 else { return }

        // スキル効果音
        audioManager.playSoundEffect(.skill)

        switch currentSkill.type {
        case .dash:
            // ダッシュ: 次の移動で2マス移動可能
            isSkillActive = true
        case .diagonal:
            // 斜め移動: 常時有効なので何もしない
            break
        case .invisible:
            // 透明化: 衝突時に自動発動するため、手動アクティベーション不要
            break
        case .bind:
            // 拘束: 敵を停止させる（敵をタップした時に発動）
            break
        case .shield:
            // 盾ガード: シールドをアクティブにする
            shieldActive = true
            comboCount = 0 // スキル使用でコンボリセット
        }
    }

    func bindEnemy() {
        guard gameStatus == .playing else { return }
        guard currentSkill.type == .bind else { return }
        guard remainingSkillUses > 0 else { return }

        // スキル効果音
        audioManager.playSoundEffect(.skill)

        enemyStoppedTurns = Constants.bindDurationTurns
        skillUsageCount += 1
    }

    func nextFloor() {
        // クリア表示を非表示
        showFloorClear = false

        currentFloor += 1
        turnCount = 0

        // 10階層ごとにスキル使用回数をリセット
        if currentFloor % Constants.skillResetInterval == 1 {
            skillUsageCount = 0
            showSkillReset = true
            Task { @MainActor [weak self] in
                try? await Task.sleep(for: .seconds(3))
                self?.showSkillReset = false
            }
        }

        pendingPlayerMove = nil
        hasMovedThisTurn = false
        isInvisible = false
        enemyStoppedTurns = 0
        isSkillActive = false
        defeatReason = nil
        showGameOverOverlay = false
        shieldActive = false
        lastTimingGrade = nil
        showBossWarning = false

        // 100階層でクリア
        if currentFloor > Constants.maxFloors {
            endGame(result: .win)
            return
        }

        // 階層に応じたBGM切り替え（範囲が変わった場合のみ）
        let nextBGMType = AudioManager.BGMType.forFloor(currentFloor)
        if audioManager.currentBGMType != nextBGMType {
            audioManager.playBGMMusic(nextBGMType)
        }

        // BPM設定
        var newBPM = stageManager.getBPM(for: currentFloor)
        #if DEBUG
        if let overrideBPM = playerViewModelInstance?.debugBPMOverride, overrideBPM > 0 {
            newBPM = overrideBPM
        }
        let debugBeats = playerViewModelInstance?.debugTurnCountdownBeats ?? Constants.turnCountdownBeats
        audioManager.setTurnCountdownBeats(debugBeats)
        turnCountdown = debugBeats
        #endif

        // 特殊ルール設定
        specialRule = stageManager.getSpecialRule(for: currentFloor)
        updateDisappearedCells()

        // ランダム配置（消失したマスを避ける）
        let availablePositions = Array(1...9).filter { !disappearedCells.contains($0) }
        guard availablePositions.count >= 2 else {
            // 消失したマスが多すぎる場合のフォールバック
            playerPosition = 1
            enemyPosition = 9
            return
        }

        guard let playerPos = availablePositions.randomElement() else { return }
        var enemyPos = availablePositions.filter { $0 != playerPos }.randomElement() ?? (playerPos == 1 ? 9 : 1)
        playerPosition = playerPos
        enemyPosition = enemyPos

        // 初回ターンも移動必須
        pendingPlayerMove = nil

        // BGMを停止（カウントダウン後に再開）
        audioManager.stopBGM()

        // ボスフロア警告を表示してからカウントダウン開始
        if Floor.isBossFloor(currentFloor) {
            showBossWarning = true
            DispatchQueue.main.asyncAfter(deadline: .now() + 2.0) { [weak self] in
                self?.showBossWarning = false
            }
        }

        // ゲーム開始カウントダウン → 完了後にBeatEngine開始
        startGameStartCountdown { [weak self] in
            guard let self = self else { return }
            self.audioManager.resetTurnCountdown()
            self.audioManager.startBGM(bpm: newBPM)
        }
    }

    // MARK: - Special Rules
    private func updateDisappearedCells() {
        if specialRule == .disappear || specialRule == .fogDisappear {
            // 難易度に応じて消失マスの数を変更
            let numberOfDisappearingCells = getNumberOfDisappearingCells(for: currentFloor)
            let allCells = Set(1...Constants.gridSize)
            let availableCells = allCells.subtracting([playerPosition, enemyPosition])

            var disappeared: Set<Int> = []
            let cellsToDisappear = min(numberOfDisappearingCells, availableCells.count)

            // ランダムで指定数のマスを消失
            var remainingCells = Array(availableCells)
            for _ in 0..<cellsToDisappear {
                if let cell = remainingCells.randomElement() {
                    disappeared.insert(cell)
                    remainingCells.removeAll { $0 == cell }
                }
            }

            disappearedCells = disappeared
        } else {
            disappearedCells = []
        }
    }

    /// 階層に応じた消失マスの数を計算（段階的スケーリング）
    private func getNumberOfDisappearingCells(for floor: Int) -> Int {
        for stage in Constants.disappearCellStages {
            if floor >= stage.floor {
                return stage.count
            }
        }
        return 1
    }

    // 霧マップ: プレイヤーから見えるマスかどうか
    func isCellVisible(_ position: Int) -> Bool {
        // 消失マスは常に見える（プレイヤーが避けられるように）
        if disappearedCells.contains(position) {
            return true
        }

        if specialRule == .fog || specialRule == .fogDisappear {
            // 自分の位置と隣接するマスのみ見える
            let playerRow = Constants.rowFromPosition(playerPosition)
            let playerCol = Constants.columnFromPosition(playerPosition)
            let cellRow = Constants.rowFromPosition(position)
            let cellCol = Constants.columnFromPosition(position)

            let rowDiff = abs(cellRow - playerRow)
            let colDiff = abs(cellCol - playerCol)

            // 自分の位置または隣接するマス（上下左右斜め）
            return (rowDiff <= 1 && colDiff <= 1)
        }
        return true // 霧がない場合は全て見える
    }

    // マスが消失しているか
    func isCellDisappeared(_ position: Int) -> Bool {
        return disappearedCells.contains(position)
    }

    func endGame(result: GameStatus) {
        gameStatus = result
        audioManager.stopBGM()
        audioManager.stopBGMMusic()

        // リザルトBGM再生
        if result == .win {
            audioManager.playBGMMusic(.clear)
        } else if result == .lose {
            audioManager.playSoundEffect(.gameOver)
            audioManager.playBGMMusic(.gameOver)
        }

        // デイリーチャレンジ完了を記録（勝利時のみ）
        if result == .win && dailyChallengeMode {
            DailyChallengeService.shared.markCompleted(achievedFloor: currentFloor)
            dailyChallengeMode = false
        }

        // スコア送信（ローカル + Game Center）
        if result == .win || result == .lose {
            RankingService.shared.submitScore(
                floor: currentFloor,
                characterType: currentCharacter.type.rawValue
            )
            Task {
                await GameCenterService.shared.submitScore(floor: currentFloor)
            }
        }

        // 実績チェック（勝利時のみ）
        if result == .win {
            let skillUsed = skillUsageCount > 0
            let currentBPM = stageManager.getBPM(for: currentFloor)
            AchievementManager.shared.checkAchievements(
                floor: currentFloor,
                skillUsed: skillUsed,
                currentBPM: currentBPM,
                gameWon: true
            )
        }
    }

    func pauseGame() {
        gameStatus = .paused
        audioManager.pauseBGM()
        audioManager.pauseBGMMusic()
    }

    func resumeGame() {
        gameStatus = .playing
        audioManager.resumeBGM()
        audioManager.resumeBGMMusic()
    }

    func resetGame() {
        gameStartCountdownTimer?.invalidate()
        gameStartCountdownTimer = nil
        audioManager.stopBGM()
        audioManager.stopBGMMusic()
        currentFloor = 1
        turnCount = 0
        playerPosition = 1
        enemyPosition = 9
        gameStatus = .idle  // リセット後はidle状態（startGameで.playingに変更）
        skillUsageCount = 0
        pendingPlayerMove = nil
        hasMovedThisTurn = false
        specialRule = .none
        disappearedCells = []
        showFloorClear = false
        showSkillReset = false
        isInvisible = false
        enemyStoppedTurns = 0
        isSkillActive = false
        defeatReason = nil
        showGameOverOverlay = false
        shieldActive = false
        comboCount = 0
        lastTimingGrade = nil
        showBossWarning = false
        dailyChallengeMode = false
        dailyChallengeConditions = []
        turnCountdown = Constants.turnCountdownBeats
        gameStartCountdown = 0
        isGameStartCountdownActive = false
    }

    // MARK: - Daily Challenge

    /// デイリーチャレンジモードでゲームを設定する
    func setupDailyChallenge(_ challenge: DailyChallenge) {
        dailyChallengeMode = true
        dailyChallengeConditions = challenge.conditions
    }

    private func applyDailyChallengeConditions() {
        for condition in dailyChallengeConditions {
            switch condition {
            case .characterLock:
                break // キャラクターはGameView側でロック済み
            case .noSkillAllowed:
                skillUsageCount = currentSkill.maxUsage // スキルを使い切り状態に
            case .forcedAI(let level):
                selectedAILevel = level
            case .startFloor(let floor):
                currentFloor = max(1, min(floor, Constants.maxFloors))
            }
        }
    }
}
