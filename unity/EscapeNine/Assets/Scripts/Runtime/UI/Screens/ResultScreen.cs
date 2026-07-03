// ResultScreen.cs
// Swift 正本: Views/Result/ResultView.swift (Sprint 1「発射台型 Game Over」)
//             + Views/Components/ShareSheet.swift (シェア導線 = ShareTextBuilder)
// 「離脱口」ではなく「発射台」: 巨大リトライを主役に、シェア/ホームは補助ボタン。
// Sprint 1 の 5 要素 = 1) 惜しさメーター 2) 巨大リトライ 3) 挑戦時間 4) 自己ベスト誘発演出 5) シェア
// を静的 UI として移植する (bounceIn / shimmer / glow 等のアニメーション演出は Phase 4 送り)。
//
// レイアウトは全て UIFactory.Place の親比率 0..1 (固定 px 禁止)。
// 座標系は Unity 左下原点: cy が大きいほど画面上部 (Swift の top-left と逆)。

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EscapeNine.Core;
using EscapeNine.Runtime.UI.Fx;

namespace EscapeNine.Runtime.UI
{
    /// <summary>
    /// リザルト画面 (ScreenId.Result)。
    /// payload は ScreenPayloads.cs の <see cref="ResultPayload"/> を正の契約とする。
    /// null または別型が来た場合は GameController / GameSession / PlayerState から補完し、
    /// payload なしでも完全動作する (Swift の ResultView デフォルト引数と同じ後方互換思想)。
    /// </summary>
    public sealed class ResultScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Result;

        // MARK: - 表示データ (OnShow で解決した結果のスナップショット)

        private struct ResultData
        {
            public int Floor;               // 到達階層 (勝利時は Swift quirk 踏襲で 101)
            public bool IsVictory;
            public DefeatReason? Reason;    // 敗北時のみ
            public bool IsNewBest;          // Swift: isPersonalBest = floor > previousBest
            public int PreviousBest;        // 「ベスト: N階」表示用 (永続化前の最高記録)
            public int BestFloor;           // 「最高記録」表示用 (Swift: max(floor, previousBest))
            public double ElapsedSeconds;   // 挑戦時間
            public int NearMissDistance;    // 1 = あと1マス (Chebyshev 距離)
            public int PlayerPosition;      // シェア用 (1-9)
            public int EnemyPosition;       // シェア用 (1-9)
            public string CharacterName;    // 使用キャラクター名
        }

        private ResultData _data;

        // MARK: - リトライ条件 (Swift: onPlayAgain は GameView が「同キャラ・同難易度」で再スタート)
        private CharacterType _retryCharacter = CharacterType.Hero;
        private AILevel _retryLevel = AILevel.Easy;

        // MARK: - 動的 UI 参照 (OnShow のたびに書き換える)
        private Text _titleLabel;
        private RectTransform _bestBadge;       // 「自己ベスト!」バッジ (新記録時のみ)
        private Text _bestCaptionLabel;         // 「ベスト: N階」(新記録でない時のみ)
        private Text _floorNumberLabel;
        private Text _elapsedLabel;
        private Text _characterNameLabel;
        private RectTransform _defeatRow;
        private Text _defeatLabel;
        private RectTransform _newRecordBadge;  // カード内の「NEW RECORD!」(Swift は自己ベストと二重に出す仕様)
        private Text _bestFloorLabel;
        private RectTransform _nearMissBanner;  // 「あと1マスで生存だった!」
        private GameObject _oneTapLayer;        // ワンタップリトライ用透明レイヤー
        private RectTransform _toast;
        private Text _toastLabel;
        private Coroutine _toastRoutine;

        // ---- 実績解除ポップアップ (Swift: AchievementPopupView) ----
        private RectTransform _achievementPopup;
        private Image _achievementPopupImage;
        private Text _achievementPopupLabel;
        private Coroutine _achievementPopupRoutine;

        // MARK: - 内部状態

        // App.cs は screen.BuildUI() → Router.Register(screen) の順で呼び、Register 内でも
        // BuildUI() が走るため計 2 回呼ばれる。二重構築 (UI が重なって全ボタン二重化) を防ぐガード。
        private bool _built;
        private bool _subscribed;

        /// <summary>
        /// ラン開始時点の最高記録スナップショット (-1 = 未取得)。payload 欠損時のフォールバック用。
        /// Swift はリザルト表示前に GameView が「永続化する直前の highestFloor」を previousBest として
        /// 渡すが、Unity 版は GameController.EndGame が先に UpdateHighestFloor するため、
        /// リザルト表示時に PlayerState を読むと常に「新記録でない」判定になってしまう。
        /// そこで OnGameStarted 時点の HighestFloor を控えておく — ラン中に HighestFloor は
        /// 変化しない (EndGame でのみ更新) ので、このスナップショットが Swift の previousBest と一致する。
        /// </summary>
        private int _bestBeforeRun = -1;

