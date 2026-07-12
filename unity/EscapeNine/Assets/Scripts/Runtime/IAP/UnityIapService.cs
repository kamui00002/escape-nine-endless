// UnityIapService.cs
// IIapService の Unity IAP (com.unity.purchasing 5.4.1) 実装。
// Swift 正本: Services/StoreKitService.swift + PurchaseManager.swift の挙動を、
// Unity IAP v5 の Coded IAP API (StoreController / UnityIAPServices) で 1:1 移植する。
//
// 【重要】Unity IAP 5.4.1 は v4 系 (IStoreListener / ConfigurationBuilder / ProcessPurchase) とは
// 別の新 API (StoreController.Connect() → イベント購読) を採用している。旧 API の記憶で書くと
// 存在しないメンバーを呼んでコンパイルが通らないため、このファイルで使う型・メンバーは全て
// ~/EscapeNineUnity/Library/PackageCache/com.unity.purchasing@db17cc2e78b3/ の実ファイルで
// シグネチャを確認済み (対応表はクラス末尾のコメント参照)。
//
// 付与処理 (キャラ解放/広告削除) は StubIapService と同じく PlayerState.AddPurchasedProduct
// 経由のみで行う (新規フラグ禁止・SSOT厳守)。

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using EscapeNine.Core;
using EscapeNine.Runtime.Analytics;

namespace EscapeNine.Runtime.IAP
{
    public sealed class UnityIapService : IIapService
    {
        private readonly PlayerState _player;
        private readonly AnalyticsService _analytics;

        private StoreController _store;
        private bool _isConnected;

        /// <summary>
        /// Purchase(productId) 呼び出しごとの onComplete を productId 単位で追跡する。
        /// Unity IAP のイベントは productId をそのまま渡してこないため (Order.CartOrdered から
        /// 引く必要がある)、どの呼び出しに対する結果かを productId をキーに対応付ける。
        /// 同一 productId の購入が同時に 2 件走ることはない前提 (Purchase() 側で多重発行をガード)。
        /// </summary>
        private readonly Dictionary<string, Action<bool>> _pendingPurchaseCallbacks =
            new Dictionary<string, Action<bool>>();

        public UnityIapService(PlayerState player, AnalyticsService analytics = null)
        {
            _player = player;
            _analytics = analytics;
        }

        // MARK: - Initialize (Swift: StoreKitService.init = トランザクションリスナー開始 + loadPurchasedProducts)

        public void Initialize()
        {
            if (_store != null)
            {
                Debug.LogWarning("[UnityIapService] 二重初期化は無視 (既に初期化済み)");
                return;
            }

            _store = UnityIAPServices.StoreController();

            // 公式ガイド通り、Connect() より前に全イベントを購読する
            // (再接続時に pending purchase が即座に発火し得るため)。
            _store.OnPurchasePending += OnPurchasePending;
            _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            _store.OnPurchaseFailed += OnPurchaseFailed;
            _store.OnPurchaseDeferred += OnPurchaseDeferred;
            _store.OnStoreConnected += OnStoreConnected;
            _store.OnStoreDisconnected += OnStoreDisconnected;
            _store.OnProductsFetched += OnProductsFetched;
            _store.OnProductsFetchFailed += OnProductsFetchFailed;
            _store.OnPurchasesFetched += OnPurchasesFetched;
            _store.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
            // v5.4+: アカウント切替でキャッシュされた Product/Order が全て破棄される
            // (see: com.unity.purchasing AiAssistantSkills/references/api-notes.md
            //  "OnAuthAccountChanged — breaking behavior in v5.4")。カタログを再取得して追従する。
            _store.OnAuthAccountChanged += OnAuthAccountChanged;

            ConnectAsync();
        }

        private async void ConnectAsync()
        {
            try
            {
                await _store.Connect();
            }
            catch (Exception e)
            {
                // Connect() は Task を返す (throw され得る)。未接続のまま Purchase が来た場合は
                // Purchase 側の _isConnected ガードで onComplete(false) される (silent failure にはしない)。
                Debug.LogError($"[UnityIapService] ストア接続エラー: {e.Message}");
            }
        }

