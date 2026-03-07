//
//  HomeView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct HomeView: View {
    @StateObject private var playerViewModel = PlayerViewModel()
    @StateObject private var adMobService = AdMobService.shared
    @State private var showGame = false
    @State private var showRanking = false
    @State private var showSettings = false
    @State private var showCharacterSelection = false
    @State private var showShop = false
    @State private var showAchievements = false
    @State private var showTutorial = !UserDefaults.standard.bool(forKey: "tutorialCompleted")

    var body: some View {
        NavigationStack {
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
            .navigationDestination(isPresented: $showGame) {
                GameView()
            }
            .navigationDestination(isPresented: $showRanking) {
                RankingView()
            }
            .navigationDestination(isPresented: $showSettings) {
                SettingsView()
            }
            .navigationDestination(isPresented: $showCharacterSelection) {
                CharacterSelectionView()
            }
            .navigationDestination(isPresented: $showShop) {
                ShopView()
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
                showGame = true
            }
            .glow(color: Color(hex: GameColors.available), radius: 10, intensity: 0.4)
            .slideIn(from: .leading, delay: 0.6)
            .pulse(minScale: 1.0, maxScale: 1.02, duration: 2.0)

            GameButton(title: "キャラクター", icon: "person.2.fill", style: .secondary, maxWidth: buttonWidth) {
                showCharacterSelection = true
            }
            .slideIn(from: .leading, delay: 0.7)

            GameButton(title: "ランキング", icon: "trophy.fill", style: .secondary, maxWidth: buttonWidth) {
                showRanking = true
            }
            .slideIn(from: .leading, delay: 0.8)

            GameButton(title: "ショップ", icon: "bag.fill", style: .secondary, maxWidth: buttonWidth) {
                showShop = true
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
                showSettings = true
            }
            .slideIn(from: .leading, delay: 0.95)
        }
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
