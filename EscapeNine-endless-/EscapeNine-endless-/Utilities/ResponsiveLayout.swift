//
//  ResponsiveLayout.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct ResponsiveLayout {
    // デバイスタイプの判定
    static func isIPad() -> Bool {
        UIDevice.current.userInterfaceIdiom == .pad
    }

    // MARK: - 比率ベースのレイアウト定数
    // iPad/iPhone共通で画面サイズに対する比率で計算し、はみ出しを防止

    // 画面サイズに応じたグリッドセルサイズ（親ビューの幅に対する比率ベース）
    static func gridCellSize(for geometry: GeometryProxy) -> CGFloat {
        let availableWidth = min(geometry.size.width, geometry.size.height)
        // 幅の25%をセルサイズとし、3列 + スペーシングが収まるように
        let cellSize = (availableWidth - padding(for: geometry) * 2 - spacing(for: geometry) * 2) / 3
        if isIPad() {
            return min(cellSize, 120)
        } else {
            return min(cellSize, 120)
        }
    }

    // キャラクターサイズ（プレイヤー/敵の円）
    static func characterSize(for geometry: GeometryProxy) -> CGFloat {
        return gridCellSize(for: geometry) * 0.55
    }

    // ボタン幅（画面幅の比率ベース）
    static func buttonWidth(for geometry: GeometryProxy) -> CGFloat {
        if isIPad() {
            return min(geometry.size.width * 0.45, 360)
        } else {
            return min(geometry.size.width * 0.85, 280)
        }
    }

    // グリッドボードの最大高さ（画面高さの比率ベース）
    static func gridMaxHeight(for geometry: GeometryProxy) -> CGFloat {
        if isIPad() {
            return geometry.size.height * 0.32
        } else {
            return geometry.size.height * 0.40
        }
    }

    // グリッドボードの最大幅（画面幅の比率ベース）
    static func gridMaxWidth(for geometry: GeometryProxy) -> CGFloat {
        if isIPad() {
            return min(geometry.size.width * 0.45, 420)
        } else {
            return .infinity
        }
    }

    // フォントサイズのスケーリング
    static func scaleFontSize(_ baseSize: CGFloat, for geometry: GeometryProxy) -> CGFloat {
        return isIPad() ? baseSize * 1.2 : baseSize
    }

    // スペーシング（画面高さの比率ベース）
    static func spacing(for geometry: GeometryProxy) -> CGFloat {
        return isIPad() ? 12 : 8
    }

    // 垂直スペーシング（GameView等のセクション間）
    static func verticalSpacing(for geometry: GeometryProxy) -> CGFloat {
        return isIPad() ? 12 : 6
    }

    // パディング
    static func padding(for geometry: GeometryProxy) -> CGFloat {
        return isIPad() ? 40 : 20
    }

    // ビートインジケーターサイズ（外枠リング）
    static func beatIndicatorSize(for geometry: GeometryProxy) -> CGFloat {
        return gridCellSize(for: geometry) * 0.75
    }

    // BPMInfoView のパディング
    static func bpmInfoPadding(for geometry: GeometryProxy) -> (horizontal: CGFloat, vertical: CGFloat) {
        return isIPad() ? (horizontal: 36, vertical: 16) : (horizontal: 24, vertical: 12)
    }

    // BPMInfoView のディバイダー高さ
    static func dividerHeight(for geometry: GeometryProxy) -> CGFloat {
        return isIPad() ? 50 : 40
    }
}