        /// <summary>Swift: appearTime。eg_retry_tapped.seconds_until_tap の基準 (Phase 3 計装用)。</summary>
        private float _shownAtRealtime;

        // MARK: - Phase 4 (juice) 演出用の参照・コルーチンハンドル
        // 「発射台」演出 (担当B): 既存の ApplyData (表示 ON/OFF ロジック) は変更せず、
        // 演出だけをここに追加する。参照は BuildUI で 1 回だけ捕捉する。
        private RectTransform _statsCardRt;      // 統計カード (時差 SlideIn 用。Swift: statsSection.slideIn(from:.bottom, delay:0.4))
        private RectTransform _retryButtonRt;    // 巨大リトライボタン (BeatPulse + フォールバックパルス用)
        private Image _bestBadgeImage;           // 「自己ベスト!」バッジの背景 (Flash用)
        private Image _nearMissBannerImage;      // 惜しさメーターの背景 (Flash用)

        private Coroutine _entranceRoutine;
        private Coroutine _newRecordLoopRoutine;
        private Coroutine _retryFallbackPulseRoutine;

        // MARK: - BuildUI

        public override void BuildUI()
        {
            if (_built) return; // Router.Register が 1 回だけ呼ぶ (再入防御。呼び出し元は ScreenRouter.Register のみ)
            _built = true;

            // 画面ルートを親いっぱいに固定 (シーン側の配置ミスに影響されないための防御。HomeScreen と同作法)
            var rootRt = GetComponent<RectTransform>();
            if (rootRt != null)
            {
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;
            }

            // ---- 背景 (Swift: GameBackground)。ノッチ下まで全面に敷く ----
            var bg = UIFactory.ColorRect(transform, "Background", UITheme.Background);
            UIFactory.Place((RectTransform)bg.transform, 0.5f, 0.5f, 1f, 1f);

            // ---- ワンタップリトライ用透明レイヤー (Swift: Sprint 1 Issue 02) ----
            // Swift は ZStack 最下層に Color.clear + onTapGesture を敷き、上に重なるボタンが
            // ヒットテストで優先される。uGUI では「先に生成した兄弟」が描画順で下 = raycast 優先度も低い。
            // 後から生成するボタン群がタップを先取りするため、Swift と同じ
            // 「ボタン優先・余白タップでリトライ」の優先順位がそのまま成立する。
            // alpha=0 の Image でも raycastTarget=true (Panel の仕様) ならタップを拾える。
            RectTransform oneTap = UIFactory.Panel(transform, "OneTapRetryLayer", new Color(0f, 0f, 0f, 0f));
            Button oneTapButton = oneTap.gameObject.AddComponent<Button>();
            oneTapButton.transition = Selectable.Transition.None; // 透明レイヤーなので視覚フィードバック無し
            oneTapButton.onClick.AddListener(TriggerRetry);
            _oneTapLayer = oneTap.gameObject;

            // ---- コンテンツはセーフエリア内に収める (SwiftUI では自動処理されていた部分) ----
            RectTransform safe = UIFactory.Panel(transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // ---- タイトル (VICTORY! / DEFEAT) ----
            // Swift はグラデーション文字 + グロー。単色に簡略化 (グラデ/グロー/bounceIn は Phase 4)。
            _titleLabel = UIFactory.Label(safe, "Title", "DEFEAT", 110, UITheme.Warning,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_titleLabel.transform, 0.5f, 0.905f, 0.9f, 0.08f);

            // ---- 自己ベスト演出 (Swift: personalBestSection) ----
            // 新記録時: 「自己ベスト!」バッジ / 非更新時: 「ベスト: N階」控えめ表示 (排他)
            _bestBadge = UIFactory.Panel(safe, "PersonalBestBadge", UITheme.WithAlpha(UITheme.Available, 0.18f));
            UIFactory.Place(_bestBadge, 0.5f, 0.825f, 0.56f, 0.048f);
            AddBorder(_bestBadge, UITheme.WithAlpha(UITheme.Available, 0.6f), 0.010f, 0.06f);
            _bestBadgeImage = _bestBadge.GetComponent<Image>(); // Phase4 juice: NEW RECORD ループ内で Flash する対象
            Text badgeLabel = UIFactory.Label(_bestBadge, "Label", "自己ベスト!", 48, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)badgeLabel.transform, 0.5f, 0.5f, 1f, 1f);

            _bestCaptionLabel = UIFactory.Label(safe, "PreviousBestCaption", "",
                32, UITheme.WithAlpha(UITheme.TextColor, 0.7f));
            UIFactory.Place((RectTransform)_bestCaptionLabel.transform, 0.5f, 0.825f, 0.6f, 0.04f);

            // ---- 統計カード (Swift: statsSection / GameCard) ----
            RectTransform card = UIFactory.Panel(safe, "StatsCard", UITheme.BackgroundSecondary);
            UIFactory.Place(card, 0.5f, 0.615f, 0.84f, 0.30f);
            AddBorder(card, UITheme.WithAlpha(UITheme.GridBorder, 0.5f), 0.008f, 0.012f); // GameCard のゴールド枠
            _statsCardRt = card; // Phase4 juice: タイトルより遅れて SlideIn する時差演出用

            Text floorCaption = UIFactory.Label(card, "FloorCaption", "到達階層",
                32, UITheme.WithAlpha(UITheme.TextColor, 0.7f));
            UIFactory.Place((RectTransform)floorCaption.transform, 0.5f, 0.92f, 0.9f, 0.10f);

            // Swift: AnimatedNumber (0.8 秒カウントアップ) + glow → 静的表示に簡略化 (Phase 4)
            _floorNumberLabel = UIFactory.Label(card, "FloorNumber", "1", 130, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_floorNumberLabel.transform, 0.5f, 0.775f, 0.9f, 0.20f);

            // 挑戦時間 (Swift: elapsedSeconds > 0 のときのみ表示。stopwatch アイコンは Phase 4)
            _elapsedLabel = UIFactory.Label(card, "Elapsed", "", 34, UITheme.GoldText);
            UIFactory.Place((RectTransform)_elapsedLabel.transform, 0.5f, 0.60f, 0.9f, 0.09f);

            // 使用キャラクター (Swift: caption + 名前の 2 トーン HStack。左右 2 ラベルで再現)
            Text charCaption = UIFactory.Label(card, "CharacterCaption", "使用キャラクター",
                32, UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleRight);
            UIFactory.Place((RectTransform)charCaption.transform, 0.30f, 0.49f, 0.44f, 0.09f);
            _characterNameLabel = UIFactory.Label(card, "CharacterName", "", 34, UITheme.GoldText,
                TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)_characterNameLabel.transform, 0.70f, 0.49f, 0.34f, 0.09f);

