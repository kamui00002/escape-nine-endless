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
    
    // 画面サイズに応じたグリッドセルサイズ
    static func gridCellSize(for geometry: GeometryProxy) -> CGFloat {
        let minDimension = min(geometry.size.width, geometry.size.height)
        let padding: CGFloat = 40
        let spacing: CGFloat = 8 * 2 // 2つのスペース
        let availableWidth = minDimension - padding - spacing
        
        // iPadの場合はより大きく、iPhoneの場合は適度なサイズ
        if isIPad() {
            // iPad: 最小120pt、最大150pt
            return min(max(availableWidth / 3, 120), 150)
        } else {
            // iPhone: 最小80pt、最大120pt
            return min(max(availableWidth / 3, 80), 120)
        }
    }
    
    // キャラクターサイズ（プレイヤー/敵の円）
    static func characterSize(for geometry: GeometryProxy) -> CGFloat {
        let cellSize = gridCellSize(for: geometry)
        // セルサイズの50-60%をキャラクターサイズにする
        return cellSize * 0.55
    }
    
    // ボタン幅
    static func buttonWidth(for geometry: GeometryProxy) -> CGFloat {
        if isIPad() {
            // iPad: 最大400pt
            return min(geometry.size.width * 0.6, 400)
        } else {
            // iPhone: 最大280pt
            return min(geometry.size.width * 0.85, 280)
        }
    }
    
    // フォントサイズのスケーリング
    static func scaleFontSize(_ baseSize: CGFloat, for geometry: GeometryProxy) -> CGFloat {
        if isIPad() {
            return baseSize * 1.2
        } else {
            return baseSize
        }
    }
    
    // スペーシング
    static func spacing(for geometry: GeometryProxy) -> CGFloat {
        if isIPad() {
            return 12
        } else {
            return 8
        }
    }
    
    // パディング
    static func padding(for geometry: GeometryProxy) -> CGFloat {
        if isIPad() {
            return 40
        } else {
            return 20
        }
    }
}




