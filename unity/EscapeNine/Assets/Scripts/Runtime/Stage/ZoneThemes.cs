// ZoneThemes.cs
// Wave 4: 階層帯 (ゾーン) ごとの見た目テーブル。ScriptableObject は使わず、design 指定の
// 「コード定義の静的テーブル」(定数一元管理の規約に合わせる) とする。
//
// ゾーン境界の正本は Floor.GetEnemySprite() と同じ範囲 (赤鬼1-25 / 青鬼26-50 / 骸骨51-75 /
// ドラゴン76-100)。Floor 0 (プロローグ) は floor<=25 に含まれるため RedOni 扱いになる
// (GetEnemySprite の "red_oni" フォールバックと同じ挙動)。
//
// 色は既存の「明るい冒険ファンタジー系」パレット (UITheme: #f4a460 系) から離れすぎない
// 範囲でゾーンごとに変化させる。UITheme.cs は変更しない (Hex ヘルパーは private のため、
// 本ファイルを自己完結させる目的で同一の 1 行ヘルパーを複製する。TileView.cs が
// GridCellWidget.cs の色定数を複製しているのと同じ理由)。
//
// 情報パリティ (絶対制約 §2) への関与: 本ファイルはただの色/数値テーブルであり、
// 可視性判定には一切関与しない (判定は GameSession に一元化されたまま)。

using UnityEngine;

namespace EscapeNine.Runtime.Stage
{
    /// <summary>ゾーン別アンビエントパーティクルの種別。StageParticles が参照する。</summary>
    public enum ZoneParticleKind
    {
        Embers,    // 火の粉 (上昇・橙) — 赤鬼ゾーン (溶岩)
        Snow,      // 雪片 (下降・白青) — 青鬼ゾーン (氷洞)
        Spirits,   // 霊魂 (漂う・紫) — 骸骨ゾーン (紫闇)
        BigEmbers, // 火の粉大 (速い・赤橙) — ドラゴンゾーン (劫火)
    }

    /// <summary>1 ゾーン分の見た目パラメータ。</summary>
    public readonly struct ZoneTheme
    {
        /// <summary>BoardStage が「ゾーンが変わったか」を安価に比較するための識別子 (0..3)。</summary>
        public readonly int ZoneIndex;

        public readonly string Name;

        /// <summary>ゾーン主光 (Directional Light) の色。</summary>
        public readonly Color LightColor;

        /// <summary>ゾーン主光の強度 (霧が無い時の基準値。霧時は StageLights が別途減光する)。</summary>
        public readonly float LightIntensity;

        /// <summary>環境光 (RenderSettings.ambientLight) の色 (霧が無い時の基準値)。</summary>
        public readonly Color AmbientColor;

        /// <summary>カメラのクリアカラー (StageCameraDirector.ZoneBackgroundOverride へ渡す)。</summary>
        public readonly Color CameraBackgroundColor;

        /// <summary>通常マス (TileView._normalFillColor の既定色) のゾーンティント。</summary>
        public readonly Color TileTint;

        /// <summary>StagePostFx.Bloom.tint (発光色) へ適用するティント。</summary>
        public readonly Color BloomTint;

        public readonly ZoneParticleKind Particle;

        public ZoneTheme(int zoneIndex, string name, Color lightColor, float lightIntensity, Color ambientColor,
            Color cameraBackgroundColor, Color tileTint, Color bloomTint, ZoneParticleKind particle)
        {
            ZoneIndex = zoneIndex;
            Name = name;
            LightColor = lightColor;
            LightIntensity = lightIntensity;
            AmbientColor = ambientColor;
            CameraBackgroundColor = cameraBackgroundColor;
            TileTint = tileTint;
            BloomTint = bloomTint;
            Particle = particle;
        }
    }

    public static class ZoneThemes
    {
        /// <summary>霧時に環境光へ掛ける減衰係数 (design: 「大きく落とし」)。StageLights が参照する。</summary>
        public const float FogAmbientDimFactor = 0.12f;

        /// <summary>霧時にゾーン主光 (Directional Light) へ掛ける減衰係数。
        /// 環境光だけでなく主光も落とさないと URP Lit タイルが霧中でも均一に明るく見えてしまい、
        /// 「プレイヤータイル上だけ灯る」効果が視認できないため (advisor 指摘)。</summary>
        public const float FogDirectionalDimFactor = 0.2f;

        /// <summary>溶岩の暖色 (階層1-25)。</summary>
        public static readonly ZoneTheme RedOni = new ZoneTheme(
            0, "RedOni",
            Hex("#ff9d52"), 1.1f,
            Hex("#3a1c0f"),
            Hex("#2c1810"), // UITheme.Background と同値 (既定ゾーン。理由はファイル冒頭コメント参照)
            Hex("#55362a"),
            Hex("#ffb066"),
            ZoneParticleKind.Embers);

        /// <summary>氷洞の寒色 (階層26-50)。</summary>
        public static readonly ZoneTheme BlueOni = new ZoneTheme(
            1, "BlueOni",
            Hex("#bfe3ff"), 1.0f,
            Hex("#16232e"),
            Hex("#16222c"),
            Hex("#39424a"),
            Hex("#bfe3ff"),
            ZoneParticleKind.Snow);

        /// <summary>紫闇 (階層51-75)。</summary>
        public static readonly ZoneTheme Skeleton = new ZoneTheme(
            2, "Skeleton",
            Hex("#b48cff"), 0.85f,
            Hex("#1c1224"),
            Hex("#1a1020"),
            Hex("#3a2c47"),
            Hex("#c9a3ff"),
            ZoneParticleKind.Spirits);

        /// <summary>劫火 (階層76-100)。</summary>
        public static readonly ZoneTheme Dragon = new ZoneTheme(
            3, "Dragon",
            Hex("#ff5a36"), 1.3f,
            Hex("#3d1206"),
            Hex("#2a0d05"),
            Hex("#5c2314"),
            Hex("#ff6a33"),
            ZoneParticleKind.BigEmbers);

        /// <summary>階層 (Floor 0 のプロローグ含む) → ゾーンテーマ。境界の正本: Floor.GetEnemySprite()。</summary>
        public static ZoneTheme ForFloor(int floor)
        {
            if (floor <= 25) return RedOni;
            if (floor <= 50) return BlueOni;
            if (floor <= 75) return Skeleton;
            return Dragon;
        }

        // UITheme.Hex() と同一の 1 行ヘルパー (private のため複製。理由はファイル冒頭コメント参照)。
        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
        }
    }
}
