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

/// v1.1 動的オンボーディングの 4 ステップを順に提示する View。
///
/// 各 Step は **静的ミニ盤面** + **TutorialStepInstructionView** で構成され、
/// 子 View (`TutorialHighlightView` / `DangerZoneView`) を盤面に重ねて
/// チュートリアル意図を視覚化する。
///
/// **本 PR スコープ**: 静的盤面プレビュー + Step 3 アクセシビリティ動線
/// (Reduce Motion 完全 opt-out + 強演出予告 + Step 3 専用スキップ)。
///
/// **本 PR スコープ外 (別 PR で追加予定)**:
/// - CLEAR バースト演出 (Step 4)
/// - 心拍音 .wav 音源 (Step 3、現状 no-op)
/// - 赤フラッシュ演出 (まだ未実装)
/// - 触覚スイッチ `@AppStorage("hapticsEnabled")` の SettingsView 追加
/// - GameViewModel.startPrologueFloor() 経由のプレイアブル化
struct OnboardingTutorialView: View {
    @Binding var isPresented: Bool

    @AppStorage("hasSeenTutorial") private var hasSeenTutorial: Bool = false
    @AppStorage("hasSeenTutorialV1_1") private var hasSeenTutorialV1_1: Bool = false

    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    @State private var currentStep: Int = 1
    @State private var startTime: Date = Date()
    @State private var showingStep3Warning: Bool = false
    @State private var step3WarningTask: DispatchWorkItem?
    @State private var step4Cleared: Bool = false

    private let audioManager = AudioManager.shared
    private let totalSteps = 4

    private static let step3WarningDuration: TimeInterval = 1.5
    private static let step3SkipLongPressDuration: TimeInterval = 1.0
    private static let step3SkipTapCount: Int = 3

    var body: some View {
        ZStack {
            Color(hex: GameColors.background)
                .ignoresSafeArea()

            VStack(spacing: 18) {
                stepIndicator
                instructionForCurrentStep
                boardForCurrentStep
                Spacer(minLength: 0)
                if currentStep == 3 {
                    step3SkipControl
                }
                nextButton
            }
            .padding(.horizontal, 24)
            .padding(.top, 56)
            .padding(.bottom, 28)

            VStack {
                HStack {
                    Spacer()
                    skipButton
                }
                Spacer()
            }
            .padding(.horizontal, 16)
            .padding(.top, 16)

            if showingStep3Warning {
                step3WarningOverlay
                    .transition(.opacity)
            }
        }
        .animation(.easeInOut(duration: 0.2), value: showingStep3Warning)
        .onAppear {
            startTime = Date()
            AnalyticsLogger.logTutorialStarted()
            logger.info("Onboarding tutorial started")
        }
        .onDisappear {
            step3WarningTask?.cancel()
            step3WarningTask = nil
            audioManager.stopHeartbeatLoop()
            audioManager.resumeBeatEngine()
        }
    }

    // MARK: - Subviews

    private var stepIndicator: some View {
        HStack(spacing: 8) {
            ForEach(1...totalSteps, id: \.self) { step in
                Capsule()
                    .fill(step <= currentStep ? Color(hex: GameColors.accent) : Color.gray.opacity(0.3))
                    .frame(width: step == currentStep ? 32 : 16, height: 6)
            }
        }
        .accessibilityHidden(true)
    }

    @ViewBuilder
    private var instructionForCurrentStep: some View {
        let copy = Self.instructionCopy(for: currentStep)
        TutorialStepInstructionView(
            stepNumber: currentStep,
            totalSteps: totalSteps,
            title: copy.title,
            subtitle: copy.subtitle
        )
    }

    @ViewBuilder
    private var boardForCurrentStep: some View {
        if currentStep == totalSteps {
            TutorialStep4Game(
                totalTurns: 3,
                onClear: { step4Cleared = true }
            )
            .frame(maxWidth: 320)
        } else {
            let config = Self.boardConfig(for: currentStep)
            TutorialBoardPreview(
                playerPos: config.playerPos,
                enemyPos: config.enemyPos,
                highlightedPositions: config.highlightedPositions,
                dangerPositions: config.dangerPositions,
                stepNumber: currentStep,
                totalSteps: totalSteps,
                instructionTitle: Self.instructionCopy(for: currentStep).title
            )
            .frame(maxWidth: 320)
        }
    }