        private void OnStoreConnected()
        {
            _isConnected = true;
            Debug.Log("[UnityIapService] ストア接続完了");
            FetchCatalogAndPurchases();
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
        {
            _isConnected = false;
            Debug.LogError($"[UnityIapService] ストア切断: {failure.Message}");
        }

        private void OnAuthAccountChanged()
        {
            // Products/Purchases のキャッシュは Unity IAP 内部で既にクリア済みの状態でこのイベントが飛ぶ。
            // GetProducts()/GetPurchases() を読まず、素直に再取得する。
            Debug.Log("[UnityIapService] 認証アカウント変更を検知、カタログ・購入状態を再取得");
            FetchCatalogAndPurchases();
        }

        /// <summary>
        /// 商品カタログの取得 (価格表示用) と、既存購入の取得 (Swift: loadPurchasedProducts の
        /// Transaction.currentEntitlements ループ相当、起動時のエンタイトルメント再検証) をまとめて行う。
        /// </summary>
        private void FetchCatalogAndPurchases()
        {
            var defs = IapProductIds.All
                .Select(id => new ProductDefinition(id, ProductType.NonConsumable))
                .ToList();
            _store.FetchProducts(defs);

            // NonConsumable のエンタイトルメントは起動毎に再検証するのが公式推奨パターン
            // (再インストール・別端末購入・ローカル保存の消失に追従するため)。
            _store.FetchPurchases();
        }

        private void OnProductsFetched(List<Product> products)
        {
            Debug.Log($"[UnityIapService] 商品情報取得完了: {products.Count}件");
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            Debug.LogWarning($"[UnityIapService] 商品情報取得失敗: {failure.FailureReason}");
        }

        // MARK: - Purchase (Swift: PurchaseManager.purchaseCharacter/purchaseAdRemoval → StoreKitService.purchase)

        public void Purchase(string productId, Action<bool> onComplete)
        {
            if (!_isConnected || _store == null)
            {
                Debug.LogWarning($"[UnityIapService] ストア未接続のため購入失敗: {productId}");
                onComplete?.Invoke(false);
                return;
            }

            if (_pendingPurchaseCallbacks.ContainsKey(productId))
            {
                // 二重タップ等で同一商品の購入が既に進行中。既存の onComplete を上書きすると
                // 呼び出し漏れ (silent failure) になるため、ここで即座に false を返して終わらせる。
                Debug.LogWarning($"[UnityIapService] 既に購入処理中のため多重リクエストを拒否: {productId}");
                onComplete?.Invoke(false);
                return;
            }

            var product = _store.GetProductById(productId);
            if (product == null)
            {
                Debug.LogWarning($"[UnityIapService] 商品が見つからない (未取得または不正なID): {productId}");
                onComplete?.Invoke(false);
                return;
            }

            _pendingPurchaseCallbacks[productId] = onComplete;
            _store.PurchaseProduct(product);
        }

        /// <summary>
        /// Two-Step Purchase Flow の Step2。購入 (課金) は完了したがまだ確定していない状態。
        /// Swift の updatePurchasedProducts(transaction) + transaction.finish() に相当。
        /// 「保存してから確定する」の順序を守るため、PlayerState.AddPurchasedProduct (内部で Save()
        /// を同期実行) を ConfirmPurchase より先に呼ぶ。
        /// 復元 (RestoreTransactions) や起動時の FetchPurchases でも同じイベントが飛んでくるため、
        /// このメソッドは Purchase() からの呼び出しに限定されない (pending callback が無くても正しく動く)。
        /// </summary>
        private void OnPurchasePending(PendingOrder order)
        {
            var product = order.CartOrdered?.Items()?.FirstOrDefault()?.Product;
            var productId = product?.definition?.id;

            if (string.IsNullOrEmpty(productId))
            {
                // productId 不明のため付与・callback照合ができない既知の限界 (課金済み未付与という重大事態、再設計はしない)。
                Debug.LogError("[UnityIapService] PendingOrder から productId を解決できない");
                _store.ConfirmPurchase(order);
                return;
            }

            // 起動時の FetchPurchases 等で未確定注文が再配信されても LogPurchase が毎回再発火しないよう、
            // 付与前の所有状態 (IsPurchased、RemoveAds は AdRemoved との複合判定を内包) を捕捉しておく。
            bool wasOwned = IsPurchased(productId);
            _player?.AddPurchasedProduct(productId); // キャラ解放/広告削除は内部で反映 (SSOT)
            Debug.Log($"[UnityIapService] 購入付与完了 (確定前): {productId}");

            // 課金コンバージョン計測 (Swift: ConversionService.trackPurchase 相当)。
            // 価格は PlayerState.ProductRemoveAds のみ広告削除の固定額、それ以外はキャラ価格 (GameConfig)。
            // 非消耗型は本物の再購入が起きないため、新規付与時のみ送信し PostHog 指標の水増しを防ぐ。
            if (!wasOwned)
            {
                double value = productId == PlayerState.ProductRemoveAds
                    ? 480.0
                    : GameConfig.PremiumCharacterPrice;
                _analytics?.LogPurchase(productId, value, "JPY");
            }

            _store.ConfirmPurchase(order);

            if (_pendingPurchaseCallbacks.TryGetValue(productId, out var callback))
            {
                _pendingPurchaseCallbacks.Remove(productId);
                callback?.Invoke(true);
            }
        }

        /// <summary>
        /// Two-Step Purchase Flow の Step3 (確定結果)。ConfirmedOrder/FailedOrder のどちらかが来る。
        /// onComplete は既に OnPurchasePending で呼び終えているため (Swift の
        /// transaction.finish() 失敗を追跡しないのと同じ思想)、ここではログのみ行う。
        /// </summary>
        private void OnPurchaseConfirmed(Order order)
        {
            switch (order)
            {
                case ConfirmedOrder:
                    var confirmedId = order.CartOrdered?.Items()?.FirstOrDefault()?.Product?.definition?.id;
                    Debug.Log($"[UnityIapService] 購入確定完了: {confirmedId}");
                    break;
                case FailedOrder failed:
                    Debug.LogWarning($"[UnityIapService] 購入確定失敗: {failed.FailureReason} - {failed.Details}");
                    break;
            }
        }

        /// <summary>
        /// 購入そのものが失敗 (ユーザーキャンセル・決済拒否等)。Swift の
        /// purchaseState = .failed(error) / userCancelled → return false に相当。
        /// </summary>
        private void OnPurchaseFailed(FailedOrder failedOrder)
        {
            var product = failedOrder.CartOrdered?.Items()?.FirstOrDefault()?.Product;
            var productId = product?.definition?.id;
            Debug.LogWarning($"[UnityIapService] 購入失敗: {productId} ({failedOrder.FailureReason} - {failedOrder.Details})");

            if (!string.IsNullOrEmpty(productId) && _pendingPurchaseCallbacks.TryGetValue(productId, out var callback))
            {
                _pendingPurchaseCallbacks.Remove(productId);
                callback?.Invoke(false);
            }
        }

        /// <summary>
        /// 親の承認待ち等で購入が保留。Swift の case .pending: purchaseState = .pending; return false
        /// と同じく、今回の呼び出しとしては失敗 (未完了) 扱いで即座に onComplete(false) する
        /// (承認が下りて後日 OnPurchasePending が飛んできた時点で PlayerState への付与は別途行われる)。
        /// </summary>
        private void OnPurchaseDeferred(DeferredOrder order)
        {
            var product = order.CartOrdered?.Items()?.FirstOrDefault()?.Product;
            var productId = product?.definition?.id;
            Debug.Log($"[UnityIapService] 購入保留 (承認待ち): {productId}");

            if (!string.IsNullOrEmpty(productId) && _pendingPurchaseCallbacks.TryGetValue(productId, out var callback))
            {
                _pendingPurchaseCallbacks.Remove(productId);
                callback?.Invoke(false);
            }
        }

        // MARK: - Restore (Swift: PurchaseManager.restorePurchases → StoreKitService.restorePurchases)

        public void Restore(Action onComplete)
        {
            if (!_isConnected || _store == null)
            {
                Debug.LogWarning("[UnityIapService] ストア未接続のため復元をスキップ");
                onComplete?.Invoke();
                return;
            }

            _store.RestoreTransactions((success, error) =>
            {
                if (success)
                {
                    // 実際の付与は、復元された各購入について飛んでくる OnPurchasePending
                    // (このクラスで既に購読済み) 経由で行われる。ここでは復元リクエスト自体の
                    // 成否のみ扱う。
                    Debug.Log("[UnityIapService] 購入復元リクエスト完了");
                }
                else
                {
                    Debug.LogWarning($"[UnityIapService] 購入復元リクエスト失敗: {error}");
                }
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// 起動時の FetchPurchases (Swift: Transaction.currentEntitlements ループ) で確定済み購入が
        /// 見つかった場合の反映。Pending は自動で OnPurchasePending に回るため (デフォルト動作
        /// ProcessPendingOrdersOnPurchasesFetched(true))、ここでは ConfirmedOrders のみ扱う。
        /// </summary>
        private void OnPurchasesFetched(Orders orders)
        {
            foreach (var confirmed in orders.ConfirmedOrders)
            {
                var productId = confirmed.CartOrdered?.Items()?.FirstOrDefault()?.Product?.definition?.id;
                if (string.IsNullOrEmpty(productId)) continue;

                _player?.AddPurchasedProduct(productId); // 冪等 (PlayerState 側で重複ガード済み)
            }
        }

        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            Debug.LogWarning($"[UnityIapService] 購入情報取得失敗: {failure.Message}");
        }

        // MARK: - Query (正本データは PlayerState、ストアには問い合わせない)

        /// <summary>
        /// 広告削除は旧データ互換のため AdRemoved フラグと PurchasedProducts の両方を見る
        /// (StubIapService と同一ロジック、既存 ShopScreen.RefreshItems の判定と揃える)。
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

        public string LocalizedPrice(string productId)
        {
            var product = _store?.GetProductById(productId);
            var priceString = product?.metadata?.localizedPriceString;
            return string.IsNullOrEmpty(priceString) ? IapProductIds.FallbackPrice(productId) : priceString;
        }
    }
}

// 使用 API と実在確認箇所 (com.unity.purchasing@db17cc2e78b3, version 5.4.1):
//   UnityIAPServices.StoreController()          Runtime/Purchasing/Core/UnityIAPServices.cs:61-64
//   StoreController (Connect/FetchProducts/FetchPurchases/PurchaseProduct/ConfirmPurchase/
//                     RestoreTransactions/GetProductById/各 event)
//                                                Runtime/Purchasing/Core/StoreController.cs
//   ProductDefinition(string id, ProductType type)
//                                                Runtime/Purchasing/ProductDefinition.cs:80-82
//   Product.definition / Product.metadata       Runtime/Purchasing/Product.cs:102 / 108
//   ProductMetadata.localizedPriceString        Runtime/Purchasing/ProductMetadata.cs:50
//   ProductType.NonConsumable                    Runtime/Purchasing/ProductType.cs
//   Order / PendingOrder / ConfirmedOrder / FailedOrder / DeferredOrder / Orders
//                                                Runtime/Purchasing/Core/Purchasing/Models/*.cs
//   ICart.Items() / CartItem.Product             Runtime/Purchasing/Core/Purchasing/Interfaces/ICart.cs,
//                                                Runtime/Purchasing/Core/Purchasing/Models/CartItem.cs
//   PurchaseFailureReason                        Runtime/Purchasing/PurchaseFailureReason.cs
//   StoreConnectionFailureDescription.Message    Runtime/Purchasing/Core/Store/Models/StoreConnectionFailureDescription.cs:26
//   ProductFetchFailed.FailureReason (string)    Runtime/Purchasing/Core/Product/Models/ProductFetchFailed.cs:21
//   PurchasesFetchFailureDescription.Message     Runtime/Purchasing/Core/Purchasing/Models/PurchasesFetchFailureDescription.cs (Message プロパティ)
// 参照ガイド: PackageCache 同梱の AiAssistantSkills/in-app-purchases/references/api-notes.md
//   (Anti-Hallucination: v5 vs Legacy API 節、Initialization Example 節、Restore Transactions 節)
