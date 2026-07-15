// EscapeNineATT.mm
// App Tracking Transparency (ATT) ネイティブブリッジ。GMA (Google Mobile Ads) Unity plugin は
// ATT ダイアログ自体を提供しないため、C# 側 (Runtime/Ads/AttBridge.cs) から P/Invoke で
// 呼ばれるネイティブ実装をここに置く。
// Swift 正本: EscapeNine_endless_App.swift の
//   let status = await ATTrackingManager.requestTrackingAuthorization()
// 相当。
//
// フレームワークリンク: Xcode の Clang Modules (CLANG_ENABLE_MODULES、Unity 生成 Xcode
// プロジェクトの既定設定) により #import <AppTrackingTransparency/...> だけで自動リンクされる
// (加えて GMA 本体の CocoaPod 依存 Google-Mobile-Ads-SDK が推移的に ATT を要求する)。
// 明示的な PBXProject フレームワークリンクの postprocess は追加していない
// (最小変更・過剰な事前対応を避ける方針。リンクエラーが出た場合は Editor 側で
// AppTrackingTransparency.framework を明示リンクする postprocess を追加すること)。

#import <Foundation/Foundation.h>
#if __has_include(<AppTrackingTransparency/AppTrackingTransparency.h>)
#import <AppTrackingTransparency/AppTrackingTransparency.h>
#define ESCAPENINE_HAS_ATT 1
#else
#define ESCAPENINE_HAS_ATT 0
#endif

// AttBridge.cs の AttResultCallback (delegate void AttResultCallback(int status)) と一致させる。
typedef void (*EscapeNineAttCallback)(int status);

extern "C" {

// ATT ダイアログをリクエストし、完了時に status (ATTrackingManagerAuthorizationStatus の生値:
// notDetermined=0, restricted=1, denied=2, authorized=3) を callback へ渡す。
// completionHandler は保証されないスレッドから呼ばれるため、Unity のマネージド callback
// (GameController.StartNewRun / Router.Show など UnityEngine API に触れ得る) を安全に
// 呼べるよう、必ず dispatch_get_main_queue() 経由でメインスレッドへディスパッチしてから呼ぶ。
void EscapeNineRequestTrackingAuthorization(EscapeNineAttCallback callback)
{
#if ESCAPENINE_HAS_ATT
    if (@available(iOS 14, *)) {
        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
            dispatch_async(dispatch_get_main_queue(), ^{
                if (callback != NULL) {
                    callback((int)status);
                }
            });
        }];
        return;
    }
#endif
    // iOS 14 未満、または ATT フレームワーク非搭載ビルドでは ATT 自体が存在しないため、
    // authorized (3) 相当として即時完了扱いにする (AttBridge.cs の Editor/非iOS 分岐と同じ思想)。
    if (callback != NULL) {
        callback(3);
    }
}

}
