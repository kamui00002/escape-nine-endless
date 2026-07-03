// BPMInfoWidget.cs
// Swift 正本: Views/Game/BPMInfoView.swift
// 「階層 | BPM | 速度」の 3 カラム情報パネル。速度レベルの区分・色は Swift と同一。
// 毎フレーム処理を持たないため MonoBehaviour ではなく素のクラス
// (GameScreen が階層変化時に Render を呼ぶだけ)。
//
// Phase 4 (juice) 追記 (担当B): BPM 値が変わった瞬間 (階層クリアで加速した瞬間) に
// 数値を PunchScale + 一瞬 Warning 色へ Flash する。本クラスは MonoBehaviour ではないため
// FxKit の host には常駐シングルトン FxLayer.I を使う (Render は FxLayer.Install 済みの
// タイミングでのみ呼ばれるため null になることはない想定だが、host==null は FxKit 側で
// 安全に no-op されるため未初期化でも例外にはならない)。

using EscapeNine.Runtime.UI.Fx;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeNine.Runtime.UI
{
    public sealed class BPMInfoWidget
    {
        private readonly Text _floorValue;
        private readonly Text _bpmValue;
        private readonly Text _speedValue;

        // Phase 4 (juice): 直前の BPM。double.NaN = 未初期化 (初回 Render で誤発火しない)。
        private double _lastBpm = double.NaN;

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

            // Phase 4 (juice): BPM 変更 (階層クリアによる加速) を数値パンチ + 一瞬 Warning 色で強調。
            // CalculateBPM(floor) は純関数 (同じ floor なら常に同じ bpm) のため、変化検知に等価比較で十分。
            if (!double.IsNaN(_lastBpm) && bpm != _lastBpm)
            {
                FxKit.PunchScale(FxLayer.I, (RectTransform)_bpmValue.transform, 0.3f, 0.3f);
                FxKit.Flash(FxLayer.I, _bpmValue, UITheme.Warning, 0.35f);
            }
            _lastBpm = bpm;

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
