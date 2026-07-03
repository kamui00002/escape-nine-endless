// PixelArtImportSettings.cs
// Swift 正本: 対応コード無し (Xcode ではアセットカタログが自動処理)。
// Unity ではインポート設定を手動で毎回いじると漏れが出るため、AssetPostprocessor で
// Resources/Sprites 配下のテクスチャと Resources/Sounds/SFX 配下の音を自動設定する。
//
// - ドット絵 (64x64、docs/game-spec.md「ドット絵は 64x64 ピクセルで統一」) は
//   Point フィルタ + 無圧縮でないと拡大時にぼけて世界観が壊れる。
// - SFX はビート同期ゲームなので再生遅延が命: DecompressOnLoad で
//   再生時のデコード遅延をゼロにする (メモリ増は短尺 wav なので許容)。

using UnityEditor;
using UnityEngine;

namespace EscapeNine.EditorTools
{
    /// <summary>
    /// Resources/Sprites・Resources/Sounds/SFX へのアセット追加時に
    /// インポート設定を強制する (新規スプライト追加のたびの手動設定を排除)。
    /// </summary>
    public sealed class PixelArtImportSettings : AssetPostprocessor
    {
        private const string SpritesPathToken = "Resources/Sprites";
        private const string SfxPathToken = "Resources/Sounds/SFX";

        /// <summary>スプライト用テクスチャ: ドット絵設定を適用。</summary>
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains(SpritesPathToken)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single; // 1ファイル=1キャラの独立スプライト運用
            importer.filterMode = FilterMode.Point;              // ドット絵の輪郭を保つ (バイリニア禁止)
            importer.textureCompression = TextureImporterCompression.Uncompressed; // 64x64 なら容量影響は無視できる
            importer.mipmapEnabled = false;                      // UI 表示のみ = ミップマップ不要
            importer.spritePixelsPerUnit = 64f;                  // 64px = 1 unit (原寸基準)
        }

        /// <summary>効果音: 低遅延再生設定を適用。</summary>
        private void OnPreprocessAudio()
        {
            if (!assetPath.Contains(SfxPathToken)) return;

            var importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; // 再生瞬間のデコード遅延を排除
            importer.defaultSampleSettings = settings;
        }
    }
}
