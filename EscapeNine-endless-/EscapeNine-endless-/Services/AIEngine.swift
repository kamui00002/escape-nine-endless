//
//  AIEngine.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import Foundation

class AIEngine {
    static let shared = AIEngine()
    
    private init() {}
    
    // MARK: - Calculate Next Move
    func calculateNextMove(
        from enemyPosition: Int,
        target playerPosition: Int,
        level: AILevel
    ) -> Int {
        switch level {
        case .easy:
            return easyAI(enemyPosition: enemyPosition, playerPosition: playerPosition)
        case .normal:
            return normalAI(enemyPosition: enemyPosition, playerPosition: playerPosition)
        case .hard:
            return hardAI(enemyPosition: enemyPosition, playerPosition: playerPosition)
        }
    }
    
    // MARK: - Easy AI
    private func easyAI(enemyPosition: Int, playerPosition: Int) -> Int {
        let availableMoves = GameEngine.shared.getAvailableMoves(from: enemyPosition)

        // 50%の確率でプレイヤーに近づく、50%はランダム移動（初心者向けバランス調整）
        if Double.random(in: 0...1) < 0.5 {
            if let moveTowardsPlayer = getMoveTowardsPlayer(
                from: enemyPosition,
                target: playerPosition,
                availableMoves: availableMoves
            ) {
                return moveTowardsPlayer
            }
        }

        // ランダム移動（50%の確率）
        return availableMoves.randomElement() ?? availableMoves[0]
    }
    
    // MARK: - Normal AI
    private func normalAI(enemyPosition: Int, playerPosition: Int) -> Int {
        let availableMoves = GameEngine.shared.getAvailableMoves(from: enemyPosition)

        // 常にプレイヤーに最短距離で近づく（100%追跡）
        if let moveTowardsPlayer = getMoveTowardsPlayer(
            from: enemyPosition,
            target: playerPosition,
            availableMoves: availableMoves
        ) {
            return moveTowardsPlayer
        }

        // フォールバック（通常はここに到達しない）
        return availableMoves[0]
    }
    
    // MARK: - Hard AI
    private func hardAI(enemyPosition: Int, playerPosition: Int) -> Int {
        let availableMoves = GameEngine.shared.getAvailableMoves(from: enemyPosition)

        // プレイヤーの次の手を予測
        let predictedPlayerPosition = predictPlayerMove(playerPosition: playerPosition, enemyPosition: enemyPosition)

        // 予測位置に向かって移動
        if let moveTowardsPrediction = getMoveTowardsPlayer(
            from: enemyPosition,
            target: predictedPlayerPosition,
            availableMoves: availableMoves
        ) {
            return moveTowardsPrediction
        }

        // フォールバック: 現在位置に向かって移動
        return getMoveTowardsPlayer(from: enemyPosition, target: playerPosition, availableMoves: availableMoves) ?? availableMoves[0]
    }

    /// プレイヤーの次の移動を予測（鬼から最も遠ざかる方向を選ぶと仮定）
    private func predictPlayerMove(playerPosition: Int, enemyPosition: Int) -> Int {
        let playerMoves = GameEngine.shared.getAvailableMoves(from: playerPosition)

        // プレイヤーは鬼から最も遠ざかる方向に移動すると予測
        var maxDistance = -1
        var predictedMove = playerPosition

        for move in playerMoves {
            let moveRow = (move - 1) / 3
            let moveCol = (move - 1) % 3
            let enemyRow = (enemyPosition - 1) / 3
            let enemyCol = (enemyPosition - 1) % 3

            // マンハッタン距離
            let distance = abs(moveRow - enemyRow) + abs(moveCol - enemyCol)

            if distance > maxDistance {
                maxDistance = distance
                predictedMove = move
            }
        }

        return predictedMove
    }
    
    // MARK: - Helper
    private func getMoveTowardsPlayer(
        from enemyPosition: Int,
        target playerPosition: Int,
        availableMoves: [Int]
    ) -> Int? {
        let playerRow = (playerPosition - 1) / 3
        let playerCol = (playerPosition - 1) % 3
        
        var bestMove: Int?
        var minDistance = Int.max
        
        for move in availableMoves {
            let moveRow = (move - 1) / 3
            let moveCol = (move - 1) % 3
            
            // マンハッタン距離を計算
            let distance = abs(moveRow - playerRow) + abs(moveCol - playerCol)
            
            if distance < minDistance {
                minDistance = distance
                bestMove = move
            }
        }
        
        return bestMove
    }
}

