//
//  GameViewModel.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI
import Combine

class GameViewModel: ObservableObject {
    // MARK: - Published Properties
    @Published var currentFloor: Int = 1
    @Published var turnCount: Int = 0
    @Published var playerPosition: Int = 1
    @Published var enemyPosition: Int = 9
    @Published var gameStatus: GameStatus = .idle
    @Published var skillUsageCount: Int = 0
    @Published var pendingPlayerMove: Int? = nil // 次のビートで移動する位置
    @Published var hasMovedThisBeat: Bool = false // このビートで移動したか
    @Published var specialRule: SpecialRule = .none // 現在の特殊ルール
    @Published var disappearedCells: Set<Int> = [] // 消失したマス
    @Published var showFloorClear: Bool = false // 階層クリア表示
    @Published var isInvisible: Bool = false // 透明化状態
    @Published var enemyStopped: Bool = false // 敵が停止しているか
    @Published var isSkillActive: Bool = false // スキルがアクティブか（ダッシュ、斜め移動用）
    @Published var consecutiveWaits: Int = 0 // 連続待機回数
    @Published var showSkillReset: Bool = false // スキルリセット通知

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
        let vm = playerViewModelInstance ?? PlayerViewModel()
        return Character.getCharacter(for: vm.selectedCharacter)
    }
    
    var currentSkill: Skill {
        currentCharacter.skill
    }
    
    var remainingSkillUses: Int {
        currentSkill.maxUsage - skillUsageCount
    }
    
    func setPlayerViewModel(_ viewModel: PlayerViewModel) {
        playerViewModelInstance = viewModel
    }
    
    // MARK: - Combine
    private var cancellables = Set<AnyCancellable>()
    private var lastProcessedBeat: Int = 0
    
    // MARK: - Computed Properties
    var currentBeat: Int {
        audioManager.currentBeat
    }
    
    var isPlaying: Bool {
        audioManager.isPlaying
    }
    
    // MARK: - Initialization
    init() {
        setupBeatObserver()
    }
    
    // MARK: - Setup
    private func setupBeatObserver() {
        audioManager.beatPublisher
            .sink { [weak self] beat in
                self?.onBeat(beat)
            }
            .store(in: &cancellables)
    }
    
    // MARK: - Beat Handler
    private func onBeat(_ beat: Int) {
        guard gameStatus == .playing else { return }
        guard beat > lastProcessedBeat else { return }
        lastProcessedBeat = beat

        // 新しいビートの開始時にリセット
        hasMovedThisBeat = false

        // プレイヤーが移動先を指定していない場合、ゲームオーバー
        guard let nextPosition = pendingPlayerMove else {
            endGame(result: .lose)
            return
        }

        // 移動可能かチェック（スキルを考慮）
        let (isValid, shouldConsume) = validateMove(from: playerPosition, to: nextPosition)

        guard isValid else {
            endGame(result: .lose)
            return
        }

        // スキル使用回数を消費（ダッシュ、斜め移動の場合）
        if shouldConsume && remainingSkillUses > 0 {
            skillUsageCount += 1
        }
        isSkillActive = false
        
        // 消失したマスに入っていないかチェック
        if disappearedCells.contains(nextPosition) {
            endGame(result: .lose)
            return
        }

        // 【重要】同時移動の実装
        // 鬼の次の位置を「プレイヤー移動前」に計算
        let previousPlayerPosition = playerPosition
        let previousEnemyPosition = enemyPosition
        var nextEnemyPosition = enemyPosition

        if !enemyStopped {
            // 選択されたAI難易度を使用（全階層で固定）
            nextEnemyPosition = aiEngine.calculateNextMove(
                from: enemyPosition,
                target: playerPosition, // プレイヤーの「現在」の位置を使用
                level: selectedAILevel
            )
        } else {
            // 拘束中は敵を移動させない
            enemyStopped = false
        }

        // 待機判定（連続待機カウンター更新）
        if nextPosition == previousPlayerPosition {
            consecutiveWaits += 1
        } else {
            consecutiveWaits = 0
        }

        // 両方の位置を同時に更新
        playerPosition = nextPosition
        enemyPosition = nextEnemyPosition
        pendingPlayerMove = nil
        hasMovedThisBeat = true
        
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
            } else {
                endGame(result: .lose)
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

        // hasMovedThisBeatはtrueのまま維持（次のビートでリセット）
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
    
    // MARK: - Game Control
    func startGame(aiLevel: AILevel) {
        // デバッグ用の開始階層を使用（設定されている場合）
        let startFloor = playerViewModelInstance?.debugStartFloor ?? 1
        currentFloor = max(1, min(startFloor, Constants.maxFloors))
        
        // AI難易度を保存（全階層で固定）
        // デバッグ用のAI難易度を使用（設定されている場合）
        selectedAILevel = playerViewModelInstance?.debugAILevel ?? aiLevel

        turnCount = 0
        gameStatus = .playing
        skillUsageCount = 0
        hasMovedThisBeat = false
        lastProcessedBeat = 0
        isInvisible = false
        enemyStopped = false
        isSkillActive = false
        consecutiveWaits = 0

        // ランダム配置（同じ位置にならないように）
        let playerPos = Int.random(in: 1...9)
        var enemyPos = Int.random(in: 1...9)
        while playerPos == enemyPos {
            enemyPos = Int.random(in: 1...9)
        }
        playerPosition = playerPos
        enemyPosition = enemyPos

        // 初期位置を設定（最初のビートで即ゲームオーバーにならないように）
        pendingPlayerMove = playerPosition
        
        // ゲームスタート効果音
        audioManager.playSoundEffect(.gameStart)
        
        // BPM設定
        let bpm = stageManager.getBPM(for: currentFloor)
        audioManager.startBGM(bpm: bpm)
        
        // 特殊ルール設定
        specialRule = stageManager.getSpecialRule(for: currentFloor)
        updateDisappearedCells()
    }
    
    // 移動先を指定（次のビートで移動）
    func selectMove(to position: Int) {
        guard gameStatus == .playing else { return }
        // hasMovedThisBeatチェックを削除：プレイヤーは次のビートまで何度でも移動先を変更できる

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

        // 次のビートで移動する位置を設定
        pendingPlayerMove = position
    }
    
    // 移動可能な位置を取得
    func getAvailableMoves() -> [Int] {
        var moves: [Int] = []

        // 待機オプション（連続待機が上限未満の場合のみ）
        if consecutiveWaits < Constants.maxConsecutiveWaits {
            moves.append(playerPosition)
        }

        // 基本移動
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

        // 消失したマスを除外
        return Array(Set(moves)).filter { !disappearedCells.contains($0) }
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
            // 透明化: アクティブにする
            isInvisible = true
        case .bind:
            // 拘束: 敵を停止させる（敵をタップした時に発動）
            break
        }
    }
    
    func bindEnemy() {
        guard gameStatus == .playing else { return }
        guard currentSkill.type == .bind else { return }
        guard remainingSkillUses > 0 else { return }
        
        // スキル効果音
        audioManager.playSoundEffect(.skill)
        
        enemyStopped = true
        skillUsageCount += 1
    }
    
    private func moveEnemy() {
        guard gameStatus == .playing else { return }

        // 選択されたAI難易度を使用（全階層で固定）
        enemyPosition = aiEngine.calculateNextMove(
            from: enemyPosition,
            target: playerPosition,
            level: selectedAILevel
        )
        
        if enemyPosition == playerPosition {
            endGame(result: .lose)
        }
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
            // 3秒後に通知を非表示
            DispatchQueue.main.asyncAfter(deadline: .now() + 3.0) { [weak self] in
                self?.showSkillReset = false
            }
        }

        pendingPlayerMove = nil
        hasMovedThisBeat = false
        isInvisible = false
        enemyStopped = false
        isSkillActive = false
        consecutiveWaits = 0  // 連続待機カウンターをリセット
        
        // 100階層でクリア
        if currentFloor > Constants.maxFloors {
            endGame(result: .win)
            return
        }
        
        // BPM変更
        let newBPM = stageManager.getBPM(for: currentFloor)
        audioManager.changeBPM(newBPM)
        
        // 特殊ルール設定
        specialRule = stageManager.getSpecialRule(for: currentFloor)
        updateDisappearedCells()
        
        // ランダム配置（消失したマスを避ける）
        let availablePositions = Array(1...9).filter { !disappearedCells.contains($0) }
        guard availablePositions.count >= 2 else {
            // 消失したマスが多すぎる場合のフォールバック
            playerPosition = 1
            enemyPosition = 9
            pendingPlayerMove = playerPosition
            return
        }

        let playerPos = availablePositions.randomElement()!
        var enemyPos = availablePositions.randomElement()!
        while enemyPos == playerPos {
            enemyPos = availablePositions.randomElement()!
        }
        playerPosition = playerPos
        enemyPosition = enemyPos

        // 初期位置を設定（次の階層開始時に即ゲームオーバーにならないように）
        pendingPlayerMove = playerPosition

        // 次の階層の準備ができたら再開
        audioManager.resumeBGM()
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

    /// 階層に応じた消失マスの数を計算（最大2マス）
    private func getNumberOfDisappearingCells(for floor: Int) -> Int {
        switch floor {
        case Constants.disappearStartFloor..<70:
            return 1 // 階層41-69: 1マス消失
        case 70...:
            return 2 // 階層70+: 2マス消失（上限）
        default:
            return 1
        }
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
        
        // ゲームオーバー効果音
        if result == .lose {
            audioManager.playSoundEffect(.gameOver)
        }
        
        // スコア送信
        if result == .win || result == .lose {
            RankingService.shared.submitScore(floor: currentFloor)
        }
    }
    
    func pauseGame() {
        gameStatus = .paused
        audioManager.pauseBGM()
    }
    
    func resumeGame() {
        gameStatus = .playing
        audioManager.resumeBGM()
    }
    
    func resetGame() {
        audioManager.stopBGM()
        currentFloor = 1
        turnCount = 0
        playerPosition = 1
        enemyPosition = 9
        gameStatus = .idle  // リセット後はidle状態（startGameで.playingに変更）
        skillUsageCount = 0
        pendingPlayerMove = nil
        hasMovedThisBeat = false
        lastProcessedBeat = 0
        specialRule = .none
        disappearedCells = []
        showFloorClear = false
        showSkillReset = false
        isInvisible = false
        enemyStopped = false
        isSkillActive = false
        consecutiveWaits = 0
    }
}

