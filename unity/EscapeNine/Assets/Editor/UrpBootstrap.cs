// UrpBootstrap.cs
// Phase 4.5 Wave 0: Built-in RP → URP 移行の「前提工事」(docs/unity-phase4-5-visual-upgrade-design.md §4)。
// 見た目を一切変えないことがこの Wave のゲート条件 — Bloom 等の演出は Wave 3 以降の仕事。
//
// ProjectSettings/GraphicsSettings.asset は repo 管理外 (bootstrap で再生成され得る) のため、
// リポジトリ内のここを設定の正本にする — BuildScripts.cs の PlayerSettings 一元管理と同じ思想。
// EnsureConfigured() は何度呼んでも安全 (冪等): 既存の .asset は再利用し、新規作成しない。
// Graphics/Quality への割当だけは毎回再確認する (途中失敗からの再実行や、外部要因で
// 割当が外れた場合の自己修復のため。既存アセットへの再代入は無害)。
//
// MCP 越しの対話ではなく、実行完了後にプロジェクト直下へ書く結果マーカーファイル
// (urp-bootstrap-result.txt) を正とする — BuildScripts.cs の WriteResultMarker パターン踏襲。
//
// カラースペースには触れない — 見た目不変ゲートのため、リニア/ガンマの切替は本スクリプトの範囲外。
// シーンにも触れない — カメラの postProcessing フラグ有効化は Wave 3 (BeatVolumePulse) の責務。

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EscapeNine.EditorTools
{
    public static class UrpBootstrap
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RendererDataAssetPath = "Assets/Settings/EscapeNineRendererData.asset";
        private const string PipelineAssetPath = "Assets/Settings/EscapeNineURP.asset";

        // URP 標準の PostProcessData。GraphicsSettings 経由の取得 API はバージョン差異があるため、
        // パッケージ内の既知パスを直接指定する (17.3 で確認済み)。
        private const string DefaultPostProcessDataPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        private const string ResultMarkerFileName = "urp-bootstrap-result.txt";

        // 実行時に Shader.Find でのみ参照する URP シェーダ。マテリアル資産から参照されないため
        // ビルド時のシェーダストリップで削除され、iOS 実機で Shader.Find が null → フォールバックの
        // Standard が URP 下でマゼンタ描画になる (盤面全体がマゼンタ矩形化)。macOS はストリップが緩く再現しない。
        // AlwaysIncludedShaders に登録してストリップから守る (2026-07-05 実機テストで発見)。
        private static readonly string[] AlwaysIncludeShaderNames =
        {
            "Universal Render Pipeline/Lit",             // TileView (盤面タイル)
            "Universal Render Pipeline/Unlit",           // フォールバック用 (Standard の代替)
            "Universal Render Pipeline/Particles/Unlit", // StageParticles / BoardStage バースト粒子
            // TMP テキストの保険 (iOS で TMP シェーダがストリップされると全テキストが不可視/マゼンタ化)。
            // 見つからなければ EnsureAlwaysIncludedShaders が警告してスキップする (無害)。
            "TextMeshPro/Distance Field",
            "TextMeshPro/Mobile/Distance Field",
            "TextMeshPro/Sprite",
        };

        [MenuItem("EscapeNine/Setup URP")]
        public static void Setup()
        {
            EnsureConfigured();
        }

        /// <summary>
        /// URP アセット一式 (RendererData + PipelineAsset) を構成し、Graphics/Quality 設定へ割り当てる。
        /// 既に .asset が存在する場合は再利用するだけで新規作成しない (冪等)。
        /// BuildScripts.BuildMac() からビルド前に呼ばれ、フレッシュなプロジェクトでも
        /// URP が構成された状態でビルドされることを保証する。
        /// </summary>
        public static void EnsureConfigured()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string resultMarkerPath = Path.Combine(projectRoot, ResultMarkerFileName);

            // 前回の結果を必ず消してから開始する (呼び出し側が古いマーカーを誤読しないように)。
            if (File.Exists(resultMarkerPath))
            {
                File.Delete(resultMarkerPath);
            }

            try
            {
                if (!AssetDatabase.IsValidFolder(SettingsFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "Settings");
                }

                UniversalRendererData rendererData =
                    AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataAssetPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();

                    var postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(DefaultPostProcessDataPath);
                    if (postProcessData == null)
                    {
                        Debug.LogWarning(
                            "[UrpBootstrap] PostProcessData が見つかりません (" + DefaultPostProcessDataPath +
                            ")。Bloom 等のポストプロセスが動作しない可能性があります。");
                    }
                    rendererData.postProcessData = postProcessData;

                    AssetDatabase.CreateAsset(rendererData, RendererDataAssetPath);
                }

                UniversalRenderPipelineAsset urpAsset =
                    AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
                if (urpAsset == null)
                {
                    urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
                    urpAsset.supportsHDR = true; // ビート同期 Bloom (Wave 3) のため
                    urpAsset.msaaSampleCount = 1; // 2D スプライト中心のため MSAA 不要
                    urpAsset.shadowDistance = 30f; // 小さなジオラマ盤面用

                    AssetDatabase.CreateAsset(urpAsset, PipelineAssetPath);
                }

                AssignToGraphicsAndQuality(urpAsset);
                EnsureAlwaysIncludedShaders();
                AssetDatabase.SaveAssets();

                WriteResultMarker(resultMarkerPath, urpAsset);
            }
            catch (Exception e)
            {
                // 構成が例外で中断した場合も、成否判定できるようマーカーだけは必ず残す。
                WriteExceptionMarker(resultMarkerPath, e);
            }
        }

        private static void AssignToGraphicsAndQuality(UniversalRenderPipelineAsset urpAsset)
        {
            // GraphicsSettings.renderPipelineAsset は Unity 6 で obsolete
            // (-> defaultRenderPipeline に置換、UnityUpgradable 警告あり)。
            GraphicsSettings.defaultRenderPipeline = urpAsset;

            // QualitySettings には index 指定の Set メソッドが存在しない (GetRenderPipelineAssetAt のみ)。
            // URP 純正コンバータ (RenderSettingsConverter.cs) と同じパターンで、
            // 各 Quality Level を一時的にアクティブにしてから renderPipeline プロパティへ代入する。
            int originalLevel = QualitySettings.GetQualityLevel();
            int levelCount = QualitySettings.names.Length;
            for (int i = 0; i < levelCount; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urpAsset;
            }
            QualitySettings.SetQualityLevel(originalLevel, false);
        }

        /// <summary>
        /// 実行時 Shader.Find 専用の URP シェーダを GraphicsSettings の AlwaysIncludedShaders へ登録し、
        /// iOS ビルドのシェーダストリップから守る。冪等 (既に含まれていれば追加しない)。
        /// </summary>
        private static void EnsureAlwaysIncludedShaders()
        {
            var graphicsSettings = GraphicsSettings.GetGraphicsSettings();
            var so = new SerializedObject(graphicsSettings);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null)
            {
                Debug.LogWarning("[UrpBootstrap] m_AlwaysIncludedShaders プロパティが見つかりません。シェーダ登録を中止。");
                return;
            }

            bool changed = false;
            foreach (var name in AlwaysIncludeShaderNames)
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogWarning($"[UrpBootstrap] シェーダ '{name}' が見つからず AlwaysIncluded に追加できません。");
                    continue;
                }

                bool present = false;
                for (int i = 0; i < arr.arraySize; i++)
                {
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) { present = true; break; }
                }
                if (present) continue;

                int idx = arr.arraySize;
                arr.InsertArrayElementAtIndex(idx);
                arr.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
                changed = true;
                Debug.Log($"[UrpBootstrap] AlwaysIncludedShaders に追加: {name}");
            }

            if (changed) so.ApplyModifiedProperties();
        }

        private static void WriteResultMarker(string markerPath, UniversalRenderPipelineAsset urpAsset)
        {
            string line = string.Format("result=Configured pipeline={0}", urpAsset.name);
            File.WriteAllText(markerPath, line);
            Debug.Log("[UrpBootstrap] " + line);
        }

        private static void WriteExceptionMarker(string markerPath, Exception e)
        {
            string sanitizedMessage = e.Message.Replace("\n", " ").Replace("\r", " ");
            string line = string.Format("result=Failed pipeline=none exception={0}", sanitizedMessage);
            File.WriteAllText(markerPath, line);
            Debug.LogError("[UrpBootstrap] 構成例外: " + e);
        }
    }
}
#endif
