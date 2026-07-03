// MainSceneBuilder.cs
// Phase 2 のメインシーンを「クリック無し」でプログラム生成する Editor ツール
// (Phase0SceneBuilder.cs の流儀を踏襲)。
// CLI から呼び出す:
//   Unity -batchmode -quit -projectPath <PROJECT> \
//         -executeMethod EscapeNine.EditorTools.MainSceneBuilder.Build -logFile -
//
// 生成物: Assets/Scenes/Main.unity
//   - "Main Camera"  : 2D 想定 (orthographic / solid color = UITheme.Background) + AudioListener
//   - "EventSystem"  : Active Input Handling に応じた入力モジュール (下記コメント参照)
//   - "Canvas"       : Screen Space Overlay / CanvasScaler ScaleWithScreenSize 1170x2532 match 0.5
//     └ "ScreenRoot" : 10 画面 (ScreenBase) の親コンテナ (全画面 inactive で待機)
//   - "App"          : App + GameController + AudioDirector + Conductor + AudioSource
//   + Build Settings の index 0 に Main を登録 (Phase0 は残す)

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EscapeNine.Runtime;
using EscapeNine.Runtime.UI;

namespace EscapeNine.EditorTools
{
    public static class MainSceneBuilder
    {
        private const string SceneDir = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string MenuBgmResourcePath = "Sounds/BGM/bgm_menu"; // Resources 相対 (拡張子なし)

        /// <summary>参照解像度 (iPhone 14 Pro 系 1170x2532)。UIFactory の比率レイアウト前提と対。</summary>
        private static readonly Vector2 ReferenceResolution = new Vector2(1170f, 2532f);

        [MenuItem("EscapeNine/Build Main Scene")]
        public static void Build()
        {
            Debug.Log("[MainScene] scene build start");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildEventSystem();
            RectTransform screenRoot = BuildCanvasAndScreenRoot();
            BuildScreens(screenRoot);
            BuildApp(screenRoot);

            SaveScene(scene);
            RegisterInBuildSettings();

            Debug.Log("[MainScene] done");
        }

        // MARK: - Camera

        private static void BuildCamera()
        {
            // UI は Screen Space Overlay なのでカメラには依存しないが、
            // 「カメラ無しシーン」警告の抑止と、背景色 (Canvas 描画前のクリア色) のために置く。
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f); // 2D 定石の引き位置

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true; // 2D 想定
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UITheme.Background; // 色値の複製禁止 → UITheme を正とする

            go.AddComponent<AudioListener>(); // EmptyScene には無いので明示追加 (音ゲーの必須品)
        }

        // MARK: - EventSystem

        private static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
            // Active Input Handling が Input System (New) を含む場合。
            // 本 asmdef は Unity.InputSystem を参照しない (パッケージ未導入の環境でも
            // コンパイルを通すため) ので、型はリフレクションで解決する。
            var inputModuleType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null)
            {
                go.AddComponent(inputModuleType);
            }
            else
            {
                // define だけ立っていて型が見つからない異常系 → 旧モジュールへフォールバック
                Debug.LogWarning("[MainScene] InputSystemUIInputModule が見つからないため StandaloneInputModule を使用");
                go.AddComponent<StandaloneInputModule>();
            }
