//
//  GameCenterService.swift
//  EscapeNine-endless-
//
//  Game Center integration for leaderboards and achievements
//

import GameKit
import SwiftUI
import Combine

@MainActor
class GameCenterService: ObservableObject {
    static let shared = GameCenterService()

    // MARK: - Published Properties
    @Published var isAuthenticated = false
    @Published var localPlayerName: String = "プレイヤー"
    @Published var showLeaderboard = false

    // MARK: - Constants
    static let leaderboardID = "com.escapenine.highestfloor"

    private init() {}

    // MARK: - Authentication

    /// Authenticate the local player with Game Center
    func authenticate() {
        GKLocalPlayer.local.authenticateHandler = { [weak self] viewController, error in
            Task { @MainActor in
                if let error = error {
                    print("[GameCenter] Authentication error: \(error.localizedDescription)")
                    self?.isAuthenticated = false
                    return
                }

                if let vc = viewController {
                    // Present the Game Center login view
                    if let windowScene = UIApplication.shared.connectedScenes.first as? UIWindowScene,
                       let rootVC = windowScene.windows.first?.rootViewController {
                        rootVC.present(vc, animated: true)
                    }
                    return
                }

                // Successfully authenticated
                self?.isAuthenticated = GKLocalPlayer.local.isAuthenticated
                self?.localPlayerName = GKLocalPlayer.local.displayName
                print("[GameCenter] Authenticated: \(GKLocalPlayer.local.displayName)")
            }
        }
    }

    // MARK: - Leaderboard

    /// Submit a score to the leaderboard
    func submitScore(floor: Int) async {
        guard isAuthenticated else {
            print("[GameCenter] Not authenticated - skipping score submit")
            return
        }

        do {
            try await GKLeaderboard.submitScore(
                floor,
                context: 0,
                player: GKLocalPlayer.local,
                leaderboardIDs: [Self.leaderboardID]
            )
            print("[GameCenter] Score submitted: \(floor)")
        } catch {
            print("[GameCenter] Score submit error: \(error.localizedDescription)")
        }
    }

    /// Load top scores from the leaderboard
    func loadTopScores(count: Int = 10) async -> [(name: String, score: Int)] {
        guard isAuthenticated else { return [] }

        do {
            let leaderboards = try await GKLeaderboard.loadLeaderboards(IDs: [Self.leaderboardID])
            guard let leaderboard = leaderboards.first else { return [] }

            let result = try await leaderboard.loadEntries(
                for: .global,
                timeScope: .allTime,
                range: NSRange(location: 1, length: count)
            )

            let entries = result.1

            return entries.map { entry in
                (name: entry.player.displayName, score: entry.score)
            }
        } catch {
            print("[GameCenter] Load scores error: \(error.localizedDescription)")
            return []
        }
    }

    /// Show the Game Center leaderboard UI
    func presentLeaderboard() {
        guard isAuthenticated else {
            print("[GameCenter] Not authenticated - cannot show leaderboard")
            return
        }

        showLeaderboard = true
    }
}

// MARK: - Game Center Leaderboard View (SwiftUI Wrapper)
struct GameCenterLeaderboardView: UIViewControllerRepresentable {
    @Environment(\.dismiss) var dismiss

    func makeUIViewController(context: Context) -> GKGameCenterViewController {
        let vc = GKGameCenterViewController(
            leaderboardID: GameCenterService.leaderboardID,
            playerScope: .global,
            timeScope: .allTime
        )
        vc.gameCenterDelegate = context.coordinator
        return vc
    }

    func updateUIViewController(_ uiViewController: GKGameCenterViewController, context: Context) {}

    func makeCoordinator() -> Coordinator {
        Coordinator(dismiss: dismiss)
    }

    class Coordinator: NSObject, GKGameCenterControllerDelegate {
        let dismiss: DismissAction

        init(dismiss: DismissAction) {
            self.dismiss = dismiss
        }

        func gameCenterViewControllerDidFinish(_ gameCenterViewController: GKGameCenterViewController) {
            dismiss()
        }
    }
}
