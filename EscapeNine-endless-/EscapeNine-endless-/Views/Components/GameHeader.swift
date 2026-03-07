//
//  GameHeader.swift
//  EscapeNine-endless-
//
//  再利用可能なナビゲーションヘッダー
//

import SwiftUI

struct GameHeader: View {
    let title: String
    var onBack: (() -> Void)? = nil

    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(spacing: 0) {
            LinearGradient(
                colors: [
                    Color(hex: GameColors.background),
                    Color(hex: GameColors.backgroundSecondary)
                ],
                startPoint: .top,
                endPoint: .bottom
            )
            .frame(height: ResponsiveLayout.isIPad() ? 100 : 80)
            .overlay(
                HStack {
                    Button(action: {
                        AudioManager.shared.playSoundEffect(.buttonTap)
                        if let onBack = onBack {
                            onBack()
                        } else {
                            dismiss()
                        }
                    }) {
                        HStack(spacing: 4) {
                            Image(systemName: "chevron.left")
                            Text("戻る")
                        }
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text))
                        .padding(.horizontal, 12)
                        .padding(.vertical, 6)
                        .background(
                            RoundedRectangle(cornerRadius: 8)
                                .fill(Color(hex: GameColors.backgroundSecondary))
                                .overlay(
                                    RoundedRectangle(cornerRadius: 8)
                                        .stroke(Color(hex: GameColors.gridBorder).opacity(0.5), lineWidth: 1)
                                )
                        )
                    }

                    Spacer()

                    Text(title)
                        .font(.fantasyHeading())
                        .foregroundColor(Color(hex: GameColors.text))

                    Spacer()

                    Color.clear
                        .frame(width: 80)
                }
                .padding()
            )
        }
    }
}
