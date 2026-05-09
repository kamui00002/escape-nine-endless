//
//  ResultView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//
//  Sprint 1 (Game Over 刷新): 「離脱口」から「発射台」へ
//  追加要素: 1) 惜しさメーター 2) 巨大リトライ 3) 挑戦時間 4) 自己ベスト誘発演出 5) シェア
//  ※ 既存の AchievementPopup / InterstitialAd / DefeatReason / NEW RECORD は壊さない
//

import SwiftUI
import UIKit

struct ResultView: View {
    let floor: Int
    let result: GameStatus
    var defeatReason: DefeatReason? = nil
    let onPlayAgain: () -> Void
    let onHome: () -> Void

    // MARK: - Sprint 1: 追加プロパティ (デフォルト値で後方互換性維持)
    /// ゲーム開始からの経過秒数
    var elapsedSeconds: Double = 0
    /// 敵から何マス離れて死亡したか (Chebyshev 距離: 1=隣接=惜しい)
    var nearMissDistance: Int = 0
    /// プレイヤー最終位置 (1-9) — シェア用 9 マス絵文字に使用
    var playerPosition: Int = 0
    /// 敵最終位置 (1-9) — シェア用 9 マス絵文字に使用
    var enemyPosition: Int = 0

    @StateObject private var playerViewModel = PlayerViewModel()
    @StateObject private var adMobService = AdMobService.shared
    @StateObject private var achievementManager = AchievementManager.shared
    @State private var adShown = false

    // MARK: - Sprint 1: シェア用 state
    @State private var showShareSheet = false

    // MARK: - 派生プロパティ

    /// 自己ベストを更新したか (Sprint 1: 「自己ベスト!」演出用)
    private var isPersonalBest: Bool {
        floor > playerViewModel.highestFloor
    }

    /// 表示用の最高記録 (今回のスコアと既存ベストの大きい方)
    private var bestFloor: Int {
        max(floor, playerViewModel.highestFloor)
    }

    /// 「あと1マスで生存」を表示すべきか (敗北時 + 隣接死亡時のみ)
    private var shouldShowNearMiss: Bool {
        result == .lose && nearMissDistance == 1
    }

