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
    
    var name: String {
        switch self {
        case .hero: return "勇者"
        case .thief: return "盗賊"
        case .wizard: return "魔法使い"
        case .elf: return "エルフ"
        }
    }
    
    var isFree: Bool {
        switch self {
        case .hero, .thief: return true
        case .wizard, .elf: return false
        }
    }
    
    var price: Int? {
        switch self {
        case .hero, .thief: return nil
        case .wizard, .elf: return 240
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
}

