//
//  OnboardingTutorialView.swift
//  EscapeNine-endless-
//
//  Sprint 3 v1.1 動的オンボーディング (4 ステップ)。
//  設計典拠: docs/onboarding-v1.1-design.md §3
//

import SwiftUI
import os

private let logger = Logger(
    subsystem: Bundle.main.bundleIdentifier ?? "com.escapenine.app",
    category: "OnboardingTutorialView"
)

/// v1.1 動的オンボーディングの 4 ステップを順に提示する View (スケルトン版)。
///
/// 本 PR は **動く最小スケルトン** のみ:
/// - 4 ステップの容器 + 「次へ」ボタン + 右上 × スキップ
/// - Step 3 開始時に AudioManager.startHeartbeatLoop() + suspendBeatEngine()、
///   Step 3 → 4 遷移時に stopHeartbeatLoop() + resumeBeatEngine()
/// - 完了/スキップ時に hasSeenTutorial と hasSeenTutorialV1_1 を両方 true にセット
/// - Analytics: started / stepCompleted / complete
///
/// **未実装 (本 PR スコープ外、別 PR で追加予定)**:
/// - TutorialHighlightView (中央プレイヤー + 隣接マス点滅)
/// - 危険圏オーバーレイ (斜線パターン + 警告アイコン)
/// - CLEAR バースト演出
/// - 心拍音 .wav 音源 (現状 no-op)
/// - Reduce Motion / VoiceOver 完全対応
/// - GameViewModel.startPrologueFloor() 経由の Step 4 プレイアブル化
struct OnboardingTutorialView: View {
    @Binding var isPresented: Bool

    @AppStorage("hasSeenTutorial") private var hasSeenTutorial: Bool = false
    @AppStorage("hasSeenTutorialV1_1") private var hasSeenTutorialV1_1: Bool = false

    @State private var currentStep: Int = 1
    @State private var startTime: Date = Date()

    private let audioManager = AudioManager.shared

    var body: some View {
        ZStack {
            // 背景
            Color(hex: GameColors.background)
                .ignoresSafeArea()

            VStack(spacing: 32) {
                stepIndicator
                Spacer()
                stepContent
                Spacer()
                nextButton
            }
            .padding(.horizontal, 32)
            .padding(.vertical, 48)

            // 右上 スキップボタン
            VStack {
                HStack {
                    Spacer()
                    skipButton
                }
                Spacer()
            }
            .padding(.horizontal, 16)
            .padding(.top, 16)
        }
        .onAppear {
            startTime = Date()
            AnalyticsLogger.logTutorialStarted()
            logger.info("Onboarding tutorial started")
        }
        .onDisappear {
            // 念のための cleanup (途中 dismiss 時に心拍音/BeatEngine が残らないように)
            audioManager.stopHeartbeatLoop()
            audioManager.resumeBeatEngine()
        }
    }

    // MARK: - Subviews

    private var stepIndicator: some View {
        HStack(spacing: 8) {
            ForEach(1...4, id: \.self) { step in
                Capsule()
                    .fill(step <= currentStep ? Color(hex: GameColors.accent) : Color.gray.opacity(0.3))
                    .frame(width: step == currentStep ? 32 : 16, height: 6)
            }
        }
    }

    @ViewBuilder
    private var stepContent: some View {
        switch currentStep {
        case 1:
            stepPlaceholder(
                title: "Step 1: 移動を覚える",
                subtitle: "中央のプレイヤーから隣のマスへ移動できます",
                systemImage: "arrow.up.right.and.arrow.down.left.rectangle"
            )
        case 2:
            stepPlaceholder(
                title: "Step 2: 鬼との距離",
                subtitle: "鬼の隣のマス (危険圏) には入らないように",
                systemImage: "exclamationmark.triangle"
            )
        case 3:
            stepPlaceholder(
                title: "Step 3: ビートに乗る",
                subtitle: "心拍音 BPM 60。リズムに合わせて移動してみよう",
                systemImage: "heart.fill"
            )
        case 4:
            stepPlaceholder(
                title: "Step 4: 3 ターン耐える",
                subtitle: "実戦練習。3 ターン逃げ切ればクリア",
                systemImage: "flag.checkered"
            )
        default:
            EmptyView()
        }
    }

