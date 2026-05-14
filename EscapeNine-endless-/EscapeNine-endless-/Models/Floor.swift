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
    
    /// ボスフロアかどうか（10の倍数階）
    static func isBossFloor(_ floor: Int) -> Bool {
        floor % Constants.bossFloorInterval == 0
    }

    static func calculateBPM(for floor: Int) -> Double {
        // v1.1 オンボーディング: Floor 0 はプロローグ専用で常に bpmCurveStart (60 BPM)。
        // 既存の clampedFloor - 1 ロジックは floor=0 で負値になり pow() で破綻するため、
        // 明示的に早期 return する (docs/onboarding-v1.1-design.md §4 反映)。
        if floor == TutorialConstants.prologueFloor {
            return Constants.bpmCurveStart
        }
        // べき乗曲線: BPM = start + (end - start) * (floor/99)^exponent
        // 序盤は緩やか、終盤に急加速
        let clampedFloor = max(1, min(floor, Constants.maxFloors))
        let ratio = Double(clampedFloor - 1) / 99.0
        let scaled = pow(ratio, Constants.bpmCurveExponent)
        return Constants.bpmCurveStart + (Constants.bpmCurveEnd - Constants.bpmCurveStart) * scaled
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
    
    /// 階層の自然難易度を取得
    static func getNaturalDifficulty(for floor: Int) -> AILevel {
        if floor < Constants.aiNaturalNormalFloor {
            return .easy
        } else if floor < Constants.aiNaturalHardFloor {
            return .normal
        } else {
            return .hard
        }
    }

    /// プレイヤー選択を考慮した実効AI難易度を取得
    /// - Easy選択: 自然難易度より1段下
    /// - Normal選択: 自然難易度と一致
    /// - Hard選択: 自然難易度より1段上
    /// - ボスフロア: 常に .boss（プレイヤー選択を無視）
    static func getEffectiveAILevel(for floor: Int, playerSelection: AILevel) -> AILevel {
        if isBossFloor(floor) { return .boss }
        let natural = getNaturalDifficulty(for: floor)

        switch playerSelection {
        case .easy:
            // 1段下げる
            switch natural {
            case .easy: return .easy
            case .normal: return .easy
            case .hard: return .normal
            case .boss: return .hard  // ボスは下げない
            }
        case .normal:
            return natural
        case .hard:
            // 1段上げる
            switch natural {
            case .easy: return .normal
            case .normal: return .hard
            case .hard: return .hard
            case .boss: return .boss
            }
        case .boss:
            return .boss
        }
    }
    
    /// 階層に応じた敵のスプライト名を取得
    static func getEnemySprite(for floor: Int) -> String {
        switch floor {
        case 1...25:
            return "red_oni"
        case 26...50:
            return "blue_oni"
        case 51...75:
            return "skeleton"
        case 76...100:
            return "dragon"
        default:
            return "red_oni" // フォールバック
        }
    }
}

