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
// デスクトップ用 PlayerSettings (ウィンドウモード等) はビルド前に本スクリプトが毎回適用する。
// ProjectSettings.asset は repo 管理外 (bootstrap で再生成され得る) のため、
// リポジトリ内のここを設定の正本にする — Unity 新規プロジェクトの既定は
// FullScreenWindow 起動で閉じるボタンが無く、アプリを終了できない (2026-07-04 オーナー報告)。

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EscapeNine.EditorTools
{
    public static class BuildScripts
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ResultMarkerFileName = "build-mac-result.txt";
        private const string IosResultMarkerFileName = "build-ios-result.txt";

        // Bundle ID = 配信中 Swift 版と同一 (2026-07-16 差し替え: Unity 版を既存アプリのアップデートとして配信し、
        // レビュー/順位資産を維持 + 既存ユーザーのセーブ移行 B-1 を成立させるため)。本番 bundle は
        // GoogleService-Info.plist / 全 build config / 配信中アプリ(1.5.7 build21) の3点一致で確認済。
        // ※ docs の com.souatou.escapenine は古い仕様書(DEVELOPMENT_SWIFT.md)の誤記。旧テスト用分離 bundle: com.yoshidometoru.escapenine.unity。
        private const string IosBundleId = "com.yoshidometoru.EscapeNine-endless-";
        private const string IosTeamId = "B7F79FDM78"; // 元 Swift アプリと同一 Apple Developer チーム
        private const string IosMinVersion = "15.0";
        // App Store 提出バージョン: 配信中 1.5.7 (build 21) を「厳密に超える」こと (ASC の要件。超えないと upload 拒否)。
        // トレイン運用 (memory version_train_scheme): 1.5.x トレイン。upload で altool 90186 (train closed) が
        // 出たら MARKETING_VERSION を次番号へ上げて再アップロード (build 番号も +1)。
        private const string IosMarketingVersion = "1.5.8";
        private const string IosBuildNumber = "22";

        /// <summary>
        /// デスクトップ(スタンドアロン)専用の PlayerSettings。iOS/Android には影響しない。
        /// ウィンドウ表示 + リサイズ可 (閉じる/最小化ボタンが付く)。初期窓は参照解像度
        /// 1170x2532 の 1/2.5 縮小 (縦長 468x1013) — DesktopPillarbox v2 が任意サイズへ
        /// 相似形で追従するため、画面に収まらない環境では macOS のクランプに任せてよい。
        /// </summary>
        private static void ApplyDesktopPlayerSettings()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 468;
            PlayerSettings.defaultScreenHeight = 1013;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
        }

        [MenuItem("EscapeNine/Build macOS")]
        public static void BuildMac()
        {
            ApplyDesktopPlayerSettings();

            // Phase 4.5 Wave 0: フレッシュなプロジェクトでも URP が構成された状態でビルドされるようにする。
            UrpBootstrap.EnsureConfigured();

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
                // アクティブターゲットが Standalone でなければ切り替える (iOS ビルド後などに実行しても
                // BuildPlayer が非アクティブターゲットを弾かないように。BuildIOS と対称)。
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
                }

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

        /// <summary>
        /// iOS 実機テスト用: PlayerSettings を設定 → iOS ターゲットへ切替 → Xcode プロジェクトを
        /// Builds/ios/ に生成する。署名・実機転送は生成後に Xcode で行う (自動署名前提)。
        /// 初回のターゲット切替は全アセットの iOS 再インポートが走るため時間がかかる。
        /// 結果は build-ios-result.txt に書く (macOS と同じマーカー方式)。
        /// </summary>
        [MenuItem("EscapeNine/Build iOS (Xcode project)")]
        public static void BuildIOS()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string resultMarkerPath = Path.Combine(projectRoot, IosResultMarkerFileName);
            if (File.Exists(resultMarkerPath)) File.Delete(resultMarkerPath);

            string outputPath = Path.Combine(projectRoot, "Builds", "ios");

            try
            {
                UrpBootstrap.EnsureConfigured();
                ApplyIosPlayerSettings();

                // iOS ターゲットが非アクティブなら切り替える (BuildPlayer は非アクティブターゲットを弾く)。
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
                }

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.iOS,
                    // ★ 2026-07-16 App Store 提出のため None (Release) に設定。DEVELOPMENT_BUILD が未定義になり、
                    // HomeScreen の「管理者用設定 (DEBUG)」パネル (開始階層/BPM/AI/ターンCD/全キャラ解放/
                    // カウントダウン省略) が出荷ビルドから消え、AdConfig も本番広告 ID を返す
                    // (#if UNITY_EDITOR || DEVELOPMENT_BUILD ゲート)。
                    // 実機デバッグ (デバッグパネル/テスト広告/console ログ) が要る時のみ一時的に
                    // BuildOptions.Development へ戻す (提出前に必ず None に戻すこと)。
                    options = BuildOptions.None,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                WriteResultMarker(resultMarkerPath, report, outputPath);
            }
            catch (Exception e)
            {
                WriteExceptionMarker(resultMarkerPath, outputPath, e);
            }
        }

        /// <summary>
        /// iOS シミュレータ用: 物理端末が手元に無い時に Mac 上のシミュレータで自動 UI テストするための
        /// Xcode プロジェクトを Builds/ios-sim/ に生成する。実機用 (BuildIOS) との違いは SDK を
        /// SimulatorSDK にする点のみ。署名は不要 (シミュレータは未署名 .app を起動できる)。
        /// 結果は build-ios-sim-result.txt に書く。ネイティブ端末専用プラグインが増えたら
        /// シミュレータビルドは失敗し得る (その時は実機ビルドに戻す)。
        /// CLI: Unity -batchmode -quit -projectPath <P> -executeMethod EscapeNine.EditorTools.BuildScripts.BuildIOSSimulator
        /// </summary>
        [MenuItem("EscapeNine/Build iOS Simulator (Xcode project)")]
        public static void BuildIOSSimulator()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string resultMarkerPath = Path.Combine(projectRoot, "build-ios-sim-result.txt");
            if (File.Exists(resultMarkerPath)) File.Delete(resultMarkerPath);

            string outputPath = Path.Combine(projectRoot, "Builds", "ios-sim");

            try
            {
                UrpBootstrap.EnsureConfigured();
                ApplyIosPlayerSettings();
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK; // ← 実機用との唯一の差

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
                }

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.iOS,
                    options = BuildOptions.None,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                WriteResultMarker(resultMarkerPath, report, outputPath);
            }
            catch (Exception e)
            {
                WriteExceptionMarker(resultMarkerPath, outputPath, e);
            }
        }

        /// <summary>iOS 専用 PlayerSettings (Bundle ID / 署名チーム / 縦向き / 最小 iOS)。macOS には影響しない。</summary>
        private static void ApplyIosPlayerSettings()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, IosBundleId);
            PlayerSettings.iOS.appleDeveloperTeamID = IosTeamId;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true; // Xcode 自動署名
            PlayerSettings.iOS.targetOSVersionString = IosMinVersion;
            // App Store 提出バージョン (配信中 1.5.7/build21 を超える)。更新配信として受理されるために必須。
            PlayerSettings.bundleVersion = IosMarketingVersion;
            PlayerSettings.iOS.buildNumber = IosBuildNumber;
            // 実機ビルドは常に Device SDK。BuildIOSSimulator が SimulatorSDK に切替え得るため、
            // 直後に実機ビルドしても取り残しでシミュレータ SDK のままにならないよう明示的に戻す。
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;

            // 縦向き固定 (本作は縦持ち専用。回転で 3D 舞台のフレーミングが崩れるのを防ぐ)。
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
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
