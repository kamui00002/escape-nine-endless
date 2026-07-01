// Floor.cs
// Swift 正本からの忠実移植: Models/Floor.swift
// BPM曲線・特殊ルール・AI階層スケーリング。数値の正本は本ファイル + GameConfig。

using System;

namespace EscapeNine.Core
{
    public static class Floor
    {
        /// <summary>ボスフロアかどうか（10の倍数階）。Floor 0 (プロローグ) は除外。</summary>
        public static bool IsBossFloor(int floor)
        {
            return floor > 0 && floor % GameConfig.BossFloorInterval == 0;
        }

        /// <summary>
        /// べき乗曲線: BPM = start + (end - start) * ((floor-1)/99)^exponent。
        /// Floor 0 はプロローグ専用で 60 BPM。Swift: Floor.calculateBPM(for:)
        /// </summary>
        public static double CalculateBPM(int floor)
        {
            if (floor == TutorialConstants.PrologueFloor)
            {
                return TutorialConstants.PrologueBPM;
            }

            int clampedFloor = Math.Max(1, Math.Min(floor, GameConfig.MaxFloors));
            double ratio = (clampedFloor - 1) / 99.0;
            double scaled = Math.Pow(ratio, GameConfig.BpmCurveExponent);
            return GameConfig.BpmCurveStart + (GameConfig.BpmCurveEnd - GameConfig.BpmCurveStart) * scaled;
        }

        /// <summary>階層の特殊ルールを取得。Swift: Floor.getSpecialRule(for:)</summary>
        public static SpecialRule GetSpecialRule(int floor)
        {
            if (floor < GameConfig.FogStartFloor) return SpecialRule.None;
            if (floor < GameConfig.DisappearStartFloor) return SpecialRule.Fog;
            if (floor < GameConfig.CombinedRulesStartFloor) return SpecialRule.Disappear;
            return SpecialRule.FogDisappear;
        }

        /// <summary>階層の自然難易度。Swift: Floor.getNaturalDifficulty(for:)</summary>
        public static AILevel GetNaturalDifficulty(int floor)
        {
            if (floor < GameConfig.AINaturalNormalFloor) return AILevel.Easy;
            if (floor < GameConfig.AINaturalHardFloor) return AILevel.Normal;
            return AILevel.Hard;
        }

        /// <summary>
        /// プレイヤー選択を考慮した実効AI難易度。ボスフロアは常に Boss。
        /// Swift: Floor.getEffectiveAILevel(for:playerSelection:)
        /// </summary>
        public static AILevel GetEffectiveAILevel(int floor, AILevel playerSelection)
        {
            if (IsBossFloor(floor)) return AILevel.Boss;
            AILevel natural = GetNaturalDifficulty(floor);

            switch (playerSelection)
            {
                case AILevel.Easy:
                    // 1段下げる
                    switch (natural)
                    {
                        case AILevel.Easy: return AILevel.Easy;
                        case AILevel.Normal: return AILevel.Easy;
                        case AILevel.Hard: return AILevel.Normal;
                        case AILevel.Boss: return AILevel.Hard; // ボスは下げない
                        default: return AILevel.Easy;
                    }
                case AILevel.Normal:
                    return natural;
                case AILevel.Hard:
                    // 1段上げる
                    switch (natural)
                    {
                        case AILevel.Easy: return AILevel.Normal;
                        case AILevel.Normal: return AILevel.Hard;
                        case AILevel.Hard: return AILevel.Hard;
                        case AILevel.Boss: return AILevel.Boss;
                        default: return AILevel.Hard;
                    }
                case AILevel.Boss:
                    return AILevel.Boss;
                default:
                    return natural;
            }
        }

        /// <summary>階層に応じた敵スプライト名。Swift: Floor.getEnemySprite(for:)</summary>
        public static string GetEnemySprite(int floor)
        {
            if (floor >= 1 && floor <= 25) return "red_oni";
            if (floor >= 26 && floor <= 50) return "blue_oni";
            if (floor >= 51 && floor <= 75) return "skeleton";
            if (floor >= 76 && floor <= 100) return "dragon";
            return "red_oni"; // フォールバック
        }
    }
}
