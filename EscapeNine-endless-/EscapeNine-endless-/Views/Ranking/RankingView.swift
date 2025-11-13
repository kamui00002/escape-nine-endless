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
        ZStack {
            LinearGradient(
                colors: [
                    Color(hex: GameColors.background),
                    Color(hex: GameColors.backgroundSecondary)
                ],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea()
            
            VStack(spacing: 20) {
                // Header
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
                
                // My Record
                VStack(spacing: 12) {
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
                .padding()
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
                .padding(.horizontal)
                
                // Global Ranking
                Text("世界ランキング")
                    .font(.fantasySubheading())
                    .foregroundColor(Color(hex: GameColors.text))
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.horizontal)
                
                if viewModel.isLoading {
                    Spacer()
                    ProgressView()
                        .progressViewStyle(CircularProgressViewStyle(tint: Color(hex: GameColors.available)))
                    Spacer()
                } else if viewModel.rankings.isEmpty {
                    Spacer()
                    Text("まだランキングがありません")
                        .font(.fantasyBody())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                    Spacer()
                } else {
                    List(viewModel.rankings.indices, id: \.self) { index in
                        HStack {
                            Text("#\(index + 1)")
                                .font(.fantasyNumber())
                                .foregroundColor(Color(hex: GameColors.available))
                                .frame(width: 60)
                            
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
                                .padding(.vertical, 4)
                        )
                        .listRowSeparator(.hidden)
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
}

