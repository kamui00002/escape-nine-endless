//
//  TutorialHighlightView.swift
//  EscapeNine-endless-
//
//  Sprint 3 v1.1 動的オンボーディング — タップ可能マスを示す光るリング。
//  設計典拠: docs/onboarding-v1.1-design.md §3 Step 1 / §7 実装タスク分解。
//

import SwiftUI

/// チュートリアル中に「タップで移動できるマス」を示すための装飾オーバーレイ。
///
/// GridCellView の上に ZStack で重ねる前提のサイズドコンポーネント。
/// 位置は親が決めるため本 View 自体はセル 1 枚分のサイズしか持たない。
///
/// Reduce Motion: ON のときは pulse を止めて静的 glow にフォールバック
/// (Apple HIG Accessibility / WCAG 2.3.3)。
struct TutorialHighlightView: View {
    let cellSize: CGFloat

    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var isPulsing = false

    var body: some View {
        let ringSize = cellSize * 0.92
        let lineWidth: CGFloat = max(3, cellSize * 0.05)

        Circle()
            .stroke(Color(hex: GameColors.accent), lineWidth: lineWidth)
            .frame(width: ringSize, height: ringSize)
            .shadow(color: Color(hex: GameColors.accent).opacity(0.8), radius: 8)
            .shadow(color: Color(hex: GameColors.accent).opacity(0.5), radius: 16)
            .scaleEffect(reduceMotion ? 1.0 : (isPulsing ? 1.08 : 1.0))
            .opacity(reduceMotion ? 0.9 : (isPulsing ? 0.6 : 1.0))
            .onAppear {
                guard !reduceMotion else { return }
                withAnimation(.easeInOut(duration: 0.9).repeatForever(autoreverses: true)) {
                    isPulsing = true
                }
            }
            .accessibilityHidden(true)
    }
}

#Preview("iPhone — single cell", traits: .sizeThatFitsLayout) {
    ZStack {
        Color(hex: GameColors.background)
        RoundedRectangle(cornerRadius: 8)
            .fill(Color(hex: GameColors.backgroundSecondary))
            .frame(width: 90, height: 90)
        TutorialHighlightView(cellSize: 90)
    }
    .frame(width: 140, height: 140)
}

#Preview("iPad — large cell", traits: .sizeThatFitsLayout) {
    ZStack {
        Color(hex: GameColors.background)
        RoundedRectangle(cornerRadius: 12)
            .fill(Color(hex: GameColors.backgroundSecondary))
            .frame(width: 140, height: 140)
        TutorialHighlightView(cellSize: 140)
    }
    .frame(width: 200, height: 200)
}
