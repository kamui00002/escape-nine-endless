// KeyboardInput.cs
// Phase 6a: デスクトップ(Steam体験版)技術基盤。Swift 正本には対応なし (iOS はタッチ操作のみ) —
// Unity デスクトップ版のみのキーボード入力層。
//
// 入力系は旧 Input Manager 前提 (ProjectSettings: activeInputHandler=0 確認済み) のため
// Input.GetKeyDown を使う (Input System パッケージには依存しない)。
//
// モバイル実機 (Application.isMobilePlatform == true) では Update 冒頭で早期 return し無効化する。
// Application.isMobilePlatform は Editor では常に false (ビルドターゲットに関わらず) なので、
// Editor 実行中は常にキーボード操作が効く (開発時の動作確認を容易にするための意図的挙動)。

using UnityEngine;
using EscapeNine.Runtime.UI;

namespace EscapeNine.Runtime
{
    public sealed class KeyboardInput : MonoBehaviour
    {
        // ---- 3x3 盤面キーマッピング (盤面 position 1..9 = 上段左から 1,2,3 / 中段 4,5,6 / 下段 7,8,9) ----
        // テンキーは物理配置が上下逆 (Keypad7/8/9 が最上段) のため、盤面の上段 (1,2,3) へ反転マップする。
        private static readonly (KeyCode key, int position)[] MoveKeyMap =
        {
            // 数字キー(メイン列): そのまま 1..9
            (KeyCode.Alpha1, 1), (KeyCode.Alpha2, 2), (KeyCode.Alpha3, 3),
            (KeyCode.Alpha4, 4), (KeyCode.Alpha5, 5), (KeyCode.Alpha6, 6),
            (KeyCode.Alpha7, 7), (KeyCode.Alpha8, 8), (KeyCode.Alpha9, 9),

            // QWE / ASD / ZXC: 盤面と同じ空間配置 (Q=1,W=2,E=3 / A=4,S=5,D=6 / Z=7,X=8,C=9)
            (KeyCode.Q, 1), (KeyCode.W, 2), (KeyCode.E, 3),
            (KeyCode.A, 4), (KeyCode.S, 5), (KeyCode.D, 6),
            (KeyCode.Z, 7), (KeyCode.X, 8), (KeyCode.C, 9),

            // テンキー: 物理配置 (Keypad7/8/9 が上段) を盤面の上段 (position 1,2,3) へ反転マップ
            (KeyCode.Keypad7, 1), (KeyCode.Keypad8, 2), (KeyCode.Keypad9, 3),
            (KeyCode.Keypad4, 4), (KeyCode.Keypad5, 5), (KeyCode.Keypad6, 6),
            (KeyCode.Keypad1, 7), (KeyCode.Keypad2, 8), (KeyCode.Keypad3, 9),
        };

        // ---- 画面インスタンスのキャッシュ ----
        // MainSceneBuilder が生成する固定 8 画面は各型ともシーンに 1 個だけ存在する前提のため、
        // Awake 時に 1 回だけ検索してキャッシュする (Update 毎の検索コストを避ける)。
        private GameScreen _gameScreen;
        private ResultScreen _resultScreen;
        private HomeScreen _homeScreen;

        private void Awake()
        {
            _gameScreen = FindFirstObjectByType<GameScreen>(FindObjectsInactive.Include);
            _resultScreen = FindFirstObjectByType<ResultScreen>(FindObjectsInactive.Include);
            _homeScreen = FindFirstObjectByType<HomeScreen>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (Application.isMobilePlatform) return; // 実機モバイルでは無効 (Editor は常時有効)
            if (App.I == null || App.I.Router == null) return;

            switch (App.I.Router.Current)
            {
                case ScreenId.Game:
                    UpdateGameInput();
                    break;
                case ScreenId.Result:
                    UpdateResultInput();
                    break;
                case ScreenId.Home:
                    UpdateHomeInput();
                    break;
            }
        }

        // MARK: - Game 画面

        private void UpdateGameInput()
        {
            GameController game = App.I.Game;
            if (game == null) return;

            if (game.IsRelicDraftPending)
            {
                // Phase 5a (docs/unity-phase5-roguelike-design.md §2.1): レリックドラフト提示中は
                // 1/2/3/4 キーのみカード選択として扱い、既存の1-9移動キーとは排他にする
                // (Steam体験版のキーボード操作対応の必須要件)。
                // 4 は Phase 5b の #18 蒐集家の目 (候補3→4) 用。候補が3枚以下のときは
                // GameScreen.SelectRelicCardFromKeyboard 側の範囲ガードで無視される。
                if (Input.GetKeyDown(KeyCode.Alpha1) && _gameScreen != null) _gameScreen.SelectRelicCardFromKeyboard(0);
                if (Input.GetKeyDown(KeyCode.Alpha2) && _gameScreen != null) _gameScreen.SelectRelicCardFromKeyboard(1);
                if (Input.GetKeyDown(KeyCode.Alpha3) && _gameScreen != null) _gameScreen.SelectRelicCardFromKeyboard(2);
                if (Input.GetKeyDown(KeyCode.Alpha4) && _gameScreen != null) _gameScreen.SelectRelicCardFromKeyboard(3);
            }
            else
            {
                for (int i = 0; i < MoveKeyMap.Length; i++)
                {
                    if (Input.GetKeyDown(MoveKeyMap[i].key))
                    {
                        game.RequestMove(MoveKeyMap[i].position);
                    }
                }
            }

            // FloorClear オーバーレイの「スタート」相当。ドラフト提示中でもまだ「スタート」を
            // 押していない間 (GameScreen 側がドラフト画面を開いていない間) は有効なままにする
            // 必要があるため、上の draftPending 分岐の外側で常時判定する。
            // GameController.AdvanceToNextFloor を直接叩くとドラフトへの分岐が起きない
            // (IsRelicDraftPending ゲートで無視されるだけになる) ため、必ず GameScreen 経由にする。
            if (IsAdvanceKeyDown() && game.IsFloorClearPending)
            {
                if (_gameScreen != null) _gameScreen.TriggerFloorClearStartFromKeyboard();
            }

            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                game.ActivateSkill();
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                game.TapEnemy();
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                if (_gameScreen != null) _gameScreen.TogglePauseFromKeyboard();
            }
        }

        // MARK: - Result 画面

        private void UpdateResultInput()
        {
            if (Input.GetKeyDown(KeyCode.R) || IsAdvanceKeyDown())
            {
                if (_resultScreen != null) _resultScreen.TriggerRetry();
            }
        }

        // MARK: - Home 画面

        private void UpdateHomeInput()
        {
            if (IsAdvanceKeyDown())
            {
                if (_homeScreen != null) _homeScreen.TriggerPlay();
            }
        }

        /// <summary>Space / Enter (メイン + テンキー) の共通判定。</summary>
        private static bool IsAdvanceKeyDown()
        {
            return Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter);
        }
    }
}
