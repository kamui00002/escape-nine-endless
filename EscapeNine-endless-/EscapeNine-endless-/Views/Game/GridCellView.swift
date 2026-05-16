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
    let playerSprite: String? // プレイヤーのスプライト名
    let enemySprite: String? // 敵のスプライト名

    @State private var isPressed = false
    @State private var availablePulse = false

    var body: some View {
        Button(action: {
            HapticsHelper.impact(isAvailable ? .medium : .light)
            onTap()
        }) {
            ZStack {
                // 消失したマス
                if isDisappeared {
                    RoundedRectangle(cornerRadius: 12)
                        .fill(
                            LinearGradient(
                                colors: [
                                    Color(hex: GameColors.disappeared),
                                    Color(hex: GameColors.disappeared).opacity(0.7)
                                ],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            )
                        )
                        .frame(width: cellSize, height: cellSize)
                        .overlay(
                            RoundedRectangle(cornerRadius: 12)
                                .stroke(Color(hex: GameColors.warning).opacity(0.3), lineWidth: 2)
                        )
                        .overlay(
                            Image(systemName: "xmark")
                                .font(.system(size: cellSize * 0.25, weight: .thin))
                                .foregroundColor(Color(hex: GameColors.warning).opacity(0.3))
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
                        .overlay(
                            Image(systemName: "cloud.fog.fill")
                                .font(.system(size: cellSize * 0.2))
                                .foregroundColor(Color(hex: GameColors.text).opacity(0.15))
                        )
                }
                // 通常のマス
                else {
                    ZStack {
                        // ベースのグリッドセル
                        RoundedRectangle(cornerRadius: 12)
                            .fill(
                                // Into the Breach 風: マス背景色で陣営識別
                                isPlayer
                                ? LinearGradient(
                                    colors: [
                                        Color(hex: GameColors.player).opacity(0.45),
                                        Color(hex: GameColors.player).opacity(0.15)
                                    ],
                                    startPoint: .topLeading,
                                    endPoint: .bottomTrailing
                                )
                                : isEnemy
                                ? LinearGradient(
                                    colors: [
                                        Color(hex: GameColors.enemy).opacity(0.45),
                                        Color(hex: GameColors.enemy).opacity(0.15)
                                    ],
                                    startPoint: .topLeading,
                                    endPoint: .bottomTrailing
                                )
                                : isSelected
                                ? LinearGradient(
                                    colors: [
                                        Color(hex: GameColors.available).opacity(0.35),
                                        Color(hex: GameColors.available).opacity(0.15)
                                    ],
                                    startPoint: .topLeading,
                                    endPoint: .bottomTrailing
                                )
                                : LinearGradient(
                                    colors: [
                                        Color(hex: GameColors.grid),
                                        Color(hex: GameColors.grid).opacity(0.85)
                                    ],
                                    startPoint: .topLeading,
                                    endPoint: .bottomTrailing
                                )
                            )
                            .overlay(
                                RoundedRectangle(cornerRadius: 12)
                                    .stroke(
                                        isPlayer
                                        ? LinearGradient(
                                            colors: [
                                                Color(hex: GameColors.player),
                                                Color(hex: GameColors.player).opacity(0.6)
                                            ],
                                            startPoint: .topLeading,
                                            endPoint: .bottomTrailing
                                        )
                                        : isEnemy
                                        ? LinearGradient(
                                            colors: [
                                                Color(hex: GameColors.enemy),
                                                Color(hex: GameColors.enemy).opacity(0.6)
                                            ],
                                            startPoint: .topLeading,
                                            endPoint: .bottomTrailing
                                        )
                                        : isAvailable
                                        ? LinearGradient(
                                            colors: [
                                                Color(hex: GameColors.available),
                                                Color(hex: GameColors.available).opacity(0.6)
                                            ],
                                            startPoint: .topLeading,
                                            endPoint: .bottomTrailing
                                        )
                                        : LinearGradient(
                                            colors: [
                                                Color(hex: GameColors.gridBorder),
                                                Color(hex: GameColors.gridBorder).opacity(0.5)
                                            ],
                                            startPoint: .topLeading,
                                            endPoint: .bottomTrailing
                                        ),
                                        lineWidth: (isPlayer || isEnemy) ? 2.5 : (isAvailable ? 3 : 1.5)
                                    )
                            )
                            .frame(width: cellSize, height: cellSize)

                        // 移動可能マスの内側パルス
                        if isAvailable && !disabled {
                            RoundedRectangle(cornerRadius: 12)
                                .fill(Color(hex: GameColors.available).opacity(0.1))
                                .frame(width: cellSize - 4, height: cellSize - 4)
                                .scaleEffect(availablePulse ? 1.0 : 0.85)
                                .opacity(availablePulse ? 0.0 : 0.4)
                                .animation(
                                    .easeInOut(duration: 1.2).repeatForever(autoreverses: false),
                                    value: availablePulse
                                )
                        }

                        // 選択済みチェックマーク
                        if isSelected {
                            Image(systemName: "arrow.down.circle.fill")
                                .font(.system(size: cellSize * 0.2, weight: .bold))
                                .foregroundColor(Color(hex: GameColors.available))
                                .offset(x: cellSize * 0.3, y: -cellSize * 0.3)
                        }
                    }
                    .opacity(disabled ? 0.5 : 1.0)
                    .glow(
                        color: isAvailable ? Color(hex: GameColors.available) : .clear,
                        radius: isAvailable ? 12 : 0,
                        intensity: isAvailable ? 0.6 : 0
                    )
                    .glow(
                        color: isSelected ? Color(hex: GameColors.available) : .clear,
                        radius: isSelected ? 18 : 0,
                        intensity: isSelected ? 0.5 : 0
                    )
                    .scaleEffect(isPressed ? 0.93 : 1.0)
                    .animation(.spring(response: 0.25, dampingFraction: 0.6), value: isPressed)
                }
                
                // プレイヤー（見える場合のみ） — Into the Breach 風: シャープなピクセル + 円ハロー無し
                if isPlayer && isVisible {
                    if let sprite = playerSprite {
                        Image(sprite)
                            .resizable()
                            .interpolation(.none)  // Nearest-Neighbor 補間でピクセル感を維持
                            .scaledToFit()
                            .frame(width: characterSize, height: characterSize)
                            // 地面に落ちる薄い影だけ (オーラ系は廃止、マス背景色で陣営識別)
                            .shadow(color: Color.black.opacity(0.45), radius: 2, x: 0, y: 2)
                            // 軽いパルスは生存サインとして残す (キャラ自体のスケール変化)
                            .pulse(minScale: 1.0, maxScale: 1.03, duration: 1.4)
                    } else {
                        // フォールバック: 元のCircle表示
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
                            .glow(color: Color(hex: GameColors.player), radius: 15, intensity: 0.9)
                            .pulse(minScale: 1.0, maxScale: 1.05, duration: 1.2)
                    }
                }
                
                // 敵（見える場合のみ） — Into the Breach 風: シャープなピクセル + 円ハロー無し
                if isEnemy && isVisible {
                    if let sprite = enemySprite {
                        Image(sprite)
                            .resizable()
                            .interpolation(.none)  // Nearest-Neighbor 補間でピクセル感を維持
                            .scaledToFit()
                            .frame(width: characterSize, height: characterSize)
                            // 敵は赤みのある影で「脅威」サイン (オーラ系は廃止、マス背景色で陣営識別)
                            .shadow(color: Color(hex: GameColors.enemy).opacity(0.55), radius: 3, x: 0, y: 2)
                            // 敵は少し速めのパルス (プレイヤー 1.4s に対し 1.0s)
                            .pulse(minScale: 1.0, maxScale: 1.05, duration: 1.0)
                    } else {
                        // フォールバック: 元のCircle表示
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
                            .glow(color: Color(hex: GameColors.enemy), radius: 18, intensity: 1.0)
                            .pulse(minScale: 1.0, maxScale: 1.08, duration: 0.9)
                    }
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
        .onAppear {
            availablePulse = true
        }
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

