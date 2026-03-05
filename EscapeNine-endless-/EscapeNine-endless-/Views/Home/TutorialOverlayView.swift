//
//  TutorialOverlayView.swift
//  EscapeNine-endless-
//
//  Initial tutorial overlay for first-time players
//

import SwiftUI

struct TutorialOverlayView: View {
    @Binding var isShowing: Bool
    @State private var currentPage = 0

    private let pages: [(title: String, description: String, icon: String)] = [
        (
            "ようこそ！",
            "「Escape Nine」はビートに合わせて\n鬼から逃げるリズムゲームです。",
            "music.note"
        ),
        (
            "移動方法",
            "ビートが来る前に\n移動先のマスをタップしてください。\n上下左右に1マス移動できます。",
            "arrow.up.arrow.down"
        ),
        (
            "スキルを使おう",
            "各キャラクターには固有のスキルがあります。\nスキルボタンをタップして発動！\n使用回数に注意しましょう。",
            "sparkles"
        ),
        (
            "10ターン逃げ切れ！",
            "10ターン逃げ切れば階層クリア。\n階層が上がるとBPMが加速！\n100階層を目指しましょう。",
            "flag.fill"
        )
    ]

    var body: some View {
        ZStack {
            Color.black.opacity(0.85)
                .ignoresSafeArea()

            VStack(spacing: 30) {
                Spacer()

                // Icon
                Image(systemName: pages[currentPage].icon)
                    .font(.system(size: 60))
                    .foregroundColor(Color(hex: GameColors.available))
                    .id(currentPage) // Force animation on change

                // Title
                Text(pages[currentPage].title)
                    .font(.fantasyHeading())
                    .foregroundColor(Color(hex: GameColors.text))

                // Description
                Text(pages[currentPage].description)
                    .font(.fantasyBody())
                    .foregroundColor(Color(hex: GameColors.text).opacity(0.8))
                    .multilineTextAlignment(.center)
                    .lineSpacing(8)
                    .padding(.horizontal, 40)

                Spacer()

                // Page indicator
                HStack(spacing: 8) {
                    ForEach(0..<pages.count, id: \.self) { index in
                        Circle()
                            .fill(index == currentPage ? Color(hex: GameColors.available) : Color(hex: GameColors.text).opacity(0.3))
                            .frame(width: 8, height: 8)
                    }
                }

                // Navigation buttons
                HStack(spacing: 20) {
                    if currentPage > 0 {
                        Button(action: {
                            withAnimation {
                                currentPage -= 1
                            }
                        }) {
                            Text("前へ")
                                .font(.fantasyBody())
                                .foregroundColor(Color(hex: GameColors.text))
                                .padding(.horizontal, 30)
                                .padding(.vertical, 14)
                                .background(
                                    RoundedRectangle(cornerRadius: 12)
                                        .fill(Color(hex: GameColors.backgroundSecondary))
                                        .overlay(
                                            RoundedRectangle(cornerRadius: 12)
                                                .stroke(Color(hex: GameColors.gridBorder).opacity(0.5), lineWidth: 1)
                                        )
                                )
                        }
                    }

                    Button(action: {
                        if currentPage < pages.count - 1 {
                            withAnimation {
                                currentPage += 1
                            }
                        } else {
                            // Mark tutorial as complete
                            UserDefaults.standard.set(true, forKey: "tutorialCompleted")
                            withAnimation {
                                isShowing = false
                            }
                        }
                    }) {
                        Text(currentPage < pages.count - 1 ? "次へ" : "始める！")
                            .font(.fantasyBody())
                            .foregroundColor(.white)
                            .padding(.horizontal, 30)
                            .padding(.vertical, 14)
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
                            .cornerRadius(12)
                    }
                }

                // Skip button
                if currentPage < pages.count - 1 {
                    Button(action: {
                        UserDefaults.standard.set(true, forKey: "tutorialCompleted")
                        withAnimation {
                            isShowing = false
                        }
                    }) {
                        Text("スキップ")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.5))
                    }
                }

                Spacer().frame(height: 40)
            }
        }
        .transition(.opacity)
    }
}
