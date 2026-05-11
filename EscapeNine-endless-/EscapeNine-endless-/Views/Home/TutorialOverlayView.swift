//
//  TutorialOverlayView.swift
//  EscapeNine-endless-
//
//  Tutorial overlay for first-time players (3-page swipeable, no skip button).
//  Sprint 1 Issue 03: 3 画面のスワイプ式チュートリアル + 初回のみ表示。
//

import SwiftUI

struct TutorialOverlayView: View {
    let onComplete: () -> Void
    @State private var currentPage = 0

    private struct Page {
        let title: String
        let icon: String
        let description: String
    }

    private let pages: [Page] = [
        Page(
            title: "影が動き出す",
            icon: "figure.run",
            description: "9 マスの結界の中で、影 (敵) があなたを追います"
        ),
        Page(
            title: "1 手で動く",
            icon: "hand.tap",
            description: "あなたは隣接マスに 1 手だけ動けます"
        ),
        Page(
            title: "逃げ切れ",
            icon: "flag.checkered",
            description: "影に捕まらないよう、何階まで逃げ切れるか挑戦しましょう"
        )
    ]

    var body: some View {
        ZStack {
            GameBackground()
                .ignoresSafeArea()

            VStack(spacing: 32) {
                Spacer()

                TabView(selection: $currentPage) {
                    ForEach(0..<pages.count, id: \.self) { index in
                        VStack(spacing: 24) {
                            Image(systemName: pages[index].icon)
                                .font(.system(size: 80))
                                .foregroundColor(Color(hex: GameColors.available))
                                .glow(color: Color(hex: GameColors.available), radius: 12, intensity: 0.5)

                            Text(pages[index].title)
                                .font(.fantasyTitle())
                                .foregroundColor(Color(hex: GameColors.text))
                                .multilineTextAlignment(.center)

                            Text(pages[index].description)
                                .font(.fantasyBody())
                                .foregroundColor(Color(hex: GameColors.textSecondary))
                                .multilineTextAlignment(.center)
                                .lineSpacing(6)
                                .padding(.horizontal, 40)
                        }
                        .tag(index)
                    }
                }
                .tabViewStyle(.page(indexDisplayMode: .always))
                .indexViewStyle(.page(backgroundDisplayMode: .always))
                .frame(maxHeight: 400)

                Spacer()

                // 最後の画面でのみ「はじめる」ボタン表示。
                // スキップボタンは置かない (会議録: 小林の指示)。
                if currentPage == pages.count - 1 {
                    GameButton(title: "はじめる", style: .primary, maxWidth: 280) {
                        onComplete()
                    }
                    .glow(color: Color(hex: GameColors.available), radius: 15, intensity: 0.6)
                    .transition(.opacity.combined(with: .scale))
                }

                Spacer().frame(height: 40)
            }
            .animation(.easeInOut(duration: 0.25), value: currentPage)
        }
    }
}

// MARK: - Previews

#Preview {
    TutorialOverlayView {
        // no-op
    }
}
