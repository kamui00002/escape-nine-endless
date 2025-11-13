//
//  StageManager.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import Foundation

class StageManager {
    static let shared = StageManager()
    
    private init() {}
    
    func getBPM(for floor: Int) -> Double {
        return Floor.calculateBPM(for: floor)
    }
    
    func getSpecialRule(for floor: Int) -> SpecialRule {
        return Floor.getSpecialRule(for: floor)
    }
    
    func getDifficulty(for floor: Int) -> AILevel {
        return Floor.getDifficulty(for: floor)
    }
    
    func getFloorDescription(for floor: Int) -> String {
        let rule = getSpecialRule(for: floor)
        let bpm = getBPM(for: floor)
        
        var description = "Floor \(floor) - BPM: \(Int(bpm))"
        
        switch rule {
        case .fog:
            description += " (Fog)"
        case .disappear:
            description += " (Disappear)"
        case .fogDisappear:
            description += " (Fog + Disappear)"
        case .none:
            break
        }
        
        return description
    }
}