#else
            // 本プロジェクトの ProjectSettings は activeInputHandler=0 (Input Manager / Old) の
            // 想定 → 旧入力の StandaloneInputModule (uGUI 同梱) を使う。
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // MARK: - Canvas + ScreenRoot

        private static RectTransform BuildCanvasAndScreenRoot()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // 幅と高さの折衷 (iPhone/iPad 両対応の指示値)

            // ScreenRoot: 8 画面の親 (Canvas 全面に stretch)。
            // 注意 — 指示の「SafeArea Panel (SafeAreaFitter)」から意図的に変更している:
            // 各 ScreenBase.BuildUI は内部に自前の SafeAreaFitter パネルを構築し、
            // 背景だけノッチ下まで全面に敷く (Swift の ignoresSafeArea 相当) 設計になっている。
            // ここにも Fitter を付けると実機でセーフエリアが二重に差し引かれてレイアウトが
            // 縮むため、シーン側は Fitter 無しの素のコンテナにする (統合時の整合判断)。
            var rootGo = new GameObject("ScreenRoot", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            var rootRt = (RectTransform)rootGo.transform;
            Stretch(rootRt);
            return rootRt;
        }

        // MARK: - Screens (10 画面。初期は全て inactive — Router.Show(Home) が Home だけ点灯する)

        private static void BuildScreens(RectTransform screenRoot)
        {
            AddScreen<HomeScreen>(screenRoot, "HomeScreen");
            AddScreen<GameScreen>(screenRoot, "GameScreen");
            AddScreen<ResultScreen>(screenRoot, "ResultScreen");
            AddScreen<RankingScreen>(screenRoot, "RankingScreen");
            AddScreen<ShopScreen>(screenRoot, "ShopScreen");
            AddScreen<CharacterScreen>(screenRoot, "CharacterScreen");   // ScreenId.CharacterSelect
            AddScreen<SettingsScreen>(screenRoot, "SettingsScreen");
            AddScreen<TutorialScreen>(screenRoot, "TutorialScreen");
            AddScreen<DailyChallengeScreen>(screenRoot, "DailyChallengeScreen"); // Phase 2.5
            AddScreen<AchievementScreen>(screenRoot, "AchievementScreen");       // Phase 2.5
        }

        private static void AddScreen<T>(RectTransform parent, string name) where T : ScreenBase
        {
            // UI 子孫の比率レイアウト (UIFactory.Place) が機能するよう、必ず RectTransform 付きで生成
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            go.AddComponent<T>();
            // BuildUI は実行時に App.Awake → Router.Register が 1 回だけ呼ぶ。
            // inactive でも GetComponentsInChildren(true) で発見される。
            go.SetActive(false);
        }

        // MARK: - App (サービス群のルート)

        private static void BuildApp(RectTransform screenRoot)
        {
            var go = new GameObject("App");

            // Conductor 用 AudioSource。
            // 楽曲再生は AudioDirector に一本化する設計 (Foundation 判断) のため、
            // Conductor.song には「PlayScheduled の null clip 警告回避」として bgm_menu を
            // 割り当てつつ mute する — dspTime 拍クロックはミュートでも正確に進む。
            // mute しないと ChangeBPM → StartSong のたびに menu 曲が AudioDirector の
            // BGM と二重再生されてしまう。
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.mute = true;

            // 生成順 = シリアライズ順。実行時の Awake 順序保証は無いが、
            // AudioDirector.Init / EnsureSources が順序非依存に作られているため安全。
            go.AddComponent<AudioDirector>();
            var conductor = go.AddComponent<Conductor>();
            go.AddComponent<GameController>();
            var app = go.AddComponent<App>();

            var clip = Resources.Load<AudioClip>(MenuBgmResourcePath);
            if (clip != null)
            {
                conductor.song = clip;
                source.clip = clip;
                Debug.Log($"[MainScene] Conductor.song に割当: {MenuBgmResourcePath}");
            }
            else
            {
                Debug.LogWarning(
                    $"[MainScene] BGM が見つかりません: Resources/{MenuBgmResourcePath}.*\n" +
                    "Conductor は null clip でも拍クロックとして動作しますが、PlayScheduled の警告が出る場合があります。");
            }

            // App が Canvas/ScreenRoot 配下の画面を発見するための参照 (App.Awake で使用)
            app.screenRoot = screenRoot;
        }

        // MARK: - Save + Build Settings

        private static void SaveScene(UnityEngine.SceneManagement.Scene scene)
        {
            if (!AssetDatabase.IsValidFolder(SceneDir))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[MainScene] scene saved: {ScenePath}");
        }

        private static void RegisterInBuildSettings()
        {
            // Main を index 0 (起動シーン) に。既存エントリ (Phase0 等) は残す。
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath); // 再実行時の重複防止
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.SaveAssets();
        }

        // MARK: - Helpers

        /// <summary>親いっぱいに広げる (UIFactory.Place と同じ「固定 px ゼロ」原則)。</summary>
        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
#endif
