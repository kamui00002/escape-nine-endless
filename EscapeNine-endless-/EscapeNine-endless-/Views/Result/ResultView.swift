//
//  ResultView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct ResultView: View {
    let floor: Int
    let result: GameStatus
    let onPlayAgain: () -> Void
    let onHome: () -> Void

    @StateObject private var playerViewModel = PlayerViewModel()
    @StateObject private var adMobService = AdMobService.shared
    @StateObject private var achievementManager = AchievementManager.shared
    @State private var adShown = false

    var body: some View {
        ZStack {
            // Background
            LinearGradient(
                colors: [
                    Color(hex: GameColors.background),
                    Color(hex: GameColors.backgroundSecondary)
                ],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea()

            // Celebration effect on win
            if result == .win {
                CelebrationEffect()
                    .ignoresSafeArea()
            }

            VStack(spacing: 30) {
                Spacer()

                // Result Title
                Text(result == .win ? "VICTORY!" : "DEFEAT")
                    .font(.fantasyTitle())
                    .foregroundColor(result == .win ? Color(hex: GameColors.success) : Color(hex: GameColors.warning))
                    .overlay(
                        (result == .win ?
                         LinearGradient(
                             colors: [
                                 Color(hex: GameColors.success),
                                 Color(hex: GameColors.available)
                             ],
                             startPoint: .leading,
                             endPoint: .trailing
                         ) :
                         LinearGradient(
                             colors: [
                                 Color(hex: GameColors.warning),
                                 Color(hex: GameColors.enemy)
                             ],
                             startPoint: .leading,
                             endPoint: .trailing
                         ))
                        .mask(Text(result == .win ? "VICTORY!" : "DEFEAT").font(.fantasyTitle()))
                    )
                    .shadow(color: (result == .win ? Color(hex: GameColors.success) : Color(hex: GameColors.warning)).opacity(0.5), radius: 15)
                    .bounceIn(delay: 0.1)

                // Stats
                VStack(spacing: 16) {
                    // Floor reached
                    VStack(spacing: 4) {
                        Text("到達階層")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))

                        AnimatedNumber(value: floor, duration: 0.8)
                            .font(.fantasyNumber())
                            .foregroundColor(Color(hex: GameColors.available))
                            .glow(color: Color(hex: GameColors.available), radius: 8, intensity: 0.4)
                    }

                    // Character used
                    HStack(spacing: 8) {
                        Text("使用キャラクター")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                        Text(playerViewModel.selectedCharacter.name)
                            .font(.fantasyBody())
                            .foregroundColor(Color(hex: GameColors.textSecondary))
                    }

                    // New Record
                    if floor > playerViewModel.highestFloor {
                        Text("NEW RECORD!")
                            .font(.fantasySubheading())
                            .foregroundColor(Color(hex: GameColors.available))
                            .padding(.horizontal, 20)
                            .padding(.vertical, 10)
                            .background(
                                RoundedRectangle(cornerRadius: 12)
                                    .fill(Color(hex: GameColors.available).opacity(0.15))
                                    .overlay(
                                        RoundedRectangle(cornerRadius: 12)
                                            .stroke(Color(hex: GameColors.available).opacity(0.5), lineWidth: 2)
                                    )
                            )
                            .shimmer(duration: 2.0)
                            .bounceIn(delay: 0.3)
                    }

                    // Best score
                    VStack(spacing: 2) {
                        Text("最高記録")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.5))
                        Text("\(max(floor, playerViewModel.highestFloor))階層")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.textSecondary))
                    }
                }
                .padding()
                .background(
                    RoundedRectangle(cornerRadius: 16)
                        .fill(Color(hex: GameColors.backgroundSecondary))
                        .overlay(
                            RoundedRectangle(cornerRadius: 16)
                                .stroke(Color(hex: GameColors.gridBorder).opacity(0.3), lineWidth: 1)
                        )
                )
                .padding(.horizontal, 40)
                .slideIn(from: .bottom, delay: 0.4)

                Spacer()

                // Buttons
                VStack(spacing: 16) {
                    Button(action: {
                        AudioManager.shared.playSoundEffect(.buttonTap)
                        onPlayAgain()
                    }) {
                        Text("再挑戦")
                            .font(.fantasyBody())
                            .foregroundColor(.white)
                            .frame(maxWidth: 280)
                            .padding()
                            .background(
                                LinearGradient(
                                    colors: [
                                        Color(hex: GameColors.available),
                                        Color(hex: GameColors.main)
                                    ],
                                    startPoint: .leading,
                                    endPoint: .trailing
                                )
                            )
                            .cornerRadius(16)
                            .shadow(color: Color(hex: GameColors.available).opacity(0.6), radius: 15)
                    }
                    .pressableButton(scale: 0.96, shadowRadius: 12)

                    Button(action: {
                        AudioManager.shared.playSoundEffect(.buttonTap)
                        onHome()
                    }) {
                        Text("ホームへ")
                            .font(.fantasyBody())
                            .foregroundColor(Color(hex: GameColors.text))
                            .frame(maxWidth: 280)
                            .padding()
                            .background(
                                RoundedRectangle(cornerRadius: 16)
                                    .fill(Color(hex: GameColors.backgroundSecondary))
                                    .overlay(
                                        RoundedRectangle(cornerRadius: 16)
                                            .stroke(Color(hex: GameColors.gridBorder).opacity(0.5), lineWidth: 2)
                                    )
                            )
                    }
                }
                .slideIn(from: .bottom, delay: 0.6)

                Spacer()
            }
            .padding()

            // Achievement popup
            if let achievement = achievementManager.newlyUnlockedAchievement {
                VStack {
                    AchievementPopupView(achievement: achievement)
                        .padding(.top, 60)
                    Spacer()
                }
            }
        }
        .onAppear {
            // Show interstitial ad (once)
            if !adShown {
                adShown = true
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                    InterstitialAdPresenter.show { _ in }
                }
            }
        }
    }
}
