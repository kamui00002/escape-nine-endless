//
//  BeatIndicatorView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct BeatIndicatorView: View {
    let turnCountdown: Int // 3, 2, 1
    let turnCount: Int
    @State private var scale: CGFloat = 1.0
    @State private var pulse: Bool = false
    @State private var progress: Double = 1.0
    @State private var timer: Timer?
    @State private var ringRotation: Double = 0

    private let audioManager = AudioManager.shared

    // カウントダウンに応じた色
    private var countdownColor: Color {
        switch turnCountdown {
        case 3: return Color(hex: GameColors.available) // 金
        case 2: return Color.orange // オレンジ
        case 1: return Color(hex: GameColors.warning) // 赤
        default: return Color(hex: GameColors.available)
        }
    }

    var body: some View {
        VStack(spacing: 10) {
            ZStack {
                // Outer decorative ring
                Circle()
                    .stroke(
                        AngularGradient(
                            colors: [
                                countdownColor.opacity(0.1),
                                countdownColor.opacity(0.3),
                                countdownColor.opacity(0.1)
                            ],
                            center: .center
                        ),
                        lineWidth: 2
                    )
                    .frame(width: 90, height: 90)
                    .rotationEffect(.degrees(ringRotation))

                // Progress ring background
                Circle()
                    .stroke(
                        Color(hex: GameColors.gridBorder).opacity(0.2),
                        lineWidth: 5
                    )
                    .frame(width: 78, height: 78)

                // Progress ring countdown
                Circle()
                    .trim(from: 0, to: progress)
                    .stroke(
                        LinearGradient(
                            colors: [
                                countdownColor,
                                countdownColor.opacity(0.6)
                            ],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        ),
                        style: StrokeStyle(lineWidth: 5, lineCap: .round)
                    )
                    .frame(width: 78, height: 78)
                    .rotationEffect(.degrees(-90))
                    .animation(.linear(duration: 0.05), value: progress)

                // Outer glow ring (on beat)
                Circle()
                    .stroke(
                        LinearGradient(
                            colors: [
                                countdownColor.opacity(0.8),
                                countdownColor.opacity(0.4)
                            ],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        ),
                        lineWidth: 3
                    )
                    .frame(width: 68, height: 68)
                    .scaleEffect(pulse ? 1.3 : 1.0)
                    .opacity(pulse ? 0.0 : 0.6)

                // Main circle with inner gradient
                Circle()
                    .fill(
                        RadialGradient(
                            colors: [
                                countdownColor.opacity(0.9),
                                countdownColor.opacity(0.5)
                            ],
                            center: .center,
                            startRadius: 5,
                            endRadius: 25
                        )
                    )
                    .frame(width: 48, height: 48)
                    .scaleEffect(scale)
                    .shadow(color: countdownColor.opacity(0.8), radius: 12)

                // Countdown number
                Text("\(turnCountdown)")
                    .font(.system(size: 22, weight: .black, design: .rounded))
                    .foregroundColor(.white)
                    .shadow(color: .black.opacity(0.3), radius: 2, y: 1)

                // Warning pulse for countdown = 1
                if turnCountdown == 1 {
                    Circle()
                        .stroke(Color(hex: GameColors.warning), lineWidth: 2)
                        .frame(width: 90, height: 90)
                        .scaleEffect(pulse ? 1.5 : 1.0)
                        .opacity(pulse ? 0.0 : 0.8)
                }
            }

            // Turn indicator dots
            HStack(spacing: 5) {
                ForEach(1...Constants.maxTurns, id: \.self) { turn in
                    let isActive = turn <= turnCount
                    Circle()
                        .fill(
                            isActive
                            ? Color(hex: GameColors.available)
                            : Color(hex: GameColors.gridBorder).opacity(0.25)
                        )
                        .frame(width: isActive ? 7 : 5, height: isActive ? 7 : 5)
                        .shadow(
                            color: isActive ? Color(hex: GameColors.available).opacity(0.5) : .clear,
                            radius: 3
                        )
                        .animation(.spring(response: 0.3, dampingFraction: 0.6), value: isActive)
                }
            }
        }
        .onAppear {
            startTimer()
            withAnimation(.linear(duration: 8.0).repeatForever(autoreverses: false)) {
                ringRotation = 360
            }
        }
        .onDisappear {
            stopTimer()
        }
        .onChange(of: turnCountdown) {
            withAnimation(.spring(response: 0.15, dampingFraction: 0.5)) {
                scale = 1.5
                pulse = true
                progress = 1.0
            }
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.12) {
                withAnimation(.spring(response: 0.2, dampingFraction: 0.6)) {
                    scale = 1.0
                    pulse = false
                }
            }
        }
    }

    // MARK: - Timer Management
    private func startTimer() {
        timer = Timer.scheduledTimer(withTimeInterval: 0.05, repeats: true) { _ in
            progress = audioManager.timeUntilNextBeat()
        }
    }

    private func stopTimer() {
        timer?.invalidate()
        timer = nil
    }
}
