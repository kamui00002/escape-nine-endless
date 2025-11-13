//
//  GridCellView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct GridCellView: View {
    let position: Int
    let isPlayer: Bool
    let isEnemy: Bool
    let isAvailable: Bool // 移動可能なマスか
    let isSelected: Bool // 選択された移動先か
    let isVisible: Bool // 霧マップで見えるか
    let isDisappeared: Bool // 消失したマスか
    let onTap: () -> Void
    let disabled: Bool
    
    var body: some View {
        Button(action: {
            // アニメーションなしで即座に実行
            onTap()
        }) {
            ZStack {
                // 消失したマス
                if isDisappeared {
                    RoundedRectangle(cornerRadius: 12)
                        .fill(Color(hex: GameColors.disappeared))
                        .frame(width: 100, height: 100)
                        .overlay(
                            RoundedRectangle(cornerRadius: 12)
                                .stroke(Color(hex: GameColors.warning).opacity(0.3), lineWidth: 2)
                        )
                }
                // 霧で見えないマス
                else if !isVisible {
                    RoundedRectangle(cornerRadius: 12)
                        .fill(Color(hex: GameColors.fog))
                        .overlay(
                            RoundedRectangle(cornerRadius: 12)
                                .stroke(Color(hex: GameColors.gridBorder).opacity(0.2), lineWidth: 2)
                        )
                        .frame(width: 100, height: 100)
                        .opacity(0.4)
                }
                // 通常のマス
                else {
                    RoundedRectangle(cornerRadius: 12)
                        .fill(isSelected ? Color(hex: GameColors.available).opacity(0.3) : Color(hex: GameColors.grid))
                        .overlay(
                            RoundedRectangle(cornerRadius: 12)
                                .stroke(
                                    isAvailable ? Color(hex: GameColors.available) : Color(hex: GameColors.gridBorder),
                                    lineWidth: isAvailable ? 4 : 2
                                )
                        )
                        .frame(width: 100, height: 100)
                        .opacity(disabled ? 0.5 : 1.0)
                        .shadow(
                            color: isAvailable ? Color(hex: GameColors.available).opacity(0.6) : .clear,
                            radius: isAvailable ? 15 : 0
                        )
                        .shadow(
                            color: isSelected ? Color(hex: GameColors.available).opacity(0.4) : .clear,
                            radius: isSelected ? 20 : 0
                        )
                }
                
                // プレイヤー（見える場合のみ）
                if isPlayer && isVisible {
                    Circle()
                        .fill(
                            LinearGradient(
                                colors: [
                                    Color(hex: GameColors.player),
                                    Color(hex: GameColors.success)
                                ],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            )
                        )
                        .frame(width: 50, height: 50)
                        .shadow(color: Color(hex: GameColors.player).opacity(0.8), radius: 10)
                        .overlay(
                            Circle()
                                .stroke(Color(hex: GameColors.available), lineWidth: 3)
                        )
                }
                
                // 敵（見える場合のみ）
                if isEnemy && isVisible {
                    Circle()
                        .fill(
                            LinearGradient(
                                colors: [
                                    Color(hex: GameColors.enemy),
                                    Color(hex: GameColors.warning)
                                ],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            )
                        )
                        .frame(width: 50, height: 50)
                        .shadow(color: Color(hex: GameColors.enemy).opacity(0.8), radius: 10)
                        .overlay(
                            Circle()
                                .stroke(Color(hex: GameColors.warning), lineWidth: 3)
                        )
                }
            }
        }
        .disabled(disabled || !isAvailable || isDisappeared)
        .buttonStyle(PlainButtonStyle()) // デフォルトのボタンアニメーションを無効化
    }
}

// MARK: - Color Extension
extension Color {
    init(hex: String) {
        let hex = hex.trimmingCharacters(in: CharacterSet.alphanumerics.inverted)
        var int: UInt64 = 0
        Scanner(string: hex).scanHexInt64(&int)
        let a, r, g, b: UInt64
        switch hex.count {
        case 3: // RGB (12-bit)
            (a, r, g, b) = (255, (int >> 8) * 17, (int >> 4 & 0xF) * 17, (int & 0xF) * 17)
        case 6: // RGB (24-bit)
            (a, r, g, b) = (255, int >> 16, int >> 8 & 0xFF, int & 0xFF)
        case 8: // ARGB (32-bit)
            (a, r, g, b) = (int >> 24, int >> 16 & 0xFF, int >> 8 & 0xFF, int & 0xFF)
        default:
            (a, r, g, b) = (255, 0, 0, 0)
        }
        self.init(
            .sRGB,
            red: Double(r) / 255,
            green: Double(g) / 255,
            blue: Double(b) / 255,
            opacity: Double(a) / 255
        )
    }
}

