//
//  SettingsView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct SettingsView: View {
    @StateObject private var playerViewModel = PlayerViewModel()
    @StateObject private var purchaseManager = PurchaseManager.shared
    @Environment(\.dismiss) var dismiss
    
    var body: some View {
        GeometryReader { geometry in
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

                                Text("設定")
                                    .font(.fantasyHeading())
                                    .foregroundColor(Color(hex: GameColors.text))

                                Spacer()

                                Color.clear
                                    .frame(width: 80)
                            }
                            .padding()
                        )
                    }
                    
                    // Scrollable Content
                    ScrollView {
                        VStack(spacing: 24) {
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
                            
                            // MARK: - Purchase Section (課金設定)
                            VStack(alignment: .leading, spacing: 16) {
                                Text("課金設定")
                                    .font(.fantasySubheading())
                                    .foregroundColor(Color(hex: GameColors.text))
                                
                                VStack(alignment: .leading, spacing: 16) {
                                    // 広告削除
                                    VStack(alignment: .leading, spacing: 8) {
                                        HStack {
                                            VStack(alignment: .leading, spacing: 4) {
                                                Text("広告削除")
                                                    .font(.fantasyBody())
                                                    .foregroundColor(Color(hex: GameColors.textSecondary))
                                                
                                                Text("すべての広告を非表示にします")
                                                    .font(.fantasyCaption())
                                                    .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                                            }
                                            
                                            Spacer()
                                            
                                            if purchaseManager.isAdRemoved {
                                                Text("購入済み")
                                                    .font(.fantasyCaption())
                                                    .foregroundColor(Color(hex: GameColors.available))
                                                    .padding(.horizontal, 12)
                                                    .padding(.vertical, 6)
                                                    .background(
                                                        RoundedRectangle(cornerRadius: 8)
                                                            .fill(Color(hex: GameColors.available).opacity(0.2))
                                                    )
                                            } else {
                                                Button(action: {
                                                    Task {
                                                        _ = await purchaseManager.purchaseAdRemoval()
                                                    }
                                                }) {
                                                    Text(purchaseManager.adRemovalPrice)
                                                        .font(.fantasyNumber())
                                                        .foregroundColor(.white)
                                                        .padding(.horizontal, 16)
                                                        .padding(.vertical, 8)
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
                                                        .cornerRadius(8)
                                                }
                                            }
                                        }
                                    }
                                    
                                    Divider()
                                        .background(Color(hex: GameColors.gridBorder).opacity(0.3))
                                    
                                    // 購入の復元
                                    Button(action: {
                                        Task {
                                            await purchaseManager.restorePurchases()
                                        }
                                    }) {
                                        HStack {
                                            Text("購入を復元")
                                                .font(.fantasyBody())
                                                .foregroundColor(Color(hex: GameColors.textSecondary))
                                            
                                            Spacer()
                                            
                                            Image(systemName: "arrow.clockwise")
                                                .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                                        }
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
                            
                            // MARK: - Debug/Admin Section (管理者用 - 後で削除可能)
                            VStack(alignment: .leading, spacing: 16) {
                                Text("管理者用設定")
                                    .font(.fantasySubheading())
                                    .foregroundColor(Color(hex: GameColors.warning))
                                
                                VStack(alignment: .leading, spacing: 16) {
                                    // 開始階層選択
                                    VStack(alignment: .leading, spacing: 8) {
                                        Text("開始階層")
                                            .font(.fantasyCaption())
                                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                                        
                                        Picker("開始階層", selection: $playerViewModel.debugStartFloor) {
                                            ForEach(1...Constants.maxFloors, id: \.self) { floor in
                                                Text("\(floor)階層").tag(floor)
                                            }
                                        }
                                        .pickerStyle(MenuPickerStyle())
                                        .onChange(of: playerViewModel.debugStartFloor) { _ in
                                            playerViewModel.saveData()
                                        }
                                    }
                                    
                                    Divider()
                                        .background(Color(hex: GameColors.gridBorder).opacity(0.3))
                                    
                                    // AI難易度選択
                                    VStack(alignment: .leading, spacing: 8) {
                                        Text("AI難易度")
                                            .font(.fantasyCaption())
                                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                                        
                                        Picker("AI難易度", selection: $playerViewModel.debugAILevel) {
                                            ForEach(AILevel.allCases, id: \.self) { level in
                                                Text(level.rawValue).tag(level)
                                            }
                                        }
                                        .pickerStyle(SegmentedPickerStyle())
                                        .onChange(of: playerViewModel.debugAILevel) { _ in
                                            playerViewModel.saveData()
                                        }
                                    }
                                    
                                    Divider()
                                        .background(Color(hex: GameColors.gridBorder).opacity(0.3))
                                    
                                    // 全キャラクターアンロック
                                    VStack(alignment: .leading, spacing: 8) {
                                        HStack {
                                            Text("全キャラクターアンロック")
                                                .font(.fantasyCaption())
                                                .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                                            
                                            Spacer()
                                            
                                            Toggle("", isOn: Binding(
                                                get: { playerViewModel.debugUnlockAllCharacters },
                                                set: { _ in
                                                    playerViewModel.toggleUnlockAllCharacters()
                                                }
                                            ))
                                            .tint(Color(hex: GameColors.available))
                                        }
                                        
                                        if playerViewModel.debugUnlockAllCharacters {
                                            Text("全てのキャラクターが選択可能です")
                                                .font(.system(size: 12))
                                                .foregroundColor(Color(hex: GameColors.warning).opacity(0.8))
                                        }
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
                                                        Color(hex: GameColors.warning).opacity(0.5),
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
                        }
                        .padding(.top, ResponsiveLayout.isIPad() ? 16 : 12)
                        .padding(.bottom, 20)
                    }
                }
            }
        }
        .toolbar(.hidden, for: .navigationBar)
        .navigationBarBackButtonHidden(true)
        .navigationBarTitleDisplayMode(.inline)
    }
}

