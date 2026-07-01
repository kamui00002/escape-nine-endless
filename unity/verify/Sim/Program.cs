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
//  - 乱数はシード固定で再現可能。

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

        public static int Main(string[] args)
        {
            var characters = new[]
            {
                CharacterType.Hero, CharacterType.Thief, CharacterType.Wizard,
                CharacterType.Elf, CharacterType.Knight
            };
            var aiLevels = new[] { AILevel.Easy, AILevel.Normal, AILevel.Hard };

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

        /// <summary>1ラン実行し、到達階層を返す (100階クリアは 101)。</summary>
        private static int SimulateRun(CharacterType characterType, AILevel selectedAI, int seed)
        {
            var rng = new SystemRandomSource(seed);
            var session = new GameSession(
                Character.GetCharacter(characterType),
                selectedAI,
                new AIEngine(rng),
                rng);
            var policyRng = new Random(seed ^ 0x5f3759df);

            session.StartGame(1);

            while (session.Status == GameStatus.Playing)
            {
                PlayTurn(session, policyRng);

                if (session.Status != GameStatus.Playing) break;
            }

            return session.Status == GameStatus.Win ? GameConfig.MaxFloors + 1 : session.CurrentFloor;
        }

        private static void PlayTurn(GameSession s, Random policyRng)
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
                s.ResolveTurn();
                return;
            }

            int best = ChooseGreedyEscape(moves, s.EnemyPosition, policyRng);
            s.SelectMove(best, TimingGrade.Just);
            var result = s.ResolveTurn();

            if (result == TurnResult.FloorCleared)
            {
                s.NextFloor(); // GameWon なら Status=Win でループ終了
            }
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
