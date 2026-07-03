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

        /// <summary>ローカルランキング (契約外の追加公開。Ranking 画面 / Result 画面が参照)。</summary>
        public RankingStore Ranking;

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
            Player = new PlayerState();
            Ranking = new RankingStore();

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
            Game.Configure(Conductor, Audio, Player, Ranking);

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

            Router.Show(ScreenId.Home);
            Audio.PlayMenuBgm();
        }
    }
}
