//
//  Floor.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import Foundation

struct Floor {
    let number: Int
    let bpm: Double
    let specialRule: SpecialRule
    let aiLevel: AILevel
    
    static func calculateBPM(for floor: Int) -> Double {
        let bpmRange = Int((Constants.maxBPM - Constants.minBPM) / Constants.bpmIncrement)
        let floorsPerBPM = Constants.maxFloors / bpmRange

        let bpmIndex = min((floor - 1) / floorsPerBPM, bpmRange)
        return Constants.minBPM + (Double(bpmIndex) * Constants.bpmIncrement)
    }

    static func getSpecialRule(for floor: Int) -> SpecialRule {
        if floor < Constants.fogStartFloor {
            return .none
        } else if floor < Constants.disappearStartFloor {
            return .fog
        } else if floor < Constants.combinedRulesStartFloor {
            return .disappear
        } else {
            return .fogDisappear
        }
    }
    
    static func getDifficulty(for floor: Int) -> AILevel {
        if floor < Constants.normalDifficultyStartFloor {
            return .easy
        } else if floor < Constants.hardDifficultyStartFloor {
            return .normal
        } else {
            return .hard
        }
    }
}

