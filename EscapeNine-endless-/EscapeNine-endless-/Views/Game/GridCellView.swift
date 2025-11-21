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
    let cellSize: CGFloat // レスポンシブ対応のセルサイズ
    let characterSize: CGFloat // レスポンシブ対応のキャラクターサイズ

    @State private var isPressed = false

    var body: some View {
        Button(action: {
            // ハプティックフィードバック
            let impact = UIImpactFeedbackGenerator(style: .light)
            impact.impactOccurred()
            onTap()
        }) {
            ZStack {
                // 消失したマス
                if isDisappeared {
                    RoundedRectangle(cornerRadius: 12)
                        .fill(Color(hex: GameColors.disappeared))
                        .frame(width: cellSize, height: cellSize)
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
                        .frame(width: cellSize, height: cellSize)
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
                        .frame(width: cellSize, height: cellSize)
                        .opacity(disabled ? 0.5 : 1.0)
                        // 移動可能なマスにパルスエフェクト
                        .if(isAvailable) { view in
                            view.pulse(minScale: 0.98, maxScale: 1.02, duration: 0.8)
                        }
                        .glow(
                            color: isAvailable ? Color(hex: GameColors.available) : .clear,
                            radius: isAvailable ? 15 : 0,
                            intensity: isAvailable ? 0.8 : 0
                        )
                        .glow(
                            color: isSelected ? Color(hex: GameColors.available) : .clear,
                            radius: isSelected ? 20 : 0,
                            intensity: isSelected ? 0.6 : 0
                        )
                        .scaleEffect(isPressed ? 0.95 : 1.0)
                        .animation(.spring(response: 0.3, dampingFraction: 0.6), value: isPressed)
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
                        .frame(width: characterSize, height: characterSize)
                        .overlay(
                            Circle()
                                .stroke(Color(hex: GameColors.available), lineWidth: 3)
                        )
                        // プレイヤーにグローエフェクト
                        .glow(color: Color(hex: GameColors.player), radius: 15, intensity: 0.9)
                        // プレイヤーに軽いパルス
                        .pulse(minScale: 1.0, maxScale: 1.05, duration: 1.2)
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
                        .frame(width: characterSize, height: characterSize)
                        .overlay(
                            Circle()
                                .stroke(Color(hex: GameColors.warning), lineWidth: 3)
                        )
                        // 敵にグローエフェクト（脅威を強調）
                        .glow(color: Color(hex: GameColors.enemy), radius: 18, intensity: 1.0)
                        // 敵にパルス（プレイヤーより速く）
                        .pulse(minScale: 1.0, maxScale: 1.08, duration: 0.9)
                }
            }
        }
        .disabled(disabled || !isAvailable || isDisappeared)
        .buttonStyle(PlainButtonStyle()) // デフォルトのボタンアニメーションを無効化
        .simultaneousGesture(
            DragGesture(minimumDistance: 0)
                .onChanged { _ in isPressed = true }
                .onEnded { _ in isPressed = false }
        )
        .accessibilityLabel(accessibilityLabel)
        .accessibilityHint(accessibilityHint)
        .accessibilityAddTraits(accessibilityTraits)
    }

    // MARK: - Accessibility Helpers
    private var accessibilityLabel: String {
        var label = "マス\(position)"

        if isPlayer {
            label += "、プレイヤーの位置"
        }
        if isEnemy {
            label += "、敵の位置"
        }
        if isDisappeared {
            label += "、消失したマス"
        }
        if !isVisible {
            label += "、霧で見えないマス"
        }

        return label
    }

    private var accessibilityHint: String {
        if isDisappeared {
            return "このマスには移動できません"
        }
        if !isVisible {
            return "霧で見えません"
        }
        if isAvailable {
            return "タップして移動先に選択"
        }
        return "移動できないマスです"
    }

    private var accessibilityTraits: AccessibilityTraits {
        var traits: AccessibilityTraits = [.isButton]

        if isSelected {
            traits.insert(.isSelected)
        }
        if disabled || !isAvailable || isDisappeared {
            traits.insert(.isStaticText)
        }

        return traits
    }
}

// MARK: - View Extension for Conditional Modifiers
extension View {
    @ViewBuilder
    func `if`<Transform: View>(_ condition: Bool, transform: (Self) -> Transform) -> some View {
        if condition {
            transform(self)
        } else {
            self
        }
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

