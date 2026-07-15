// App.cs
// Swift 正本からの忠実移植: EscapeNine_endless_App.swift (エントリポイント) 相当。
// Unity ではシングルトン MonoBehaviour としてサービス群を生成・結線し、
// 配下の ScreenBase を全て構築して Home を表示する。
//
// 生成順序が重要:
//   PlayerState (永続化) → AudioDirector (音、PlayerState 依存) → Conductor (拍クロック)
//   → GameController (全部に依存) → ScreenRouter / 各 Screen (UI、App.I 経由で全サービスへ)

using UnityEngine;
using EscapeNine.Runtime.UI;
using EscapeNine.Runtime.Ads;
using EscapeNine.Runtime.IAP;
using EscapeNine.Runtime.Analytics;
using EscapeNine.Runtime.Ranking;

namespace EscapeNine.Runtime
{
    public sealed class App : MonoBehaviour
    {
        public static App I { get; private set; }

        public PlayerState Player;
        public AudioDirector Audio;
        public EscapeNine.Runtime.UI.ScreenRouter Router;
        public GameController Game;
        public Conductor Conductor;

        /// <summary>
        /// 広告サービスの継ぎ目 (Phase 3 groundwork)。現状は StubAdService (no-op) のみ。
        /// ネイティブ SDK 導入時はここの生成箇所を実装クラスに差し替えるだけでよい
        /// (docs/unity-ads-iap-decision-brief.md §4.2)。
        /// </summary>
        public IAdService Ads;

        /// <summary>
        /// 課金サービスの継ぎ目 (IAP groundwork)。現状は StubIapService (no-op、Editor/DevBuild のみ
        /// 擬似成功) のみ。ネイティブ SDK (Unity IAP) 導入時はここの生成箇所を実装クラスに
        /// 差し替えるだけでよい。
        /// </summary>
        public IIapService Iap;

        /// <summary>
        /// 分析サービスの継ぎ目 (PostHog REST 直叩き、単一ファサード)。UnityWebRequest がコルーチンを
        /// 要するため MonoBehaviour として GameObject に AddComponent する (Audio/Conductor と同じ流儀)。
        /// </summary>
        public AnalyticsService Analytics;

        /// <summary>ローカルランキング (契約外の追加公開。Ranking 画面 / Result 画面が参照)。</summary>
        public RankingStore Ranking;

        /// <summary>
        /// 世界ランキング (Firebase Auth REST + Firestore REST 直叩き、単一ファサード)。UnityWebRequest が
        /// コルーチンを要するため MonoBehaviour として GameObject に AddComponent する
        /// (Analytics と同じ流儀)。
        /// </summary>
        public OnlineRankingService OnlineRanking;

        /// <summary>デイリーチャレンジ (契約外の追加公開、Phase 2.5)。Home / DailyChallenge 画面が参照。</summary>
        public DailyChallengeStore DailyChallenge;

        /// <summary>
        /// 画面 (ScreenBase) 群の親。画面は App の子ではなく Canvas/ScreenRoot の子に置かれるため、
        /// MainSceneBuilder がシーン生成時にここへ参照を割り当てる。
        /// 未割り当てでもシーン全体検索 (非アクティブ含む) にフォールバックして動く。
        /// </summary>
        public RectTransform screenRoot;