    private func stepPlaceholder(title: String, subtitle: String, systemImage: String) -> some View {
        VStack(spacing: 24) {
            Image(systemName: systemImage)
                .resizable()
                .scaledToFit()
                .frame(width: 80, height: 80)
                .foregroundColor(Color(hex: GameColors.accent))
            Text(title)
                .font(.title2)
                .fontWeight(.bold)
                .foregroundColor(Color(hex: GameColors.text))
            Text(subtitle)
                .font(.body)
                .foregroundColor(Color(hex: GameColors.text).opacity(0.85))
                .multilineTextAlignment(.center)
        }
    }

    private var nextButton: some View {
        Button(action: advance) {
            Text(currentStep == 4 ? "はじめる" : "次へ")
                .font(.headline)
                .fontWeight(.bold)
                .foregroundColor(Color(hex: GameColors.background))
                .frame(maxWidth: .infinity)
                .frame(height: 56)
                .background(Color(hex: GameColors.accent))
                .clipShape(RoundedRectangle(cornerRadius: 12))
        }
    }

    private var skipButton: some View {
        Button(action: skip) {
            Image(systemName: "xmark.circle.fill")
                .resizable()
                .frame(width: 32, height: 32)
                .foregroundColor(Color(hex: GameColors.text).opacity(0.6))
        }
        .accessibilityLabel("チュートリアルをスキップ")
    }

    // MARK: - Actions

    /// 「次へ」ボタンタップ時の処理。
    /// - Step ごとの完了 Analytics 発火
    /// - Step 3 開始/終了時に AudioManager の心拍音 + BeatEngine 制御
    /// - Step 4 完了時に hasSeenTutorial / hasSeenTutorialV1_1 をセットして dismiss
    private func advance() {
        AnalyticsLogger.logTutorialStepCompleted(stepNumber: currentStep, skipped: false)

        // Step 遷移に伴う音響制御
        let nextStep = currentStep + 1
        handleAudioTransition(from: currentStep, to: nextStep)

        if currentStep == 4 {
            complete()
        } else {
            currentStep = nextStep
        }
    }

    private func skip() {
        AnalyticsLogger.logTutorialStepCompleted(stepNumber: currentStep, skipped: true)
        // スキップ時も音響 cleanup を確実に
        audioManager.stopHeartbeatLoop()
        audioManager.resumeBeatEngine()
        complete()
    }

    private func complete() {
        let elapsed = Date().timeIntervalSince(startTime)
        AnalyticsLogger.logTutorialComplete(elapsedSeconds: elapsed)
        logger.info("Onboarding tutorial completed in \(elapsed, privacy: .public) seconds")

        // 両フラグセット (PR #30 scaffold の併存ルールに従う)
        hasSeenTutorial = true
        hasSeenTutorialV1_1 = true

        isPresented = false
    }

    /// Step 遷移時の音響制御。
    /// - Step 3 開始 (2 → 3): メトロノーム停止 + 心拍音開始
    /// - Step 3 終了 (3 → 4): 心拍音停止 + メトロノーム再開
    private func handleAudioTransition(from: Int, to: Int) {
        if from == 2 && to == 3 {
            audioManager.suspendBeatEngine()
            audioManager.startHeartbeatLoop()
        } else if from == 3 && to == 4 {
            audioManager.stopHeartbeatLoop()
            audioManager.resumeBeatEngine()
        }
    }
}

#Preview("Onboarding Step 1") {
    OnboardingTutorialView(isPresented: .constant(true))
}
