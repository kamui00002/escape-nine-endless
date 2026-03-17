//
//  HomeView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

enum HomeDestination: Hashable {
    case game, ranking, settings, characterSelection, shop, dailyChallenge
}

struct HomeView: View {
    @StateObject private var playerViewModel = PlayerViewModel()
    @StateObject private var adMobService = AdMobService.shared
    @StateObject private var dailyChallengeService = DailyChallengeService.shared
    @State private var path = NavigationPath()
    @State private var showAchievements = false
    @State private var showTutorial = !UserDefaults.standard.bool(forKey: "tutorialCompleted")

    var body: some View {
        NavigationStack(path: $path) {
            GeometryReader { geometry in
                ZStack {
                    GameBackground(showParticles: true)

                    let buttonWidth = ResponsiveLayout.buttonWidth(for: geometry)

                    VStack(spacing: ResponsiveLayout.isIPad() ? 60 : 50) {
                        Spacer()

                        titleSection

                        Spacer()

                        buttonSection(buttonWidth: buttonWidth)

                        highestFloorSection

                        Spacer()
                    }
                    .padding(ResponsiveLayout.padding(for: geometry))

                    VStack {
                        Spacer()
                        BannerAdView()
                    }
                }
            }
            .navigationDestination(for: HomeDestination.self) { destination in
                switch destination {
                case .game:
                    GameView()
                case .ranking:
                    RankingView()
                case .settings:
                    SettingsView()
                case .characterSelection:
                    CharacterSelectionView()
                case .shop:
                    ShopView()
                case .dailyChallenge:
                    DailyChallengeView(challenge: dailyChallengeService.todaysChallenge)
                }
            }
            .sheet(isPresented: $showAchievements) {
                AchievementListView()
            }
        }
        .overlay {
            if showTutorial {
                TutorialOverlayView(isShowing: $showTutorial)
            }
        }
        .onAppear {
            AudioManager.shared.playBGMMusic(.menu)
        }
        .onChange(of: path) { oldPath, newPath in
            if oldPath.count > newPath.count {
                // Popped back — check if we left the game
                AudioManager.shared.playBGMMusic(.menu)
            }
        }
    }

    // MARK: - Title Section

    private var titleSection: some View {
        VStack(spacing: 16) {
            Text("ESCAPE NINE")
                .font(.fantasyTitle())
                .foregroundColor(Color(hex: GameColors.available))
                .overlay(
                    LinearGradient(
                        colors: [
                            Color(hex: GameColors.available),
                            Color(hex: GameColors.main)
                        ],
                        startPoint: .leading,
                        endPoint: .trailing
                    )
                    .mask(Text("ESCAPE NINE").font(.fantasyTitle()))
                )
                .glow(color: Color(hex: GameColors.available), radius: 8, intensity: 0.3)
                .shimmer(duration: 3.0)
                .bounceIn(delay: 0.2)

            Text("Endless Dungeon")
                .font(.fantasySubheading())
                .foregroundColor(Color(hex: GameColors.text).opacity(0.8))
                .slideIn(from: .bottom, delay: 0.4)
        }
    }

    // MARK: - Button Section

    private func buttonSection(buttonWidth: CGFloat) -> some View {
        let spacing = ResponsiveLayout.isIPad() ? 30.0 : 20.0

        return VStack(spacing: spacing) {
            GameButton(title: "冒険を始める", icon: "play.fill", style: .primary, maxWidth: buttonWidth) {
                AudioManager.shared.stopBGMMusic()
                path.append(HomeDestination.game)
            }
            .glow(color: Color(hex: GameColors.available), radius: 10, intensity: 0.4)
            .slideIn(from: .leading, delay: 0.6)
            .pulse(minScale: 1.0, maxScale: 1.02, duration: 2.0)

            dailyChallengeButton(buttonWidth: buttonWidth)

            GameButton(title: "キャラクター", icon: "person.2.fill", style: .secondary, maxWidth: buttonWidth) {
                path.append(HomeDestination.characterSelection)
            }
            .slideIn(from: .leading, delay: 0.7)

            GameButton(title: "ランキング", icon: "trophy.fill", style: .secondary, maxWidth: buttonWidth) {
                path.append(HomeDestination.ranking)
            }
            .slideIn(from: .leading, delay: 0.8)

            GameButton(title: "ショップ", icon: "bag.fill", style: .secondary, maxWidth: buttonWidth) {
                path.append(HomeDestination.shop)
            }
            .slideIn(from: .leading, delay: 0.85)

            GameButton(title: "実績", icon: "medal.fill", style: .secondary, maxWidth: buttonWidth) {
                showAchievements = true
            }
            .slideIn(from: .leading, delay: 0.9)

            GameButton(title: "遊び方", icon: "questionmark.circle.fill", style: .secondary, maxWidth: buttonWidth) {
                showTutorial = true
            }
            .slideIn(from: .leading, delay: 0.9)

            GameButton(title: "設定", icon: "gearshape.fill", style: .secondary, maxWidth: buttonWidth) {
                path.append(HomeDestination.settings)
            }
            .slideIn(from: .leading, delay: 0.95)
        }
    }

    // MARK: - Daily Challenge Button

    @ViewBuilder
    private func dailyChallengeButton(buttonWidth: CGFloat) -> some View {
        let isCompleted = dailyChallengeService.todaysChallenge.isCompleted

        Button(action: {
            AudioManager.shared.playSoundEffect(.buttonTap)
            path.append(HomeDestination.dailyChallenge)
        }) {
            HStack(spacing: 8) {
                Image(systemName: isCompleted ? "checkmark.seal.fill" : "calendar.badge.clock")
                    .font(.body)
                    .foregroundColor(isCompleted ? Color(hex: GameColors.success) : Color(hex: GameColors.textSecondary))

                VStack(alignment: .leading, spacing: 2) {
                    Text("デイリーチャレンジ")
                        .font(.fantasyBody())
                        .foregroundColor(isCompleted ? Color(hex: GameColors.success) : Color(hex: GameColors.text))

                    Text(isCompleted ? "本日クリア済み" : "毎日新しい挑戦")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.6))
                }

                Spacer()

                if !isCompleted {
                    Text("NEW")
                        .font(.system(size: 10, weight: .black))
                        .foregroundColor(.white)
                        .padding(.horizontal, 6)
                        .padding(.vertical, 3)
                        .background(Color.red)
                        .cornerRadius(4)
                }
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 12)
            .frame(maxWidth: buttonWidth)
            .background(
                RoundedRectangle(cornerRadius: 12)
                    .fill(Color(hex: GameColors.backgroundSecondary))
                    .overlay(
                        RoundedRectangle(cornerRadius: 12)
                            .stroke(
                                isCompleted ? Color(hex: GameColors.success).opacity(0.5) : Color(hex: GameColors.textSecondary).opacity(0.5),
                                lineWidth: 1.5
                            )
                    )
            )
        }
        .slideIn(from: .leading, delay: 0.65)
    }

    // MARK: - Highest Floor Section

    private var highestFloorSection: some View {
        VStack(spacing: 8) {
            Text("最高到達階層")
                .font(.fantasyCaption())
                .foregroundColor(Color(hex: GameColors.text).opacity(0.7))

            AnimatedNumber(value: playerViewModel.highestFloor, duration: 1.0)
                .font(.fantasyNumber())
                .foregroundColor(Color(hex: GameColors.available))
                .glow(color: Color(hex: GameColors.available), radius: 6, intensity: 0.3)
        }
        .padding(.top, ResponsiveLayout.isIPad() ? 40 : 30)
        .slideIn(from: .bottom, delay: 1.0)
    }
}
