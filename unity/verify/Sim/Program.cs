// Program.cs — Escape Nine ヘッドレス・バランスシミュレータ
//
// GameSession (Swift 正本から移植済) を使い、キャラ5種 × プレイヤー選択AI 3段階で
// 多数のランを回して到達階層の分布を出す。Phase 5 のバランス調整の基礎データ。
//
// モデル化の前提 (レポートにも明記):
//  - タイミングは常に Just (完璧) — 人間のミス率は含まない。よって結果は
//    「AI/盤面構造から来る捕まりやすさの上限値」であり、実プレイの難易度は
//    これにタイミングミス死が上乗せされる。
//  - プレイヤー方策は貪欲法: 敵の現在位置からマンハッタン距離が最大になるマスへ移動。
//    同率は乱数で選択。霧は無視 (敵位置は常に既知として行動)。
//  - スキル方策 (簡易):
//      エルフ: 敵が隣接(チェビシェフ<=1)で残数あり → 拘束
//      ナイト: 敵が隣接で残数あり・盾未展開 → 盾
//      勇者: ダッシュ移動先が通常より距離を稼げる時のみ発動 (発動中はダッシュ先のみ選択)
//      盗賊/魔法使い: 常時型 (GetAvailableMoves / 衝突時自動) に任せる
//      (レリックモード) 拘束スキル非所持キャラ: #17 心話の絆の疑似拘束チャージがあれば同条件で使用
//  - 乱数はシード固定で再現可能。
//
// Phase 5b (docs/unity-phase5-roguelike-design.md §6.5): `dotnet run -- phase5` で
// レリック無効 (baseline) / 有効の両モードを 15構成 × 1000ラン 走らせ、
// pass/fail 基準 a〜d を判定して BALANCE_REPORT_PHASE5.md を生成する。
// レリックドラフト方策: RelicConfig.ShouldOfferDraft が許可した階層クリア時のみ、§2.2 の
// 重み付きドラフト (RelicDraftService) から「最高レアリティ優先・同率は弱点タグ一致優先・
// さらに同率は先頭」で1つ取得する。
//
// Phase 5c (BALANCE_REPORT_PHASE5.md 参照): `dotnet run -- phase5-sweep` で
// RelicConfig.DraftInterval / MaxRelicsPerRun の複数組を RelicConfig の定数を書き換えずに
// 比較し、§6.5 案A/B のチューニングに使う実測データを出す (BALANCE_REPORT_PHASE5.md へは書き込まない)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EscapeNine.Core;

namespace EscapeNine.Sim
{
    public static class Program
    {
        private const int RunsPerConfig = 1000;

        private static readonly CharacterType[] Characters =
        {
            CharacterType.Hero, CharacterType.Thief, CharacterType.Wizard,
            CharacterType.Elf, CharacterType.Knight
        };

        private static readonly AILevel[] AiLevels = { AILevel.Easy, AILevel.Normal, AILevel.Hard };

        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "phase5")
            {
                return RunPhase5();
            }

            if (args.Length > 0 && args[0] == "phase5-sweep")
            {
                return RunPhase5Sweeps();
            }

            var characters = Characters;
            var aiLevels = AiLevels;

            var report = new List<string>
            {
                "# バランスシミュレーション結果 (自動生成)",
                "",
                $"- 生成: `dotnet run` (unity/verify/Sim) / ラン数: {RunsPerConfig} per config / シード固定",
                "- 前提: **完璧タイミング** (人間のミス率ゼロ) の上限値。実プレイはこれよりも下振れする。",
                "- 方策: 貪欲逃走 (敵からマンハッタン距離最大化)、霧無視、スキル簡易方策 (Program.cs 冒頭コメント参照)。",
                "",
                "| キャラ | 選択AI | 平均到達 | 中央値 | p90 | 最高 | ≥10階 | ≥25階 | ≥50階 | 100階クリア |",
                "|---|---|---|---|---|---|---|---|---|---|"
            };

