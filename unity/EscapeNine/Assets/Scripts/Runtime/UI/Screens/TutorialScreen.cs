// TutorialScreen.cs
// Swift 正本: Views/Home/OnboardingTutorialView.swift (4 Step 動的オンボーディング / 726 行)
//             + Views/Home/TutorialOverlayView.swift (3 ページ「逃げ切れ」文言)
//             + Views/Home/TutorialStepInstructionView.swift (Step ヘッダカードの構成)
//
// Swift 正本は「4 Step」だが、本移植はタスク要件の 6 ページ構成
// (1 移動 / 2 ビート / 3 鬼 / 4 スキル / 5 特殊ルール / 6 目標) に再編する。
// 文言は Swift に存在するものは一字一句そのまま移植し、Swift に該当ページが無い
// ビート/スキル/特殊ルールの 3 ページは GameConfig / Skill.cs の値・文字列から組み立てる
// (バランス定数・スキル説明文の複製禁止ルールのため)。
//
// 図解方針 (タスク指示「図解は簡略化してよい」):
//   ミニ 3x3 グリッド (ColorRect) + スプライト (hero / red_oni) + 矢印テキストで各概念を表現。
//   Swift の TutorialHighlightView (金色グロー枠) / DangerZoneView (赤の脈動) は
//   静的な半透明オーバーレイに簡略化 (脈動・グローは Phase 4/juice 送り)。
//
// 最終ページは Swift TutorialStep4Game のミニプレイアブル移植:
//   タップで隣接 8 方向へ移動 / 敵は TutorialConstants.Step4EnemyScript の固定スクリプト /
//   TutorialConstants.TutorialClearTurns ターン耐えると「はじめる」が活性化する。
//
// レイアウトは全て UIFactory.Place の親比率 0..1 (固定 px 禁止)。
// 座標系は Unity 左下原点: cy が大きいほど画面上部 (Swift の top-left と逆)。

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EscapeNine.Core;

namespace EscapeNine.Runtime.UI
{
    /// <summary>
    /// チュートリアル画面 (6 ページ / 前へ・次へボタン + ページドット)。
    /// スワイプではなくボタン遷移を採用: uGUI のドラッグ判定を足すより確実で、
    /// Swift 正本 OnboardingTutorialView も「次へ」ボタン駆動のため挙動が近い。
    /// 完了 (はじめる / スキップ) で hasSeenTutorial を永続化して Home へ戻る。
    /// </summary>
    public sealed class TutorialScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Tutorial;

        // ---- 盤面セルのレイアウト定数 (盤面パネルに対する比率。px ではない) ----
        private const float CellSpacingRatio = 0.02f;                       // Swift: cellSpacing 6pt 相当
        private const float CellSizeRatio = (1f - CellSpacingRatio * 2f) / 3f;
        private const float SpriteInCellRatio = 0.78f;                      // Swift: cellSize * 0.78

        /// <summary>1 ページ分の定義。Swift の InstructionCopy + BoardConfig を統合したもの。</summary>
        private sealed class PageDef
        {
            public string Title;
            public string Subtitle;
            public string Caption;              // 盤面下の補足 (null = 非表示)
            public int PlayerPos;               // 0 = 非表示
            public int EnemyPos;                // 0 = 非表示
            public HashSet<int> Highlighted = new HashSet<int>(); // 金色ハイライト (移動可能表現)
            public HashSet<int> Danger = new HashSet<int>();      // 赤の危険圏 (DangerZoneView 相当)
            public HashSet<int> FogCells = new HashSet<int>();    // 霧の説明用 (暗転 + "?")
            public int DisappearedPos;          // 消失マスの説明用 (0 = なし)
            public int ArrowPos;                // 矢印テキストを置くマス (0 = なし)
            public string ArrowText;
            public bool Playable;               // 最終ページのミニプレイアブル
        }

