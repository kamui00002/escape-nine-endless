// DailyChallengeScreen.cs
// Swift 正本: Views/DailyChallenge/DailyChallengeView.swift
//   - 本日の日付・条件一覧・クリア済みバッジ・チャレンジ開始 (pendingChallenge セット→Game 画面へ) を移植。
//   - Swift の `.alert("本日は挑戦済みです")` は Unity 側にネイティブアラートが無いため、
//     HomeScreen / ResultScreen と同型のトーストへ簡略化 (Phase 4 でダイアログ化を検討)。
//
// レイアウトは全て UIFactory.Place の親比率 0..1 (固定 px 禁止)。
// 座標系は Unity 左下原点: cy が大きいほど画面上部 (Swift の top-left と逆)。

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EscapeNine.Core;

namespace EscapeNine.Runtime.UI
{
    public sealed class DailyChallengeScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.DailyChallenge;

        // DailyChallengeGenerator は常に 1〜2 個の条件を生成する (Swift: conditionCount = 1...2)。
        private const int MaxConditionRows = 2;

        // App.cs は screen.BuildUI() → Router.Register(screen) の順で呼び、Register 内でも
        // BuildUI() が走るため計 2 回呼ばれる。二重構築防止ガード (他画面と同じ対策)。
        private bool _built;

        private TextMeshProUGUI _dateLabel;
        private RectTransform _completedBadge;
        private TextMeshProUGUI _completedFloorLabel;
        private RectTransform[] _conditionRows;
        private TextMeshProUGUI[] _conditionLabels;
        private Image _actionButtonImage;
        private TextMeshProUGUI _actionButtonLabel;
        private RectTransform _toast;
        private TextMeshProUGUI _toastLabel;
        private Coroutine _toastRoutine;

        // MARK: - BuildUI

        public override void BuildUI()
        {
            if (_built) return;
            _built = true;

            var root = GetComponent<RectTransform>();
            if (root != null)
            {
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
            }

            UIFactory.SimpleDepthBackground(transform);

            var safe = UIFactory.Panel(transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildHeader(safe);
            BuildCompletedBadge(safe);
            BuildConditionRows(safe);
            BuildActionButtons(safe);
            BuildToast(safe);
        }

        public override void OnShow(object payload)
        {
            RefreshAll();
        }

        public override void OnHide()
        {
            HideToast();
        }

        // MARK: - Header (Swift: ヘッダー + 戻る overlay ボタン + 日付)

        private void BuildHeader(RectTransform parent)
        {
            UIFactory.SecondaryButton(parent, "BackButton", "< 戻る", 0.14f, 0.955f, 0.22f, 0.045f,
                OnBackTapped, 36);

            var title = UIFactory.Label(parent, "TitleLabel", "デイリーチャレンジ", 54, UITheme.GoldText,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.5f, 0.895f, 0.8f, 0.05f);

            _dateLabel = UIFactory.Label(parent, "DateLabel", "", 32,
                UITheme.WithAlpha(UITheme.TextColor, 0.6f));
            UIFactory.Place((RectTransform)_dateLabel.transform, 0.5f, 0.850f, 0.6f, 0.03f);
        }

        private void OnBackTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            App.I.Router.Show(ScreenId.Home);
        }

        // MARK: - Completed Badge (Swift: completedBadge)

        private void BuildCompletedBadge(RectTransform parent)
        {
            // HD-2D (2026-07-07): フラット塗り+手動ボーダーから Card(PanelFill) + BorderTrim へ。
            _completedBadge = UIFactory.Card(parent, "CompletedBadge", out _, UITheme.PanelFillTop, UITheme.PanelFillBottom);
            UIFactory.Place(_completedBadge, 0.5f, 0.775f, 0.7f, 0.05f);
            UIFactory.BorderTrim(_completedBadge, "CompletedBadgeBorder", UITheme.Success, 0.5f, 0.05f, 0.008f);

            _completedFloorLabel = UIFactory.Label(_completedBadge, "Label", "", 34, UITheme.Success,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_completedFloorLabel.transform, 0.5f, 0.5f, 1f, 1f);

            _completedBadge.gameObject.SetActive(false);
        }

        // MARK: - Conditions (Swift: conditionsList)

        private void BuildConditionRows(RectTransform parent)
        {
            var caption = UIFactory.Label(parent, "ConditionsCaption", "本日の条件", 36,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)caption.transform, 0.5f, 0.685f, 0.8f, 0.035f);

