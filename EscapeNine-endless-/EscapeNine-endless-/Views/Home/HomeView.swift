//
//  HomeView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct HomeView: View {
    @StateObject private var playerViewModel = PlayerViewModel()
    @State private var showGame = false
    @State private var showRanking = false
    @State private var showSettings = false
    @State private var showCharacterSelection = false
    
    var body: some View {
        NavigationStack {
            GeometryReader { geometry in
                ZStack {
                    // 背景グラデーション
                    LinearGradient(
                        colors: [
                            Color(hex: GameColors.background),
                            Color(hex: GameColors.backgroundSecondary)
                        ],
                        startPoint: .top,
                        endPoint: .bottom
                    )
                    .ignoresSafeArea()

                    // 背景パーティクル
                    ParticleView(particleCount: 50)
                        .opacity(0.8)
                        .ignoresSafeArea()
                    
                    let buttonWidth = ResponsiveLayout.buttonWidth(for: geometry)
                    let spacing = ResponsiveLayout.isIPad() ? 30 : 20
                    
                    VStack(spacing: ResponsiveLayout.isIPad() ? 60 : 50) {
                        Spacer()
                        
                        // Title
                        VStack(spacing: 16) {
                            Text("ESCAPE NINE")
                                .font(.fantasyTitle())
                                .foregroundColor(Color(hex: GameColors.available))
                                .overlay(
                                    LinearGradient(
                                        colors: [
                                            Color(hex: GameColors.available),
                                            Color(hex: GameColors.main)
                                        ],
                                        startPoint: .leading,
                                        endPoint: .trailing
                                    )
                                    .mask(Text("ESCAPE NINE").font(.fantasyTitle()))
                                )
                                .glow(color: Color(hex: GameColors.available), radius: 8, intensity: 0.3)
                                .shimmer(duration: 3.0)
                                .bounceIn(delay: 0.2)

                            Text("Endless Dungeon")
                                .font(.fantasySubheading())
                                .foregroundColor(Color(hex: GameColors.text).opacity(0.8))
                                .slideIn(from: .bottom, delay: 0.4)
                        }
                        
                        Spacer()
                        
                        // Buttons
                        VStack(spacing: CGFloat(spacing)) {
                            Button(action: {
                                showGame = true
                            }) {
                                Text("冒険を始める")
                                    .font(.fantasyBody())
                                    .foregroundColor(.white)
                                    .frame(maxWidth: buttonWidth)
                                    .padding(ResponsiveLayout.isIPad() ? 18 : 16)
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
                                    .glow(color: Color(hex: GameColors.available), radius: 10, intensity: 0.4)
                            }
                            .pressableButton(scale: 0.96, shadowRadius: 12)
                            .slideIn(from: .leading, delay: 0.6)
                            .pulse(minScale: 1.0, maxScale: 1.02, duration: 2.0)
                            
                            Button(action: {
                                showCharacterSelection = true
                            }) {
                                Text("キャラクター")
                                    .font(.fantasyBody())
                                    .foregroundColor(Color(hex: GameColors.text))
                                    .frame(maxWidth: buttonWidth)
                                    .padding(ResponsiveLayout.isIPad() ? 18 : 16)
                                    .background(
                                        RoundedRectangle(cornerRadius: 16)
                                            .fill(Color(hex: GameColors.backgroundSecondary))
                                            .overlay(
                                                RoundedRectangle(cornerRadius: 16)
                                                    .stroke(
                                                        LinearGradient(
                                                            colors: [
                                                                Color(hex: GameColors.gridBorder).opacity(0.5),
                                                                Color(hex: GameColors.main).opacity(0.3)
                                                            ],
                                                            startPoint: .topLeading,
                                                            endPoint: .bottomTrailing
                                                        ),
                                                        lineWidth: 2
                                                    )
                                            )
                                    )
                            }
                            .pressableButton()
                            .slideIn(from: .leading, delay: 0.7)
                            
                            Button(action: {
                                showRanking = true
                            }) {
                                Text("ランキング")
                                    .font(.fantasyBody())
                                    .foregroundColor(Color(hex: GameColors.text))
                                    .frame(maxWidth: buttonWidth)
                                    .padding(ResponsiveLayout.isIPad() ? 18 : 16)
                                    .background(
                                        RoundedRectangle(cornerRadius: 16)
                                            .fill(Color(hex: GameColors.backgroundSecondary))
                                            .overlay(
                                                RoundedRectangle(cornerRadius: 16)
                                                    .stroke(
                                                        LinearGradient(
                                                            colors: [
                                                                Color(hex: GameColors.gridBorder).opacity(0.5),
                                                                Color(hex: GameColors.main).opacity(0.3)
                                                            ],
                                                            startPoint: .topLeading,
                                                            endPoint: .bottomTrailing
                                                        ),
                                                        lineWidth: 2
                                                    )
                                            )
                                    )
                            }
                            .pressableButton()
                            .slideIn(from: .leading, delay: 0.8)
                            
                            Button(action: {
                                showSettings = true
                            }) {
                                Text("設定")
                                    .font(.fantasyBody())
                                    .foregroundColor(Color(hex: GameColors.text))
                                    .frame(maxWidth: buttonWidth)
                                    .padding(ResponsiveLayout.isIPad() ? 18 : 16)
                                    .background(
                                        RoundedRectangle(cornerRadius: 16)
                                            .fill(Color(hex: GameColors.backgroundSecondary))
                                            .overlay(
                                                RoundedRectangle(cornerRadius: 16)
                                                    .stroke(
                                                        LinearGradient(
                                                            colors: [
                                                                Color(hex: GameColors.gridBorder).opacity(0.5),
                                                                Color(hex: GameColors.main).opacity(0.3)
                                                            ],
                                                            startPoint: .topLeading,
                                                            endPoint: .bottomTrailing
                                                        ),
                                                        lineWidth: 2
                                                    )
                                            )
                                    )
                            }
                            .pressableButton()
                            .slideIn(from: .leading, delay: 0.9)
                        }
                        
                        // Highest Floor
                        VStack(spacing: 8) {
                            Text("最高到達階層")
                                .font(.fantasyCaption())
                                .foregroundColor(Color(hex: GameColors.text).opacity(0.7))

                            AnimatedNumber(value: playerViewModel.highestFloor, duration: 1.0)
                                .font(.fantasyNumber())
                                .foregroundColor(Color(hex: GameColors.available))
                                .glow(color: Color(hex: GameColors.available), radius: 6, intensity: 0.3)
                        }
                        .padding(.top, ResponsiveLayout.isIPad() ? 40 : 30)
                        .slideIn(from: .bottom, delay: 1.0)
                        
                        Spacer()
                    }
                    .padding(ResponsiveLayout.padding(for: geometry))
                }
            }
            .navigationDestination(isPresented: $showGame) {
                GameView()
            }
            .navigationDestination(isPresented: $showRanking) {
                RankingView()
            }
            .navigationDestination(isPresented: $showSettings) {
                SettingsView()
            }
            .navigationDestination(isPresented: $showCharacterSelection) {
                CharacterSelectionView()
            }
        }
    }
}

