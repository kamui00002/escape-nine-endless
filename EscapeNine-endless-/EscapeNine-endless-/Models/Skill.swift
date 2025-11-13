//
//  Skill.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import Foundation

enum SkillType: String {
    case dash = "dash"
    case diagonal = "diagonal"
    case invisible = "invisible"
    case bind = "bind"
    
    var name: String {
        switch self {
        case .dash: return "ダッシュ"
        case .diagonal: return "斜め移動"
        case .invisible: return "透明化"
        case .bind: return "拘束"
        }
    }
    
    var description: String {
        switch self {
        case .dash: return "2マス移動できる"
        case .diagonal: return "斜め方向に移動可能"
        case .invisible: return "鬼に当たっても無敵"
        case .bind: return "鬼を1ターン停止させる"
        }
    }
}

struct Skill {
    let type: SkillType
    let name: String
    let description: String
    let maxUsage: Int
}

