// StubIapService.cs
// IIapService の no-op 実装。ネイティブ SDK (Unity IAP / com.unity.purchasing) 未導入の間、
// 呼び出し口の配線だけを確定させておくためのプレースホルダー (Debug.Log で「本物SDK未導入」を明示する)。
// 実 SDK 導入時は、この代わりに例えば UnityIapService : IIapService を新規実装して
// App.cs の生成箇所を差し替えるだけでよい。
//
// 購入状態の正本は PlayerState.PurchasedProducts / AdRemoved (StubAdService が AdRemoved を
// 参照のみするのと同じ思想)。ここで新規フラグを持つと二重管理になるため、
// 付与処理は必ず PlayerState.AddPurchasedProduct 経由で行う (新規フラグ禁止・SSOT厳守)。

using System;
using UnityEngine;

namespace EscapeNine.Runtime.IAP
{
    public sealed class StubIapService : IIapService
    {
        private readonly PlayerState _player;

        public StubIapService(PlayerState player)
        {
            _player = player;
        }

        public void Initialize()
        {
            Debug.Log("[StubIapService] 初期化 (本物SDK未導入・no-op)");
        }

        /// <summary>
        /// 購入スタブ。
        /// Release ビルドでは実ストアに接続していないため常に失敗させる (実ユーザーへの無料付与を
        /// 絶対に発生させない)。Editor / Development Build でのみ擬似成功させ、PlayerState の
        /// 既存 API (AddPurchasedProduct) 経由でキャラ解放/広告削除を反映することで、
        /// Swift 版の StoreKit Configuration File によるローカルテストと同等の開発体験を提供する。
        /// </summary>
        public void Purchase(string productId, Action<bool> onComplete)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _player?.AddPurchasedProduct(productId); // キャラ解放/広告削除は内部で反映 (SSOT)
            Debug.Log($"[StubIapService] (デバッグ) 擬似購入成功: {productId}");
            onComplete?.Invoke(true);
#else
            Debug.Log($"[StubIapService] 購入失敗 (本物ストア未接続・本物SDK未導入): {productId}");
            onComplete?.Invoke(false);
#endif
        }

        /// <summary>
        /// 復元スタブ。ローカル PlayerPrefs (PlayerState) に保存された購入状態がそのまま正であり、
        /// 復元すべき別個のトランザクション記録が存在しないため常に no-op (状態は変更しない)。
        /// </summary>
        public void Restore(Action onComplete)
        {
            Debug.Log("[StubIapService] 購入復元 (本物SDK未導入・no-op)");
            onComplete?.Invoke();
        }

        /// <summary>
        /// 広告削除は旧データ互換のため AdRemoved フラグと PurchasedProducts の両方を見る
        /// (Swift 正本には無い Unity 固有の互換ロジック。既存 ShopScreen.RefreshItems の判定と揃える)。
        /// </summary>
        public bool IsPurchased(string productId)
        {
            if (_player == null) return false;
            if (productId == PlayerState.ProductRemoveAds)
            {
                return _player.AdRemoved || _player.IsPurchased(productId);
            }
            return _player.IsPurchased(productId);
        }

        public string LocalizedPrice(string productId) => IapProductIds.FallbackPrice(productId);
    }
}
