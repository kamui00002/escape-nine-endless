//
//  DailyChallengeView.swift
//  EscapeNine-endless-
//
//  デイリーチャレンジの条件表示・開始UI
//

import SwiftUI

struct DailyChallengeView: View {
    let challenge: DailyChallenge

    @Environment(\.dismiss) private var dismiss
    @State private var navigateToGame = false
    @State private var showAlreadyCompleted = false

    var body: some View {
        ZStack {
            GameBackground()

            VStack(spacing: 28) {
                // ヘッダー
                VStack(spacing: 8) {
                    HStack {
                        Image(systemName: "calendar.badge.clock")
                            .font(.title2)
                            .foregroundColor(Color(hex: GameColors.textSecondary))
                        Text("デイリーチャレンジ")
                            .font(.fantasySubheading())
                            .foregroundColor(Color(hex: GameColors.textSecondary))
                    }
                    Text(challenge.date)
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.6))
                }

                if challenge.isCompleted {
                    completedBadge
                }

                conditionsList

                Spacer()

                actionButtons
            }
            .padding(.top, 24)
            .padding(.horizontal, 24)
        }
        .navigationBarBackButtonHidden(true)
        .toolbar(.hidden, for: .navigationBar)
        .overlay(alignment: .topLeading) {
            Button(action: { dismiss() }) {
                HStack(spacing: 4) {
                    Image(systemName: "chevron.left")
                    Text("戻る")
                }
                .font(.fantasyCaption())
                .foregroundColor(Color(hex: GameColors.text))
                .padding(.horizontal, 14)
                .padding(.vertical, 8)
                .background(
                    RoundedRectangle(cornerRadius: 8)
                        .fill(Color(hex: GameColors.backgroundSecondary))
                )
            }
            .padding(.top, 16)
            .padding(.leading, 16)
        }
        .navigationDestination(isPresented: $navigateToGame) {
            GameView()
        }
        .alert("本日は挑戦済みです", isPresented: $showAlreadyCompleted) {
            Button("OK") {}
        } message: {
            if let floor = challenge.achievedFloor {
                Text("到達階層: \(floor)階")
            }
        }
    }

    // MARK: - Completed Badge

    private var completedBadge: some View {
        HStack(spacing: 8) {
            Image(systemName: "checkmark.seal.fill")
                .foregroundColor(Color(hex: GameColors.success))
            Text("クリア済み")
                .font(.fantasyBody())
                .foregroundColor(Color(hex: GameColors.success))
            if let floor = challenge.achievedFloor {
                Text("到達: \(floor)階")
                    .font(.fantasyCaption())
                    .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
            }
        }
        .padding(.horizontal, 20)
        .padding(.vertical, 10)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(Color(hex: GameColors.success).opacity(0.15))
                .overlay(
                    RoundedRectangle(cornerRadius: 10)
                        .stroke(Color(hex: GameColors.success).opacity(0.5), lineWidth: 1.5)
                )
        )
    }

    // MARK: - Conditions List

    private var conditionsList: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("本日の条件")
                .font(.fantasyBody())
                .foregroundColor(Color(hex: GameColors.text).opacity(0.7))

            ForEach(challenge.conditions.indices, id: \.self) { index in
                let condition = challenge.conditions[index]
                HStack(spacing: 12) {
                    Image(systemName: condition.icon)
                        .font(.body)
                        .foregroundColor(Color(hex: GameColors.available))
                        .frame(width: 24)

                    Text(condition.description)
                        .font(.fantasyBody())
                        .foregroundColor(Color(hex: GameColors.text))

                    Spacer()
                }
                .padding(.horizontal, 16)
                .padding(.vertical, 12)
                .background(
                    RoundedRectangle(cornerRadius: 10)
                        .fill(Color(hex: GameColors.backgroundSecondary))
                        .overlay(
                            RoundedRectangle(cornerRadius: 10)
                                .stroke(Color(hex: GameColors.gridBorder).opacity(0.4), lineWidth: 1)
                        )
                )
            }
        }
    }

    // MARK: - Action Buttons

    private var actionButtons: some View {
        VStack(spacing: 12) {
            if !challenge.isCompleted {
                GameButton(title: "チャレンジ開始", icon: "play.fill", style: .primary, maxWidth: 240) {
                    AudioManager.shared.playSoundEffect(.buttonTap)
                    // pending チャレンジをサービスにセット（GameViewModel が startGame 時に読み取る）
                    DailyChallengeService.shared.pendingChallenge = challenge
                    AudioManager.shared.stopBGMMusic()
                    navigateToGame = true
                }
                .glow(color: Color(hex: GameColors.available), radius: 12, intensity: 0.5)
            } else {
                GameButton(title: "記録を確認", icon: "checkmark.seal.fill", style: .secondary, maxWidth: 240) {
                    showAlreadyCompleted = true
                }
            }

            GameButton(title: "戻る", style: .secondary, maxWidth: 240) {
                dismiss()
            }
        }
        .padding(.bottom, 32)
    }
}
