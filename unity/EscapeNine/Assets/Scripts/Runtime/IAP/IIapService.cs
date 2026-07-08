// IIapService.cs
// 課金サービスの継ぎ目 (seam)。Swift 正本: Services/StoreKitService.swift + PurchaseManager.swift の
// 公開 API をミラーする。本物の SDK (Unity IAP / com.unity.purchasing) を導入する時は、
// この interface の実装クラスを 1 つ追加するだけで済むようにするための抽象。
// 呼び出し側 (App / ShopScreen / CharacterScreen / SettingsScreen) は IIapService 越しにしか
// 課金を触らない。

using System;

namespace EscapeNine.Runtime.IAP
{
    public interface IIapService
    {
        /// <summary>
        /// 課金サービスの初期化。Swift: StoreKitService.init (トランザクションリスナー開始 +
        /// loadPurchasedProducts) 相当。
        /// </summary>
        void Initialize();

        /// <summary>
        /// 商品を購入する。Swift: PurchaseManager.purchaseCharacter / purchaseAdRemoval →
        /// StoreKitService.purchase(productID:) 相当。
        /// onComplete は成功/失敗いずれの場合も必ず呼ぶ契約 (Ads/IAdService と同じ思想、
        /// 呼び出し側はこれを UI 更新の合図として使うため実装側で握り潰してはならない)。
        /// </summary>
        void Purchase(string productId, Action<bool> onComplete);

        /// <summary>
        /// 購入を復元する。Swift: PurchaseManager.restorePurchases → StoreKitService.restorePurchases
        /// 相当。onComplete は必ず呼ぶ契約 (silent failure 禁止)。
        /// </summary>
        void Restore(Action onComplete);

        /// <summary>
        /// 商品が購入済みか (Swift: StoreKitService.isPurchased(_:))。
        /// 正本データは PlayerState.PurchasedProducts / AdRemoved — この API はそれを参照するだけの窓口。
        /// </summary>
        bool IsPurchased(string productId);

        /// <summary>
        /// 表示用の価格文字列 (Swift: StoreKitService.priceString(for:))。
        /// 本物 SDK 未導入の間はフォールバック表記 (例: "¥240") を返す。
        /// </summary>
        string LocalizedPrice(string productId);
    }
}
