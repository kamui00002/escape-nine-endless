// AdMobService.cs
// Swift 正本 Services/AdMobService.swift の移植。GMA (Google Mobile Ads) Unity plugin 11.2.0
// (com.google.ads.mobile) の実 API (GoogleMobileAds.Api namespace) を使用する。
// API シグネチャは Library/PackageCache 内のサンプル (BannerViewController.cs /
// InterstitialAdController.cs / GoogleMobileAdsController.cs) と、GoogleMobileAds.Common.dll の
// monodis 逆アセンブルによる MobileAdsEventExecutor.ExecuteInUpdate シグネチャ確認で担保している
// (推測 API 禁止の規律に従う。IAP 実装時と同じ規律)。
//
// ATT (App Tracking Transparency): GMA plugin 自体は ATT ダイアログを提供しないため、
// AttBridge.cs 経由で Plugins/iOS/EscapeNineATT.mm のネイティブ実装を呼ぶ。
//
// consent/ATT ポリシー (init=denied → ATT後切替、feedback_consent_auth_antipatterns 厳守):
// App.cs の呼び出し順序は Initialize() → RequestTrackingAuthorization() の順で固定
// (Swift 正本は逆順: ATT 完了後に MobileAds.shared.start() を呼ぶ「ad load を遅延」方式)。
// Unity 側はこの呼び出し順序を変えない制約下にあるため、「ATT 完了前にパーソナライズ広告を
// 有効化しない」を担保する手段として「非パーソナライズ (npa=1) リクエスト」方式を採る:
//   - _personalizedAdsAllowed = false で初期化。ATT が Authorized で完了するまで、
//     全ての AdRequest に Extras["npa"] = "1" を付与する (BuildAdRequest() に一元化、
//     npa は UMP 未導入環境向けの AdMob 公式の非パーソナライズ化フラグ)。
//   - RequestTrackingAuthorization() が ATT Authorized で完了した時のみ true に切り替える
//     (Denied/Restricted/NotDetermined は Swift の「.denied 維持」と同様、非パーソナライズのまま)。
// Swift正本 (delay方式) と Unity実装 (npa方式) の呼び出し順序差異は unity/PARITY_GAPS.md §A に記録済み。

using System;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

namespace EscapeNine.Runtime.Ads
{
    public sealed class AdMobService : IAdService
    {
        private readonly PlayerState _player;

        private BannerView _bannerView;
        private InterstitialAd _interstitialAd;
        private bool _isInterstitialLoading;

        /// <summary>ATT Authorized 完了前は false 固定 (npa=1 を全リクエストに付与)。ATT Authorized 完了時のみ true。</summary>
        private bool _personalizedAdsAllowed;

        // ShowInterstitial() の呼び出し元 (ResultScreen.TriggerRetry の continueRetry 等) の
        // onClosed をロード時点で登録したイベントハンドラから呼ぶための橋渡し。
        // 同時に表示中のインタースティシャルは高々 1 本 (呼び出し元も単一フローのため static 化不要)。
        private Action _pendingInterstitialOnClosed;
        private bool _pendingInterstitialClosedHandled;

        public AdMobService(PlayerState player)
        {
            _player = player;
        }

        public bool AdsRemoved => _player != null && _player.AdRemoved;