            int configIndex = 0;
            foreach (var character in characters)
            {
                foreach (var ai in aiLevels)
                {
                    var reached = new List<int>();
                    for (int run = 0; run < RunsPerConfig; run++)
                    {
                        int seed = 1000003 * configIndex + run; // 再現可能なシード
                        reached.Add(SimulateRun(character, ai, seed));
                    }
                    reached.Sort();

                    double avg = reached.Average();
                    int median = reached[reached.Count / 2];
                    int p90 = reached[(int)(reached.Count * 0.9)];
                    int max = reached[reached.Count - 1];
                    double ge10 = 100.0 * reached.Count(f => f >= 10) / reached.Count;
                    double ge25 = 100.0 * reached.Count(f => f >= 25) / reached.Count;
                    double ge50 = 100.0 * reached.Count(f => f >= 50) / reached.Count;
                    double win = 100.0 * reached.Count(f => f > GameConfig.MaxFloors) / reached.Count;

                    report.Add(
                        $"| {character.Name()} | {ai.RawValue()} | {avg:F1} | {median} | {p90} | {Math.Min(max, 100)} " +
                        $"| {ge10:F0}% | {ge25:F0}% | {ge50:F0}% | {win:F1}% |");

                    Console.WriteLine($"{character.Name(),-5} x {ai.RawValue(),-6}: avg={avg:F1} med={median} p90={p90} win={win:F1}%");
                    configIndex++;
                }
            }

            report.Add("");
            report.Add("## 読み方・注意");
            report.Add("- 「到達階層」= 死亡した階層 (勝利ランは 101 として集計し、表では 100 と表示)。");
            report.Add("- 完璧タイミング前提のため、この表は **盤面とAIの構造的な難易度** を示す。");
            report.Add("  実プレイ (人間) は BPM 上昇に伴うタイミングミスが支配的で、これより大幅に下がる。");
            report.Add("- 観察された Swift 由来の挙動 (移植で保存):");
            report.Add("  - 敵AIは消失マスを回避しない (消失マス上に乗れる)。プレイヤーのみ消失マス進入で死亡。");
            report.Add("  - ダッシュ発動中は通常隣接移動が validateMove で無効になる (UI 側で誘導しないと時間切れ/無効死の可能性)。");
            report.Add("");

