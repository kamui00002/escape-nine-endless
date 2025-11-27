//
//  ResultView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct ResultView: View {
    let floor: Int
    let result: GameStatus
    let onPlayAgain: () -> Void
    let onHome: () -> Void

    @StateObject private var playerViewModel = PlayerViewModel()
    @StateObject private var adMobService = AdMobService.shared
    @State private var adShown = false

    var body: some View {
        ZStack {
            // 背景
            LinearGradient(
                colors: [
                    Color(hex: GameColors.background),
                    Color(hex: GameColors.backgroundSecondary)
                ],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea()
            
            VStack(spacing: 40) {
                Spacer()
                
                // Result Text
                Text(result == .win ? "VICTORY!" : "DEFEAT")
                    .font(.fantasyTitle())
                    .foregroundColor(result == .win ? Color(hex: GameColors.success) : Color(hex: GameColors.warning))
                    .overlay(
                        (result == .win ?
                         LinearGradient(
                             colors: [
                                 Color(hex: GameColors.success),
                                 Color(hex: GameColors.available)
                             ],
                             startPoint: .leading,
                             endPoint: .trailing
                         ) :
                         LinearGradient(
                             colors: [
                                 Color(hex: GameColors.warning),
                                 Color(hex: GameColors.enemy)
                             ],
                             startPoint: .leading,
                             endPoint: .trailing
                         ))
                        .mask(Text(result == .win ? "VICTORY!" : "DEFEAT").font(.fantasyTitle()))
                    )
                    .shadow(color: (result == .win ? Color(hex: GameColors.success) : Color(hex: GameColors.warning)).opacity(0.5), radius: 15)
                
                // Floor
                VStack(spacing: 8) {
                    Text("到達階層")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                    
                    Text("\(floor)")
                        .font(.fantasyNumber())
                        .foregroundColor(Color(hex: GameColors.available))
                }
                
                // New Record
                if floor > playerViewModel.highestFloor {
                    Text("新記録達成！")
                        .font(.fantasySubheading())
                        .foregroundColor(Color(hex: GameColors.available))
                        .padding(.horizontal, 20)
                        .padding(.vertical, 12)
                        .background(
                            RoundedRectangle(cornerRadius: 12)
                                .fill(Color(hex: GameColors.available).opacity(0.2))
                                .overlay(
                                    RoundedRectangle(cornerRadius: 12)
                                        .stroke(Color(hex: GameColors.available).opacity(0.5), lineWidth: 2)
                                )
                        )
                }
                
                // Highest Floor
                VStack(spacing: 4) {
                    Text("最高到達階層")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                    
                    Text("\(playerViewModel.highestFloor)")
                        .font(.fantasyNumber())
                        .foregroundColor(Color(hex: GameColors.textSecondary))
                }
                
                Spacer()
                
                // Buttons
                VStack(spacing: 16) {
                    Button(action: { 
                        AudioManager.shared.playSoundEffect(.buttonTap)
                        onPlayAgain()
                    }) {
                        Text("再挑戦")
                            .font(.fantasyBody())
                            .foregroundColor(.white)
                            .frame(maxWidth: 280)
                            .padding()
                            .background(
                                LinearGradient(
                                    colors: [
                                        Color(hex: GameColors.available),
                                        Color(hex: GameColors.main)
                                    ],
                                    startPoint: .leading,
                                    endPoint: .trailing
                                )
                            )
                            .cornerRadius(16)
                            .shadow(color: Color(hex: GameColors.available).opacity(0.6), radius: 15)
                    }
                    
                    Button(action: { 
                        AudioManager.shared.playSoundEffect(.buttonTap)
                        onHome()
                    }) {
                        Text("ホームへ")
                            .font(.fantasyBody())
                            .foregroundColor(Color(hex: GameColors.text))
                            .frame(maxWidth: 280)
                            .padding()
                            .background(
                                RoundedRectangle(cornerRadius: 16)
                                    .fill(Color(hex: GameColors.backgroundSecondary))
                                    .overlay(
                                        RoundedRectangle(cornerRadius: 16)
                                            .stroke(Color(hex: GameColors.gridBorder).opacity(0.5), lineWidth: 2)
                                    )
                            )
                    }
                }
                
                Spacer()
            }
            .padding()
        }
        .onAppear {
            // インタースティシャル広告を表示（1回のみ）
            if !adShown {
                adShown = true
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                    InterstitialAdPresenter.show { success in
                        if success {
                            print("[ResultView] インタースティシャル広告表示成功")
                        }
                    }
                }
            }
        }
    }
}

