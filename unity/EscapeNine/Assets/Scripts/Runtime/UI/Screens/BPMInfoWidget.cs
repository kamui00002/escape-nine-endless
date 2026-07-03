// BPMInfoWidget.cs
// Swift 正本: Views/Game/BPMInfoView.swift
// 「階層 | BPM | 速度」の 3 カラム情報パネル。速度レベルの区分・色は Swift と同一。
// 毎フレーム処理を持たないため MonoBehaviour ではなく素のクラス
// (GameScreen が階層変化時に Render を呼ぶだけ)。

using UnityEngine;
using UnityEngine.UI;

namespace EscapeNine.Runtime.UI
{
    public sealed class BPMInfoWidget
    {
        private readonly Text _floorValue;
        private readonly Text _bpmValue;
        private readonly Text _speedValue;

        /// <summary>配置用 (GameScreen が UIFactory.Place で位置決めする)。</summary>
        public RectTransform Rect { get; }

        public BPMInfoWidget(Transform parent)
        {
            // 背景パネル (Swift: RoundedRectangle + available/main のグラデ枠 → 単色簡略化 Phase 4)
            Rect = UIFactory.Panel(parent, "BPMInfo", UITheme.BackgroundSecondary);

            _floorValue = BuildColumn(Rect, "Floor", "階層", 0.18f, UITheme.Available);
            BuildDivider(Rect, "Divider1", 0.345f);
            _bpmValue = BuildColumn(Rect, "BPM", "BPM", 0.50f, UITheme.GoldText);
            BuildDivider(Rect, "Divider2", 0.655f);
            _speedValue = BuildColumn(Rect, "Speed", "速度", 0.82f, UITheme.Success);
        }

        /// <summary>階層と BPM を反映する。速度レベルは BPM から導出 (Swift: speedLevel/speedColor)。</summary>
        public void Render(int floor, double bpm)
        {
            _floorValue.text = floor.ToString();
            _bpmValue.text = ((int)bpm).ToString();

            // Swift の switch bpm { ..<80 / 80..<120 / 120..<180 / 180..<220 / default } と同一区分
            if (bpm < 80)
            {
                _speedValue.text = "Slow";
                _speedValue.color = UITheme.Success;
            }
            else if (bpm < 120)
            {
                _speedValue.text = "Normal";
                _speedValue.color = UITheme.Available;
            }
            else if (bpm < 180)
            {
                _speedValue.text = "Fast";
                _speedValue.color = UITheme.GoldText; // Swift: textSecondary
            }
            else if (bpm < 220)
            {
                _speedValue.text = "Extreme";
                _speedValue.color = UITheme.Warning;
            }
            else
            {
                _speedValue.text = "MAX";
                _speedValue.color = UITheme.Enemy;
            }
        }

        // MARK: - 内部実装

        /// <summary>キャプション上段 + 値下段の 1 カラムを組む。返り値は値ラベル。</summary>
        private static Text BuildColumn(RectTransform parent, string name, string caption,
            float cx, Color valueColor)
        {
            var cap = UIFactory.Label(parent, name + "Caption", caption, 30,
                UITheme.WithAlpha(UITheme.TextColor, 0.6f));
            UIFactory.Place((RectTransform)cap.transform, cx, 0.72f, 0.28f, 0.36f);

            var value = UIFactory.Label(parent, name + "Value", "-", 46, valueColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)value.transform, cx, 0.32f, 0.28f, 0.48f);
            return value;
        }

        /// <summary>カラム間の縦罫線 (Swift: LinearGradient の Rectangle → 単色簡略化)。</summary>
        private static void BuildDivider(RectTransform parent, string name, float cx)
        {
            var div = UIFactory.ColorRect(parent, name, UITheme.WithAlpha(UITheme.GridBorder, 0.35f));
            UIFactory.Place((RectTransform)div.transform, cx, 0.5f, 0.004f, 0.6f);
        }
    }
}
