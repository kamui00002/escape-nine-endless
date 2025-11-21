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

    // MARK: - Methods
    func fetchRankings() async {
        await MainActor.run {
            isLoading = true
            errorMessage = nil
            hasError = false
        }

        do {
            let fetchedRankings = await rankingService.getRankings()

            await MainActor.run {
                rankings = fetchedRankings
                isLoading = false
            }
        } catch {
            await MainActor.run {
                errorMessage = "ランキングの取得に失敗しました: \(error.localizedDescription)"
                hasError = true
                isLoading = false
                rankings = []
            }
        }
    }

    func submitScore(floor: Int, playerName: String) {
        rankingService.submitScore(floor: floor)
    }

    /// エラーをクリア
    func clearError() {
        errorMessage = nil
        hasError = false
    }

    /// ランキングを再取得
    func retry() async {
        await fetchRankings()
    }
}