            _conditionRows = new RectTransform[MaxConditionRows];
            _conditionLabels = new TextMeshProUGUI[MaxConditionRows];
            const float rowH = 0.06f;
            const float rowGap = 0.075f;
            for (int i = 0; i < MaxConditionRows; i++)
            {
                float cy = 0.62f - rowGap * i;
                // HD-2D (2026-07-07): フラット塗りから Card(PanelFill) + BorderTrim へ。
                var row = UIFactory.Card(parent, "ConditionRow" + i, out _, UITheme.PanelFillTop, UITheme.PanelFillBottom);
                UIFactory.Place(row, 0.5f, cy, 0.86f, rowH);
                UIFactory.BorderTrim(row, "ConditionRow" + i + "Border", UITheme.Accent, 0.4f);
                var label = UIFactory.Label(row, "Label", "", 36, UITheme.TextColor, TextAnchor.MiddleLeft);
                UIFactory.Place((RectTransform)label.transform, 0.5f, 0.5f, 0.88f, 0.8f);
                _conditionRows[i] = row;
                _conditionLabels[i] = label;
            }
        }

        // MARK: - Action Buttons (Swift: actionButtons)

        private void BuildActionButtons(RectTransform parent)
        {
            // HD-2D (2026-07-07): 完了/未完了で色を動的に塗り替える (RefreshAll) ため、塗りを固定する
            // SecondaryButton ではなく ElevatedButton (Card 化のみ、色は渡した値のまま維持) を使う。
            var action = UIFactory.ElevatedButton(parent, "ActionButton", "チャレンジ開始", 52,
                UITheme.Available, UITheme.Background, 0.5f, 0.30f, 0.62f, 0.06f, OnActionTapped);
            UIFactory.EmbossTrim(action.transform, "ActionEmboss", UITheme.ButtonHighlightLine, UITheme.Accent);
            _actionButtonImage = action.GetComponent<Image>();
            _actionButtonLabel = action.GetComponentInChildren<TextMeshProUGUI>();
            if (_actionButtonLabel != null) _actionButtonLabel.fontStyle = FontStyles.Bold;

            UIFactory.SecondaryButton(parent, "BackBottomButton", "戻る", 0.5f, 0.215f, 0.62f, 0.05f,
                OnBackTapped, 48);
        }

        /// <summary>
        /// チャレンジ開始 / 記録確認 (Swift: 未クリア=GameButton→pendingChallenge セット→navigateToGame /
        /// クリア済み=alert「本日は挑戦済みです」)。
        /// </summary>
        private void OnActionTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            var store = App.I.DailyChallenge;
            if (store == null) return;

            if (store.TodaysChallenge.IsCompleted)
            {
                int floor = store.TodaysChallenge.AchievedFloor ?? 0;
                ShowToast("本日は挑戦済みです (到達: " + floor + "階)");
                return;
            }

            // pending チャレンジをセット (Swift: DailyChallengeService.shared.pendingChallenge = challenge)。
            // GameController.StartNewRun がこれを読み取って GameSession に適用する
            // (Game 画面は payload なしで開くため、通常のプレゲーム AI 選択オーバーレイを経由する — Swift と同フロー)。
            store.PendingChallenge = store.TodaysChallenge;
            App.I.Router.Show(ScreenId.Game);
        }

        // MARK: - 再描画

        private void RefreshAll()
        {
            var store = App.I.DailyChallenge;
            if (store == null) return;
            var challenge = store.TodaysChallenge;

            _dateLabel.text = challenge.Date;

            bool completed = challenge.IsCompleted;
            _completedBadge.gameObject.SetActive(completed);
            if (completed)
            {
                int floor = challenge.AchievedFloor ?? 0;
                _completedFloorLabel.text = "クリア済み (到達: " + floor + "階)";
            }

            for (int i = 0; i < MaxConditionRows; i++)
            {
                bool has = i < challenge.Conditions.Count;
                _conditionRows[i].gameObject.SetActive(has);
                if (has) _conditionLabels[i].text = challenge.Conditions[i].Description;
            }

            _actionButtonLabel.text = completed ? "記録を確認" : "チャレンジ開始";
            _actionButtonImage.color = completed ? UITheme.BackgroundSecondary : UITheme.Available;
            _actionButtonLabel.color = completed ? UITheme.TextColor : UITheme.Background;
        }

        // MARK: - Toast (HomeScreen / ResultScreen と同型の簡易実装をこの画面にも複製。
        // クラス間の共有シンボルを作らないため意図的に private 複製している)

        private void BuildToast(RectTransform parent)
        {
            _toast = UIFactory.Panel(parent, "Toast", UITheme.WithAlpha(Color.black, 0.75f));
            UIFactory.Place(_toast, 0.5f, 0.40f, 0.8f, 0.05f);
            _toastLabel = UIFactory.Label(_toast, "Label", "", 30, UITheme.GoldText);
            UIFactory.Place((RectTransform)_toastLabel.transform, 0.5f, 0.5f, 1f, 1f);
            _toast.gameObject.SetActive(false);
        }

        private void ShowToast(string message)
        {
            _toastLabel.text = message;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            _toast.gameObject.SetActive(true);
            yield return new WaitForSeconds(1.8f);
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
