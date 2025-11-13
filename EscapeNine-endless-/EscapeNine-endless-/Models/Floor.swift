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
        switch floor {
        case 1...10: return 40      // 1.50秒間隔（初心者向け、ゆっくり）
        case 11...20: return 45     // 1.33秒間隔
        case 21...30: return 50     // 1.20秒間隔
        case 31...40: return 55     // 1.09秒間隔
        case 41...50: return 60     // 1.00秒間隔
        case 51...60: return 65     // 0.92秒間隔
        case 61...70: return 70     // 0.86秒間隔
        case 71...80: return 75     // 0.80秒間隔
        case 81...90: return 80     // 0.75秒間隔
        default: return 85          // 0.71秒間隔（最高難易度でも余裕を持たせる）
        }
    }
    
    static func getSpecialRule(for floor: Int) -> SpecialRule {
        if floor <= 10 {
            return .none
        } else if floor <= 30 {
            return .fog
        } else if floor <= 60 {
            return .disappear
        } else {
            return .fogDisappear
        }
    }
    
    static func getDifficulty(for floor: Int) -> AILevel {
        if floor <= 20 {
            return .easy
        } else if floor <= 50 {
            return .normal
        } else {
            return .hard
        }
    }
}

