//
//  RankingService.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import Foundation

class RankingService {
    static let shared = RankingService()
    
    private init() {}
    
    func submitScore(floor: Int) {
        // TODO: Firebaseに接続
        print("Score submitted: Floor \(floor)")
    }
    
    func getRankings() async -> [RankingEntry] {
        // TODO: Firebaseから取得
        return []
    }
}

struct RankingEntry {
    let floor: Int
    let playerName: String
    let timestamp: Date
}