        public void Initialize()
        {
            if (AdsRemoved)
            {
                Debug.Log("[AdMobService] 広告削除済み - 初期化スキップ");
                return;
            }

            Debug.Log("[AdMobService] 初期化開始 (GMA 11.2.0)");

            // MobileAds.Initialize のコールバックは Unity メインスレッド外で発火し得るため
            // (HelloWorld サンプルのコメント: "Google Mobile Ads events are raised off the Unity
            // Main thread...use MobileAdsEventExecutor.ExecuteInUpdate()")、以降 GMA API
            // (InterstitialAd.Load 等) を呼ぶ前に必ずメインスレッドへ戻す。
            MobileAds.Initialize(initStatus =>
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (initStatus == null)
                    {
                        Debug.LogError("[AdMobService] GMA 初期化失敗");
                        return;
                    }

                    Debug.Log("[AdMobService] GMA 初期化完了");

                    if (!AdsRemoved)
                    {
                        LoadInterstitial();
                    }
                });
            });
        }

        // MARK: - Banner Ad

        public void ShowBanner()
        {
            if (AdsRemoved)
            {
                return; // Swift: shouldShowBannerAd = !isAdRemoved
            }

            if (_bannerView == null)
            {
                _bannerView = new BannerView(AdConfig.BannerAdUnitId, AdSize.Banner, AdPosition.Bottom);
                ListenToBannerEvents(_bannerView);
                _bannerView.LoadAd(BuildAdRequest());
                Debug.Log("[AdMobService] バナー広告を作成しロード開始");
            }

            // 既存バナーがあれば作り直さない (Hide() 後の再表示、または二重 ShowBanner 呼び出し対策)。
            _bannerView.Show();
        }

        public void HideBanner()
        {
            if (_bannerView != null)
            {
                _bannerView.Hide();
                Debug.Log("[AdMobService] バナー広告を非表示");
            }
        }

        private void ListenToBannerEvents(BannerView bannerView)
        {
            bannerView.OnBannerAdLoaded += () =>
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    Debug.Log("[AdMobService] バナー広告ロード完了");
                });
            };
            bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    Debug.LogWarning($"[AdMobService] バナー広告ロード失敗: {error}");
                });
            };
        }

        // MARK: - Interstitial Ad

        private void LoadInterstitial()
        {
            if (AdsRemoved || _isInterstitialLoading || _interstitialAd != null)
            {
                return; // 削除済み / ロード中 / 既にロード済みなら二重ロードしない
            }

            _isInterstitialLoading = true;
            Debug.Log("[AdMobService] インタースティシャル広告のロード開始");

            InterstitialAd.Load(AdConfig.InterstitialAdUnitId, BuildAdRequest(), (InterstitialAd ad, LoadAdError error) =>
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    _isInterstitialLoading = false;

                    if (error != null)
                    {
                        Debug.LogWarning($"[AdMobService] インタースティシャル広告ロード失敗: {error}");
                        return;
                    }

                    if (ad == null)
                    {
                        Debug.LogError("[AdMobService] インタースティシャル広告ロード: ad が null (想定外)");
                        return;
                    }

                    _interstitialAd = ad;
                    RegisterInterstitialEvents(ad);
                    Debug.Log("[AdMobService] インタースティシャル広告ロード完了");
                });
            });
        }

        private void RegisterInterstitialEvents(InterstitialAd ad)
        {
            // Load 完了時に 1 回だけ登録する (Show のたびに登録し直さない。GMA のサンプルと同じ流儀)。
            // 各 InterstitialAd インスタンスは Show() で 1 回使い切りのため、ShowInterstitial() 呼び出し側
            // ごとの onClosed は _pendingInterstitialOnClosed フィールド経由で橋渡しする。
            ad.OnAdFullScreenContentClosed += () =>
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    Debug.Log("[AdMobService] インタースティシャル広告クローズ");
                    InvokePendingInterstitialClosed();
                    LoadInterstitial(); // 次を先読み
                });
            };
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    Debug.LogWarning($"[AdMobService] インタースティシャル広告表示失敗: {error}");
                    InvokePendingInterstitialClosed();
                    LoadInterstitial();
                });
            };
        }

        private void InvokePendingInterstitialClosed()
        {
            // onClosed は必ず 1 回だけ呼ぶ契約 (IAdService.ShowInterstitial ドキュメント参照、silent failure 禁止)。
            // OnAdFullScreenContentClosed / Failed の両方が結線されているため二重呼び出しをガードする。
            if (_pendingInterstitialClosedHandled)
            {
                return;
            }

            _pendingInterstitialClosedHandled = true;
            Action callback = _pendingInterstitialOnClosed;
            _pendingInterstitialOnClosed = null;
            callback?.Invoke();
        }

        public void ShowInterstitial(Action onClosed = null)
        {
            if (AdsRemoved)
            {
                // Swift: guard !isAdRemoved else { completion(true); return }
                onClosed?.Invoke();
                return;
            }

            if (_interstitialAd == null || !_interstitialAd.CanShowAd())
            {
                Debug.LogWarning("[AdMobService] インタースティシャル広告が準備できていません - 即座にクローズ扱い");
                onClosed?.Invoke();
                LoadInterstitial(); // 次回に備えて読み込み直す
                return;
            }

            _pendingInterstitialOnClosed = onClosed;
            _pendingInterstitialClosedHandled = false;

            InterstitialAd adToShow = _interstitialAd;
            _interstitialAd = null; // 表示中は参照を手放す (二重 Show 防止、Swift と同じ思想)

            Debug.Log("[AdMobService] インタースティシャル広告表示");
            adToShow.Show();
        }

        // MARK: - ATT / Consent

        public void RequestTrackingAuthorization(Action onDone = null)
        {
            AttBridge.RequestTrackingAuthorization(status =>
            {
                _personalizedAdsAllowed = status == AttBridge.AttStatus.Authorized;
                Debug.Log($"[AdMobService] ATT ステータス: {status} (personalizedAdsAllowed={_personalizedAdsAllowed})");
                onDone?.Invoke();
            });
        }

        // MARK: - Helpers

        private AdRequest BuildAdRequest()
        {
            var request = new AdRequest();
            if (!_personalizedAdsAllowed)
            {
                // ATT 未許可 (または未確定) の間は非パーソナライズ広告のみ要求する
                // (npa=1 は AdMob 公式の非パーソナライズ化フラグ、UMP 未導入環境での標準対策。
                // クラスヘッダのコメント参照)。
                request.Extras.Add("npa", "1");
            }
            return request;
        }
    }
}
