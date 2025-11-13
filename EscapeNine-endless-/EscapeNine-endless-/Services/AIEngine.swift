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
        
        // 30%の確率でプレイヤーに近づく
        if Double.random(in: 0...1) < 0.3 {
            if let moveTowardsPlayer = getMoveTowardsPlayer(
                from: enemyPosition,
                target: playerPosition,
                availableMoves: availableMoves
            ) {
                return moveTowardsPlayer
            }
        }
        
        // それ以外はランダム
        return availableMoves.randomElement() ?? availableMoves[0]
    }
    
    // MARK: - Normal AI
    private func normalAI(enemyPosition: Int, playerPosition: Int) -> Int {
        let availableMoves = GameEngine.shared.getAvailableMoves(from: enemyPosition)
        
        // 50%の確率でプレイヤーに近づく
        if Double.random(in: 0...1) < 0.5 {
            if let moveTowardsPlayer = getMoveTowardsPlayer(
                from: enemyPosition,
                target: playerPosition,
                availableMoves: availableMoves
            ) {
                return moveTowardsPlayer
            }
        }
        
        // それ以外はランダム
        return availableMoves.randomElement() ?? availableMoves[0]
    }
    
    // MARK: - Hard AI
    private func hardAI(enemyPosition: Int, playerPosition: Int) -> Int {
        let availableMoves = GameEngine.shared.getAvailableMoves(from: enemyPosition)
        
        // 常にプレイヤーに最も近づく移動を選択
        if let moveTowardsPlayer = getMoveTowardsPlayer(
            from: enemyPosition,
            target: playerPosition,
            availableMoves: availableMoves
        ) {
            return moveTowardsPlayer
        }
        
        return availableMoves[0]
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

