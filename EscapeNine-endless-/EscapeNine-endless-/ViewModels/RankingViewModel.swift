//
//  RankingViewModel.swift
//  EscapeNine-endless-
//

import SwiftUI
import Combine

enum RankingTab {
    case local
    case cloud
}

@MainActor
class RankingViewModel: ObservableObject {
    // MARK: - Published Properties
    @Published var rankings: [RankingEntry] = []
    @Published var cloudRankings: [FirebaseRankingEntry] = []
    @Published var selectedTab: RankingTab = .local
    @Published var isLoading: Bool = false
    @Published var errorMessage: String? = nil
    @Published var hasError: Bool = false

    // MARK: - Dependencies
    private let rankingService = RankingService.shared
    private let firebaseService = FirebaseService.shared
    private var cancellables = Set<AnyCancellable>()

    init() {
        rankingService.$rankings
            .receive(on: DispatchQueue.main)
            .assign(to: &$rankings)
    }

    // MARK: - Methods

    func fetchRankings() async {
        isLoading = true
        errorMessage = nil
        hasError = false

        let fetchedRankings = await rankingService.getRankings()
        rankings = fetchedRankings
        isLoading = false
    }

    func fetchCloudRankings() async {
        isLoading = true
        errorMessage = nil
        hasError = false

        do {
            cloudRankings = try await firebaseService.getRankings(limit: 100)
        } catch {
            errorMessage = "クラウドランキングの取得に失敗しました"
            hasError = true
        }
        isLoading = false
    }

    func selectTab(_ tab: RankingTab) {
        selectedTab = tab
        Task {
            switch tab {
            case .local:
                await fetchRankings()
            case .cloud:
                await fetchCloudRankings()
            }
        }
    }

    /// Retry fetch
    func retry() async {
        switch selectedTab {
        case .local:
            await fetchRankings()
        case .cloud:
            await fetchCloudRankings()
        }
    }
}
