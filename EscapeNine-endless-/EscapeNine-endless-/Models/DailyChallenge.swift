//
//  DailyChallenge.swift
//  EscapeNine-endless-
//
//  デイリーチャレンジのモデル定義
//

import Foundation

// MARK: - Challenge Condition

enum ChallengeCondition: Codable, Equatable {
    case characterLock(CharacterType)   // 指定キャラで挑戦
    case noSkillAllowed                 // スキル使用禁止
    case forcedAI(AILevel)              // AI難易度固定
    case startFloor(Int)                // 開始フロア指定

    var description: String {
        switch self {
        case .characterLock(let type):
            return "\(type.name)で挑戦"
        case .noSkillAllowed:
            return "スキル使用禁止"
        case .forcedAI(let level):
            return "鬼の強さ固定: \(level.rawValue)"
        case .startFloor(let floor):
            return "\(floor)階層スタート"
        }
    }

    var icon: String {
        switch self {
        case .characterLock: return "person.fill"
        case .noSkillAllowed: return "nosign"
        case .forcedAI: return "bolt.fill"
        case .startFloor: return "arrow.up.to.line"
        }
    }
}

// MARK: - Codable conformance for enums with associated values

extension ChallengeCondition {
    private enum CodingKeys: String, CodingKey {
        case type, characterType, aiLevel, floor
    }

    private enum ConditionType: String, Codable {
        case characterLock, noSkillAllowed, forcedAI, startFloor
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let type = try container.decode(ConditionType.self, forKey: .type)
        switch type {
        case .characterLock:
            let raw = try container.decode(String.self, forKey: .characterType)
            self = .characterLock(CharacterType(rawValue: raw) ?? .hero)
        case .noSkillAllowed:
            self = .noSkillAllowed
        case .forcedAI:
            let raw = try container.decode(String.self, forKey: .aiLevel)
            self = .forcedAI(AILevel(rawValue: raw) ?? .normal)
        case .startFloor:
            let floor = try container.decode(Int.self, forKey: .floor)
            self = .startFloor(floor)
        }
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        switch self {
        case .characterLock(let type):
            try container.encode(ConditionType.characterLock, forKey: .type)
            try container.encode(type.rawValue, forKey: .characterType)
        case .noSkillAllowed:
            try container.encode(ConditionType.noSkillAllowed, forKey: .type)
        case .forcedAI(let level):
            try container.encode(ConditionType.forcedAI, forKey: .type)
            try container.encode(level.rawValue, forKey: .aiLevel)
        case .startFloor(let floor):
            try container.encode(ConditionType.startFloor, forKey: .type)
            try container.encode(floor, forKey: .floor)
        }
    }
}

// MARK: - DailyChallenge

struct DailyChallenge: Codable {
    let date: String                    // "2026-03-17" 形式
    let conditions: [ChallengeCondition]
    var isCompleted: Bool
    var achievedFloor: Int?             // クリア時の到達階層

    var displayTitle: String {
        "デイリーチャレンジ \(date)"
    }
}
