//
//  RankingView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct RankingView: View {
    @StateObject private var viewModel = RankingViewModel()
    @StateObject private var playerViewModel = PlayerViewModel()
    @Environment(\.dismiss) var dismiss
    
    var body: some View {
        GeometryReader { geometry in
            ZStack(alignment: .top) {
                LinearGradient(
                    colors: [
                        Color(hex: GameColors.background),
                        Color(hex: GameColors.backgroundSecondary)
                    ],
                    startPoint: .top,
                    endPoint: .bottom
                )
                .ignoresSafeArea()
                
                VStack(spacing: 0) {
                    // Header with background
                    VStack(spacing: 0) {
                        LinearGradient(
                            colors: [
                                Color(hex: GameColors.background),
                                Color(hex: GameColors.backgroundSecondary)
                            ],
                            startPoint: .top,
                            endPoint: .bottom
                        )
                        .frame(height: ResponsiveLayout.isIPad() ? 100 : 80)
                        .overlay(
                            HStack {
                                Button(action: { dismiss() }) {
                                    HStack(spacing: 4) {
                                        Image(systemName: "chevron.left")
                                        Text("戻る")
                                    }
                                    .font(.fantasyCaption())
                                    .foregroundColor(Color(hex: GameColors.text))
                                    .padding(.horizontal, 12)
                                    .padding(.vertical, 6)
                                    .background(
                                        RoundedRectangle(cornerRadius: 8)
                                            .fill(Color(hex: GameColors.backgroundSecondary))
                                            .overlay(
                                                RoundedRectangle(cornerRadius: 8)
                                                    .stroke(Color(hex: GameColors.gridBorder).opacity(0.5), lineWidth: 1)
                                            )
                                    )
                                }

                                Spacer()

                                Text("ランキング")
                                    .font(.fantasyHeading())
                                    .foregroundColor(Color(hex: GameColors.text))

                                Spacer()

                                Color.clear
                                    .frame(width: 80)
                            }
                            .padding()
                        )
                    }
                    
                    // My Record
                    VStack(spacing: ResponsiveLayout.isIPad() ? 16 : 12) {
                        Text("あなたの記録")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                        
                        Text("\(playerViewModel.highestFloor)")
                            .font(.fantasyNumber())
                            .foregroundColor(Color(hex: GameColors.available))
                        
                        Text("階層")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                    }
                    .frame(maxWidth: .infinity)
                    .padding(ResponsiveLayout.isIPad() ? 20 : 16)
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
                    .padding(.horizontal, ResponsiveLayout.padding(for: geometry))
                    .padding(.top, 8)
                    .padding(.bottom, 8)
                    
                    // Global Ranking
                    Text("世界ランキング")
                        .font(.fantasySubheading())
                        .foregroundColor(Color(hex: GameColors.text))
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal, ResponsiveLayout.padding(for: geometry))
                        .padding(.top, ResponsiveLayout.isIPad() ? 4 : 2)
                        .padding(.bottom, ResponsiveLayout.isIPad() ? 4 : 2)
                    
                    // Content Area
                    if viewModel.isLoading {
                        Spacer()
                        ProgressView()
                            .progressViewStyle(CircularProgressViewStyle(tint: Color(hex: GameColors.available)))
                        Spacer()
                    } else if viewModel.hasError {
                        Spacer()
                        VStack(spacing: 16) {
                            Image(systemName: "exclamationmark.triangle.fill")
                                .font(.system(size: 48))
                                .foregroundColor(Color(hex: GameColors.warning))

                            if let errorMessage = viewModel.errorMessage {
                                Text(errorMessage)
                                    .font(.fantasyBody())
                                    .foregroundColor(Color(hex: GameColors.text).opacity(0.9))
                                    .multilineTextAlignment(.center)
                                    .padding(.horizontal)
                            }

                            Button(action: {
                                Task {
                                    await viewModel.retry()
                                }
                            }) {
                                Text("再試行")
                                    .font(.fantasyBody())
                                    .foregroundColor(.white)
                                    .padding(.horizontal, 24)
                                    .padding(.vertical, 12)
                                    .background(
                                        LinearGradient(
                                            colors: [
                                                Color(hex: GameColors.main),
                                                Color(hex: GameColors.accent)
                                            ],
                                            startPoint: .leading,
                                            endPoint: .trailing
                                        )
                                    )
                                    .cornerRadius(12)
                            }
                        }
                        Spacer()
                    } else if viewModel.rankings.isEmpty {
                        Spacer()
                        VStack(spacing: 12) {
                            Image(systemName: "chart.bar.fill")
                                .font(.system(size: 48))
                                .foregroundColor(Color(hex: GameColors.text).opacity(0.5))

                            Text("まだランキングがありません")
                                .font(.fantasyBody())
                                .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                        }
                        Spacer()
                    } else {
                        List(viewModel.rankings.indices, id: \.self) { index in
                            HStack {
                                Text("#\(index + 1)")
                                    .font(.fantasyNumber())
                                    .foregroundColor(Color(hex: GameColors.available))
                                    .frame(width: ResponsiveLayout.isIPad() ? 80 : 60)
                                
                                Text(viewModel.rankings[index].playerName)
                                    .font(.fantasyBody())
                                    .foregroundColor(Color(hex: GameColors.text))
                                
                                Spacer()
                                
                                HStack(spacing: 4) {
                                    Text("\(viewModel.rankings[index].floor)")
                                        .font(.fantasyNumber())
                                        .foregroundColor(Color(hex: GameColors.textSecondary))
                                    Text("階層")
                                        .font(.fantasyCaption())
                                        .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                                }
                            }
                            .listRowBackground(
                                RoundedRectangle(cornerRadius: 8)
                                    .fill(Color(hex: GameColors.backgroundSecondary))
                                    .padding(.vertical, ResponsiveLayout.isIPad() ? 6 : 4)
                            )
                            .listRowSeparator(.hidden)
                            .padding(.horizontal, ResponsiveLayout.padding(for: geometry))
                        }
                        .listStyle(PlainListStyle())
                        .scrollContentBackground(.hidden)
                    }
                }
            }
            .task {
                await viewModel.fetchRankings()
            }
        }
        .toolbar(.hidden, for: .navigationBar)
        .navigationBarBackButtonHidden(true)
        .navigationBarTitleDisplayMode(.inline)
    }
}

