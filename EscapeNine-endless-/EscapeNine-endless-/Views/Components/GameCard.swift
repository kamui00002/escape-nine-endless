//
//  GameCard.swift
//  EscapeNine-endless-
//
//  再利用可能なカードコンポーネント
//

import SwiftUI

struct GameCard<Content: View>: View {
    var title: String? = nil
    var borderColor: Color? = nil
    var isHighlighted: Bool = false
    @ViewBuilder let content: () -> Content

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            if let title = title {
                Text(title)
                    .font(.fantasySubheading())
                    .foregroundColor(Color(hex: GameColors.text))
            }

            content()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding()
        .background(
            RoundedRectangle(cornerRadius: 16)
                .fill(Color(hex: GameColors.backgroundSecondary))
                .overlay(
                    RoundedRectangle(cornerRadius: 16)
                        .stroke(
                            LinearGradient(
                                colors: strokeColors,
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            ),
                            lineWidth: isHighlighted ? 3 : 2
                        )
                )
        )
    }

    private var strokeColors: [Color] {
        if let borderColor = borderColor {
            return [borderColor.opacity(0.5), borderColor.opacity(0.3)]
        }
        if isHighlighted {
            return [Color(hex: GameColors.available), Color(hex: GameColors.available).opacity(0.5)]
        }
        return [
            Color(hex: GameColors.gridBorder).opacity(0.5),
            Color(hex: GameColors.main).opacity(0.3)
        ]
    }
}