        // App.Awake と Router.Register の双方から BuildUI が呼ばれても二重構築しないガード
        // (HomeScreen と同じ対策)。
        private bool _built;

        private PageDef[] _pages;
        private int _currentPage; // 0-indexed

        // ---- ページ切替のたびに更新する参照 ----
        private TextMeshProUGUI _stepCaptionLabel;   // "STEP n / 6"
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _subtitleLabel;
        private TextMeshProUGUI _boardCaptionLabel;
        private RectTransform _board;     // セルを毎回作り直すコンテナ (AspectRatioFitter で正方形)
        private Image[] _dotImages;
        private RectTransform[] _dotRects;
        private GameObject _prevButtonRoot;
        private Button _nextButton;
        private Image _nextButtonImage;
        private TextMeshProUGUI _nextButtonLabel;
        private GameObject _turnCounterRoot;
        private TextMeshProUGUI _turnCounterLabel;
        private GameObject _clearLabelRoot; // "CLEAR!" (バースト演出は Phase 4 送り、静的表示)

        // ---- 最終ページ (ミニプレイアブル) の状態。Swift: TutorialStep4Game の @State ----
        private int _playerPos;
        private int _enemyPos;
        private int _turnsCompleted;
        private bool _hasCleared;

        public override void BuildUI()
        {
            if (_built) return;
            _built = true;

            // 画面ルートを親いっぱいに固定 (シーン側の配置ミスに影響されない防御)
            var root = GetComponent<RectTransform>();
            if (root != null)
            {
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
            }

            // 背景はノッチ下まで全面 (Swift: Color(hex: GameColors.background).ignoresSafeArea())
            var bg = UIFactory.ColorRect(transform, "Background", UITheme.Background);
            UIFactory.Place((RectTransform)bg.transform, 0.5f, 0.5f, 1f, 1f);

            // コンテンツはセーフエリア内 (SwiftUI では自動処理されていた部分)
            var safe = UIFactory.Panel(transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildPages();
            BuildHeader(safe);
            BuildInstructionCard(safe);
            BuildBoardArea(safe);
            BuildNavButtons(safe);
        }

        public override void OnShow(object payload)
        {
            // Swift: onAppear で startTime 記録 + AnalyticsLogger.logTutorialStarted()。
            // TODO(Phase 3): Analytics 計装 (logTutorialStarted / logTutorialStepCompleted /
            //                logTutorialComplete(elapsedSeconds)) を AnalyticsLogger 移植とセットで追加。
            ResetPlayableState();
            ShowPage(0); // 再入時 (ホームの「遊び方」から何度でも開ける) は必ず 1 ページ目から
        }

        // MARK: - ページ定義 (文言の典拠を各ページのコメントに明記)

        private void BuildPages()
        {
            // ページ 4 (スキル) の文言は Core の Skill.cs / Character.cs から組み立てる
            // (スキル名・説明文の文字列を本ファイルに複製しないため)。
            var characterTypes = new[]
            {
                CharacterType.Hero, CharacterType.Thief, CharacterType.Wizard,
                CharacterType.Elf, CharacterType.Knight,
            };
            var skillParts = new List<string>();
            foreach (var type in characterTypes)
            {
                skillParts.Add(type.Name() + "=" + Character.GetCharacter(type).Skill.Name);
            }
            string skillList = string.Join(" / ", skillParts);
            Skill heroSkill = Character.GetCharacter(CharacterType.Hero).Skill;

            // ページ 2 (ビート) のカウントダウン表記は GameConfig.TurnCountdownBeats から生成
            // ("3 → 2 → 1" のハードコードで定数を複製しないため)。
            var countdownParts = new List<string>();
            for (int beat = GameConfig.TurnCountdownBeats; beat >= 1; beat--)
            {
                countdownParts.Add(beat.ToString());
            }
            string countdownText = string.Join(" → ", countdownParts);

            _pages = new PageDef[]
            {
                // 1. 移動 — Swift: OnboardingTutorialView Step 1 (title/subtitle 一字一句移植)
                new PageDef
                {
                    Title = "動きを覚える",
                    Subtitle = "タップで周囲 8 マスのどこへでも移動できる",
                    Caption = "光るマス = 移動できるマス",
                    PlayerPos = 5,
                    Highlighted = Adjacent8(5),
                },
                // 2. ビート — Swift 正本に該当ページなし (ターン制カウントダウンは game-spec.md 準拠の新規文言)
                new PageDef
                {
                    Title = "リズムに乗る",
                    Subtitle = "カウントダウンに合わせて移動先をタップ。間に合わないと時間切れでゲームオーバー",
                    Caption = countdownText + " → 移動!",
                    PlayerPos = 5,
                    Highlighted = Adjacent8(5),
                },
                // 3. 鬼 — Swift: OnboardingTutorialView Step 2 (title/subtitle 一字一句移植)
                new PageDef
                {
                    Title = "影を避ける",
                    Subtitle = "鬼の隣接マスは危険圏。毎ターン 1 マスずつ近づいてくる",
                    Caption = "赤いマス = 鬼の危険圏",
                    PlayerPos = 1,
                    EnemyPos = 9,
                    Danger = Adjacent8(9),
                },
                // 4. スキル — Swift 正本に該当ページなし (Skill.cs の名前・説明文から生成)
                new PageDef
                {
                    Title = "スキルで切り抜ける",
                    Subtitle = skillList + "。" + GameConfig.SkillResetInterval + " 階層ごとに回数リセット",
                    Caption = heroSkill.Name + ": " + heroSkill.Description,
                    PlayerPos = 4,
                    Highlighted = new HashSet<int> { 6 }, // ダッシュ (2 マス移動) の着地点
                    ArrowPos = 5,
                    ArrowText = "→",
                },
                // 5. 特殊ルール — Swift 正本に該当ページなし (発動階層は GameConfig を典拠に生成)
                new PageDef
                {
                    Title = "結界の異変",
                    Subtitle = "階層 " + GameConfig.FogStartFloor + " からは霧、階層 "
                        + GameConfig.DisappearStartFloor + " からはマス消失、階層 "
                        + GameConfig.CombinedRulesStartFloor + " からは両方が発動する",
                    Caption = "? = 霧で見えない / 黒いマス = 消失 (入ると即アウト)",
                    PlayerPos = 5,
                    FogCells = new HashSet<int> { 1, 3, 7 },
                    DisappearedPos = 9,
                },
                // 6. 目標 — Swift: OnboardingTutorialView Step 4 (subtitle のターン数のみ GameConfig 参照に置換)
                //          + Caption は TutorialOverlayView 3 ページ目「逃げ切れ」の説明文を一字一句移植
                new PageDef
                {
                    Title = "階層クリア",
                    Subtitle = "周囲のマスをタップして " + TutorialConstants.TutorialClearTurns
                        + " ターン逃げ切ろう。本当の旅が始まる",
                    Caption = "影に捕まらないよう、何階まで逃げ切れるか挑戦しましょう",
                    Playable = true,
                },
            };
        }

        // MARK: - ヘッダ (ページドット + スキップ)

        private void BuildHeader(RectTransform parent)
        {
            // ページドット (Swift: stepIndicator。現在ページのカプセルだけ幅 2 倍)
            _dotImages = new Image[_pages.Length];
            _dotRects = new RectTransform[_pages.Length];
            for (int i = 0; i < _pages.Length; i++)
            {
                var dot = UIFactory.ColorRect(parent, "PageDot" + (i + 1), UITheme.Accent);
                _dotImages[i] = dot;
                _dotRects[i] = (RectTransform)dot.transform;
                // 位置は ShowPage() の UpdateDots() で毎回 Place し直す (現在ページだけ幅が変わるため)
            }

            // スキップ (Swift: skipButton = 右上 xmark.circle.fill。常設)
            var skip = UIFactory.TextButton(parent, "SkipButton", "×", 64,
                UITheme.WithAlpha(UITheme.BackgroundSecondary, 0.6f),
                UITheme.WithAlpha(UITheme.TextColor, 0.6f), OnSkipTapped);
            UIFactory.Place((RectTransform)skip.transform, 0.92f, 0.965f, 0.10f, 0.04f);
        }

        // MARK: - 説明カード (Swift: TutorialStepInstructionView)

        private void BuildInstructionCard(RectTransform parent)
        {
            var card = UIFactory.Panel(parent, "InstructionCard",
                UITheme.WithAlpha(UITheme.BackgroundSecondary, 0.85f));
            UIFactory.Place(card, 0.5f, 0.868f, 0.92f, 0.145f);
            AddCardBorder(card); // Swift: stroke(accent.opacity(0.4))

            // "STEP n / 6" (Swift: "Step \(n) / \(total)" — textSecondary = GoldText)
            _stepCaptionLabel = UIFactory.Label(card, "StepCaption", "", 34, UITheme.GoldText,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_stepCaptionLabel.transform, 0.5f, 0.84f, 0.9f, 0.22f);

            _titleLabel = UIFactory.Label(card, "TitleLabel", "", 58, UITheme.TextColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_titleLabel.transform, 0.5f, 0.58f, 0.94f, 0.28f);

            _subtitleLabel = UIFactory.Label(card, "SubtitleLabel", "", 38,
                UITheme.WithAlpha(UITheme.TextColor, 0.85f));
            UIFactory.Place((RectTransform)_subtitleLabel.transform, 0.5f, 0.22f, 0.92f, 0.40f);
        }

        /// <summary>カードのアクセント色細枠 (ResultScreen の AddCardBorder と同じ簡易表現)。</summary>
        private static void AddCardBorder(RectTransform card)
        {
            Color border = UITheme.WithAlpha(UITheme.Accent, 0.4f);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(card, "BorderTop", border).transform, 0.5f, 1f, 1f, 0.015f);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(card, "BorderBottom", border).transform, 0.5f, 0f, 1f, 0.015f);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(card, "BorderLeft", border).transform, 0f, 0.5f, 0.006f, 1f);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(card, "BorderRight", border).transform, 1f, 0.5f, 0.006f, 1f);
        }

        // MARK: - 盤面エリア

        private void BuildBoardArea(RectTransform parent)
        {
            // ターンカウンタ (最終ページのみ表示。Swift: TutorialStep4Game.turnCounter)
            var counter = UIFactory.Panel(parent, "TurnCounter");
            UIFactory.Place(counter, 0.5f, 0.742f, 0.7f, 0.035f);
            _turnCounterRoot = counter.gameObject;
            _turnCounterLabel = UIFactory.Label(counter, "TurnCounterLabel", "", 44, UITheme.Accent,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_turnCounterLabel.transform, 0.5f, 0.5f, 1f, 1f);

            // 盤面の外枠エリア。中の Board は AspectRatioFitter で常に正方形に保つ
            // (Swift: .aspectRatio(1, contentMode: .fit) の uGUI 等価。iPad の縦横比でも歪まない)。
            var boardArea = UIFactory.Panel(parent, "BoardArea");
            UIFactory.Place(boardArea, 0.5f, 0.505f, 0.92f, 0.40f);

            _board = UIFactory.Panel(boardArea, "Board");
            var fitter = _board.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            // CLEAR! 表示 (Swift: clearBurstOverlay。スパークル放射 + バウンスは Phase 4/juice 送り。
            // Reduce Motion フォールバックと同じ「静的表示」に簡略化し、消去タイマーも置かない)
            var clearLabel = UIFactory.Label(boardArea, "ClearLabel", "CLEAR!", 120, UITheme.Success,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)clearLabel.transform, 0.5f, 0.5f, 0.9f, 0.3f);
            _clearLabelRoot = clearLabel.gameObject;
            _clearLabelRoot.SetActive(false);

            // 盤面下の補足キャプション (図解の凡例。Swift には無い簡略図解ゆえの追加)
            _boardCaptionLabel = UIFactory.Label(parent, "BoardCaption", "", 36,
                UITheme.WithAlpha(UITheme.TextColor, 0.75f));
            UIFactory.Place((RectTransform)_boardCaptionLabel.transform, 0.5f, 0.272f, 0.9f, 0.045f);
        }

        // MARK: - ナビゲーションボタン (前へ / 次へ・はじめる)

        private void BuildNavButtons(RectTransform parent)
        {
            // 前へ (Swift 正本には無いが、6 ページ構成の読み返し用にタスク要件で追加)
            var prev = UIFactory.TextButton(parent, "PrevButton", "前へ", 54,
                UITheme.BackgroundSecondary, UITheme.TextColor, OnPrevTapped);
            UIFactory.Place((RectTransform)prev.transform, 0.28f, 0.115f, 0.42f, 0.06f);
            _prevButtonRoot = prev.gameObject;

            // 次へ / はじめる (Swift: nextButton = accent 背景 + background 色文字)
            _nextButton = UIFactory.TextButton(parent, "NextButton", "次へ", 54,
                UITheme.Accent, UITheme.Background, OnNextTapped);
            UIFactory.Place((RectTransform)_nextButton.transform, 0.72f, 0.115f, 0.42f, 0.06f);
            _nextButtonImage = _nextButton.GetComponent<Image>();
            _nextButtonLabel = _nextButton.GetComponentInChildren<TextMeshProUGUI>();
            if (_nextButtonLabel != null) _nextButtonLabel.fontStyle = FontStyles.Bold;
        }

        // MARK: - ページ表示

        private void ShowPage(int index)
        {
            _currentPage = Mathf.Clamp(index, 0, _pages.Length - 1);
            PageDef page = _pages[_currentPage];

            _stepCaptionLabel.text = "STEP " + (_currentPage + 1) + " / " + _pages.Length;
            _titleLabel.text = page.Title;
            _subtitleLabel.text = page.Subtitle;

            _boardCaptionLabel.text = page.Caption ?? "";
            _boardCaptionLabel.gameObject.SetActive(!string.IsNullOrEmpty(page.Caption));

            _prevButtonRoot.SetActive(_currentPage > 0);
            _turnCounterRoot.SetActive(page.Playable);
            _clearLabelRoot.SetActive(page.Playable && _hasCleared);
            if (page.Playable) UpdateTurnCounter();

            UpdateDots();
            UpdateNextButton();
            RebuildBoard();
        }

        /// <summary>
        /// ページドットの再配置。Swift: 現在ステップのカプセルだけ幅 32pt / 他 16pt
        /// → 比率で 0.06 / 0.03 に置き換え。到達済み = accent / 未到達 = グレー 30%。
        /// </summary>
        private void UpdateDots()
        {
            float slotSpacing = 0.075f;
            float firstCx = 0.5f - slotSpacing * (_pages.Length - 1) * 0.5f;
            for (int i = 0; i < _pages.Length; i++)
            {
                bool isCurrent = i == _currentPage;
                bool reached = i <= _currentPage;
                _dotImages[i].color = reached ? UITheme.Accent : new Color(0.5f, 0.5f, 0.5f, 0.3f);
                UIFactory.Place(_dotRects[i], firstCx + slotSpacing * i, 0.968f,
                    isCurrent ? 0.06f : 0.03f, 0.008f);
            }
        }

        /// <summary>
        /// 「次へ」ボタンの文言と活性状態。
        /// Swift: 最終 Step は「はじめる」+ 3 ターン耐えるまで disable (accent.opacity(0.35))。
        /// </summary>
        private void UpdateNextButton()
        {
            bool isLast = _currentPage == _pages.Length - 1;
            bool disabled = isLast && !_hasCleared;

            if (_nextButtonLabel != null) _nextButtonLabel.text = isLast ? "はじめる" : "次へ";
            _nextButton.interactable = !disabled;
            if (_nextButtonImage != null)
            {
                _nextButtonImage.color = disabled ? UITheme.WithAlpha(UITheme.Accent, 0.35f) : UITheme.Accent;
            }
        }

        // MARK: - 盤面の構築 (ページ切替・プレイアブルの 1 手ごとに全再構築)
        // 毎フレームではなくユーザー操作時のみ走るため、差分更新より単純さを優先する
        // (SwiftUI の宣言的再描画に発想を合わせた実装)。

        private void RebuildBoard()
        {
            for (int i = _board.childCount - 1; i >= 0; i--)
            {
                GameObject old = _board.GetChild(i).gameObject;
                // Destroy はフレーム末尾まで遅延するため、先に非表示化して
                // 新旧セルが 1 フレーム重なって見えるゴーストを防ぐ
                old.SetActive(false);
                Destroy(old);
            }

            PageDef page = _pages[_currentPage];
            for (int position = 1; position <= GameConfig.GridSize; position++)
            {
                BuildCell(page, position);
            }
        }

        private void BuildCell(PageDef page, int position)
        {
            // Swift の position 番号は 1..9 で左上起点 (1=左上, 9=右下)。
            // Unity は左下原点のため row を上下反転して配置する。
            int rowFromTop = (position - 1) / GameConfig.GridColumns;
            int col = (position - 1) % GameConfig.GridColumns;
            float cx = CellSizeRatio * 0.5f + col * (CellSizeRatio + CellSpacingRatio);
            float cy = 1f - (CellSizeRatio * 0.5f + rowFromTop * (CellSizeRatio + CellSpacingRatio));

            bool tappable = page.Playable && IsTappable(position);

            RectTransform cell;
            if (tappable)
            {
                // タップ可能セルは Panel(bg) + Button (ColorRect は raycastTarget=false 固定のため不可)
                cell = UIFactory.Panel(_board, "Cell" + position, UITheme.BackgroundSecondary);
                var button = cell.gameObject.AddComponent<Button>();
                button.targetGraphic = cell.GetComponent<Image>();
                button.transition = Selectable.Transition.ColorTint;
                int captured = position; // クロージャに loop 変数を直接渡さないための退避
                button.onClick.AddListener(() => HandleBoardTap(captured));
            }
            else
            {
                cell = (RectTransform)UIFactory.ColorRect(_board, "Cell" + position,
                    UITheme.BackgroundSecondary).transform;
            }
            UIFactory.Place(cell, cx, cy, CellSizeRatio, CellSizeRatio);

            // --- オーバーレイ (Swift の描画順: 危険圏 → キャラ → ハイライト) ---

            if (page.Danger.Contains(position))
            {
                // Swift: DangerZoneView (赤の脈動オーバーレイ) → 静的な半透明赤に簡略化 (脈動は Phase 4)
                var danger = UIFactory.ColorRect(cell, "Danger", UITheme.WithAlpha(UITheme.Warning, 0.30f));
                UIFactory.Place((RectTransform)danger.transform, 0.5f, 0.5f, 1f, 1f);
            }

            if (page.FogCells.Contains(position))
            {
                // 実ゲームの霧はマスの中身を隠す表現。図解では「見えない」ことを ? で示す
                var fog = UIFactory.ColorRect(cell, "Fog", UITheme.WithAlpha(Color.black, 0.55f));
                UIFactory.Place((RectTransform)fog.transform, 0.5f, 0.5f, 1f, 1f);
                var mark = UIFactory.Label(cell, "FogMark", "?", 64,
                    UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Place((RectTransform)mark.transform, 0.5f, 0.5f, 1f, 1f);
            }

            if (page.DisappearedPos == position)
            {
                // 消失マス (Swift/実ゲーム: GameColors.disappeared で塗り潰し)
                var gone = UIFactory.ColorRect(cell, "Disappeared", UITheme.Disappeared);
                UIFactory.Place((RectTransform)gone.transform, 0.5f, 0.5f, 1f, 1f);
            }

            // キャラスプライト (プレイアブルページは実行中の状態、静的ページは PageDef の配置)
            int playerPos = page.Playable ? _playerPos : page.PlayerPos;
            int enemyPos = page.Playable ? _enemyPos : page.EnemyPos;
            if (position == enemyPos)
            {
                var enemy = UIFactory.SpriteImage(cell, "Enemy", UIFactory.LoadSprite("red_oni"));
                enemy.raycastTarget = false; // 図解用: セルのタップ判定を邪魔しない
                UIFactory.Place((RectTransform)enemy.transform, 0.5f, 0.5f, SpriteInCellRatio, SpriteInCellRatio);
            }
            else if (position == playerPos)
            {
                // Swift 正本どおり常に hero を表示 (選択キャラには追従しない)
                var player = UIFactory.SpriteImage(cell, "Player", UIFactory.LoadSprite("hero"));
                player.raycastTarget = false;
                UIFactory.Place((RectTransform)player.transform, 0.5f, 0.5f, SpriteInCellRatio, SpriteInCellRatio);
            }

            if (page.ArrowPos == position && !string.IsNullOrEmpty(page.ArrowText))
            {
                var arrow = UIFactory.Label(cell, "Arrow", page.ArrowText, 84, UITheme.GoldText,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Place((RectTransform)arrow.transform, 0.5f, 0.5f, 1f, 1f);
            }

            if (page.Highlighted.Contains(position) || tappable)
            {
                // Swift: TutorialHighlightView (金色グロー枠) → 半透明ゴールドの面に簡略化 (グローは Phase 4)
                var highlight = UIFactory.ColorRect(cell, "Highlight",
                    UITheme.WithAlpha(UITheme.Available, 0.28f));
                UIFactory.Place((RectTransform)highlight.transform, 0.5f, 0.5f, 1f, 1f);
            }
        }

        // MARK: - ミニプレイアブル (Swift: TutorialStep4Game の忠実移植)

        private void ResetPlayableState()
        {
            // Swift: @State 初期値 player=1, enemy=9 (最大距離 = 4 の安全配置)
            _playerPos = 1;
            _enemyPos = 9;
            _turnsCompleted = 0;
            _hasCleared = false;
        }

        /// <summary>
        /// タップ可能判定 (Swift: isTappable)。
        /// プレイヤー隣接 8 マスのうち、敵の現在位置と「次ターン位置」を除外する二重衝突ガード。
        /// 敵スクリプトとの組み合わせで算数的に衝突不可 = チュートリアルで絶対に負けない設計。
        /// </summary>
        private bool IsTappable(int position)
        {
            if (_hasCleared) return false;
            if (position == _playerPos) return false;
            if (position == _enemyPos) return false;
            if (position == ScriptedEnemyPosition(_turnsCompleted + 1)) return false;
            return Adjacent8(_playerPos).Contains(position);
        }

        /// <summary>1-indexed ターン後の敵位置 (Swift: scriptedEnemyPosition。範囲外は現在位置維持)。</summary>
        private int ScriptedEnemyPosition(int turn)
        {
            int[] script = TutorialConstants.Step4EnemyScript;
            if (turn < 1 || turn > script.Length) return _enemyPos;
            return script[turn - 1];
        }

        private void HandleBoardTap(int position)
        {
            if (!IsTappable(position)) return; // 再構築の谷間の連打対策 (Swift: guard isTappable)

            App.I.Audio.PlaySfx("move"); // Swift の Step4 は無音だが、本番の移動音に合わせた意図的追加

            // 1. プレイヤー移動 → 2. ターン進行 + 敵スクリプト適用 (Swift: handleTap と同順)
            _playerPos = position;
            _turnsCompleted++;
            _enemyPos = ScriptedEnemyPosition(_turnsCompleted);

            // 3. クリア検知 (Swift: totalTurns=3 → TutorialConstants.TutorialClearTurns を唯一の正とする)
            if (_turnsCompleted >= TutorialConstants.TutorialClearTurns && !_hasCleared)
            {
                _hasCleared = true;
                App.I.Audio.PlaySfx("floor_clear"); // 達成感の演出 (Swift はバースト演出のみ)
                _clearLabelRoot.SetActive(true);
                UpdateNextButton(); // 「はじめる」を活性化 (Swift: onClear() → 親の disabled 解除)
            }

            UpdateTurnCounter();
            RebuildBoard();
        }

        private void UpdateTurnCounter()
        {
            int shown = Mathf.Min(_turnsCompleted, TutorialConstants.TutorialClearTurns);
            string text = "ターン " + shown + " / " + TutorialConstants.TutorialClearTurns;
            if (_hasCleared) text += "  クリア!";
            _turnCounterLabel.text = text;
        }

        // MARK: - 操作

        private void OnPrevTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            if (_currentPage > 0) ShowPage(_currentPage - 1);
        }

        private void OnNextTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            // TODO(Phase 3): AnalyticsLogger.logTutorialStepCompleted(stepNumber, skipped: false)
            if (_currentPage >= _pages.Length - 1)
            {
                Complete();
                return;
            }
            ShowPage(_currentPage + 1);
        }

        /// <summary>全体スキップ (Swift: skip() = 右上 ×)。現在ページを打ち切って完了扱い。</summary>
        private void OnSkipTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            // TODO(Phase 3): AnalyticsLogger.logTutorialStepCompleted(stepNumber, skipped: true)
            Complete();
        }

        /// <summary>
        /// 完了処理 (Swift: complete())。
        /// hasSeenTutorial / hasSeenTutorialV1_1 の両方を立てるのは Swift 正本と同じ
        /// (v1.1 オンボーディング再表示判定を将来移植しても再発火しないようにするため)。
        /// </summary>
        private void Complete()
        {
            // TODO(Phase 3): AnalyticsLogger.logTutorialComplete(elapsedSeconds)
            var player = App.I.Player;
            player.HasSeenTutorial = true;
            player.HasSeenTutorialV11 = true;
            player.Save();

            // Swift は isPresented=false で fullScreenCover を閉じる → Unity では Home へ戻す。
            // ホームの「遊び方」から開いた場合も Home へ戻る (単純さ優先の意図的差分)。
            App.I.Router.Show(ScreenId.Home);
        }

        // MARK: - 盤面ジオメトリ (Swift: private enum TutorialBoardGeometry の移植)
        // GameEngine.GetAvailableMoves は上下左右 4 方向のみのため、チュートリアルの
        // 「周囲 8 マス」表現には使えない。Swift 正本どおり View 専用ヘルパーとして持つ。

        /// <summary>8 方向隣接マス (要件定義書「縦横斜め 8 方向」)。盤面外は除外。</summary>
        private static HashSet<int> Adjacent8(int position)
        {
            var result = new HashSet<int>();
            if (position < 1 || position > GameConfig.GridSize) return result;
            int row = (position - 1) / GameConfig.GridColumns;
            int col = (position - 1) % GameConfig.GridColumns;
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int nr = row + dr;
                    int nc = col + dc;
                    if (nr < 0 || nr >= GameConfig.GridRows || nc < 0 || nc >= GameConfig.GridColumns) continue;
                    result.Add(nr * GameConfig.GridColumns + nc + 1);
                }
            }
            return result;
        }
    }
}
