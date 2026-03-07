//
//  GameButton.swift
//  EscapeNine-endless-
//
//  再利用可能なボタンコンポーネント（プライマリ/セカンダリ）
//

import SwiftUI

enum GameButtonStyle {
    case primary
    case secondary
    case danger
}

struct GameButton: View {
    let title: String
    var icon: String? = nil
    var style: GameButtonStyle = .primary
    var maxWidth: CGFloat? = nil
    var withSound: Bool = true
    let action: () -> Void

    var body: some View {
        Button(action: {
            if withSound {
                AudioManager.shared.playSoundEffect(.buttonTap)
            }
            action()
        }) {
            HStack(spacing: 10) {
                if let icon = icon {
                    Image(systemName: icon)
                        .font(.system(size: 16, weight: .semibold))
                }
                Text(title)
                    .font(.fantasyBody())
            }
            .foregroundColor(foregroundColor)
            .frame(maxWidth: maxWidth ?? .infinity)
            .padding(ResponsiveLayout.isIPad() ? 18 : 16)
            .background(backgroundView)
        }
        .pressableButton(scale: style == .primary ? 0.96 : 0.95, shadowRadius: style == .primary ? 12 : 8)
    }

    private var foregroundColor: Color {
        switch style {
        case .primary, .danger:
            return .white
        case .secondary:
            return Color(hex: GameColors.text)
        }
    }

    @ViewBuilder
    private var backgroundView: some View {
        switch style {
        case .primary:
            LinearGradient(
                colors: [
                    Color(hex: GameColors.available),
                    Color(hex: GameColors.main)
                ],
                startPoint: .leading,
                endPoint: .trailing
            )
            .clipShape(RoundedRectangle(cornerRadius: 16))
        case .secondary:
            RoundedRectangle(cornerRadius: 16)
                .fill(Color(hex: GameColors.backgroundSecondary))
                .overlay(
                    RoundedRectangle(cornerRadius: 16)
                        .stroke(
                            LinearGradient(
                                colors: [
                                    Color(hex: GameColors.gridBorder).opacity(0.5),
                                    Color(hex: GameColors.main).opacity(0.3)
                                ],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            ),
                            lineWidth: 2
                        )
                )
        case .danger:
            LinearGradient(
                colors: [
                    Color(hex: GameColors.warning),
                    Color(hex: GameColors.enemy)
                ],
                startPoint: .leading,
                endPoint: .trailing
            )
            .clipShape(RoundedRectangle(cornerRadius: 16))
        }
    }
}
