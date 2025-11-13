//
//  RankingViewModel.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI
import Combine

class RankingViewModel: ObservableObject {
    // MARK: - Published Properties
    @Published var rankings: [RankingEntry] = []
    @Published var isLoading: Bool = false
    
    // MARK: - Dependencies
    private let rankingService = RankingService.shared
    
    // MARK: - Methods
    func fetchRankings() async {
        await MainActor.run {
            isLoading = true
        }
        
        let fetchedRankings = await rankingService.getRankings()
        
        await MainActor.run {
            rankings = fetchedRankings
            isLoading = false
        }
    }
    
    func submitScore(floor: Int, playerName: String) {
        rankingService.submitScore(floor: floor)
    }
}

