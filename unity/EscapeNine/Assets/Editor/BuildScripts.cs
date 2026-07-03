// BuildScripts.cs
// Phase 6a: デスクトップ(Steam体験版)技術基盤。
// CLI (batchmode -executeMethod) からも呼べる macOS ビルドエントリポイント。
// MCP 越しの対話ではなく、ビルド完了後にプロジェクト直下へ書く結果マーカーファイル
// (build-mac-result.txt) を正とする — 呼び出し側の MCP セッションがタイムアウトしても
// ファイルを見れば成否・エラー数・出力サイズが分かるようにするため。
//
// CLI 呼び出し例:
//   Unity -batchmode -quit -projectPath <PROJECT> \
//         -executeMethod EscapeNine.EditorTools.BuildScripts.BuildMac -logFile -
//
// PlayerSettings は本スクリプトからは一切変更しない (現行設定のままビルドする)。

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EscapeNine.EditorTools
{
    public static class BuildScripts
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ResultMarkerFileName = "build-mac-result.txt";

        [MenuItem("EscapeNine/Build macOS")]
        public static void BuildMac()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string resultMarkerPath = Path.Combine(projectRoot, ResultMarkerFileName);

            // 前回の結果を必ず消してから開始する (呼び出し側が古いマーカーを誤読しないように)。
            if (File.Exists(resultMarkerPath))
            {
                File.Delete(resultMarkerPath);
            }

            string outputPath = Path.Combine(projectRoot, "Builds", "mac", "EscapeNine.app");

            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.None,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                WriteResultMarker(resultMarkerPath, report, outputPath);
            }
            catch (Exception e)
            {
                // ビルド自体が例外で中断した場合も、成否判定できるようマーカーだけは必ず残す。
                WriteExceptionMarker(resultMarkerPath, outputPath, e);
            }
        }

        private static void WriteResultMarker(string markerPath, BuildReport report, string outputPath)
        {
            BuildSummary summary = report.summary;
            double sizeMB = summary.totalSize / (1024.0 * 1024.0);

            string line = string.Format(
                "result={0} errors={1} warnings={2} sizeMB={3:F1} path={4}",
                summary.result, summary.totalErrors, summary.totalWarnings, sizeMB, outputPath);

            File.WriteAllText(markerPath, line);
            Debug.Log("[BuildScripts] " + line);
        }

        private static void WriteExceptionMarker(string markerPath, string outputPath, Exception e)
        {
            string sanitizedMessage = e.Message.Replace("\n", " ").Replace("\r", " ");
            string line = string.Format(
                "result=Failed errors=1 warnings=0 sizeMB=0 path={0} exception={1}",
                outputPath, sanitizedMessage);

            File.WriteAllText(markerPath, line);
            Debug.LogError("[BuildScripts] ビルド例外: " + e);
        }
    }
}
#endif
