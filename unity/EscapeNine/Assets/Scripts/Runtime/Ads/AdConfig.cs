// AdConfig.cs
// Swift 正本からの忠実移植: Services/AdMobService.swift の AdMobConfig 構造体。
// 広告ユニット ID は「公開識別子」(アプリバイナリに埋め込まれ秘匿性なし) のためハードコードでよい
// (docs/unity-ads-iap-decision-brief.md §2.2、feedback_no_creds_in_docs の対象外)。
//
// ★ ここは ID 定数のみ。GMA/Unity Ads の実 API は一切呼ばない (ネイティブ SDK 未導入、
//   Packages/manifest.json 変更禁止のスコープ)。着手時ランブックは decision brief §4 参照。

namespace EscapeNine.Runtime.Ads
{
    /// <summary>
    /// AdMob 広告ユニット ID (Swift: AdMobConfig)。Editor/Development Build はテスト ID、Release は本番 ID を返す
    /// (本コードベースの debug gating 慣例 #if UNITY_EDITOR || DEVELOPMENT_BUILD に合わせる。Swift の #if DEBUG 相当)。
    /// メディエーション方針 (decision brief §1): A = AdMob Unity plugin + Unity Ads メディエーション継続。
    /// </summary>
    public static class AdConfig
    {
        /// <summary>AdMob App ID (GADApplicationIdentifier 相当。Info.plist/Player Settings 注入は Phase 3)。</summary>
        public const string AppId = "ca-app-pub-5237930968754753~9585848266";

        // テスト用広告ユニット ID (Google 公式サンプル ID、開発時に使用)
        public const string TestBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
        public const string TestInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";

        // 本番用広告ユニット ID (Swift AdMobService と同一構成に揃える。decision brief §2.2)
        private const string ProdBannerAdUnitId = "ca-app-pub-5237930968754753/3156438181";
        private const string ProdInterstitialAdUnitId = "ca-app-pub-5237930968754753/7861969950";

        /// <summary>Unity Ads Game ID (AdMob メディエーション経由、サーバー配信)。
        /// アダプタ導入時は Exact 4.16.500 固定 (4.16.601+ は GMA 13 必須、decision brief §2.3)。</summary>
        public const string UnityAdsGameId = "800002603";

        public static string BannerAdUnitId
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return TestBannerAdUnitId;
#else
                return ProdBannerAdUnitId;
#endif
            }
        }

        public static string InterstitialAdUnitId
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return TestInterstitialAdUnitId;
#else
                return ProdInterstitialAdUnitId;
#endif
            }
        }
    }
}
