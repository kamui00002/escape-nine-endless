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

    // MARK: - Sprint 1 Issue 02: ワンタップリトライ設定 (@AppStorage で永続化)
    /// ResultView と同一キー (`oneTapRetryEnabled`) を共有
    @AppStorage("oneTapRetryEnabled") private var oneTapRetryEnabled: Bool = true

    // MARK: - Sprint 3 v1.1 #12: 触覚フィードバック設定
    @AppStorage("hapticsEnabled") private var hapticsEnabled: Bool = true

    var body: some View {
        GeometryReader { geometry in
            ZStack {
                GameBackground()

                VStack(spacing: 0) {
                    GameHeader(title: "設定")

                    ScrollView {
                        VStack(spacing: 24) {
                            playerInfoSection
                            gameplaySection
                            soundSection
                            aboutSection
                            purchaseSection

                            #if DEBUG
                            debugSection
                            #endif
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

    // MARK: - Player Info

    private var playerInfoSection: some View {
        GameCard(title: "冒険者情報") {
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
        .padding(.horizontal)
    }

    // MARK: - Sprint 1 Issue 02: ゲームプレイ設定 (ワンタップリトライ)

    private var gameplaySection: some View {
        GameCard(title: "ゲームプレイ") {
            VStack(alignment: .leading, spacing: 16) {
                HStack(alignment: .top, spacing: 12) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("ワンタップリトライ")
                            .font(.fantasyBody())
                            .foregroundColor(Color(hex: GameColors.textSecondary))

                        Text("Game Over 後、画面のどこをタップしても即再挑戦します")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                            .fixedSize(horizontal: false, vertical: true)
                    }

                    Spacer()

                    Toggle("", isOn: $oneTapRetryEnabled)
                        .labelsHidden()
                        .tint(Color(hex: GameColors.available))
                }

                Divider()
                    .background(Color(hex: GameColors.gridBorder).opacity(0.3))

                HStack(alignment: .top, spacing: 12) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("触覚フィードバック")
                            .font(.fantasyBody())
                            .foregroundColor(Color(hex: GameColors.textSecondary))

                        Text("ビート・タップ・衝突時に振動を発生させます。OFF にすると一切振動しません")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                            .fixedSize(horizontal: false, vertical: true)
                    }

                    Spacer()

                    Toggle("", isOn: $hapticsEnabled)
                        .labelsHidden()
                        .tint(Color(hex: GameColors.available))
                }
            }
        }
        .padding(.horizontal)
    }

    // MARK: - Sound Settings

    private var soundSection: some View {
        GameCard(title: "サウンド設定") {
            VStack(alignment: .leading, spacing: 16) {
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
        .padding(.horizontal)
    }

    // MARK: - About

    private var aboutSection: some View {
        GameCard(title: "アプリについて") {
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
        .padding(.horizontal)
    }

    // MARK: - Purchase

    private var purchaseSection: some View {
        GameCard(title: "課金設定") {
            VStack(alignment: .leading, spacing: 16) {
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
                                AudioManager.shared.playSoundEffect(.buttonTap)
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

                Button(action: {
                    AudioManager.shared.playSoundEffect(.buttonTap)
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
        .padding(.horizontal)
    }

    // MARK: - Debug

    #if DEBUG
    private var debugSection: some View {
        GameCard(title: "管理者用設定", borderColor: Color(hex: GameColors.warning)) {
            VStack(alignment: .leading, spacing: 16) {
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
                    .onChange(of: playerViewModel.debugStartFloor) {
                        playerViewModel.saveData()
                    }
                }

                Divider()
                    .background(Color(hex: GameColors.gridBorder).opacity(0.3))

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
                    .onChange(of: playerViewModel.debugAILevel) {
                        playerViewModel.saveData()
                    }
                }

                Divider()
                    .background(Color(hex: GameColors.gridBorder).opacity(0.3))

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

                Divider()
                    .background(Color(hex: GameColors.gridBorder).opacity(0.3))

                // BPMオーバーライド
                VStack(alignment: .leading, spacing: 8) {
                    HStack {
                        Text("BPMオーバーライド")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                        Spacer()
                        Text(playerViewModel.debugBPMOverride == 0 ? "自動" : "\(Int(playerViewModel.debugBPMOverride)) BPM")
                            .font(.fantasyNumber())
                            .foregroundColor(Color(hex: GameColors.available))
                    }

                    Slider(value: $playerViewModel.debugBPMOverride, in: 0...300, step: 10) { _ in
                        playerViewModel.saveData()
                    }
                    .tint(Color(hex: GameColors.warning))

                    Text("0=フロア曲線に従う")
                        .font(.system(size: 11))
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.5))
                }

                Divider()
                    .background(Color(hex: GameColors.gridBorder).opacity(0.3))

                // ターンカウントダウンビート数
                VStack(alignment: .leading, spacing: 8) {
                    HStack {
                        Text("ターンカウントダウン")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                        Spacer()
                        Text("\(playerViewModel.debugTurnCountdownBeats) ビート")
                            .font(.fantasyNumber())
                            .foregroundColor(Color(hex: GameColors.available))
                    }

                    Stepper("", value: $playerViewModel.debugTurnCountdownBeats, in: 1...10)
                        .labelsHidden()
                        .onChange(of: playerViewModel.debugTurnCountdownBeats) {
                            playerViewModel.saveData()
                        }
                }

                Divider()
                    .background(Color(hex: GameColors.gridBorder).opacity(0.3))

                // ゲーム開始カウントダウンスキップ
                VStack(alignment: .leading, spacing: 8) {
                    HStack {
                        Text("開始カウントダウンスキップ")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))

                        Spacer()

                        Toggle("", isOn: $playerViewModel.debugSkipStartCountdown)
                            .tint(Color(hex: GameColors.warning))
                            .onChange(of: playerViewModel.debugSkipStartCountdown) {
                                playerViewModel.saveData()
                            }
                    }

                    if playerViewModel.debugSkipStartCountdown {
                        Text("3→2→1→GO!をスキップして即開始")
                            .font(.system(size: 12))
                            .foregroundColor(Color(hex: GameColors.warning).opacity(0.8))
                    }
                }
            }
        }
        .padding(.horizontal)
    }
    #endif
}
