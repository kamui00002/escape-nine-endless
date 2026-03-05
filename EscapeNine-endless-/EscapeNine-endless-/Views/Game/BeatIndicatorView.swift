//
//  BeatIndicatorView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct BeatIndicatorView: View {
    let currentBeat: Int
    @State private var scale: CGFloat = 1.0
    @State private var pulse: Bool = false
    @State private var progress: Double = 1.0
    @State private var timer: Timer?

    private let audioManager = AudioManager.shared

    var body: some View {
        VStack(spacing: 8) {
            ZStack {
                // Progress ring background
                Circle()
                    .stroke(
                        Color(hex: GameColors.gridBorder).opacity(0.3),
                        lineWidth: 6
                    )
                    .frame(width: 80, height: 80)

                // Progress ring countdown
                Circle()
                    .trim(from: 0, to: progress)
                    .stroke(
                        LinearGradient(
                            colors: [
                                Color(hex: GameColors.available),
                                Color(hex: GameColors.main)
                            ],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        ),
                        style: StrokeStyle(lineWidth: 6, lineCap: .round)
                    )
                    .frame(width: 80, height: 80)
                    .rotationEffect(.degrees(-90))
                    .animation(.linear(duration: 0.05), value: progress)

                // Outer glow ring (on beat)
                Circle()
                    .stroke(
                        LinearGradient(
                            colors: [
                                Color(hex: GameColors.available).opacity(0.8),
                                Color(hex: GameColors.main).opacity(0.4)
                            ],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        ),
                        lineWidth: 3
                    )
                    .frame(width: 70, height: 70)
                    .scaleEffect(pulse ? 1.2 : 1.0)
                    .opacity(pulse ? 0.3 : 0.6)

                // Main circle
                Circle()
                    .fill(
                        LinearGradient(
                            colors: [
                                Color(hex: GameColors.available),
                                Color(hex: GameColors.main)
                            ],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        )
                    )
                    .frame(width: 50, height: 50)
                    .scaleEffect(scale)
                    .shadow(color: Color(hex: GameColors.available).opacity(0.8), radius: 15)

                // Beat number
                Text("\(currentBeat)")
                    .font(.system(size: 18, weight: .bold, design: .rounded))
                    .foregroundColor(.white)
            }

            // Turn indicator (shows as dots)
            HStack(spacing: 4) {
                ForEach(1...Constants.maxTurns, id: \.self) { turn in
                    Circle()
                        .fill(turn <= currentBeat % (Constants.maxTurns + 1)
                              ? Color(hex: GameColors.available)
                              : Color(hex: GameColors.gridBorder).opacity(0.3))
                        .frame(width: 6, height: 6)
                }
            }
        }
        .onAppear {
            startTimer()
        }
        .onDisappear {
            stopTimer()
        }
        .onChange(of: currentBeat) {
            withAnimation(.spring(response: 0.2, dampingFraction: 0.6)) {
                scale = 1.4
                pulse = true
                progress = 1.0
            }
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) {
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
