//
//  BPMInfoView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct BPMInfoView: View {
    let floor: Int
    let bpm: Double

    private var speedLevel: String {
        switch bpm {
        case ..<80: return "Slow"
        case 80..<120: return "Normal"
        case 120..<180: return "Fast"
        case 180..<220: return "Extreme"
        default: return "MAX"
        }
    }

    private var speedColor: Color {
        switch bpm {
        case ..<80: return Color(hex: GameColors.success)
        case 80..<120: return Color(hex: GameColors.available)
        case 120..<180: return Color(hex: GameColors.textSecondary)
        case 180..<220: return Color(hex: GameColors.warning)
        default: return Color(hex: GameColors.enemy)
        }
    }

    var body: some View {
        GeometryReader { geometry in
            let bpmPadding = ResponsiveLayout.bpmInfoPadding(for: geometry)
            let divHeight = geometry.size.height * 0.6
            let speedFontSize = ResponsiveLayout.scaleFontSize(14, for: geometry)
            let hSpacing = ResponsiveLayout.isIPad() ? CGFloat(20) : CGFloat(16)

            HStack(spacing: hSpacing) {
                // Floor
                VStack(spacing: 4) {
                    Text("階層")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.6))

                    Text("\(floor)")
                        .font(.fantasyNumber())
                        .foregroundColor(Color(hex: GameColors.available))
                }

                // Divider
                Rectangle()
                    .fill(
                        LinearGradient(
                            colors: [
                                Color(hex: GameColors.gridBorder).opacity(0.1),
                                Color(hex: GameColors.gridBorder).opacity(0.5),
                                Color(hex: GameColors.gridBorder).opacity(0.1)
                            ],
                            startPoint: .top,
                            endPoint: .bottom
                        )
                    )
                    .frame(width: 1, height: divHeight)

                // BPM
                VStack(spacing: 4) {
                    Text("BPM")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.6))

                    Text("\(Int(bpm))")
                        .font(.fantasyNumber())
                        .foregroundColor(Color(hex: GameColors.textSecondary))
                }

                // Divider
                Rectangle()
                    .fill(
                        LinearGradient(
                            colors: [
                                Color(hex: GameColors.gridBorder).opacity(0.1),
                                Color(hex: GameColors.gridBorder).opacity(0.5),
                                Color(hex: GameColors.gridBorder).opacity(0.1)
                            ],
                            startPoint: .top,
                            endPoint: .bottom
                        )
                    )
                    .frame(width: 1, height: divHeight)

                // Speed Level
                VStack(spacing: 4) {
                    Text("速度")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.6))

                    Text(speedLevel)
                        .font(.system(size: speedFontSize, weight: .bold, design: .serif))
                        .foregroundColor(speedColor)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .padding(.horizontal, bpmPadding.horizontal)
            .padding(.vertical, bpmPadding.vertical)
            .background(
                RoundedRectangle(cornerRadius: 16)
                    .fill(Color(hex: GameColors.backgroundSecondary))
                    .overlay(
                        RoundedRectangle(cornerRadius: 16)
                            .stroke(
                                LinearGradient(
                                    colors: [
                                        Color(hex: GameColors.available).opacity(0.4),
                                        Color(hex: GameColors.main).opacity(0.2)
                                    ],
                                    startPoint: .topLeading,
                                    endPoint: .bottomTrailing
                                ),
                                lineWidth: 1.5
                            )
                    )
            )
            .shadow(color: Color(hex: GameColors.available).opacity(0.15), radius: 8)
        }
        .frame(height: ResponsiveLayout.isIPad() ? 82 : 66)
    }
}
