//
//  SettingsView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct SettingsView: View {
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
            
            VStack(spacing: 24) {
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
                    
                    Text("設定")
                        .font(.fantasyHeading())
                        .foregroundColor(Color(hex: GameColors.text))
                    
                    Spacer()
                    
                    Color.clear
                        .frame(width: 80)
                }
                .padding()
                
                // Player Info
                VStack(alignment: .leading, spacing: 16) {
                    Text("冒険者情報")
                        .font(.fantasySubheading())
                        .foregroundColor(Color(hex: GameColors.text))
                    
                    VStack(alignment: .leading, spacing: 12) {
                        HStack {
                            Text("最高到達階層")
                                .font(.fantasyCaption())
                                .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                            Spacer()
                            Text("\(playerViewModel.highestFloor)")
                                .font(.fantasyNumber())
                                .foregroundColor(Color(hex: GameColors.available))
                        }
                        
                        Divider()
                            .background(Color(hex: GameColors.gridBorder).opacity(0.3))
                        
                        HStack {
                            Text("選択キャラクター")
                                .font(.fantasyCaption())
                                .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                            Spacer()
                            Text(playerViewModel.selectedCharacter.name)
                                .font(.fantasyBody())
                                .foregroundColor(Color(hex: GameColors.textSecondary))
                        }
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
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
                .padding(.horizontal)
                
                // Volume Settings
                VStack(alignment: .leading, spacing: 16) {
                    Text("サウンド設定")
                        .font(.fantasySubheading())
                        .foregroundColor(Color(hex: GameColors.text))
                    
                    VStack(alignment: .leading, spacing: 16) {
                        // BGM音量
                        VStack(alignment: .leading, spacing: 8) {
                            HStack {
                                Text("BGM")
                                    .font(.fantasyCaption())
                                    .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                                Spacer()
                                Text("\(Int(playerViewModel.bgmVolume * 100))%")
                                    .font(.fantasyNumber())
                                    .foregroundColor(Color(hex: GameColors.available))
                            }
                            
                            Slider(value: $playerViewModel.bgmVolume, in: 0...1) { _ in
                                playerViewModel.saveData()
                            }
                            .tint(Color(hex: GameColors.available))
                        }
                        
                        Divider()
                            .background(Color(hex: GameColors.gridBorder).opacity(0.3))
                        
                        // 効果音量
                        VStack(alignment: .leading, spacing: 8) {
                            HStack {
                                Text("効果音")
                                    .font(.fantasyCaption())
                                    .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                                Spacer()
                                Text("\(Int(playerViewModel.seVolume * 100))%")
                                    .font(.fantasyNumber())
                                    .foregroundColor(Color(hex: GameColors.available))
                            }
                            
                            Slider(value: $playerViewModel.seVolume, in: 0...1) { _ in
                                playerViewModel.saveData()
                            }
                            .tint(Color(hex: GameColors.available))
                        }
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
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
                .padding(.horizontal)
                
                // About
                VStack(alignment: .leading, spacing: 16) {
                    Text("アプリについて")
                        .font(.fantasySubheading())
                        .foregroundColor(Color(hex: GameColors.text))
                    
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Escape Nine: Endless")
                            .font(.fantasyBody())
                            .foregroundColor(Color(hex: GameColors.textSecondary))
                        
                        Text("バージョン 1.0.0")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                        
                        Divider()
                            .background(Color(hex: GameColors.gridBorder).opacity(0.3))
                        
                        Text("リズムに合わせてダンジョンを攻略する\nエンドレスチャレンジゲーム")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.8))
                            .lineSpacing(6)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
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
                .padding(.horizontal)
                
                Spacer()
            }
        }
    }
}

