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
    
    var body: some View {
        VStack(spacing: 8) {
            ZStack {
                // 外側の光るリング
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
                
                // メインの円
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
                
                Text("♪")
                    .font(.system(size: 24))
                    .foregroundColor(.white)
            }
            
            Text("Beat: \(currentBeat)")
                .font(.fantasyCaption())
                .foregroundColor(Color(hex: GameColors.textSecondary))
        }
        .onChange(of: currentBeat) {
            withAnimation(.spring(response: 0.2, dampingFraction: 0.6)) {
                scale = 1.4
                pulse = true
            }
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) {
                withAnimation(.spring(response: 0.2, dampingFraction: 0.6)) {
                    scale = 1.0
                    pulse = false
                }
            }
        }
    }
}

