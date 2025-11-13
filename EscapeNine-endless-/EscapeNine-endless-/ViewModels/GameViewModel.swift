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
    @Published var gameStatus: GameStatus = .playing
    @Published var skillUsageCount: Int = 0
    @Published var pendingPlayerMove: Int? = nil // 次のビートで移動する位置
    @Published var hasMovedThisBeat: Bool = false // このビートで移動したか
    @Published var specialRule: SpecialRule = .none // 現在の特殊ルール
    @Published var disappearedCells: Set<Int> = [] // 消失したマス
    @Published var showFloorClear: Bool = false // 階層クリア表示
    @Published var isInvisible: Bool = false // 透明化状態
    @Published var enemyStopped: Bool = false // 敵が停止しているか
    @Published var isSkillActive: Bool = false // スキルがアクティブか（ダッシュ、斜め移動用）
    
    // MARK: - Dependencies
    private let beatEngine = BeatEngine()
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
        beatEngine.currentBeat
    }
    
    var isPlaying: Bool {
        beatEngine.isPlaying
    }
    
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
    
    // MARK: - Beat Handler
    private func onBeat(_ beat: Int) {
        guard gameStatus == .playing else { return }
        guard beat > lastProcessedBeat else { return }
        lastProcessedBeat = beat
        
        // プレイヤーが移動先を指定していない場合、ゲームオーバー
        guard let nextPosition = pendingPlayerMove else {
            endGame(result: .lose)
            return
        }
        
        // 移動可能かチェック（スキルを考慮）
        var isValid = false
        var shouldConsumeSkill = false
        
        if isSkillActive {
            // スキルがアクティブな場合
            switch currentSkill.type {
            case .dash:
                isValid = gameEngine.isValidDashMove(from: playerPosition, to: nextPosition)
                shouldConsumeSkill = isValid
            case .diagonal:
                isValid = gameEngine.isValidDiagonalMove(from: playerPosition, to: nextPosition) || gameEngine.isValidMove(from: playerPosition, to: nextPosition)
                shouldConsumeSkill = gameEngine.isValidDiagonalMove(from: playerPosition, to: nextPosition)
            default:
                isValid = gameEngine.isValidMove(from: playerPosition, to: nextPosition)
            }
        } else if currentSkill.type == .diagonal {
            // 盗賊の斜め移動は常時有効
            isValid = gameEngine.isValidDiagonalMove(from: playerPosition, to: nextPosition) || gameEngine.isValidMove(from: playerPosition, to: nextPosition)
            shouldConsumeSkill = gameEngine.isValidDiagonalMove(from: playerPosition, to: nextPosition)
        } else {
            isValid = gameEngine.isValidMove(from: playerPosition, to: nextPosition)
        }
        
        guard isValid else {
            endGame(result: .lose)
            return
        }
        
        // スキル使用回数を消費（ダッシュ、斜め移動の場合）
        if shouldConsumeSkill && remainingSkillUses > 0 {
            skillUsageCount += 1
        }
        isSkillActive = false
        
        // 消失したマスに入っていないかチェック
        if disappearedCells.contains(nextPosition) {
            endGame(result: .lose)
            return
        }
        
        // プレイヤーと鬼を同時に移動
        playerPosition = nextPosition
        pendingPlayerMove = nil
        hasMovedThisBeat = true
        
        // 敵の移動（拘束スキルを考慮）
        if !enemyStopped {
            moveEnemy()
        } else {
            // 拘束中は敵を移動させない
            enemyStopped = false
        }
        
        // 衝突チェック（透明化スキルを考慮）
        if playerPosition == enemyPosition {
            if currentSkill.type == .invisible && isInvisible && remainingSkillUses > 0 {
                // 透明化スキルで無敵
                skillUsageCount += 1
                // 次のビートで透明化を解除
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.1) {
                    self.isInvisible = false
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
            beatEngine.pause()
        }
        
        // 次のビートの準備
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.1) {
            self.hasMovedThisBeat = false
        }
    }
    
    // MARK: - Game Control
    func startGame(aiLevel: AILevel) {
        currentFloor = 1
        turnCount = 0
        gameStatus = .playing
        skillUsageCount = 0
        pendingPlayerMove = nil
        hasMovedThisBeat = false
        lastProcessedBeat = 0
        isInvisible = false
        enemyStopped = false
        isSkillActive = false
        
        // ランダム配置（同じ位置にならないように）
        let playerPos = Int.random(in: 1...9)
        var enemyPos = Int.random(in: 1...9)
        while playerPos == enemyPos {
            enemyPos = Int.random(in: 1...9)
        }
        playerPosition = playerPos
        enemyPosition = enemyPos
        
        // BPM設定
        let bpm = stageManager.getBPM(for: currentFloor)
        let bgmVolume = playerViewModelInstance?.bgmVolume ?? 0.7
        beatEngine.loadMusic(bpm: bpm, volume: bgmVolume)
        beatEngine.play()
        
        // 特殊ルール設定
        specialRule = stageManager.getSpecialRule(for: currentFloor)
        updateDisappearedCells()
    }
    
    // 移動先を指定（次のビートで移動）
    func selectMove(to position: Int) {
        guard gameStatus == .playing else { return }
        guard !hasMovedThisBeat else { return } // 既にこのビートで移動済み
        
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
        let row = (position - 1) / 3
        let col = (position - 1) % 3
        
        // 上2マス
        if row >= 2 {
            moves.append((row - 2) * 3 + col + 1)
        }
        // 下2マス
        if row <= 0 {
            moves.append((row + 2) * 3 + col + 1)
        }
        // 左2マス
        if col >= 2 {
            moves.append(row * 3 + (col - 2) + 1)
        }
        // 右2マス
        if col <= 0 {
            moves.append(row * 3 + (col + 2) + 1)
        }
        
        return moves
    }
    
    private func getDiagonalMoves(from position: Int) -> [Int] {
        var moves: [Int] = []
        let row = (position - 1) / 3
        let col = (position - 1) % 3
        
        // 左上
        if row > 0 && col > 0 {
            moves.append((row - 1) * 3 + (col - 1) + 1)
        }
        // 右上
        if row > 0 && col < 2 {
            moves.append((row - 1) * 3 + (col + 1) + 1)
        }
        // 左下
        if row < 2 && col > 0 {
            moves.append((row + 1) * 3 + (col - 1) + 1)
        }
        // 右下
        if row < 2 && col < 2 {
            moves.append((row + 1) * 3 + (col + 1) + 1)
        }
        
        return moves
    }
    
    // MARK: - Skill Actions
    func activateSkill() {
        guard gameStatus == .playing else { return }
        guard remainingSkillUses > 0 else { return }
        
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
        
        enemyStopped = true
        skillUsageCount += 1
    }
    
    private func moveEnemy() {
        guard gameStatus == .playing else { return }
        
        let difficulty = stageManager.getDifficulty(for: currentFloor)
        enemyPosition = aiEngine.calculateNextMove(
            from: enemyPosition,
            target: playerPosition,
            level: difficulty
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
        skillUsageCount = 0
        pendingPlayerMove = nil
        hasMovedThisBeat = false
        isInvisible = false
        enemyStopped = false
        isSkillActive = false
        
        // 100階層でクリア
        if currentFloor > Constants.maxFloors {
            endGame(result: .win)
            return
        }
        
        // BPM変更
        let newBPM = stageManager.getBPM(for: currentFloor)
        let bgmVolume = playerViewModelInstance?.bgmVolume ?? 0.7
        beatEngine.changeBPM(newBPM, volume: bgmVolume)
        
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
        
        let playerPos = availablePositions.randomElement()!
        var enemyPos = availablePositions.randomElement()!
        while enemyPos == playerPos {
            enemyPos = availablePositions.randomElement()!
        }
        playerPosition = playerPos
        enemyPosition = enemyPos
        
        // 次の階層の準備ができたら再開
        beatEngine.resume()
    }
    
    // MARK: - Special Rules
    private func updateDisappearedCells() {
        if specialRule == .disappear || specialRule == .fogDisappear {
            // ランダムで1マス消失
            let allCells = Set(1...9)
            let availableCells = allCells.subtracting([playerPosition, enemyPosition])
            if let disappearedCell = availableCells.randomElement() {
                disappearedCells = [disappearedCell]
            } else {
                disappearedCells = []
            }
        } else {
            disappearedCells = []
        }
    }
    
    // 霧マップ: プレイヤーから見えるマスかどうか
    func isCellVisible(_ position: Int) -> Bool {
        if specialRule == .fog || specialRule == .fogDisappear {
            // 自分の位置と隣接するマスのみ見える
            let playerRow = (playerPosition - 1) / 3
            let playerCol = (playerPosition - 1) % 3
            let cellRow = (position - 1) / 3
            let cellCol = (position - 1) % 3
            
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
        beatEngine.stop()
        
        // スコア送信
        if result == .win || result == .lose {
            RankingService.shared.submitScore(floor: currentFloor)
        }
    }
    
    func pauseGame() {
        gameStatus = .paused
        beatEngine.pause()
    }
    
    func resumeGame() {
        gameStatus = .playing
        beatEngine.resume()
    }
    
    func resetGame() {
        beatEngine.stop()
        currentFloor = 1
        turnCount = 0
        playerPosition = 1
        enemyPosition = 9
        gameStatus = .playing
        skillUsageCount = 0
        pendingPlayerMove = nil
        hasMovedThisBeat = false
        lastProcessedBeat = 0
        specialRule = .none
        disappearedCells = []
        showFloorClear = false
        isInvisible = false
        enemyStopped = false
        isSkillActive = false
    }
}

