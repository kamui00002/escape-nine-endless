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
    
    var body: some View {
        HStack(spacing: 12) {
            // 階層表示
            VStack(spacing: 4) {
                Text("階層")
                    .font(.fantasyCaption())
                    .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                
                Text("\(floor)")
                    .font(.fantasyNumber())
                    .foregroundColor(Color(hex: GameColors.available))
            }
            
            // 区切り線
            Rectangle()
                .fill(Color(hex: GameColors.gridBorder).opacity(0.5))
                .frame(width: 1, height: 40)
            
            // BPM表示
            VStack(spacing: 4) {
                Text("BPM")
                    .font(.fantasyCaption())
                    .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                
                Text("\(Int(bpm))")
                    .font(.fantasyNumber())
                    .foregroundColor(Color(hex: GameColors.textSecondary))
            }
        }
        .padding(.horizontal, 20)
        .padding(.vertical, 12)
        .background(
            RoundedRectangle(cornerRadius: 16)
                .fill(Color(hex: GameColors.backgroundSecondary))
                .overlay(
                    RoundedRectangle(cornerRadius: 16)
                        .stroke(
                            LinearGradient(
                                colors: [
                                    Color(hex: GameColors.available).opacity(0.5),
                                    Color(hex: GameColors.main).opacity(0.3)
                                ],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            ),
                            lineWidth: 2
                        )
                )
        )
        .shadow(color: Color(hex: GameColors.available).opacity(0.2), radius: 10)
    }
}