    var body: some View {
        ZStack {
            GameBackground()

            if result == .win {
                CelebrationEffect()
                    .ignoresSafeArea()
            }

            VStack(spacing: 24) {
                Spacer(minLength: 16)

                resultTitle

                // Sprint 1: 自己ベスト誘発演出 (NEW RECORD と整合: 自己ベスト時は強調、そうでない時はベスト表示)
                personalBestSection

                statsSection

                // Sprint 1: 「あと1マスで生存」(惜しさメーター)
                if shouldShowNearMiss {
                    nearMissBanner
                }

                Spacer(minLength: 8)

                // Sprint 1: 巨大リトライボタン + 補助ボタン群 (シェア / ホーム)
                buttonSection
            }
            .padding(.horizontal)

            if let achievement = achievementManager.newlyUnlockedAchievement {
                VStack {
                    AchievementPopupView(achievement: achievement)
                        .padding(.top, 60)
                    Spacer()
                }
            }
        }
        .onAppear {
            // Sprint 1: Game Over 表示時に Haptic フィードバック (PDF #1 のガイドライン)
            let style: UIImpactFeedbackGenerator.FeedbackStyle = (result == .win) ? .heavy : .medium
            UIImpactFeedbackGenerator(style: style).impactOccurred()

            if !adShown {
                adShown = true
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                    InterstitialAdPresenter.show { _ in }
                }
            }
        }
        .sheet(isPresented: $showShareSheet) {
            // Sprint 1: Wordle 風シェアテキスト
            ShareSheet(activityItems: [
                ShareTextBuilder.build(
                    floor: floor,
                    elapsedSeconds: elapsedSeconds,
                    isVictory: result == .win,
                    playerPosition: playerPosition,
                    enemyPosition: enemyPosition
                )
            ])
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

    // MARK: - Sprint 1: 自己ベスト誘発演出

    /// 自己ベスト時は「自己ベスト!」を強調表示、そうでない時は「ベスト: X階」を控えめに表示
    @ViewBuilder
    private var personalBestSection: some View {
        if isPersonalBest {
            Text("自己ベスト!")
                .font(.fantasySubheading())
                .foregroundColor(Color(hex: GameColors.available))
                .padding(.horizontal, 24)
                .padding(.vertical, 10)
                .background(
                    RoundedRectangle(cornerRadius: 14)
                        .fill(Color(hex: GameColors.available).opacity(0.18))
                        .overlay(
                            RoundedRectangle(cornerRadius: 14)
                                .stroke(Color(hex: GameColors.available).opacity(0.6), lineWidth: 2)
                        )
                )
                .shimmer(duration: 2.0)
                .bounceIn(delay: 0.25)
        } else if playerViewModel.highestFloor > 0 {
            Text("ベスト: \(playerViewModel.highestFloor)階")
                .font(.fantasyCaption())
                .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                .slideIn(from: .top, delay: 0.25)
        }
    }

    // MARK: - Stats

    private var statsSection: some View {
        GameCard {
            VStack(spacing: 14) {
                VStack(spacing: 4) {
                    Text("到達階層")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.7))

                    AnimatedNumber(value: floor, duration: 0.8)
                        .font(.fantasyNumber())
                        .foregroundColor(Color(hex: GameColors.available))
                        .glow(color: Color(hex: GameColors.available), radius: 8, intensity: 0.4)
                }

                // Sprint 1: 挑戦時間表示 (elapsedSeconds が有意な値のときのみ)
                if elapsedSeconds > 0 {
                    HStack(spacing: 8) {
                        Image(systemName: "stopwatch")
                            .foregroundColor(Color(hex: GameColors.textSecondary))
                        Text("今回の挑戦時間: \(Int(elapsedSeconds.rounded()))秒")
                            .font(.fantasyBody())
                            .foregroundColor(Color(hex: GameColors.textSecondary))
                    }
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

                // 既存の NEW RECORD 表示 (Sprint 1: 自己ベストセクションと整合させつつ残す)
                if isPersonalBest {
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
                    Text("\(bestFloor)階層")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.textSecondary))
                }
            }
        }
        .padding(.horizontal, 40)
        .slideIn(from: .bottom, delay: 0.4)
    }

    // MARK: - Sprint 1: 惜しさメーター (敗北時 + 隣接死亡時のみ)

    private var nearMissBanner: some View {
        HStack(spacing: 10) {
            Image(systemName: "flame.fill")
                .foregroundColor(Color(hex: GameColors.warning))
            Text("あと1マスで生存だった!")
                .font(.fantasySubheading())
                .foregroundColor(Color(hex: GameColors.warning))
        }
        .padding(.horizontal, 20)
        .padding(.vertical, 12)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .fill(Color(hex: GameColors.warning).opacity(0.15))
                .overlay(
                    RoundedRectangle(cornerRadius: 12)
                        .stroke(Color(hex: GameColors.warning).opacity(0.55), lineWidth: 2)
                )
        )
        .shimmer(duration: 1.6)
        .bounceIn(delay: 0.5)
    }

    // MARK: - Buttons

    /// Sprint 1: 巨大リトライボタンを中心に配置。視線・指の自然動線を意識。
    /// 「もう一回」を画面下半分の主役に、シェア / ホームは補助ボタンとして小さく。
    private var buttonSection: some View {
        VStack(spacing: 14) {
            // Sprint 1: 巨大リトライボタン (height: 180、font: fantasyTitle())
            Button(action: {
                UIImpactFeedbackGenerator(style: .heavy).impactOccurred()
                onPlayAgain()
            }) {
                VStack(spacing: 6) {
                    Image(systemName: "arrow.clockwise.circle.fill")
                        .font(.system(size: 44, weight: .bold))
                    Text("もう一回")
                        .font(.fantasySubheading())
                }
                .foregroundColor(.white)
                .frame(maxWidth: .infinity)
                .frame(height: 180)
                .background(
                    LinearGradient(
                        colors: [
                            Color(hex: GameColors.available),
                            Color(hex: GameColors.success)
                        ],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    )
                )
                .clipShape(RoundedRectangle(cornerRadius: 24))
                .overlay(
                    RoundedRectangle(cornerRadius: 24)
                        .stroke(Color.white.opacity(0.25), lineWidth: 2)
                )
            }
            .buttonStyle(.plain)
            .glow(color: Color(hex: GameColors.available), radius: 18, intensity: 0.7)
            .padding(.horizontal, 8)

            // Sprint 1: 補助ボタン (シェア + ホーム) を横並びで小さく
            HStack(spacing: 12) {
                Button(action: {
                    UIImpactFeedbackGenerator(style: .light).impactOccurred()
                    showShareSheet = true
                }) {
                    HStack(spacing: 6) {
                        Image(systemName: "square.and.arrow.up")
                        Text("シェア")
                    }
                    .font(.fantasyBody())
                    .foregroundColor(Color(hex: GameColors.text))
                    .frame(maxWidth: .infinity)
                    .frame(height: 48)
                    .background(
                        RoundedRectangle(cornerRadius: 12)
                            .fill(Color(hex: GameColors.text).opacity(0.08))
                            .overlay(
                                RoundedRectangle(cornerRadius: 12)
                                    .stroke(Color(hex: GameColors.text).opacity(0.25), lineWidth: 1)
                            )
                    )
                }
                .buttonStyle(.plain)

                Button(action: {
                    UIImpactFeedbackGenerator(style: .light).impactOccurred()
                    onHome()
                }) {
                    HStack(spacing: 6) {
                        Image(systemName: "house.fill")
                        Text("ホーム")
                    }
                    .font(.fantasyBody())
                    .foregroundColor(Color(hex: GameColors.text))
                    .frame(maxWidth: .infinity)
                    .frame(height: 48)
                    .background(
                        RoundedRectangle(cornerRadius: 12)
                            .fill(Color(hex: GameColors.text).opacity(0.08))
                            .overlay(
                                RoundedRectangle(cornerRadius: 12)
                                    .stroke(Color(hex: GameColors.text).opacity(0.25), lineWidth: 1)
                            )
                    )
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 8)
        }
        .padding(.bottom, 16)
        .slideIn(from: .bottom, delay: 0.6)
    }
}
