//
//  Character.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import Foundation

enum CharacterType: String, CaseIterable {
    case hero = "hero"
    case thief = "thief"
    case wizard = "wizard"
    case elf = "elf"
    case knight = "knight"

    var name: String {
        switch self {
        case .hero: return "勇者"
        case .thief: return "盗賊"
        case .wizard: return "魔法使い"
        case .elf: return "エルフ"
        case .knight: return "ナイト"
        }
    }

    var isFree: Bool {
        switch self {
        case .hero, .thief: return true
        case .wizard, .elf, .knight: return false
        }
    }

    var price: Int? {
        switch self {
        case .hero, .thief: return nil
        case .wizard, .elf, .knight: return Constants.premiumCharacterPrice
        }
    }
}

struct Character {
    let id: String
    let type: CharacterType
    let name: String
    let skill: Skill
    let isFree: Bool
    let price: Int?
    let spriteName: String
    
    static func getCharacter(for type: CharacterType) -> Character {
        let skill: Skill
        switch type {
        case .hero:
            skill = Skill(type: .dash, name: "ダッシュ", description: "2マス移動できる", maxUsage: Constants.heroSkillMaxUsage)
        case .thief:
            skill = Skill(type: .diagonal, name: "斜め移動", description: "斜め方向に移動可能", maxUsage: Constants.thiefSkillMaxUsage)
        case .wizard:
            skill = Skill(type: .invisible, name: "透明化", description: "鬼に当たっても無敵", maxUsage: Constants.wizardSkillMaxUsage)
        case .elf:
            skill = Skill(type: .bind, name: "拘束", description: "鬼を\(Constants.bindDurationTurns)ターン停止させる", maxUsage: Constants.elfSkillMaxUsage)
        case .knight:
            skill = Skill(type: .shield, name: "盾ガード", description: "次の衝突を1回無効化する", maxUsage: Constants.knightSkillMaxUsage)
        }

        return Character(
            id: type.rawValue,
            type: type,
            name: type.name,
            skill: skill,
            isFree: type.isFree,
            price: type.price,
            spriteName: type.rawValue
        )
    }
    
    static var defaultCharacter: Character {
        return getCharacter(for: .hero)
    }
}

