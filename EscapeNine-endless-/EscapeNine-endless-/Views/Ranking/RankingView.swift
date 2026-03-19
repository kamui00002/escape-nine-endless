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
    @StateObject private var gameCenterService = GameCenterService.shared
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

                    // Game Center Button
                    if gameCenterService.isAuthenticated {
                        Button(action: {
                            AudioManager.shared.playSoundEffect(.buttonTap)
                            gameCenterService.presentLeaderboard()
                        }) {
                            HStack(spacing: 8) {
                                Image(systemName: "globe")
                                Text("世界ランキング (Game Center)")
                            }
                            .font(.fantasyBody())
                            .foregroundColor(.white)
                            .frame(maxWidth: .infinity)
                            .padding(12)
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
                            .cornerRadius(12)
                        }
                        .padding(.horizontal, ResponsiveLayout.padding(for: geometry))
                        .padding(.bottom, 8)
                        .sheet(isPresented: $gameCenterService.showLeaderboard) {
                            GameCenterLeaderboardView()
                        }
                    }

                    // Tab Selector
                    Picker("", selection: Binding(
                        get: { viewModel.selectedTab },
                        set: { viewModel.selectTab($0) }
                    )) {
                        Text("プレイ履歴").tag(RankingTab.local)
                        Text("クラウド").tag(RankingTab.cloud)
                    }
                    .pickerStyle(.segmented)
                    .padding(.horizontal, ResponsiveLayout.padding(for: geometry))
                    .padding(.top, 4)
                    .padding(.bottom, 4)

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
                                .font(.system(size: ResponsiveLayout.isIPad() ? 58 : 48))
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
                    } else if currentEntries.isEmpty {
                        Spacer()
                        VStack(spacing: 12) {
                            Image(systemName: "chart.bar.fill")
                                .font(.system(size: ResponsiveLayout.isIPad() ? 58 : 48))
                                .foregroundColor(Color(hex: GameColors.text).opacity(0.5))

                            Text("まだランキングがありません")
                                .font(.fantasyBody())
                                .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                        }
                        Spacer()
                    } else {
                        List(currentEntries.indices, id: \.self) { index in
                            rankingRow(entry: currentEntries[index], index: index, geometry: geometry)
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

    // タブに応じた統一エントリ（floor, name, characterType, date）
    private var currentEntries: [(floor: Int, name: String, characterType: String, date: String)] {
        switch viewModel.selectedTab {
        case .local:
            return viewModel.rankings.map {
                (floor: $0.floor, name: $0.playerName, characterType: $0.characterType, date: $0.formattedDate)
            }
        case .cloud:
            let formatter = DateFormatter()
            formatter.dateStyle = .short
            formatter.timeStyle = .short
            return viewModel.cloudRankings.map {
                (floor: $0.floor, name: $0.displayName, characterType: $0.characterType,
                 date: formatter.string(from: $0.timestamp))
            }
        }
    }

    @ViewBuilder
    private func rankingRow(
        entry: (floor: Int, name: String, characterType: String, date: String),
        index: Int,
        geometry: GeometryProxy
    ) -> some View {
        HStack {
            Text("#\(index + 1)")
                .font(.fantasyNumber())
                .foregroundColor(
                    index == 0 ? Color(hex: GameColors.available) :
                    index == 1 ? Color(hex: GameColors.textSecondary) :
                    index == 2 ? Color(hex: GameColors.main) :
                    Color(hex: GameColors.text).opacity(0.7)
                )
                .frame(width: ResponsiveLayout.isIPad() ? 80 : 60)

            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Text(characterEmoji(for: entry.characterType))
                    Text(entry.name)
                        .font(.fantasyBody())
                        .foregroundColor(Color(hex: GameColors.text))
                }
                Text(entry.date)
                    .font(.system(size: 10))
                    .foregroundColor(Color(hex: GameColors.text).opacity(0.5))
            }

            Spacer()

            Text("\(entry.floor)階")
                .font(.fantasyNumber())
                .foregroundColor(Color(hex: GameColors.textSecondary))
        }
        .listRowBackground(
            RoundedRectangle(cornerRadius: 8)
                .fill(Color(hex: GameColors.backgroundSecondary))
                .padding(.vertical, ResponsiveLayout.isIPad() ? 6 : 4)
        )
        .listRowSeparator(.hidden)
        .padding(.horizontal, ResponsiveLayout.padding(for: geometry))
    }

    private func characterEmoji(for type: String) -> String {
        switch type {
        case "hero": return "🗡️"
        case "thief": return "🗡️"
        case "wizard": return "🔮"
        case "elf": return "🏹"
        default: return "⚔️"
        }
    }
}