        private void Awake()
        {
            // シングルトン確立 (二重生成はシーン再ロード時などに起こり得る)
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            DontDestroyOnLoad(gameObject);

            // --- サービス初期化 ---
            // Swift 版セーブデータの一回限り移行 (B-1)。PlayerState.Load() が PlayerPrefs を
            // 読み込む前に、Swift 形式 (NSUserDefaults NSArray/Data・Keychain) のデータを
            // Unity 形式へ変換しておく必要があるため、PlayerState 生成より必ず前に呼ぶ。
            SwiftSaveMigration.RunOnce();
            Player = new PlayerState();
            Ranking = new RankingStore();
            DailyChallenge = new DailyChallengeStore();

            // 分析 (PostHog REST): UnityWebRequest のコルーチン実行主体として MonoBehaviour 化。
            // Iap より前に構築する (OnPurchasePending からの LogPurchase 呼び出しに使うため)。
            Analytics = GetComponent<AnalyticsService>();
            if (Analytics == null) Analytics = gameObject.AddComponent<AnalyticsService>();

            // 世界ランキング (Firebase Auth REST + Firestore REST): UnityWebRequest のコルーチン実行主体
            // として MonoBehaviour 化 (Analytics と同じ流儀)。起動時に EnsureAuth を 1 回だけ呼び、
            // 以後は保存済み refreshToken で再認証する (匿名UIDを永続化、毎起動 signUp しない)。
            OnlineRanking = GetComponent<OnlineRankingService>();
            if (OnlineRanking == null) OnlineRanking = gameObject.AddComponent<OnlineRankingService>();
            OnlineRanking.EnsureAuth(success =>
            {
                if (!success)
                {
                    Debug.LogWarning("[App] 世界ランキングの認証に失敗しました (オフライン等、ゲーム進行には影響しない)");
                }
            });

            // 広告 (GMA 11.2.0 実 SDK): PlayerState 生成後に構築し、
            // AdRemoved (StoreKit/IAP 購入導線が更新する既存フラグ) をそのまま参照させる。
            // 呼び出し順序 Initialize() → RequestTrackingAuthorization() は固定
            // (init=denied → ATT 完了後に npa 切替。AdMobService クラスヘッダ参照)。
            Ads = new AdMobService(Player);
            Ads.Initialize();
            // ATT はアプリ起動につき 1 回 (Awake は App シングルトン確立時にしか通らないため自然に once-only)。
            Ads.RequestTrackingAuthorization();

            // IAP (Unity IAP 5.4.1): PlayerState 生成後に構築し、
            // 購入済み商品 (PurchasedProducts / AdRemoved、既存の SSOT) をそのまま参照させる。
            Iap = new UnityIapService(Player, Analytics);
            Iap.Initialize();

            // UnityEngine.Object の null 判定は == を使う (?? は偽 null を貫通するため不可)
            Audio = GetComponent<AudioDirector>();
            if (Audio == null) Audio = gameObject.AddComponent<AudioDirector>();
            Audio.Init(Player);

            // Conductor は [RequireComponent(typeof(AudioSource))] のため AddComponent で
            // AudioSource も自動追加される。song は割り当てない = 純粋な拍クロックとして使い、
            // 楽曲再生は AudioDirector に一本化する (Swift の BeatEngine / bgmPlayer 分離を踏襲)。
            Conductor = GetComponent<Conductor>();
            if (Conductor == null) Conductor = gameObject.AddComponent<Conductor>();

            Game = GetComponent<GameController>();
            if (Game == null) Game = gameObject.AddComponent<GameController>();
            Game.Configure(Conductor, Audio, Player, Ranking, DailyChallenge, Analytics);

            // --- UI 構築 ---
            // 画面は Canvas/ScreenRoot 配下 (= App の子ではない) ため、screenRoot 経由で発見する。
            // screenRoot 未割り当て (手組みシーン等) はシーン全体検索にフォールバック。
            // 画面は初期状態で inactive のため includeInactive=true / FindObjectsInactive.Include が必須。
            Router = new ScreenRouter();
            ScreenBase[] screens = screenRoot != null
                ? screenRoot.GetComponentsInChildren<ScreenBase>(true)
                : FindObjectsByType<ScreenBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (screens.Length == 0)
            {
                Debug.LogError("[App] ScreenBase が 1 つも見つからない。MainSceneBuilder でシーンを生成したか確認すること。");
            }
            foreach (var screen in screens)
            {
                // BuildUI は Register 内で 1 回だけ呼ばれる (ここで直接呼ぶと二重構築になる)
                Router.Register(screen);
            }

            // Fx 基盤 (Phase 4 juice): screenRoot の最後の子として常駐させ、
            // 画面切替に関わらず常に最前面へ破片バースト等を描画できるようにする。
            RectTransform fxParent = screenRoot != null
                ? screenRoot
                : (screens.Length > 0 ? screens[0].transform.parent as RectTransform : null);
            if (fxParent != null)
            {
                EscapeNine.Runtime.UI.Fx.FxLayer.Install(fxParent);
            }

            // Phase 6a (デスクトップ/Steam体験版基盤): モバイルでは無害な追加コンポーネント。
            // ピラーボックスは Fx と同じ contentRoot (screenRoot) を対象にする。
            DesktopPillarbox.Install(screenRoot);
            gameObject.AddComponent<KeyboardInput>();

            Router.Show(ScreenId.Home);
            Audio.PlayMenuBgm();
        }
    }
}
