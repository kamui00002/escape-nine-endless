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
    @AppStorage("hasSeenTutorial") private var hasSeenTutorial: Bool = false
    // Sprint 3 v1.1 動的オンボーディング用キー (docs/onboarding-v1.1-design.md §6)。
    // OnboardingTutorialView 完成までは予約のみ、書き込まれない。
    @AppStorage("hasSeenTutorialV1_1") private var hasSeenTutorialV1_1: Bool = false
    @State private var showTutorial = false

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
        .fullScreenCover(isPresented: $showTutorial) {
            TutorialOverlayView {
                hasSeenTutorial = true
                showTutorial = false
                ConversionService.shared.trackTutorialComplete()
            }
        }
        .onAppear {
            AudioManager.shared.playBGMMusic(.menu)
            // GameView は別の PlayerViewModel インスタンスを持つため、
            // ゲーム後に highestFloor が更新されていても反映されていない。
            // onAppear で UserDefaults から再読み込みして同期する。
            playerViewModel.reload()

            // Sprint 1: 旧キー (`tutorialCompleted`) からの一回限り migration。
            // 1.4.2 までのリリースで完了フラグを保存していた既存ユーザーが、
            // 更新後に skip 不可なフルスクリーンチュートリアルを再度通らないようにする。
            if !hasSeenTutorial && UserDefaults.standard.bool(forKey: "tutorialCompleted") {
                hasSeenTutorial = true
            }

            // Sprint 3 v1.1: 表示判定の二段階化 (docs/onboarding-v1.1-design.md §6)。
            // OnboardingTutorialView 完成後にこのブロックを有効化する。現状は読み取りのみ。
            // キー併存ルール:
            //   - hasSeenTutorialV1_1 == false → v1.1 動的版を表示 (完了時に両方 true セット)
            //   - hasSeenTutorialV1_1 == true  → 表示しない (hasSeenTutorial の値は無視)
            // 既存ユーザー (hasSeenTutorial==true / hasSeenTutorialV1_1==false) も
            // v1.1 リリース後に 1 回だけ動的版を通る。
            //
            // 本実装で有効化する判定 (現在はコメントアウト):
            //     if !hasSeenTutorialV1_1 {
            //         showOnboardingTutorialV1_1 = true
            //         return
            //     }
            _ = hasSeenTutorialV1_1 // フラグの未使用警告抑制 (v1.1 本実装で削除)

            // 初回起動時のみチュートリアルを表示 (2 回目以降は自動スキップ)。
            if !hasSeenTutorial {
                showTutorial = true
            }

            // Sprint 2 F2: 抜かれ検出 (GameCenter 認証済み + レート制限内なら no-op)。
            Task {
                await LeaderboardWatcher.shared.checkAndNotify()
            }
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

            if playerViewModel.highestFloor >= 10 {
                dailyChallengeButton(buttonWidth: buttonWidth)
            }

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

// MARK: - Previews

#Preview("iPhone") {
    HomeView()
}

#Preview("iPad") {
    HomeView()
        .previewDevice("iPad Pro 13-inch (M4)")
}
