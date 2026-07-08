// IAdService.cs
// 広告サービスの継ぎ目 (seam)。Swift 正本: Services/AdMobService.swift の公開 API をミラーする。
// 本物の SDK (Google Mobile Ads Unity plugin + Unity Ads メディエーション、decision brief §4.2) を
// 導入する時は、この interface の実装クラスを 1 つ追加するだけで済むようにするための抽象。
// 呼び出し側 (App / HomeScreen / ResultScreen) は IAdService 越しにしか広告を触らない。

using System;

namespace EscapeNine.Runtime.Ads
{
    public interface IAdService
    {
        /// <summary>
        /// 広告 SDK の初期化。consent/ATT はこの時点では未許可扱い (denied) とし、
        /// RequestTrackingAuthorization の完了後にのみ許可へ切り替える
        /// (ATT 前に consent granted は禁止。feedback_consent_auth_antipatterns 厳守)。
        /// </summary>
        void Initialize();

        /// <summary>ホーム画面下部のバナー広告を表示する。AdsRemoved 時は no-op。</summary>
        void ShowBanner();

        /// <summary>バナー広告を非表示にする (画面離脱時)。</summary>
        void HideBanner();

        /// <summary>
        /// インタースティシャル広告を表示する。ゲームオーバー→リトライ時に呼ぶ (Swift: InterstitialAdPresenter)。
        /// AdsRemoved 時、または広告が準備できていない時は即座に onClosed を呼ぶ (Swift: completion(true) 相当)。
        /// onClosed は必ず呼ばれる契約 — 呼び出し側はこれを「リトライ続行」の合図として使うため、
        /// 実装側で握り潰してはならない (silent failure 禁止)。
        /// </summary>
        void ShowInterstitial(Action onClosed = null);

        /// <summary>広告削除購入済みか (Swift: AdMobService.isAdRemoved / shouldShowBannerAd の否定)。
        /// 正本データは PlayerState.AdRemoved — このプロパティはそれを参照するだけの窓口。</summary>
        bool AdsRemoved { get; }

        /// <summary>
        /// ATT (App Tracking Transparency) の許可リクエスト。iOS 14.5+ で起動時に 1 回だけ呼ぶ想定
        /// (Swift: requestTrackingAuthorization。decision brief §3「ATT は起動時」)。
        /// 完了後に onDone を呼ぶ (成否に関わらず必ず呼ぶ契約、ShowInterstitial の onClosed と同じ思想)。
        /// </summary>
        void RequestTrackingAuthorization(Action onDone = null);
    }
}