            // 敗因表示 (Swift: result == .lose && defeatReason != nil のときのみ。
            //  SF Symbols アイコン (exclamationmark.triangle / clock.badge.xmark) は Phase 4 で画像化)
            _defeatRow = UIFactory.Panel(card, "DefeatRow", UITheme.WithAlpha(UITheme.Warning, 0.10f));
            UIFactory.Place(_defeatRow, 0.5f, 0.37f, 0.70f, 0.10f);
            AddBorder(_defeatRow, UITheme.WithAlpha(UITheme.Warning, 0.3f), 0.008f, 0.04f);
            _defeatLabel = UIFactory.Label(_defeatRow, "Label", "", 34, UITheme.Warning);
            UIFactory.Place((RectTransform)_defeatLabel.transform, 0.5f, 0.5f, 1f, 1f);

            // NEW RECORD! (Swift: 自己ベストバッジと二重表示のまま残す既存仕様を踏襲)
            _newRecordBadge = UIFactory.Panel(card, "NewRecordBadge", UITheme.WithAlpha(UITheme.Available, 0.15f));
            UIFactory.Place(_newRecordBadge, 0.5f, 0.24f, 0.60f, 0.10f);
            AddBorder(_newRecordBadge, UITheme.WithAlpha(UITheme.Available, 0.5f), 0.010f, 0.05f);
            Text newRecordLabel = UIFactory.Label(_newRecordBadge, "Label", "NEW RECORD!",
                40, UITheme.Available, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)newRecordLabel.transform, 0.5f, 0.5f, 1f, 1f);

            // 最高記録 (Swift: 常時表示の控えめ 2 段)
            Text bestCaption = UIFactory.Label(card, "BestCaption", "最高記録",
                28, UITheme.WithAlpha(UITheme.TextColor, 0.5f));
            UIFactory.Place((RectTransform)bestCaption.transform, 0.5f, 0.115f, 0.9f, 0.07f);
            _bestFloorLabel = UIFactory.Label(card, "BestFloor", "", 30, UITheme.GoldText);
            UIFactory.Place((RectTransform)_bestFloorLabel.transform, 0.5f, 0.048f, 0.9f, 0.07f);

            // TODO(Phase 3): Firebase 未サインイン時の「ハイスコアの保存にはネット接続が必要です」
            //                注記 (Swift: firebaseService.isSignedIn)。Firebase 導入時に追加。

            // ---- 惜しさメーター (Swift: nearMissBanner。敗北 + 隣接死亡時のみ) ----
            _nearMissBanner = UIFactory.Panel(safe, "NearMissBanner", UITheme.WithAlpha(UITheme.Warning, 0.15f));
            UIFactory.Place(_nearMissBanner, 0.5f, 0.435f, 0.72f, 0.05f);
            AddBorder(_nearMissBanner, UITheme.WithAlpha(UITheme.Warning, 0.55f), 0.008f, 0.06f);
            _nearMissBannerImage = _nearMissBanner.GetComponent<Image>(); // Phase4 juice: 表示時に赤 Flash 1 回
            Text nearMissLabel = UIFactory.Label(_nearMissBanner, "Label", "あと1マスで生存だった!",
                40, UITheme.Warning, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)nearMissLabel.transform, 0.5f, 0.5f, 1f, 1f);

            // ---- 巨大リトライボタン (Swift: buttonSection の主役、height 96 相当) ----
            // Swift は available→success の対角グラデーション + glow。単色 (中間色) に簡略化 (Phase 4)。
            Color retryBg = Color.Lerp(UITheme.Available, UITheme.Success, 0.5f);
            Button retryButton = UIFactory.TextButton(safe, "RetryButton", "もう一回", 64,
                retryBg, Color.white, TriggerRetry);
            var retryRt = (RectTransform)retryButton.transform;
            UIFactory.Place(retryRt, 0.5f, 0.30f, 0.88f, 0.11f);
            AddBorder(retryRt, UITheme.WithAlpha(Color.white, 0.25f), 0.006f, 0.03f);
            _retryButtonRt = retryRt;
            // Phase4 juice: 曲が再生中 (Conductor が拍を刻んでいる) の間は拍に合わせて微パルス。
            // 停止中 (Result 画面の常態) は RetryFallbackPulseRoutine が 1 秒周期の自前パンチで代替する。
            BeatPulse retryBeatPulse = retryRt.gameObject.AddComponent<BeatPulse>();
            retryBeatPulse.scaleAmount = 0.05f;
            retryBeatPulse.alphaAmount = 0f;
            retryBeatPulse.onlyWhilePlaying = true;

            // ---- 補助ボタン (シェア / ホーム)。Swift: 横並び 2 分割・控えめトーン ----
            Color subBg = UITheme.WithAlpha(UITheme.TextColor, 0.08f);
            Button shareButton = UIFactory.TextButton(safe, "ShareButton", "シェア", 36,
                subBg, UITheme.TextColor, HandleShare);
            UIFactory.Place((RectTransform)shareButton.transform, 0.275f, 0.21f, 0.42f, 0.055f);

            Button homeButton = UIFactory.TextButton(safe, "HomeButton", "ホーム", 36,
                subBg, UITheme.TextColor, HandleHome);
            UIFactory.Place((RectTransform)homeButton.transform, 0.725f, 0.21f, 0.42f, 0.055f);

            // ---- トースト (「コピーしました」。シェア = クリップボードコピーの Phase 2 簡略化に伴う通知) ----
            _toast = UIFactory.Panel(safe, "Toast", UITheme.WithAlpha(Color.black, 0.75f));
            UIFactory.Place(_toast, 0.5f, 0.115f, 0.5f, 0.045f);
            _toastLabel = UIFactory.Label(_toast, "Label", "コピーしました", 32, UITheme.GoldText);
            UIFactory.Place((RectTransform)_toastLabel.transform, 0.5f, 0.5f, 1f, 1f);
            _toast.gameObject.SetActive(false);

            // ---- 実績解除ポップアップ (Swift: AchievementPopupView。簡易バナー) ----
            // 最後に生成し、他の全要素より手前に描画されるようにする (Toast と同じ作法)。
            // 画面上端ぎりぎりに置くことで Title (cy=0.905, 高さ 0.08 → 上端 0.945) との重なりを避ける。
            _achievementPopup = UIFactory.Panel(safe, "AchievementPopup", UITheme.BackgroundSecondary);
            UIFactory.Place(_achievementPopup, 0.5f, 0.972f, 0.86f, 0.055f);
            AddBorder(_achievementPopup, UITheme.WithAlpha(UITheme.Available, 0.6f), 0.008f, 0.05f);
            _achievementPopupImage = _achievementPopup.GetComponent<Image>(); // FxKit.Flash 対象
            _achievementPopupLabel = UIFactory.Label(_achievementPopup, "Label", "", 32, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_achievementPopupLabel.transform, 0.5f, 0.5f, 0.92f, 0.85f);
            _achievementPopup.gameObject.SetActive(false);

            TrySubscribe();
        }

        /// <summary>
        /// 枠線 (Swift: RoundedRectangle().stroke() 相当の簡易版)。
        /// tx = 左右線の太さ (要素幅比)、ty = 上下線の太さ (要素高さ比)。
        /// 角丸は uGUI 標準では不可のため直角枠 (角丸スプライト化は Phase 4)。
        /// </summary>
        private static void AddBorder(RectTransform target, Color color, float tx, float ty)
        {
            UIFactory.Place((RectTransform)UIFactory.ColorRect(target, "BorderTop", color).transform,
                0.5f, 1f, 1f, ty);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(target, "BorderBottom", color).transform,
                0.5f, 0f, 1f, ty);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(target, "BorderLeft", color).transform,
                0f, 0.5f, tx, 1f);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(target, "BorderRight", color).transform,
                1f, 0.5f, tx, 1f);
        }

        // MARK: - ライフサイクル

        public override void OnShow(object payload)
        {
            TrySubscribe(); // BuildUI 時点で App 未初期化だった場合の保険

            _shownAtRealtime = Time.realtimeSinceStartup; // Swift: appearTime = Date()
            _data = ResolveData(payload as ResultPayload);
            ApplyData();
            PlayEntranceEffects(); // Phase4 juice: 表示データ確定後に演出を開始 (ApplyData のロジックには手を入れない)

            // Swift: onAppear の Haptic (win=heavy / lose=medium) は Phase 4 送り。
            // Swift: InterstitialAdPresenter.show (表示 0.5 秒後) は Phase 3 (広告) のため置かない。
        }

        public override void OnHide()
        {
            HideToast();
            StopEntranceEffects();
            if (_achievementPopup != null) _achievementPopup.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_subscribed && App.I != null && App.I.Game != null)
            {
                App.I.Game.OnGameStarted -= HandleGameStarted;
            }
        }

        /// <summary>GameController.OnGameStarted の購読 (自己ベストスナップショット用)。</summary>
        private void TrySubscribe()
        {
            if (_subscribed) return;
            if (App.I == null || App.I.Game == null) return;
            App.I.Game.OnGameStarted += HandleGameStarted;
            _subscribed = true;
        }

        private void HandleGameStarted()
        {
            // ラン開始 (GO) 時点の最高記録を控える。ラン中 HighestFloor は不変 (EndGame でのみ更新)
            // なので、この値が「永続化直前のベスト」= Swift の previousBest と一致する。
            if (App.I != null && App.I.Player != null)
            {
                _bestBeforeRun = App.I.Player.HighestFloor;
            }
        }

        // MARK: - データ解決 (ResultPayload → GameController/Session → PlayerState の順に補完)

        private ResultData ResolveData(ResultPayload p)
        {
            GameController game = (App.I != null) ? App.I.Game : null;
            GameSession session = (game != null) ? game.Session : null;
            PlayerState player = (App.I != null) ? App.I.Player : null;

            ResultData d = new ResultData();

            if (p != null)
            {
                // 正規ルート: GameScreen が ScreenPayloads.ResultPayload で全項目を渡してくる
                d.Floor = p.Floor;
                d.IsVictory = p.Won;
                d.Reason = p.DefeatReason;
                d.ElapsedSeconds = p.ElapsedSeconds;
                d.NearMissDistance = p.NearMissDistance;
                d.PlayerPosition = p.PlayerPosition;
                d.EnemyPosition = p.EnemyPosition;
                d.PreviousBest = p.PreviousBest;
                d.IsNewBest = d.Floor > d.PreviousBest; // Swift: isPersonalBest
            }
            else
            {
                // フォールバック: payload なし (または想定外の型)。サービス層から直接読む。
                d.Floor = (session != null) ? session.CurrentFloor
                        : (player != null) ? Math.Max(player.HighestFloor, 1) : 1;
                d.IsVictory = session != null && session.Status == GameStatus.Win;
                d.Reason = (session != null) ? session.LastDefeatReason : (DefeatReason?)null;
                d.ElapsedSeconds = (game != null) ? game.ElapsedSeconds : 0;
                d.NearMissDistance = (game != null) ? game.NearMissDistance : 0;
                d.PlayerPosition = (session != null) ? session.PlayerPosition : 0;
                d.EnemyPosition = (session != null) ? session.EnemyPosition : 0;

                int stored = (player != null) ? player.HighestFloor : 0;
                if (_bestBeforeRun >= 0)
                {
                    d.PreviousBest = _bestBeforeRun;
                    d.IsNewBest = d.Floor > d.PreviousBest;
                }
                else
                {
                    // スナップショット未取得 (Result が単体で表示された等)。EndGame の永続化後なので
                    // floor >= 保存値 なら「今回が記録を作った」とみなす。previousBest=0 に落とすのは
                    // Swift のデフォルト値 0 (「常に新記録」の安全側) と同思想。
                    d.PreviousBest = stored;
                    d.IsNewBest = d.Floor >= stored && d.Floor > 0;
                    if (d.IsNewBest) d.PreviousBest = 0;
                }
            }

            // Swift: bestFloor = max(floor, previousBest)。加えて永続化済みの値とも max を取り、
            // payload の欠損があっても「最高記録」欄が保存値より小さく表示されないよう防御する。
            d.BestFloor = Math.Max(d.Floor, d.PreviousBest);
            if (player != null) d.BestFloor = Math.Max(d.BestFloor, player.HighestFloor);

            // リトライ条件 + キャラ名: 終わったランの Session が正 (Swift: GameView が同条件で再スタート)。
            // Session が無い場合は PlayerState の選択値 (Result 画面上で選択は変わらないため等価)。
            _retryCharacter = (session != null) ? session.CurrentCharacter.Type
                            : (player != null) ? player.SelectedCharacter : CharacterType.Hero;
            _retryLevel = (session != null) ? session.SelectedAILevel
                        : (player != null) ? player.SelectedAILevel : AILevel.Easy;
            d.CharacterName = Character.GetCharacter(_retryCharacter).Name;

            return d;
        }

        // MARK: - 表示反映

        private void ApplyData()
        {
            bool win = _data.IsVictory;

            // タイトル (Swift: resultTitle。グラデ + glow → 単色簡略化)
            _titleLabel.text = win ? "VICTORY!" : "DEFEAT";
            _titleLabel.color = win ? UITheme.Success : UITheme.Warning;
            // TODO(Phase 4): 勝利時の CelebrationEffect (紙吹雪) / bounceIn / shimmer / glow 演出。

            // 自己ベスト演出 (Swift: personalBestSection。バッジと「ベスト: N階」は排他)
            _bestBadge.gameObject.SetActive(_data.IsNewBest);
            bool showPrevBest = !_data.IsNewBest && _data.PreviousBest > 0;
            _bestCaptionLabel.gameObject.SetActive(showPrevBest);
            if (showPrevBest)
            {
                _bestCaptionLabel.text = "ベスト: " + _data.PreviousBest + "階";
            }

            // 統計カード
            _floorNumberLabel.text = _data.Floor.ToString();

            bool showElapsed = _data.ElapsedSeconds > 0;
            _elapsedLabel.gameObject.SetActive(showElapsed);
            if (showElapsed)
            {
                // Swift: Int(elapsedSeconds.rounded()) = 四捨五入 (banker's rounding にしない)
                int seconds = (int)Math.Round(_data.ElapsedSeconds, MidpointRounding.AwayFromZero);
                _elapsedLabel.text = "今回の挑戦時間: " + seconds + "秒";
            }

            _characterNameLabel.text = _data.CharacterName;

            // 敗因 (Swift: result == .lose && defeatReason != nil のときのみ)
            bool showDefeat = !win && _data.Reason.HasValue;
            _defeatRow.gameObject.SetActive(showDefeat);
            if (showDefeat)
            {
                _defeatLabel.text = _data.Reason.Value == DefeatReason.CaughtByEnemy
                    ? "敵に捕まった"
                    : "時間切れ";
            }

            _newRecordBadge.gameObject.SetActive(_data.IsNewBest);
            _bestFloorLabel.text = _data.BestFloor + "階層";

            // 惜しさメーター (Swift: shouldShowNearMiss = lose && nearMissDistance == 1)
            _nearMissBanner.gameObject.SetActive(!win && _data.NearMissDistance == 1);

            // ワンタップリトライ (Swift: oneTapRetryEnabled && result == .lose。設定画面と同一キーを共有)
            bool oneTap = !win && App.I != null && App.I.Player != null && App.I.Player.OneTapRetryEnabled;
            _oneTapLayer.SetActive(oneTap);

            HideToast();
        }

        // MARK: - Phase 4 (juice) 演出 — 発射台型 GameOver
        // Swift 正本の bounceIn/slideIn/shimmer 相当を FxKit で移植する。
        // ここは演出専任: 表示 ON/OFF の判定は ApplyData に残したまま、副作用として動かすだけ。

        /// <summary>OnShow のたびに演出一式を (再) トリガーする。多重起動防止のため必ず前回分を止めてから開始する。</summary>
        private void PlayEntranceEffects()
        {
            if (_entranceRoutine != null) StopCoroutine(_entranceRoutine);
            _entranceRoutine = StartCoroutine(EntranceRoutine());

            if (_newRecordLoopRoutine != null) StopCoroutine(_newRecordLoopRoutine);
            _newRecordLoopRoutine = _data.IsNewBest ? StartCoroutine(NewRecordLoopRoutine()) : null;

            // 惜しさメーター (Swift: shouldShowNearMiss) が出るときだけ赤フラッシュ 1 回
            if (!_data.IsVictory && _data.NearMissDistance == 1 && _nearMissBannerImage != null)
            {
                FxKit.Flash(this, _nearMissBannerImage, UITheme.Warning, 0.4f);
            }

            if (_retryFallbackPulseRoutine != null) StopCoroutine(_retryFallbackPulseRoutine);
            _retryFallbackPulseRoutine = StartCoroutine(RetryFallbackPulseRoutine());

            // 実績解除ポップアップ (Swift: AchievementPopupView)。新規解除が無ければ何もしない。
            if (_achievementPopupRoutine != null) StopCoroutine(_achievementPopupRoutine);
            var unlocked = (App.I != null && App.I.Game != null) ? App.I.Game.LastUnlockedAchievements : null;
            _achievementPopupRoutine = (unlocked != null && unlocked.Count > 0)
                ? StartCoroutine(AchievementPopupRoutine(unlocked))
                : null;
        }

        /// <summary>OnHide で確実に演出コルーチンを止める (SetActive(false) でも自動停止されるが、明示的に手当てする)。</summary>
        private void StopEntranceEffects()
        {
            if (_entranceRoutine != null) { StopCoroutine(_entranceRoutine); _entranceRoutine = null; }
            if (_newRecordLoopRoutine != null) { StopCoroutine(_newRecordLoopRoutine); _newRecordLoopRoutine = null; }
            if (_retryFallbackPulseRoutine != null) { StopCoroutine(_retryFallbackPulseRoutine); _retryFallbackPulseRoutine = null; }
            if (_achievementPopupRoutine != null) { StopCoroutine(_achievementPopupRoutine); _achievementPopupRoutine = null; }
        }

        /// <summary>
        /// 実績解除ポップアップ (Swift: AchievementPopupView の移植。簡易バナー + FxKit SlideIn/Flash)。
        /// 複数解除時は 1 件ずつ順番に表示する (Swift は解除ごとに個別ポップアップが出る挙動を踏襲)。
        /// </summary>
        private IEnumerator AchievementPopupRoutine(IReadOnlyList<Achievement> achievements)
        {
            foreach (var achievement in achievements)
            {
                _achievementPopupLabel.text = "実績解除！ " + achievement.Title() + "\n" + achievement.Description();

                var rt = _achievementPopup;
                rt.anchoredPosition = Vector2.zero; // 中断残留対策 (EntranceRoutine と同じ作法)
                _achievementPopup.gameObject.SetActive(true);

                FxKit.SlideIn(this, rt, new Vector2(0f, 80f), 0.25f);
                if (_achievementPopupImage != null) FxKit.Flash(this, _achievementPopupImage, UITheme.Available, 0.3f);

                yield return new WaitForSecondsRealtime(2.5f); // Swift: 2.5 秒表示

                _achievementPopup.gameObject.SetActive(false);
                yield return new WaitForSecondsRealtime(0.15f); // 次のポップアップとの間隔
            }
            _achievementPopupRoutine = null;
        }

        /// <summary>
        /// Swift: resultTitle.bounceIn(delay:0.1) → statsSection.slideIn(from:.bottom, delay:0.4) の時差演出。
        /// タイトルは上から滑り込んで着地時に軽くシェイク、統計カードは少し遅れて下から滑り込む。
        /// </summary>
        private IEnumerator EntranceRoutine()
        {
            // 再入防御: 直前の OnShow 中に演出が OnHide (SetActive(false)) で強制中断された場合、
            // anchoredPosition が中間値のまま残ることがある。Place() は offsetMin/Max=0 で
            // 配置しているため「静止位置 = (0,0)」が保証される — 毎回ここへ揃えてから滑り込ませる。
            var titleRt = (RectTransform)_titleLabel.transform;
            titleRt.anchoredPosition = Vector2.zero;

            Vector2 statsCardOffset = new Vector2(0f, -260f);
            if (_statsCardRt != null)
            {
                // カードは常時アクティブ (ApplyData で SetActive 制御されない) なので、
                // 遅延中は「静止位置のまま丸見え」にならないよう最初からオフセット位置に置く。
                // SlideIn 呼び出し直前に静止位置へ戻し、そこを target として滑り込ませる
                // (Reduce Motion 時は事前オフセットをスキップし静止位置のまま据え置く)。
                _statsCardRt.anchoredPosition = FxKit.MotionEnabled ? statsCardOffset : Vector2.zero;
            }

            const float titleSlideDuration = 0.3f;
            FxKit.SlideIn(this, titleRt, new Vector2(0f, 240f), titleSlideDuration);
            yield return new WaitForSecondsRealtime(titleSlideDuration);
            FxKit.ShakeRect(this, titleRt, 10f, 0.22f);

            yield return new WaitForSecondsRealtime(0.15f); // 時差演出: カードはタイトルより遅れて登場
            if (_statsCardRt != null)
            {
                _statsCardRt.anchoredPosition = Vector2.zero; // SlideIn の target = ここを静止位置として捕捉させる
                FxKit.SlideIn(this, _statsCardRt, statsCardOffset, 0.35f);
            }
        }

        /// <summary>
        /// NEW RECORD! の間、2 秒周期で控えめに祝福を繰り返す: バッジのパンチ + 金の破片バースト +
        /// 自己ベストバッジのフラッシュ。無限ループだが OnHide / 画面非活性で必ず止まる。
        /// </summary>
        private IEnumerator NewRecordLoopRoutine()
        {
            // 再入防御: 中断された前回ループの残り scale を静止値へ揃えてから開始する。
            _newRecordBadge.localScale = Vector3.one;

            // カード (バッジの親) の SlideIn 演出が落ち着くまで初回バーストを待つ (EntranceRoutine の
            // 合計尺 ≈ 0.3 + 0.15 + 0.35 秒に合わせる)。バッジがまだオフスクリーン付近にある間に
            // 破片が散ると見た目が揃わないため。
            yield return new WaitForSecondsRealtime(0.8f);

            while (true)
            {
                FxKit.PunchScale(this, _newRecordBadge, 0.12f, 0.4f);
                if (FxLayer.I != null) FxLayer.I.BurstAt(_newRecordBadge, UITheme.Available, 10, 500f);
                if (_bestBadgeImage != null) FxKit.Flash(this, _bestBadgeImage, UITheme.GoldText, 0.5f);
                yield return new WaitForSecondsRealtime(2.0f);
            }
        }

        /// <summary>
        /// 巨大リトライボタンの「押したくなる」誘導。BeatPulse は Conductor 再生中のみ脈動するため、
        /// Result 画面のように BGM の拍が進んでいない間は自前の 1 秒周期パンチでフォールバックする。
        /// </summary>
        private IEnumerator RetryFallbackPulseRoutine()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(1.0f);
                bool conductorPlaying = App.I != null && App.I.Conductor != null && App.I.Conductor.SongPositionBeats > 0.0;
                if (!conductorPlaying && _retryButtonRt != null)
                {
                    // Result 画面では Conductor 停止が常態のため、実質こちらが「押したくなる」誘導の本体。
                    // Swift の glow(radius:18, intensity:0.7) 相当の主張度を狙い、控えめな BeatPulse (0.05) より強め。
                    FxKit.PunchScale(this, _retryButtonRt, 0.09f, 0.5f);
                }
            }
        }

        // MARK: - ボタンハンドラ

        /// <summary>
        /// リトライ (巨大ボタン + ワンタップレイヤー共通)。Swift: onPlayAgain
        /// Phase 6a (デスクトップ): KeyboardInput.cs (R/Space/Enter) からも同処理を呼べるよう公開する。
        /// </summary>
        public void TriggerRetry()
        {
            if (App.I == null) return;

            // TODO(Phase 3): AnalyticsLogger.logRetryTapped(fromFloor:, secondsUntilTap:) 相当。
            //                secondsUntilTap = Time.realtimeSinceStartup - _shownAtRealtime。
            // TODO(Phase 3): リトライ時インタースティシャル広告 (Swift: InterstitialAdPresenter)。
            App.I.Audio.PlaySfx("button_tap");

            // 同条件 (同キャラ・同難易度) で 1 階から即再スタート → Game 画面へ。
            // GameScreen の契約 (GameScreen.cs 冒頭コメント):
            //   「HomeScreen / ResultScreen は StartNewRun() 済みで Show(Game) してくる (payload=null)」
            // → GameScreen はラン進行中 (Status==Playing) を検知し、プレゲームオーバーレイを
            //   出さず即 HUD に接続する。HomeScreen と同じ「StartNewRun → Show(Game)」順。
            // 注意: ここで GameStartRequest(AutoStart=true) を渡してはいけない —
            //   GameScreen.BeginRun がもう一度 StartNewRun してしまい (二重開始)、
            //   リトライ条件が PlayerState 由来の値で上書きされるため。
            App.I.Game.StartNewRun(_retryCharacter, _retryLevel, 1);
            App.I.Router.Show(ScreenId.Game);
        }

        /// <summary>ホームへ戻る。Swift: onHome (resetGame + メニュー BGM 復帰は QuitToHome が担う)</summary>
        private void HandleHome()
        {
            if (App.I == null) return;

            // TODO(Phase 3): AnalyticsLogger.logHomeTapped(fromFloor:) 相当。
            App.I.Audio.PlaySfx("button_tap");
            App.I.Game.QuitToHome();
            App.I.Router.Show(ScreenId.Home);
        }

        /// <summary>
        /// シェア。Swift は UIActivityViewController (ネイティブ共有シート) だが、
        /// Unity 標準にネイティブ共有 API が無いため Phase 2 では
        /// 「クリップボードへコピー + トースト」に簡略化する。
        /// テキスト生成は Core の ShareTextBuilder (Swift と同一出力の純関数) に委譲。
        /// </summary>
        private void HandleShare()
        {
            if (App.I != null) App.I.Audio.PlaySfx("button_tap");

            string text = ShareTextBuilder.Build(
                _data.Floor,
                _data.ElapsedSeconds,
                _data.IsVictory,
                _data.PlayerPosition,
                _data.EnemyPosition);

            GUIUtility.systemCopyBuffer = text;
            ShowToast("コピーしました");

            // TODO(Phase 3/6): iOS ネイティブ共有シート (UIActivityViewController) の
            //                  ネイティブプラグイン連携に置き換える。
        }

        // MARK: - トースト

        private void ShowToast(string message)
        {
            _toastLabel.text = message;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            _toast.gameObject.SetActive(true);
            yield return new WaitForSeconds(1.5f);
            _toast.gameObject.SetActive(false);
            _toastRoutine = null;
        }

        private void HideToast()
        {
            if (_toastRoutine != null)
            {
                StopCoroutine(_toastRoutine);
                _toastRoutine = null;
            }
            if (_toast != null) _toast.gameObject.SetActive(false);
        }
    }
}