            string outPath = Path.Combine(AppContext.BaseDirectory, "../../../../BALANCE_REPORT.md");
            File.WriteAllLines(Path.GetFullPath(outPath), report);
            Console.WriteLine($"\nreport -> {Path.GetFullPath(outPath)}");
            return 0;
        }

        // MARK: - Phase 5b: レリック有効/無効の比較シミュレーション (§6.5)

        private sealed class ConfigResult
        {
            public CharacterType Character;
            public AILevel Ai;
            public List<int> Reached;          // ソート済み到達階層 (勝利=101)
            public double AvgRelicsCollected;  // レリックモードのみ意味を持つ

            public double Avg => Reached.Average();
            public int Median => Reached[Reached.Count / 2];
            public int P90 => Reached[(int)(Reached.Count * 0.9)];
            public double WinRate => 100.0 * Reached.Count(f => f > GameConfig.MaxFloors) / Reached.Count;
        }

        private static ConfigResult RunConfig(
            CharacterType character, AILevel ai, int configIndex, bool relics,
            int draftInterval = RelicConfig.DraftInterval, int maxRelicsPerRun = RelicConfig.MaxRelicsPerRun)
        {
            var reached = new List<int>();
            long totalRelics = 0;
            for (int run = 0; run < RunsPerConfig; run++)
            {
                int seed = 1000003 * configIndex + run; // baseline と同一のシード列
                reached.Add(SimulateRun(character, ai, seed, relics, draftInterval, maxRelicsPerRun, out int collected));
                totalRelics += collected;
            }
            reached.Sort();
            return new ConfigResult
            {
                Character = character,
                Ai = ai,
                Reached = reached,
                AvgRelicsCollected = (double)totalRelics / RunsPerConfig
            };
        }

        /// <summary>§6.5 pass/fail 基準 a〜d の判定結果一式。RunPhase5 (単一構成) と
        /// RunPhase5Sweeps (複数の DraftInterval/MaxRelicsPerRun 組の比較) の両方から使う共通ロジック。
        /// 判定式そのものは既存の BALANCE_REPORT_PHASE5.md 生成ロジックと完全に同一 (Phase 5c で
        /// 重複していたものをここへ集約しただけで、判定基準は一切変えていない)。</summary>
        private sealed class Verdict
        {
            public bool PassA;
            public bool PassB;
            public bool PassC;
            public bool PassD;
            public List<string> ThiefLines;
            public int WizHard;
            public int WizNormal;
            public double GapBefore;
            public double GapAfter;
            public List<string> WinOffenders;
        }

        private static Verdict Evaluate(
            Dictionary<(CharacterType, AILevel), ConfigResult> baseline,
            Dictionary<(CharacterType, AILevel), ConfigResult> relics)
        {
            var v = new Verdict { ThiefLines = new List<string>(), WinOffenders = new List<string>() };

            // a. 盗賊の各AI構成の中央値が上昇すること (必須)
            v.PassA = true;
            foreach (var ai in AiLevels)
            {
                int before = baseline[(CharacterType.Thief, ai)].Median;
                int after = relics[(CharacterType.Thief, ai)].Median;
                bool up = after > before;
                if (!up) v.PassA = false;
                v.ThiefLines.Add($"  - 盗賊 x {ai.RawValue()}: 中央値 {before} → {after} ({(up ? "上昇" : "上昇せず")})");
            }

            // b. Wizard-Hard 中央値がレリック有効後も Wizard-Normal を大きく下回らないこと。
            //    運用定義 [要検証]: Wizard-Hard 中央値 >= 0.8 × Wizard-Normal 中央値
            v.WizHard = relics[(CharacterType.Wizard, AILevel.Hard)].Median;
            v.WizNormal = relics[(CharacterType.Wizard, AILevel.Normal)].Median;
            v.PassB = v.WizHard >= 0.8 * v.WizNormal;

            // c. 「魔法使いの中央値 − 他4キャラ平均中央値」の差が縮小すること (原則3の検証)。
            //    キャラ代表値 = 3AI構成の中央値の平均。gapBefore は常に relics=OFF baseline との比較
            //    (relics 側だけを2組比べているわけではない点に注意)。
            double CharMeanMedian(Dictionary<(CharacterType, AILevel), ConfigResult> res, CharacterType c) =>
                AiLevels.Average(ai => (double)res[(c, ai)].Median);

            double WizardGap(Dictionary<(CharacterType, AILevel), ConfigResult> res)
            {
                double wizard = CharMeanMedian(res, CharacterType.Wizard);
                double others = Characters.Where(c => c != CharacterType.Wizard)
                    .Average(c => CharMeanMedian(res, c));
                return wizard - others;
            }

            v.GapBefore = WizardGap(baseline);
            v.GapAfter = WizardGap(relics);
            v.PassC = v.GapAfter < v.GapBefore;

            // d. 100階クリア率が極端に上振れしないこと (警告ライン 10% 超)
            v.PassD = true;
            foreach (var kv in relics)
            {
                if (kv.Value.WinRate > 10.0)
                {
                    v.PassD = false;
                    v.WinOffenders.Add($"  - {kv.Key.Item1.Name()} x {kv.Key.Item2.RawValue()}: 勝率 {kv.Value.WinRate:F1}%");
                }
            }

            return v;
        }

        private static int RunPhase5()
        {
            Console.WriteLine("Phase 5b relic simulation: 15 configs x " + RunsPerConfig + " runs x 2 modes\n");

            var baseline = new Dictionary<(CharacterType, AILevel), ConfigResult>();
            var relics = new Dictionary<(CharacterType, AILevel), ConfigResult>();

            int configIndex = 0;
            foreach (var character in Characters)
            {
                foreach (var ai in AiLevels)
                {
                    var b = RunConfig(character, ai, configIndex, relics: false);
                    var r = RunConfig(character, ai, configIndex, relics: true);
                    baseline[(character, ai)] = b;
                    relics[(character, ai)] = r;
                    Console.WriteLine(
                        $"{character.Name(),-5} x {ai.RawValue(),-6}: med {b.Median,3} -> {r.Median,3} | " +
                        $"avg {b.Avg,5:F1} -> {r.Avg,5:F1} | win {b.WinRate:F1}% -> {r.WinRate:F1}% | relics/run {r.AvgRelicsCollected:F1}");
                    configIndex++;
                }
            }

            var v = Evaluate(baseline, relics);
            bool passA = v.PassA, passB = v.PassB, passC = v.PassC, passD = v.PassD;
            var thiefLines = v.ThiefLines;
            int wizHard = v.WizHard, wizNormal = v.WizNormal;
            double gapBefore = v.GapBefore, gapAfter = v.GapAfter;
            var winOffenders = v.WinOffenders;

            // --- レポート生成 ---
            var report = new List<string>
            {
                "# Phase 5b バランスシミュレーション結果 — レリック有効 vs 無効 (自動生成)",
                "",
                $"- 生成: `dotnet run -- phase5` (unity/verify/Sim) / ラン数: {RunsPerConfig} per config × 2モード / シード固定 (baseline と同一シード列)",
                "- 前提: **完璧タイミング** (人間のミス率ゼロ) の上限値。実プレイはこれよりも下振れする。",
                "- 移動方策: 貪欲逃走 (敵からマンハッタン距離最大化)、霧無視、スキル簡易方策 (Program.cs 冒頭コメント参照)。",
                $"- ドラフト頻度/上限 (Phase 5c, §6.5 案A/B): `RelicConfig.ShouldOfferDraft` により " +
                $"{RelicConfig.DraftInterval}階層クリアごとに1回 (初回クリアは必ず提示)・1ラン最大{RelicConfig.MaxRelicsPerRun}個までドラフトを提示。",
                "- ドラフト方策 (§6.5): 提示された回に §2.2 の弱点タグ重み付きドラフト (RelicDraftService) から",
                "  「最高レアリティ優先・同率は文脈弱点タグ (盗賊=ThiefRescue / Hard選択=HardAICounter / 次階層41+=LateGame) 一致優先」で1個取得。",
                "- **Tier2 レリックの限界**: #6 コンボの守り / #15 加速の証 / #16 刻の猶予 は「常にJust判定・拍圧力なし」",
                "  の本シミュレータでは効果が測定できない (取得しても生存に影響しない)。実機/プレイテストで別途検証 (§6.5 既知の検証ギャップ)。",
                "- #11 影の抜け道 も本方策では実質無効 (方策が消失マスへ自発的に進入しないため)。",
                "",
                "## 構成別比較 (中央値 / 平均 / p90 / 勝率)",
                "",
                "| キャラ | 選択AI | 中央値 base→relic | 平均 base→relic | p90 base→relic | 勝率 base→relic | 取得レリック/ラン |",
                "|---|---|---|---|---|---|---|"
            };

            foreach (var character in Characters)
            {
                foreach (var ai in AiLevels)
                {
                    var b = baseline[(character, ai)];
                    var r = relics[(character, ai)];
                    report.Add(
                        $"| {character.Name()} | {ai.RawValue()} " +
                        $"| {b.Median} → **{r.Median}** " +
                        $"| {b.Avg:F1} → {r.Avg:F1} " +
                        $"| {b.P90} → {r.P90} " +
                        $"| {b.WinRate:F1}% → {r.WinRate:F1}% " +
                        $"| {r.AvgRelicsCollected:F1} |");
                }
            }

            report.Add("");
            report.Add("## §6.5 Pass/Fail 基準の判定");
            report.Add("");
            report.Add($"### a. 盗賊の各AI構成の中央値が上昇すること (必須) — **{(passA ? "PASS" : "FAIL")}**");
            report.AddRange(thiefLines);
            report.Add("");
            report.Add($"### b. Wizard-Hard 中央値がレリック有効後も Wizard-Normal を大きく下回らないこと — **{(passB ? "PASS" : "FAIL")}**");
            report.Add($"  - Wizard-Hard 中央値 {wizHard} vs Wizard-Normal 中央値 {wizNormal} (運用定義 [要検証]: Hard >= 0.8 × Normal)");
            report.Add("");
            report.Add($"### c. 「魔法使い − 他4キャラ平均」の中央値ギャップが縮小すること — **{(passC ? "PASS" : "FAIL")}**");
            report.Add($"  - ギャップ (キャラ代表値 = 3AI中央値の平均): {gapBefore:F1} → {gapAfter:F1} ({(passC ? "縮小" : "拡大 = 設計ミスとして要修正 (原則3)")})");
            report.Add("");
            report.Add($"### d. 100階クリア率が警告ライン (10%) を超えないこと — **{(passD ? "PASS" : "FAIL")}**");
            if (winOffenders.Count == 0)
            {
                report.Add("  - 全15構成で勝率 10% 以下。");
            }
            else
            {
                report.AddRange(winOffenders);
            }
            report.Add("");
            report.Add("## 読み方・注意");
            report.Add("- 「到達階層」= 死亡した階層 (勝利ランは 101 として集計)。中央値・平均は 1000 ラン基準。");
            report.Add("- baseline は既存 BALANCE_REPORT.md と同一シード列・同一方策のため、そのまま比較可能。");
            report.Add("- ドラフト用乱数は Session の乱数ストリームと分離済み (レリック有無で盤面・AIの乱数消費が変わらない)。");
            report.Add("");

            string outPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../BALANCE_REPORT_PHASE5.md"));
            File.WriteAllLines(outPath, report);
            Console.WriteLine($"\na={(passA ? "PASS" : "FAIL")} b={(passB ? "PASS" : "FAIL")} c={(passC ? "PASS" : "FAIL")} d={(passD ? "PASS" : "FAIL")}");
            Console.WriteLine($"report -> {outPath}");
            return 0;
        }

        // MARK: - Phase 5c: RelicConfig.DraftInterval / MaxRelicsPerRun 実験モード (§6.5 案A/B の選定)
        //
        // BALANCE_REPORT_PHASE5.md には書き込まない (canonical な report は `phase5` モードのみが生成する)。
        // ここでは baseline (レリック無効) を1回だけ計算し、複数の (draftInterval, maxRelicsPerRun) の組を
        // 使い回して比較する。RelicConfig の定数自体は書き換えず、SimulateRun/RunConfig の override 引数で
        // 一時的に差し替える。結果は選定 (BALANCE_REPORT_PHASE5.md への手動転記) の材料として標準出力に出す。

        private static int RunPhase5Sweeps()
        {
            // 選定優先度 (オーケストレータ裁定): c (魔法使いギャップ縮小・拡大は不合格) > d (勝率<=10%) >
            // a' (盗賊Easy/Normalの中央値が (1,999) 基準値 12/13 以上を維持。盗賊Hardの底上げはスコープ外) > b (現状PASS維持)。
            var combos = new (int interval, int cap, string label)[]
            {
                (1, 999, "現行 (interval=1,cap=999=実質無制限。旧デフォルト、(1,999)基準点)"),
                (1, 8,   "interval=1,cap=8"),
                (2, 999, "interval=2,cap=999"),
                (2, 8,   "interval=2,cap=8"),
                (2, 6,   "interval=2,cap=6"),
                (1, 4,   "interval=1,cap=4 (選定)"),
                (1, 2,   "[探索] interval=1,cap=2 (cが技術的にPASSする退化点の確認用)"),
            };

            Console.WriteLine("Phase 5c lever sweep: baseline once + " + combos.Length + " combos x 15 configs x " + RunsPerConfig + " runs\n");

            // baseline (レリック無効) は draftInterval/maxRelicsPerRun に依存しないため1回だけ計算する。
            var baseline = new Dictionary<(CharacterType, AILevel), ConfigResult>();
            {
                int configIndex = 0;
                foreach (var character in Characters)
                {
                    foreach (var ai in AiLevels)
                    {
                        baseline[(character, ai)] = RunConfig(character, ai, configIndex, relics: false);
                        configIndex++;
                    }
                }
            }

            const int thiefEasyRef = 12; // (1,999) 実測値 (BALANCE_REPORT_PHASE5.md 既存表)
            const int thiefNormalRef = 13;

            foreach (var (interval, cap, label) in combos)
            {
                var relics = new Dictionary<(CharacterType, AILevel), ConfigResult>();
                int configIndex = 0;
                foreach (var character in Characters)
                {
                    foreach (var ai in AiLevels)
                    {
                        relics[(character, ai)] = RunConfig(character, ai, configIndex, relics: true, interval, cap);
                        configIndex++;
                    }
                }

                var v = Evaluate(baseline, relics);
                int thiefEasy = relics[(CharacterType.Thief, AILevel.Easy)].Median;
                int thiefNormal = relics[(CharacterType.Thief, AILevel.Normal)].Median;
                int thiefHard = relics[(CharacterType.Thief, AILevel.Hard)].Median;
                bool passAPrime = thiefEasy >= thiefEasyRef && thiefNormal >= thiefNormalRef;

                Console.WriteLine($"=== {label} ===");
                Console.WriteLine(
                    $"  a={(v.PassA ? "PASS" : "FAIL")} (機械判定, 盗賊Hard含む) / a'={(passAPrime ? "PASS" : "FAIL")} " +
                    $"(盗賊Hard除外・Easy/Normalが基準値{thiefEasyRef}/{thiefNormalRef}以上) " +
                    $"b={(v.PassB ? "PASS" : "FAIL")} c={(v.PassC ? "PASS" : "FAIL")} d={(v.PassD ? "PASS" : "FAIL")}");
                Console.WriteLine($"  盗賊 中央値: Easy={thiefEasy} Normal={thiefNormal} Hard={thiefHard}");
                Console.WriteLine($"  魔法使いギャップ: {v.GapBefore:F1} -> {v.GapAfter:F1}");
                Console.WriteLine($"  Wizard中央値: Normal={v.WizNormal} Hard={v.WizHard}");
                if (v.WinOffenders.Count > 0)
                {
                    Console.WriteLine("  勝率超過:");
                    foreach (var line in v.WinOffenders) Console.WriteLine("  " + line);
                }
                foreach (var character in Characters)
                {
                    foreach (var ai in AiLevels)
                    {
                        var b = baseline[(character, ai)];
                        var r = relics[(character, ai)];
                        Console.WriteLine(
                            $"    {character.Name(),-5} x {ai.RawValue(),-6}: med {b.Median,3} -> {r.Median,3} | " +
                            $"win {r.WinRate,5:F1}% | relics/run {r.AvgRelicsCollected:F1}");
                    }
                }
                Console.WriteLine();
            }

            return 0;
        }

        /// <summary>1ラン実行し、到達階層を返す (100階クリアは 101)。</summary>
        private static int SimulateRun(CharacterType characterType, AILevel selectedAI, int seed)
        {
            return SimulateRun(characterType, selectedAI, seed, relicsEnabled: false,
                RelicConfig.DraftInterval, RelicConfig.MaxRelicsPerRun, out _);
        }

        /// <summary>
        /// 1ラン実行 (レリックモード対応版)。relicsEnabled=true のとき、
        /// RelicConfig.ShouldOfferDraft (draftInterval / maxRelicsPerRun) が true を返した
        /// 階層クリアでのみ、§2.2 の重み付きドラフト (RelicDraftService) から §6.5 の簡易方策
        /// 「最高レアリティ優先・同率は弱点タグ一致優先」で1つ取得して装備する。
        /// relicsCollected には取得レリック総数が入る (baseline は常に0)。
        /// draftInterval/maxRelicsPerRun は既定で RelicConfig の本番値。phase5-sweep のみ
        /// 定数を書き換えずに一時的な値を渡して複数組を比較する (GameController は常に既定値を使う)。
        /// </summary>
        private static int SimulateRun(CharacterType characterType, AILevel selectedAI, int seed,
            bool relicsEnabled, int draftInterval, int maxRelicsPerRun, out int relicsCollected)
        {
            var rng = new SystemRandomSource(seed);
            var session = new GameSession(
                Character.GetCharacter(characterType),
                selectedAI,
                new AIEngine(rng),
                rng);
            var policyRng = new Random(seed ^ 0x5f3759df);

            // ドラフト用乱数は Session の乱数ストリームと分離する (baseline とのシード互換のため:
            // Session 側の配置/消失マス/EasyAI の消費シーケンスをレリック有無で変えない)。
            var draftService = relicsEnabled
                ? new RelicDraftService(new SystemRandomSource(seed ^ 0x2545F491))
                : null;
            var ownedIds = relicsEnabled ? new List<string>() : null;
            relicsCollected = 0;

            session.StartGame(1);

            while (session.Status == GameStatus.Playing)
            {
                var result = PlayTurn(session, policyRng);

                if (result == TurnResult.FloorCleared)
                {
                    // GameController.ResolveTurnNow と同じ順序: 階層クリア確定 (CurrentFloor = クリア階) の
                    // タイミングで RelicConfig.ShouldOfferDraft を判定 → 通れば1個ドラフト → その後 NextFloor。
                    if (relicsEnabled
                        && RelicConfig.ShouldOfferDraft(session.CurrentFloor, ownedIds.Count, draftInterval, maxRelicsPerRun))
                    {
                        if (DraftOneRelic(session, draftService, ownedIds)) relicsCollected++;
                    }
                    session.NextFloor(); // GameWon なら Status=Win でループ終了
                }

                if (session.Status != GameStatus.Playing) break;
            }

            return session.Status == GameStatus.Win ? GameConfig.MaxFloors + 1 : session.CurrentFloor;
        }

        /// <summary>
        /// §6.5 の簡易ドラフト方策: 最高レアリティ優先、同率は「現在の文脈の弱点タグ」一致数優先、
        /// さらに同率は候補順先頭。#18 蒐集家の目所持中は候補数 3→4 (GameController と同じ消費規則)。
        /// 候補が無い (プール枯渇) 場合は false。
        /// </summary>
        private static bool DraftOneRelic(GameSession s, RelicDraftService draftService, List<string> ownedIds)
        {
            int count = s.Relics.DraftCandidateBonusFloorsRemaining > 0 ? 4 : 3;
            var candidates = draftService.DraftCandidates(
                ownedIds, s.CurrentCharacter.Type, count: count,
                selectedAI: s.SelectedAILevel, floor: s.CurrentFloor);
            if (s.Relics.DraftCandidateBonusFloorsRemaining > 0)
            {
                s.Relics.DraftCandidateBonusFloorsRemaining--;
            }
            if (candidates.Count == 0) return false;

            // 文脈の弱点タグ: 盗賊=ThiefRescue / Hard選択=HardAICounter / 次階層41+=LateGame
            RelicTag contextTags = RelicTag.None;
            if (s.CurrentCharacter.Type == CharacterType.Thief) contextTags |= RelicTag.ThiefRescue;
            if (s.SelectedAILevel == AILevel.Hard) contextTags |= RelicTag.HardAICounter;
            if (s.CurrentFloor + 1 >= GameConfig.DisappearStartFloor) contextTags |= RelicTag.LateGame;

            var best = candidates[0];
            int bestScore = TagMatchCount(best.Tags, contextTags);
            for (int i = 1; i < candidates.Count; i++)
            {
                var c = candidates[i];
                int score = TagMatchCount(c.Tags, contextTags);
                if (c.Rarity > best.Rarity || (c.Rarity == best.Rarity && score > bestScore))
                {
                    best = c;
                    bestScore = score;
                }
            }

            best.ApplyTo(s.Relics);
            ownedIds.Add(best.Id);
            return true;
        }

        private static int TagMatchCount(RelicTag tags, RelicTag context)
        {
            int n = 0;
            var matched = tags & context;
            while (matched != 0)
            {
                n += (int)matched & 1;
                matched = (RelicTag)((int)matched >> 1);
            }
            return n;
        }

        private static TurnResult PlayTurn(GameSession s, Random policyRng)
        {
            int dist = GameSession.ChebyshevDistance(s.PlayerPosition, s.EnemyPosition);

            // スキル方策
            switch (s.Skill.Type)
            {
                case SkillType.Bind:
                    if (dist <= 1 && s.RemainingSkillUses > 0 && s.EnemyStoppedTurns == 0) s.BindEnemy();
                    break;
                case SkillType.Shield:
                    if (dist <= 1 && s.RemainingSkillUses > 0 && !s.ShieldActive) s.ActivateSkill();
                    break;
                case SkillType.Dash:
                    TryActivateDash(s);
                    break;
            }

            // #17 心話の絆 (レリックモード): 拘束スキル非所持キャラでも、疑似拘束チャージがあれば
            // エルフと同じ条件 (敵隣接・未拘束) で使用する。GameSession.BindEnemy が消費を担う。
            if (s.Skill.Type != SkillType.Bind && s.Relics.PseudoBindCharges > 0
                && dist <= 1 && s.EnemyStoppedTurns == 0)
            {
                s.BindEnemy();
            }

            var moves = s.GetAvailableMoves();

            // ダッシュ発動中は validateMove がダッシュ移動のみ有効とするため候補を絞る (Swift 挙動を保存)
            if (s.IsSkillActive && s.Skill.Type == SkillType.Dash)
            {
                moves = moves.Where(m => GameEngine.IsValidDashMove(s.PlayerPosition, m)).ToList();
                if (moves.Count == 0)
                {
                    s.IsSkillActive = false; // ダッシュ先なし → 発動解除して通常移動
                    moves = s.GetAvailableMoves();
                }
            }

            if (moves.Count == 0)
            {
                s.PendingPlayerMove = null; // 移動不能 → 時間切れ死亡
                return s.ResolveTurn();
            }

            int best = ChooseGreedyEscape(moves, s.EnemyPosition, policyRng);
            s.SelectMove(best, TimingGrade.Just);
            return s.ResolveTurn();
        }

        /// <summary>ダッシュで通常移動より距離を稼げる場合のみ発動。</summary>
        private static void TryActivateDash(GameSession s)
        {
            if (s.RemainingSkillUses <= 0 || s.IsSkillActive) return;
            int dist = GameSession.ChebyshevDistance(s.PlayerPosition, s.EnemyPosition);
            if (dist > 1) return; // 危険時のみ検討

            var normalMoves = s.GetAvailableMoves();
            int bestNormal = normalMoves.Count > 0 ? normalMoves.Max(m => Manhattan(m, s.EnemyPosition)) : -1;

            s.IsSkillActive = true;
            var withDash = s.GetAvailableMoves().Where(m => GameEngine.IsValidDashMove(s.PlayerPosition, m)).ToList();
            int bestDash = withDash.Count > 0 ? withDash.Max(m => Manhattan(m, s.EnemyPosition)) : -1;

            if (bestDash <= bestNormal)
            {
                s.IsSkillActive = false; // 稼げないなら発動しない
            }
        }

        private static int ChooseGreedyEscape(List<int> moves, int enemyPosition, Random rng)
        {
            int bestDistance = moves.Max(m => Manhattan(m, enemyPosition));
            var best = moves.Where(m => Manhattan(m, enemyPosition) == bestDistance).ToList();
            return best[rng.Next(best.Count)];
        }

        private static int Manhattan(int a, int b)
        {
            int rowA = GameConfig.RowFromPosition(a), colA = GameConfig.ColumnFromPosition(a);
            int rowB = GameConfig.RowFromPosition(b), colB = GameConfig.ColumnFromPosition(b);
            return Math.Abs(rowA - rowB) + Math.Abs(colA - colB);
        }
    }
}