    /// Step 4 の「はじめる」ボタンは 3 ターン耐えるまで disable する (達成感の演出)。
    private var isNextButtonDisabled: Bool {
        currentStep == totalSteps && !step4Cleared
    }

    private var nextButton: some View {
        Button(action: advance) {
            Text(currentStep == totalSteps ? "はじめる" : "次へ")
                .font(.headline)
                .fontWeight(.bold)
                .foregroundColor(Color(hex: GameColors.background))
                .frame(maxWidth: .infinity)
                .frame(height: 56)
                .background(Color(hex: GameColors.accent).opacity(isNextButtonDisabled ? 0.35 : 1.0))
                .clipShape(RoundedRectangle(cornerRadius: 12))
        }
        .disabled(isNextButtonDisabled)
        .accessibilityLabel(currentStep == totalSteps ? "チュートリアルを終えてゲームを始める" : "次のステップへ進む")
        .accessibilityHint(isNextButtonDisabled ? "盤面で 3 ターン耐えると有効になります" : "")
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

    /// Step 3 中のみ表示される「Step 3 だけ」をスキップする操作領域。
    /// 3 タップまたは 1 秒長押しで発火 (誤タップ防止 / 設計書 §3 Step 3 手順 0)。
    private var step3SkipControl: some View {
        Text("長押し / 3 タップで Step 3 をスキップ")
            .font(.caption)
            .fontWeight(.semibold)
            .foregroundColor(Color(hex: GameColors.warning))
            .padding(.horizontal, 16)
            .padding(.vertical, 10)
            .background(
                RoundedRectangle(cornerRadius: 10)
                    .fill(Color(hex: GameColors.warning).opacity(0.08))
            )
            .overlay(
                RoundedRectangle(cornerRadius: 10)
                    .stroke(Color(hex: GameColors.warning), lineWidth: 1.2)
            )
            .contentShape(RoundedRectangle(cornerRadius: 10))
            .accessibilityElement(children: .ignore)
            .accessibilityLabel("Step 3 をスキップ")
            .accessibilityHint("3 回タップまたは 1 秒長押しで発火します")
            .accessibilityAddTraits(.isButton)
            .simultaneousGesture(
                LongPressGesture(minimumDuration: Self.step3SkipLongPressDuration)
                    .onEnded { _ in skipStep3() }
            )
            .simultaneousGesture(
                TapGesture(count: Self.step3SkipTapCount)
                    .onEnded { skipStep3() }
            )
    }

    /// Step 3 開始前に 1.5 秒だけ表示する強演出予告オーバーレイ。
    /// Reduce Motion ON のユーザーには表示せず Step 3 自体を skip する (設計書 §5)。
    private var step3WarningOverlay: some View {
        ZStack {
            Color.black.opacity(0.88)
                .ignoresSafeArea()

            VStack(spacing: 16) {
                Image(systemName: "exclamationmark.triangle.fill")
                    .font(.system(size: 56, weight: .bold))
                    .foregroundColor(Color(hex: GameColors.warning))
                Text("次のステップは強い演出を含みます")
                    .font(.title3)
                    .fontWeight(.bold)
                    .foregroundColor(.white)
                    .multilineTextAlignment(.center)
                Text("心拍音と振動が再生されます")
                    .font(.body)
                    .foregroundColor(.white.opacity(0.85))
                    .multilineTextAlignment(.center)
            }
            .padding(.horizontal, 32)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("注意。次のステップは心拍音と振動を含む強い演出が再生されます")
    }

    // MARK: - Step content tables

    private struct InstructionCopy {
        let title: String
        let subtitle: String
    }

    private struct BoardConfig {
        let playerPos: Int
        let enemyPos: Int?
        let highlightedPositions: Set<Int>
        let dangerPositions: Set<Int>
    }

    private static func instructionCopy(for step: Int) -> InstructionCopy {
        switch step {
        case 1:
            return InstructionCopy(
                title: "動きを覚える",
                subtitle: "タップで周囲 8 マスのどこへでも移動できる"
            )
        case 2:
            return InstructionCopy(
                title: "影を避ける",
                subtitle: "鬼の隣接マスは危険圏。毎ターン 1 マスずつ近づいてくる"
            )
        case 3:
            return InstructionCopy(
                title: "ヒヤリ体験",
                subtitle: "あと 1 マスで影に飲まれる。冷静に逃げ道を読み切ろう"
            )
        case 4:
            return InstructionCopy(
                title: "階層クリア",
                subtitle: "周囲のマスをタップして 3 ターン逃げ切ろう。本当の旅が始まる"
            )
        default:
            return InstructionCopy(title: "", subtitle: "")
        }
    }

    private static func boardConfig(for step: Int) -> BoardConfig {
        switch step {
        case 1:
            // 中央プレイヤー + 周囲 8 マスをハイライト
            return BoardConfig(
                playerPos: 5,
                enemyPos: nil,
                highlightedPositions: TutorialBoardGeometry.adjacent8(of: 5),
                dangerPositions: []
            )
        case 2:
            // プレイヤー対角配置 + 鬼の隣接 8 マスを危険圏
            return BoardConfig(
                playerPos: 1,
                enemyPos: 9,
                highlightedPositions: [],
                dangerPositions: TutorialBoardGeometry.adjacent8(of: 9)
            )
        case 3:
            // 中央プレイヤー + 鬼が隣接 (位置 4) でヒヤリ状態
            return BoardConfig(
                playerPos: 5,
                enemyPos: 4,
                highlightedPositions: [],
                dangerPositions: TutorialBoardGeometry.adjacent8(of: 4)
            )
        case 4:
            // 距離最大の安全配置 (Step 4 は枠のみ、演出は別 PR)
            return BoardConfig(
                playerPos: 1,
                enemyPos: 9,
                highlightedPositions: [],
                dangerPositions: []
            )
        default:
            return BoardConfig(playerPos: 5, enemyPos: nil, highlightedPositions: [], dangerPositions: [])
        }
    }

    // MARK: - Actions

    /// 「次へ」ボタンタップ時の処理。
    /// - Step ごとの完了 Analytics 発火
    /// - Step 2 → 3 遷移は Reduce Motion 判定で 3 通りに分岐:
    ///   (a) reduceMotion ON → Step 3 をまるごと skip (Analytics: step 3 skipped)
    ///   (b) reduceMotion OFF → 1.5 秒予告オーバーレイ → Step 3 開始 + 心拍音
    /// - Step 3 → 4 遷移: 心拍音停止 + メトロノーム再開
    /// - Step 4 完了時に hasSeenTutorial / hasSeenTutorialV1_1 をセットして dismiss
    private func advance() {
        AnalyticsLogger.logTutorialStepCompleted(stepNumber: currentStep, skipped: false)

        if currentStep == totalSteps {
            complete()
            return
        }

        let nextStep = currentStep + 1

        // Step 2 → 3 遷移: Reduce Motion ユーザーは Step 3 自体を skip
        if currentStep == 2 && nextStep == 3 && reduceMotion {
            AnalyticsLogger.logTutorialStepCompleted(stepNumber: 3, skipped: true)
            logger.info("Step 3 skipped automatically (Reduce Motion enabled)")
            currentStep = 4
            return
        }

        // Step 2 → 3 遷移: 1.5 秒予告 → Step 3 開始 + 心拍音
        if currentStep == 2 && nextStep == 3 {
            presentStep3Warning()
            return
        }

        // Step 3 → 4 遷移: 心拍音停止 + メトロノーム再開
        if currentStep == 3 && nextStep == 4 {
            audioManager.stopHeartbeatLoop()
            audioManager.resumeBeatEngine()
        }

        currentStep = nextStep
    }

    /// 全体スキップ (右上 ×)。現在の Step を skipped=true で記録して dismiss。
    private func skip() {
        AnalyticsLogger.logTutorialStepCompleted(stepNumber: currentStep, skipped: true)
        step3WarningTask?.cancel()
        step3WarningTask = nil
        showingStep3Warning = false
        audioManager.stopHeartbeatLoop()
        audioManager.resumeBeatEngine()
        complete()
    }

    /// Step 3 だけをスキップして Step 4 へ進める (チュートリアル全体は完走扱い)。
    /// 右上 × の `skip()` とは別物 (Analytics 発火経路もスコープも異なる)。
    private func skipStep3() {
        guard currentStep == 3 else { return }
        AnalyticsLogger.logTutorialStepCompleted(stepNumber: 3, skipped: true)
        logger.info("Step 3 skipped by user gesture")
        audioManager.stopHeartbeatLoop()
        audioManager.resumeBeatEngine()
        currentStep = 4
    }

    /// 1.5 秒の強演出予告オーバーレイを表示してから Step 3 へ自動遷移。
    private func presentStep3Warning() {
        step3WarningTask?.cancel()
        showingStep3Warning = true

        let task = DispatchWorkItem { [self] in
            showingStep3Warning = false
            step3WarningTask = nil
            currentStep = 3
            audioManager.suspendBeatEngine()
            audioManager.startHeartbeatLoop()
        }
        step3WarningTask = task
        DispatchQueue.main.asyncAfter(deadline: .now() + Self.step3WarningDuration, execute: task)
    }

    private func complete() {
        let elapsed = Date().timeIntervalSince(startTime)
        AnalyticsLogger.logTutorialComplete(elapsedSeconds: elapsed)
        logger.info("Onboarding tutorial completed in \(elapsed, privacy: .public) seconds")

        hasSeenTutorial = true
        hasSeenTutorialV1_1 = true

        isPresented = false
    }
}

// MARK: - Private board preview

/// 1〜9 のマス座標を扱うヘルパー (本 View 専用のため private)。
private enum TutorialBoardGeometry {
    /// 8 方向隣接マス (要件定義書「縦横斜め 8 方向」)。位置外は除外。
    static func adjacent8(of position: Int) -> Set<Int> {
        guard (1...9).contains(position) else { return [] }
        let row = (position - 1) / 3
        let col = (position - 1) % 3
        var result: Set<Int> = []
        for dr in -1...1 {
            for dc in -1...1 {
                guard dr != 0 || dc != 0 else { continue }
                let nr = row + dr
                let nc = col + dc
                guard (0...2).contains(nr), (0...2).contains(nc) else { continue }
                result.insert(nr * 3 + nc + 1)
            }
        }
        return result
    }
}

/// チュートリアル専用の静的 3x3 盤面プレビュー。
///
/// インタラクションなし (タップ移動・AI 追跡・カウントダウンは別 PR)。
/// 親 OnboardingTutorialView が `BoardConfig` を Step ごとに切り替える。
private struct TutorialBoardPreview: View {
    let playerPos: Int
    let enemyPos: Int?
    let highlightedPositions: Set<Int>
    let dangerPositions: Set<Int>
    let stepNumber: Int
    let totalSteps: Int
    let instructionTitle: String

    private let cellSpacing: CGFloat = 6

    var body: some View {
        GeometryReader { geometry in
            let cellSize = computeCellSize(for: geometry)
            VStack(spacing: cellSpacing) {
                ForEach(0..<3, id: \.self) { row in
                    HStack(spacing: cellSpacing) {
                        ForEach(0..<3, id: \.self) { col in
                            let position = row * 3 + col + 1
                            cell(at: position, cellSize: cellSize)
                        }
                    }
                }
            }
            .frame(width: geometry.size.width, height: geometry.size.height)
        }
        .aspectRatio(1, contentMode: .fit)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Step \(stepNumber) / \(totalSteps): \(instructionTitle)")
        .accessibilityValue(accessibilityDescription)
    }

    private func computeCellSize(for geometry: GeometryProxy) -> CGFloat {
        let side = min(geometry.size.width, geometry.size.height)
        let totalSpacing = cellSpacing * 2
        return max(40, (side - totalSpacing) / 3)
    }

    @ViewBuilder
    private func cell(at position: Int, cellSize: CGFloat) -> some View {
        ZStack {
            RoundedRectangle(cornerRadius: 8)
                .fill(Color(hex: GameColors.backgroundSecondary))
                .frame(width: cellSize, height: cellSize)

            if dangerPositions.contains(position) {
                DangerZoneView(cellSize: cellSize)
                    .clipShape(RoundedRectangle(cornerRadius: 8))
            }

            if let enemyPos, enemyPos == position {
                Image("red_oni")
                    .resizable()
                    .interpolation(.none)
                    .scaledToFit()
                    .frame(width: cellSize * 0.78, height: cellSize * 0.78)
            } else if position == playerPos {
                Image("hero")
                    .resizable()
                    .interpolation(.none)
                    .scaledToFit()
                    .frame(width: cellSize * 0.78, height: cellSize * 0.78)
            }

            if highlightedPositions.contains(position) {
                TutorialHighlightView(cellSize: cellSize)
            }
        }
        .frame(width: cellSize, height: cellSize)
    }

    private var accessibilityDescription: String {
        var parts: [String] = ["プレイヤー位置 \(playerPos)"]
        if let enemyPos {
            parts.append("鬼位置 \(enemyPos)")
        }
        if !highlightedPositions.isEmpty {
            parts.append("移動可能マス \(highlightedPositions.count) 箇所")
        }
        if !dangerPositions.isEmpty {
            parts.append("危険圏 \(dangerPositions.count) 箇所")
        }
        return parts.joined(separator: "、")
    }
}

/// Step 4 専用のミニプレイアブル盤面 (タップで移動 + スクリプト敵 + 3 ターン)。
///
/// インタラクション最小化方針:
/// - タップで隣接 8 方向のマスに移動 (TutorialBoardGeometry.adjacent8 を流用)
/// - 敵は固定スクリプトで移動 (AI 不使用、BeatEngine 不使用)
/// - スクリプトとタップ可能セルの組み合わせで **算数的に衝突不可** な配置を保証:
///   - 開始: player=1, enemy=9 (最大距離 = 4)
///   - 敵スクリプト: ターン 1 後 → 6, ターン 2 後 → 3, ターン 3 後 → 3 (固定)
///   - プレイヤーが選べるマスから敵の現在位置と次ターン位置を除外 (二重ガード)
/// - 3 ターン耐えたら `onClear()` を発火 (親側で「はじめる」ボタンが活性化)
///
/// **このスコープ外** (#11 以降で追加予定):
/// - AIEngine 接続、BeatEngine 連動、敗北リトライ、衝突演出、CLEAR バースト演出
private struct TutorialStep4Game: View {
    let totalTurns: Int
    let onClear: () -> Void

    @State private var playerPos: Int = 1
    @State private var enemyPos: Int = 9
    @State private var turnsCompleted: Int = 0
    @State private var hasCleared: Bool = false

    /// 敵スクリプト (1-indexed ターン)。3 ターン分用意し、最終ターンは固定位置に留める。
    private let enemyScript: [Int] = [6, 3, 3]
    private let cellSpacing: CGFloat = 6

    var body: some View {
        VStack(spacing: 10) {
            turnCounter
            boardView
        }
    }

    private var turnCounter: some View {
        HStack(spacing: 8) {
            Text("ターン")
                .font(.caption)
                .fontWeight(.semibold)
                .foregroundColor(Color(hex: GameColors.text).opacity(0.75))
            Text("\(min(turnsCompleted, totalTurns)) / \(totalTurns)")
                .font(.title3)
                .fontWeight(.bold)
                .foregroundColor(Color(hex: GameColors.accent))
                .monospacedDigit()
            if hasCleared {
                Text("クリア!")
                    .font(.headline)
                    .fontWeight(.bold)
                    .foregroundColor(Color(hex: GameColors.success))
                    .padding(.leading, 8)
            }
        }
    }

    private var boardView: some View {
        GeometryReader { geometry in
            let cellSize = computeCellSize(for: geometry)
            VStack(spacing: cellSpacing) {
                ForEach(0..<3, id: \.self) { row in
                    HStack(spacing: cellSpacing) {
                        ForEach(0..<3, id: \.self) { col in
                            let position = row * 3 + col + 1
                            cell(at: position, cellSize: cellSize)
                                .contentShape(Rectangle())
                                .onTapGesture { handleTap(on: position) }
                        }
                    }
                }
            }
            .frame(width: geometry.size.width, height: geometry.size.height)
        }
        .aspectRatio(1, contentMode: .fit)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Step 4 プレイ盤面")
        .accessibilityValue(accessibilityDescription)
    }

    @ViewBuilder
    private func cell(at position: Int, cellSize: CGFloat) -> some View {
        ZStack {
            RoundedRectangle(cornerRadius: 8)
                .fill(Color(hex: GameColors.backgroundSecondary))
                .frame(width: cellSize, height: cellSize)

            if position == enemyPos {
                Image("red_oni")
                    .resizable()
                    .interpolation(.none)
                    .scaledToFit()
                    .frame(width: cellSize * 0.78, height: cellSize * 0.78)
            } else if position == playerPos {
                Image("hero")
                    .resizable()
                    .interpolation(.none)
                    .scaledToFit()
                    .frame(width: cellSize * 0.78, height: cellSize * 0.78)
            }

            if isTappable(position) {
                TutorialHighlightView(cellSize: cellSize)
            }
        }
        .frame(width: cellSize, height: cellSize)
    }

    private func computeCellSize(for geometry: GeometryProxy) -> CGFloat {
        let side = min(geometry.size.width, geometry.size.height)
        let totalSpacing = cellSpacing * 2
        return max(40, (side - totalSpacing) / 3)
    }

    /// 現在タップ可能なマスか判定。
    /// - プレイヤー隣接 8 マス
    /// - 敵の現在位置と「次ターン位置」を除外 (二重衝突ガード)
    /// - クリア後はタップ不可
    private func isTappable(_ position: Int) -> Bool {
        guard !hasCleared else { return false }
        guard position != playerPos else { return false }
        guard position != enemyPos else { return false }
        let nextEnemyPos = scriptedEnemyPosition(after: turnsCompleted + 1)
        guard position != nextEnemyPos else { return false }
        return TutorialBoardGeometry.adjacent8(of: playerPos).contains(position)
    }

    /// 1-indexed ターン後の敵スクリプト位置 (範囲外なら現在位置を維持)。
    private func scriptedEnemyPosition(after turn: Int) -> Int {
        guard turn >= 1, turn <= enemyScript.count else { return enemyPos }
        return enemyScript[turn - 1]
    }

    private func handleTap(on position: Int) {
        guard isTappable(position) else { return }

        // 1. プレイヤー移動
        playerPos = position

        // 2. ターン進行 + 敵スクリプト適用
        let nextTurn = turnsCompleted + 1
        turnsCompleted = nextTurn
        enemyPos = scriptedEnemyPosition(after: nextTurn)

        // 3. クリア検知
        if turnsCompleted >= totalTurns && !hasCleared {
            hasCleared = true
            onClear()
        }
    }

    private var accessibilityDescription: String {
        if hasCleared {
            return "Step 4 クリア。プレイヤー位置 \(playerPos)、鬼位置 \(enemyPos)"
        }
        let remaining = totalTurns - turnsCompleted
        return "プレイヤー位置 \(playerPos)、鬼位置 \(enemyPos)、残り \(remaining) ターン。光るマスをタップして移動"
    }
}

// MARK: - Previews

#Preview("iPhone — Step 1 (default)") {
    OnboardingTutorialView(isPresented: .constant(true))
}

#Preview("iPhone SE 縦長検証", traits: .fixedLayout(width: 375, height: 667)) {
    OnboardingTutorialView(isPresented: .constant(true))
}

#Preview("iPad 横向き — landscape") {
    OnboardingTutorialView(isPresented: .constant(true))
        .frame(width: 1024, height: 768)
}
