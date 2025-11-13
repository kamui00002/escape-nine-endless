//
//  GameEngine.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import Foundation

class GameEngine {
    static let shared = GameEngine()
    
    private init() {}
    
    // MARK: - Move Validation
    func isValidMove(from currentPosition: Int, to newPosition: Int) -> Bool {
        // 範囲チェック
        if newPosition < 1 || newPosition > Constants.gridSize {
            return false
        }
        
        // 同じマスは移動不可
        if currentPosition == newPosition {
            return false
        }
        
        // グリッドの位置関係を計算
        let currentRow = (currentPosition - 1) / 3
        let currentCol = (currentPosition - 1) % 3
        let newRow = (newPosition - 1) / 3
        let newCol = (newPosition - 1) % 3
        
        // 隣接チェック（上下左右のみ）
        let rowDiff = abs(newRow - currentRow)
        let colDiff = abs(newCol - currentCol)
        
        // 隣接するマス（上下左右）のみ移動可能
        return (rowDiff == 1 && colDiff == 0) || (rowDiff == 0 && colDiff == 1)
    }
    
    func isValidDiagonalMove(from currentPosition: Int, to newPosition: Int) -> Bool {
        if newPosition < 1 || newPosition > Constants.gridSize {
            return false
        }
        
        if currentPosition == newPosition {
            return false
        }
        
        let currentRow = (currentPosition - 1) / 3
        let currentCol = (currentPosition - 1) % 3
        let newRow = (newPosition - 1) / 3
        let newCol = (newPosition - 1) % 3
        
        let rowDiff = abs(newRow - currentRow)
        let colDiff = abs(newCol - currentCol)
        
        // 対角移動（斜め）
        return rowDiff == 1 && colDiff == 1
    }
    
    func isValidDashMove(from currentPosition: Int, to newPosition: Int) -> Bool {
        if newPosition < 1 || newPosition > Constants.gridSize {
            return false
        }
        
        if currentPosition == newPosition {
            return false
        }
        
        let currentRow = (currentPosition - 1) / 3
        let currentCol = (currentPosition - 1) % 3
        let newRow = (newPosition - 1) / 3
        let newCol = (newPosition - 1) % 3
        
        let rowDiff = abs(newRow - currentRow)
        let colDiff = abs(newCol - currentCol)
        
        // 2マス移動（上下左右の2マス先）
        return (rowDiff == 2 && colDiff == 0) || (rowDiff == 0 && colDiff == 2)
    }
    
    // MARK: - Available Moves
    func getAvailableMoves(from position: Int) -> [Int] {
        var moves: [Int] = []
        let row = (position - 1) / 3
        let col = (position - 1) % 3
        
        // 上
        if row > 0 {
            moves.append((row - 1) * 3 + col + 1)
        }
        // 下
        if row < 2 {
            moves.append((row + 1) * 3 + col + 1)
        }
        // 左
        if col > 0 {
            moves.append(row * 3 + (col - 1) + 1)
        }
        // 右
        if col < 2 {
            moves.append(row * 3 + (col + 1) + 1)
        }
        
        return moves
    }
    
    // MARK: - Game Result
    func checkGameResult(
        playerPosition: Int,
        enemyPosition: Int,
        turnCount: Int,
        maxTurns: Int
    ) -> GameStatus {
        // 同じマスにいる = 負け
        if playerPosition == enemyPosition {
            return .lose
        }
        
        // ターン数が上限に達した = 勝ち（次の階層へ）
        if turnCount >= maxTurns {
            return .win
        }
        
        return .playing
    }
}

