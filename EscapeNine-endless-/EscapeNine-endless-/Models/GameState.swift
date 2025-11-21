//
//  GameState.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import Foundation

enum GameStatus {
    case idle      // ゲーム開始前・リセット後
    case playing
    case paused
    case win
    case lose
}

enum AILevel: String, CaseIterable {
    case easy = "Easy"
    case normal = "Normal"
    case hard = "Hard"
}

enum SpecialRule {
    case none
    case fog
    case disappear
    case fogDisappear
}

struct GameState {
    var currentFloor: Int = 1
    var turnCount: Int = 0
    var maxTurns: Int = 10
    var playerPosition: Int = 1
    var enemyPosition: Int = 9
    var gameStatus: GameStatus = .playing
    var aiLevel: AILevel = .normal
    var specialRule: SpecialRule = .none
    var skillUsageCount: Int = 0
    var maxSkillUsage: Int = 5
}

