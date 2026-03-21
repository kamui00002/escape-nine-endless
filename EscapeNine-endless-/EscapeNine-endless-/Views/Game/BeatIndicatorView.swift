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
    var maxTurns: Int = Constants.baseTurns
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
        GeometryReader { geometry in
            let baseSize = ResponsiveLayout.beatIndicatorSize(for: geometry)
            let outerSize = baseSize
            let progressSize = baseSize * 0.867  // 78/90
            let glowSize = baseSize * 0.756      // 68/90
            let mainSize = baseSize * 0.533      // 48/90
            let fontSize = ResponsiveLayout.scaleFontSize(22, for: geometry)
            let strokeWidth = baseSize * 0.056   // 5/90
            let dotSpacing = baseSize * 0.056
            let activeDotSize = baseSize * 0.078 // 7/90
            let inactiveDotSize = baseSize * 0.056 // 5/90

            VStack(spacing: dotSpacing * 2) {
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
                        .frame(width: outerSize, height: outerSize)
                        .rotationEffect(.degrees(ringRotation))

                    // Progress ring background
                    Circle()
                        .stroke(
                            Color(hex: GameColors.gridBorder).opacity(0.2),
                            lineWidth: strokeWidth
                        )
                        .frame(width: progressSize, height: progressSize)

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
                            style: StrokeStyle(lineWidth: strokeWidth, lineCap: .round)
                        )
                        .frame(width: progressSize, height: progressSize)
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
                        .frame(width: glowSize, height: glowSize)
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
                                startRadius: mainSize * 0.1,
                                endRadius: mainSize * 0.52
                            )
                        )
                        .frame(width: mainSize, height: mainSize)
                        .scaleEffect(scale)
                        .shadow(color: countdownColor.opacity(0.8), radius: mainSize * 0.25)

                    // Countdown number
                    Text("\(turnCountdown)")
                        .font(.system(size: fontSize, weight: .black, design: .rounded))
                        .foregroundColor(.white)
                        .shadow(color: .black.opacity(0.3), radius: 2, y: 1)

                    // Warning pulse for countdown = 1
                    if turnCountdown == 1 {
                        Circle()
                            .stroke(Color(hex: GameColors.warning), lineWidth: 2)
                            .frame(width: outerSize, height: outerSize)
                            .scaleEffect(pulse ? 1.5 : 1.0)
                            .opacity(pulse ? 0.0 : 0.8)
                    }
                }

                // Turn indicator dots
                HStack(spacing: dotSpacing) {
                    ForEach(1...maxTurns, id: \.self) { turn in
                        let isActive = turn <= turnCount
                        Circle()
                            .fill(
                                isActive
                                ? Color(hex: GameColors.available)
                                : Color(hex: GameColors.gridBorder).opacity(0.25)
                            )
                            .frame(width: isActive ? activeDotSize : inactiveDotSize,
                                   height: isActive ? activeDotSize : inactiveDotSize)
                            .shadow(
                                color: isActive ? Color(hex: GameColors.available).opacity(0.5) : .clear,
                                radius: 3
                            )
                            .animation(.spring(response: 0.3, dampingFraction: 0.6), value: isActive)
                    }
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
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
