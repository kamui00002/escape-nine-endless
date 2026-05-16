//
//  DangerZoneView.swift
//  EscapeNine-endless-
//
//  Sprint 3 v1.1 動的オンボーディング — 鬼の隣接 8 マスを示す危険圏マーカー。
//  設計典拠: docs/onboarding-v1.1-design.md §3 Step 2 / §5 色覚多様性。
//

import SwiftUI

/// 鬼の隣接 8 マスに重ねる「危険圏」オーバーレイ (セル 1 枚分)。
///
/// 色覚多様性配慮: 赤色 1 色に依存せず、**斜線パターン** + **⚠️ アイコン** の
/// 3 要素を併用する (multi-agent-reviewer HIGH 反映 / 設計書 §5)。
///
/// 親 View は鬼の隣接 8 マスそれぞれにこの View を ZStack で重ねる前提。
/// 位置計算 (1〜9 のグリッド座標) は親の責務。
struct DangerZoneView: View {
    let cellSize: CGFloat

    var body: some View {
        ZStack {
            // 1. 半透明赤背景 (主) — 警告色
            Color(hex: GameColors.warning)
                .opacity(0.32)

            // 2. 斜線パターン (副) — 色覚多様性配慮
            DiagonalStripes(spacing: max(6, cellSize * 0.12), lineWidth: 1.5)
                .stroke(Color.white.opacity(0.45), lineWidth: 1.5)

            // 3. 警告アイコン (副) — 形でも識別可能
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: cellSize * 0.32, weight: .bold))
                .foregroundColor(Color(hex: GameColors.warning))
                .shadow(color: Color.black.opacity(0.4), radius: 2)
        }
        .frame(width: cellSize, height: cellSize)
        .accessibilityLabel("危険圏")
        .accessibilityHint("鬼が隣接しています。このマスに移動すると捕まります")
    }
}

/// 斜線パターンの Shape。`spacing` 間隔で 45 度の平行線を引く。
private struct DiagonalStripes: Shape {
    let spacing: CGFloat
    let lineWidth: CGFloat

    func path(in rect: CGRect) -> Path {
        var path = Path()
        let diagonalReach = rect.width + rect.height
        var x: CGFloat = -rect.height
        while x < diagonalReach {
            path.move(to: CGPoint(x: x, y: 0))
            path.addLine(to: CGPoint(x: x + rect.height, y: rect.height))
            x += spacing
        }
        return path
    }
}

#Preview("iPhone — danger cell", traits: .sizeThatFitsLayout) {
    ZStack {
        Color(hex: GameColors.background)
        RoundedRectangle(cornerRadius: 8)
            .fill(Color(hex: GameColors.backgroundSecondary))
            .frame(width: 90, height: 90)
        DangerZoneView(cellSize: 90)
    }
    .frame(width: 140, height: 140)
}

#Preview("iPad — danger cell", traits: .sizeThatFitsLayout) {
    ZStack {
        Color(hex: GameColors.background)
        RoundedRectangle(cornerRadius: 12)
            .fill(Color(hex: GameColors.backgroundSecondary))
            .frame(width: 140, height: 140)
        DangerZoneView(cellSize: 140)
    }
    .frame(width: 200, height: 200)
}

#Preview("3x3 ring around center", traits: .sizeThatFitsLayout) {
    let cellSize: CGFloat = 70
    return ZStack {
        Color(hex: GameColors.background)
        VStack(spacing: 4) {
            ForEach(0..<3, id: \.self) { row in
                HStack(spacing: 4) {
                    ForEach(0..<3, id: \.self) { col in
                        ZStack {
                            RoundedRectangle(cornerRadius: 6)
                                .fill(Color(hex: GameColors.backgroundSecondary))
                                .frame(width: cellSize, height: cellSize)
                            // 中央 (1, 1) が鬼、それ以外が危険圏
                            if row == 1 && col == 1 {
                                Image(systemName: "person.fill")
                                    .font(.system(size: cellSize * 0.5))
                                    .foregroundColor(.red)
                            } else {
                                DangerZoneView(cellSize: cellSize)
                            }
                        }
                    }
                }
            }
        }
    }
    .frame(width: 260, height: 260)
}
