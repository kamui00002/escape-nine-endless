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
                
                
                VStack(spacing: 50) {
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
                            .shadow(color: Color(hex: GameColors.available).opacity(0.5), radius: 10)
                        
                        Text("Endless Dungeon")
                            .font(.fantasySubheading())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.8))
                    }
                    
                    Spacer()
                    
                    // Buttons
                    VStack(spacing: 20) {
                        Button(action: {
                            showGame = true
                        }) {
                            Text("冒険を始める")
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
                            showRanking = true
                        }) {
                            Text("ランキング")
                                .font(.fantasyBody())
                                .foregroundColor(Color(hex: GameColors.text))
                                .frame(maxWidth: 280)
                                .padding()
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
                        
                        Button(action: {
                            showCharacterSelection = true
                        }) {
                            Text("キャラクター")
                                .font(.fantasyBody())
                                .foregroundColor(Color(hex: GameColors.text))
                                .frame(maxWidth: 280)
                                .padding()
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
                        
                        Button(action: {
                            showSettings = true
                        }) {
                            Text("設定")
                                .font(.fantasyBody())
                                .foregroundColor(Color(hex: GameColors.text))
                                .frame(maxWidth: 280)
                                .padding()
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
                    }
                    
                    // Highest Floor
                    VStack(spacing: 8) {
                        Text("最高到達階層")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                        
                        Text("\(playerViewModel.highestFloor)")
                            .font(.fantasyNumber())
                            .foregroundColor(Color(hex: GameColors.available))
                    }
                    .padding(.top, 30)
                    
                    Spacer()
                }
                .padding()
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

