// AttBridge.cs
// App Tracking Transparency (ATT) のネイティブ結線。GMA (Google Mobile Ads) Unity plugin は
// ATT ダイアログ自体を提供しないため、Plugins/iOS/EscapeNineATT.mm の C 関数を P/Invoke で呼ぶ。
// Swift 正本: EscapeNine_endless_App.swift の ATTrackingManager.requestTrackingAuthorization() 相当。
//
// IL2CPP 制約: ネイティブ→マネージド callback は static メソッドを [MonoPInvokeCallback] で
// マークする必要がある (インスタンスメソッド/クロージャを直接 P/Invoke で渡せないため)。
// 加えて ATT の完了はユーザーがダイアログを操作するまで数秒〜数十秒かかる非同期処理のため、
// デリゲートを GC されないよう static readonly フィールドで root する
// (com.unity.purchasing の IosInAppBrowserLauncher.s_Callback と同じ規律を踏襲)。
//
// ATTrackingManagerAuthorizationStatus (Apple 公式値、EscapeNine_endless_App.swift の
// status.rawValue と同一): notDetermined=0, restricted=1, denied=2, authorized=3。

using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
#endif
using UnityEngine;

namespace EscapeNine.Runtime.Ads
{
    internal static class AttBridge
    {
        internal enum AttStatus
        {
            NotDetermined = 0,
            Restricted = 1,
            Denied = 2,
            Authorized = 3,
        }

#if UNITY_IOS && !UNITY_EDITOR
        private delegate void AttResultCallback(int status);

        [DllImport("__Internal", EntryPoint = "EscapeNineRequestTrackingAuthorization")]
        private static extern void NativeRequestTrackingAuthorization(AttResultCallback callback);

        // ネイティブ側 (.mm) がこの関数ポインタを保持したまま ATT ダイアログの完了 (ユーザー操作待ち)
        // まで呼び出さないため、GC されないよう static readonly で root する。
        private static readonly AttResultCallback s_Callback = OnAttResult;

        // RequestTrackingAuthorization は起動につき 1 回だけ呼ばれる想定
        // (IAdService.RequestTrackingAuthorization のドキュメントコメント、App.cs Awake の once-only 前提と同じ)。
        private static Action<AttStatus> s_PendingHandler;

        [MonoPInvokeCallback(typeof(AttResultCallback))]
        private static void OnAttResult(int status)
        {
            // ネイティブ側 (.mm) が dispatch_get_main_queue() 経由で呼び戻すため、ここは既に
            // Unity メインスレッド上。ATT は GMA のイベントではないため
            // GoogleMobileAds.Common.MobileAdsEventExecutor は使わない (混同禁止)。
            Action<AttStatus> handler = s_PendingHandler;
            s_PendingHandler = null;
            handler?.Invoke((AttStatus)status);
        }
#endif

        /// <summary>
        /// ATT (App Tracking Transparency) ダイアログをリクエストする。iOS 実機以外
        /// (Editor / 他プラットフォーム) では ATT 自体が存在しないため、即座に Authorized 扱いで
        /// 完了する (StubAdService の「即許可扱い」挙動を踏襲。テストがここで止まらないように)。
        /// </summary>
        internal static void RequestTrackingAuthorization(Action<AttStatus> onResult)
        {
#if UNITY_IOS && !UNITY_EDITOR
            s_PendingHandler = onResult;
            NativeRequestTrackingAuthorization(s_Callback);
#else
            Debug.Log("[AttBridge] ATT許可リクエスト (Editor/非iOS・即許可扱い)");
            onResult?.Invoke(AttStatus.Authorized);
#endif
        }
    }
}
