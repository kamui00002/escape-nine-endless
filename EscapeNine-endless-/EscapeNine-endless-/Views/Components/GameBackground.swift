//
//  GameBackground.swift
//  EscapeNine-endless-
//
//  再利用可能な背景グラデーション
//

import SwiftUI

struct GameBackground: View {
    var showParticles: Bool = false
    var particleCount: Int = 50

    var body: some View {
        ZStack {
            LinearGradient(
                colors: [
                    Color(hex: GameColors.background),
                    Color(hex: GameColors.backgroundSecondary)
                ],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea()

            if showParticles {
                ParticleView(particleCount: particleCount)
                    .opacity(0.8)
                    .ignoresSafeArea()
            }
        }
    }
}

#Preview("iPhone") {
    GameBackground(showParticles: true)
}

#Preview("iPad") {
    GameBackground(showParticles: true)
        .previewDevice("iPad Pro 13-inch (M4)")
}
