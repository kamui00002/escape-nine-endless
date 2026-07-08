// StubAdService.cs
// IAdService の no-op 実装。ネイティブ SDK (Google Mobile Ads Unity plugin) 未導入の間、
// 呼び出し口の配線だけを確定させておくためのプレースホルダー (Debug.Log で「本物 SDK 未導入」を明示する)。
// 実 SDK 導入時 (decision brief §4.2 ランブック実行時) は、この代わりに
// 例えば AdMobService : IAdService を新規実装して App.cs の生成箇所を差し替えるだけでよい。
//
// AdsRemoved の正本は PlayerState.AdRemoved (StoreKit/IAP 購入導線が更新する既存フラグ、
// PlayerState.cs 参照)。ここで新規フラグを持つと二重管理になるため、参照のみ行う。

using System;
using UnityEngine;

namespace EscapeNine.Runtime.Ads
{
    public sealed class StubAdService : IAdService
    {
        private readonly PlayerState _player;

        /// <summary>
        /// ATT 許可前の consent 状態。init = denied (Swift/decision brief のアンチパターン対策:
        /// ATT ダイアログの結果が出る前に consent を granted にしてはならない)。
        /// stub では実際に何かへ送信するわけではないが、実 SDK 差し替え時に同じ状態機械を
        /// 引き継げるよう、ここでフラグとして表現しておく。
        /// </summary>
        private bool _trackingAuthorized;

        public StubAdService(PlayerState player)
        {
            _player = player;
        }

        public bool AdsRemoved => _player != null && _player.AdRemoved;

        public void Initialize()
        {
            if (AdsRemoved)
            {
                Debug.Log("[StubAdService] 広告削除済み - 初期化スキップ (本物SDK未導入)");
                return;
            }
            Debug.Log("[StubAdService] 初期化 (本物SDK未導入・no-op)");
        }

        public void ShowBanner()
        {
            if (AdsRemoved)
            {
                return; // Swift: shouldShowBannerAd = !isAdRemoved
            }
            Debug.Log("[StubAdService] バナー広告を表示 (本物SDK未導入・no-op)");
        }

        public void HideBanner()
        {
            Debug.Log("[StubAdService] バナー広告を非表示 (本物SDK未導入・no-op)");
        }

        public void ShowInterstitial(Action onClosed = null)
        {
            if (AdsRemoved)
            {
                // Swift: guard !isAdRemoved else { completion(true); return }
                onClosed?.Invoke();
                return;
            }

            Debug.Log("[StubAdService] インタースティシャル広告を表示 (本物SDK未導入・no-op、即クローズ扱い)");
            // stub は実際の広告を出さないため、表示なしで即座にリトライ続行させる
            // (onClosed は必ず呼ぶ契約 — silent failure にしない)。
            onClosed?.Invoke();
        }

        public void RequestTrackingAuthorization(Action onDone = null)
        {
            // ATT ダイアログ相当。stub は実際にはダイアログを出さず、許可されたものとして扱う。
            // ここで初めて consent を「許可後」の状態に切り替える (init=denied → ATT後切替、
            // feedback_consent_auth_antipatterns 厳守)。
            _trackingAuthorized = true;
            Debug.Log("[StubAdService] ATT許可リクエスト (本物SDK未導入・即許可扱い)");
            onDone?.Invoke();
        }
    }
}
