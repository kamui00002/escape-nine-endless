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
    @Published var errorMessage: String? = nil
    @Published var hasError: Bool = false

    // MARK: - Dependencies
    private let rankingService = RankingService.shared
    private var cancellables = Set<AnyCancellable>()

    init() {
        // Observe ranking changes
        rankingService.$rankings
            .receive(on: DispatchQueue.main)
            .assign(to: &$rankings)
    }

    // MARK: - Methods
    func fetchRankings() async {
        await MainActor.run {
            isLoading = true
            errorMessage = nil
            hasError = false
        }

        let fetchedRankings = await rankingService.getRankings()

        await MainActor.run {
            rankings = fetchedRankings
            isLoading = false
        }
    }

    /// Retry fetch
    func retry() async {
        await fetchRankings()
    }
}
