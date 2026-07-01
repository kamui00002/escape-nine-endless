// GameConfig.cs
// Swift 正本からの忠実移植: Utilities/Constants.swift (enum Constants / TutorialConstants)
// バランス定数の一元管理。数値を変える場合は Swift 版と本ファイルの整合を必ず取ること。

namespace EscapeNine.Core
{
    public static class GameConfig
    {
        // Grid
        public const int GridSize = 9;
        public const int GridColumns = 3;
        public const int GridRows = 3;

        // Game Progress
        public const int MaxFloors = 100;
        public const int BaseTurns = 5;
        public const int TurnsPerFloorStep = 10; // 10階ごとにターン数+1

        /// <summary>階層に応じた最大ターン数 (Floor 1-10: 5, 11-20: 6, ..., 91-100: 14)。</summary>
        public static int GetMaxTurns(int floor)
        {
            return BaseTurns + (floor - 1) / TurnsPerFloorStep; // C# 整数除算 (Swift と同挙動)
        }

        public const int MaxSkillUsage = 5; // レガシー（キャラ別定数を優先）

        // Timing（BPM連動で動的計算）
        public const double TimingToleranceLow = 0.6;   // BPM < 100: ±60%
        public const double TimingToleranceMid = 0.45;  // BPM 100-150: ±45%
        public const double TimingToleranceHigh = 0.35; // BPM > 150: ±35%
        public const double BeatCheckInterval = 0.01;   // 10msごとにチェック
        public const double InvisibilityDuration = 0.1; // 透明化スキルの持続時間（秒）

        // BPM曲線（べき乗曲線: BPM = start + (end - start) * (floor/99)^exponent）
        public const double BpmCurveStart = 70.0;   // Floor 1
        public const double BpmCurveEnd = 200.0;    // Floor 100
        public const double BpmCurveExponent = 1.4; // 序盤緩やか→終盤急加速

        // Skill Settings
        public const int SkillResetInterval = 10; // 10階層ごとにスキルリセット

        // Skill Usage per Character
        public const int HeroSkillMaxUsage = 3;
        public const int ThiefSkillMaxUsage = 5;
        public const int WizardSkillMaxUsage = 7;
        public const int ElfSkillMaxUsage = 4;
        public const int KnightSkillMaxUsage = 2;
        public const int BindDurationTurns = 2;

        // Combo System
        public const int ComboMultiplierThreshold1 = 3;    // combo >= 3: ×1.5
        public const int ComboMultiplierThreshold2 = 5;    // combo >= 5: ×2.0
        public const double ComboJustTimingRatio = 0.20;   // ±20% = ジャスト判定
        public const double ComboGoodTimingRatio = 0.35;   // ±35% = グッド判定

        // Boss Floor
        public const int BossFloorInterval = 10;      // 10の倍数階がボス
        public const double BossAIChaseChance = 0.95; // ボスAI追跡確率

        // Turn Countdown Settings
        public const int TurnCountdownBeats = 3;
        public const int GameStartCountdownBeats = 3;

        // Countdown Audio
        public const double CountdownLowFrequency = 440.0;
        public const double CountdownHighFrequency = 880.0;

        // Floor Ranges for Special Rules
        public const int FogStartFloor = 21;       // 霧マップ: 階層21-40
        public const int DisappearStartFloor = 41; // マス消失: 階層41-60
        public const int CombinedRulesStartFloor = 61; // 霧+消失: 階層61-100

        // AI Difficulty Ranges（自然難易度の閾値）
        public const int AINaturalNormalFloor = 16;
        public const int AINaturalHardFloor = 36;

        // Easy AI行動確率
        public const double EasyAIChaseChance = 0.15;
        public const double EasyAIFleeChance = 0.20;

        // 消失マス段階（階層→消失マス数）。Swift の disappearCellStages と同順。
        public static readonly (int Floor, int Count)[] DisappearCellStages =
        {
            (86, 4), (71, 3), (56, 2), (41, 1)
        };

        // Character Unlock
        public const int ThiefUnlockFloor = 10;

        // Pricing
        public const int PremiumCharacterPrice = 240;

        // Audio
        public const double DefaultVolume = 0.7;

        /// <summary>BPM連動タイミング許容値 (拍間隔に対する比率)。Swift: Constants.timingTolerance(for:)</summary>
        public static double TimingTolerance(double bpm)
        {
            if (bpm < 100.0) return TimingToleranceLow;
            if (bpm <= 150.0) return TimingToleranceMid;
            return TimingToleranceHigh;
        }

        // Grid Position Helpers (1-indexed position ⇄ row/col)
        public static int RowFromPosition(int position) => (position - 1) / GridColumns;
        public static int ColumnFromPosition(int position) => (position - 1) % GridColumns;
        public static int PositionFromRowColumn(int row, int column) => row * GridColumns + column + 1;
    }

    /// <summary>v1.1 動的オンボーディング定数。Swift: TutorialConstants</summary>
    public static class TutorialConstants
    {
        public const int TutorialClearTurns = 3;    // Step 4 用に通常 10 から短縮
        public const int PrologueClearTurns = 3;    // Floor 0 プロローグの必要ターン
        public const int PrologueSafeMinDistance = 3; // Floor 1 初回限定の安全距離
        public const int PrologueFloor = 0;
        public const double PrologueBPM = 60.0;      // Floor 0 プロローグ専用 BPM
        public static readonly int[] Step4EnemyScript = { 6, 3, 3 };
    }
}
