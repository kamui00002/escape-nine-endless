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
    var defeatReason: DefeatReason? = nil
    let onPlayAgain: () -> Void
    let onHome: () -> Void

    @StateObject private var playerViewModel = PlayerViewModel()
    @StateObject private var adMobService = AdMobService.shared
    @StateObject private var achievementManager = AchievementManager.shared
    @State private var adShown = false

    var body: some View {
        ZStack {
            GameBackground()

            if result == .win {
                CelebrationEffect()
                    .ignoresSafeArea()
            }

            VStack(spacing: 30) {
                Spacer()

                resultTitle

                statsSection

                Spacer()

                buttonSection

                Spacer()
            }
            .padding()

            if let achievement = achievementManager.newlyUnlockedAchievement {
                VStack {
                    AchievementPopupView(achievement: achievement)
                        .padding(.top, 60)
                    Spacer()
                }
            }
        }
        .onAppear {
            if !adShown {
                adShown = true
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                    InterstitialAdPresenter.show { _ in }
                }
            }
        }
    }

    // MARK: - Result Title

    private var defeatReasonText: String {
        guard let reason = defeatReason else { return "" }
        switch reason {
        case .caughtByEnemy: return "敵に捕まった"
        case .timeOut: return "時間切れ"
        }
    }

    private var defeatReasonIcon: String {
        guard let reason = defeatReason else { return "" }
        switch reason {
        case .caughtByEnemy: return "exclamationmark.triangle.fill"
        case .timeOut: return "clock.badge.xmark"
        }
    }

    private var resultTitle: some View {
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
    }

    // MARK: - Stats

    private var statsSection: some View {
        GameCard {
            VStack(spacing: 16) {
                VStack(spacing: 4) {
                    Text("到達階層")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.7))

                    AnimatedNumber(value: floor, duration: 0.8)
                        .font(.fantasyNumber())
                        .foregroundColor(Color(hex: GameColors.available))
                        .glow(color: Color(hex: GameColors.available), radius: 8, intensity: 0.4)
                }

                HStack(spacing: 8) {
                    Text("使用キャラクター")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                    Text(playerViewModel.selectedCharacter.name)
                        .font(.fantasyBody())
                        .foregroundColor(Color(hex: GameColors.textSecondary))
                }

                if result == .lose, let _ = defeatReason {
                    HStack(spacing: 8) {
                        Image(systemName: defeatReasonIcon)
                            .foregroundColor(Color(hex: GameColors.warning))
                        Text(defeatReasonText)
                            .font(.fantasyBody())
                            .foregroundColor(Color(hex: GameColors.warning))
                    }
                    .padding(.horizontal, 16)
                    .padding(.vertical, 8)
                    .background(
                        RoundedRectangle(cornerRadius: 8)
                            .fill(Color(hex: GameColors.warning).opacity(0.1))
                            .overlay(
                                RoundedRectangle(cornerRadius: 8)
                                    .stroke(Color(hex: GameColors.warning).opacity(0.3), lineWidth: 1)
                            )
                    )
                }

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

                VStack(spacing: 2) {
                    Text("最高記録")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.5))
                    Text("\(max(floor, playerViewModel.highestFloor))階層")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.textSecondary))
                }
            }
        }
        .padding(.horizontal, 40)
        .slideIn(from: .bottom, delay: 0.4)
    }

    // MARK: - Buttons

    private var buttonSection: some View {
        VStack(spacing: 16) {
            GameButton(title: "再挑戦", style: .primary, maxWidth: 280) {
                onPlayAgain()
            }
            .glow(color: Color(hex: GameColors.available), radius: 15, intensity: 0.6)

            GameButton(title: "ホームへ", style: .secondary, maxWidth: 280) {
                onHome()
            }
        }
        .slideIn(from: .bottom, delay: 0.6)
    }
}
