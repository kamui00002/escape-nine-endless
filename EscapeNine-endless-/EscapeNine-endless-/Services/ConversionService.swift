//
//  ConversionService.swift
//  EscapeNine-endless-
//
//  Google Ads コンバージョン計測サービス
//

import Foundation
#if canImport(GoogleAdsOnDeviceConversion)
import GoogleAdsOnDeviceConversion
#endif

@MainActor
final class ConversionService {
    static let shared = ConversionService()
    private init() {}

    /// アプリ初回起動時に呼び出し
    func trackAppOpen() {
        #if canImport(GoogleAdsOnDeviceConversion)
        let converter = AppOpenForConversionManager()
        converter.requestAppOpenForConversion(
            conversionAppOpenParameters: AppOpenForConversionParameters()
        ) { error in
            if let error {
                print("[Conversion] App open tracking error: \(error)")
            } else {
                print("[Conversion] App open tracked")
            }
        }
        #endif
    }
}
