// IapProductIds.cs
// 商品ID定数の窓口。実際のID文字列は PlayerState (Swift 正本: StoreKitService.ProductID の移植) が
// 正 (SSOT) — ここでは新しい文字列リテラルを作らず、PlayerState の const をそのまま re-export する
// (const from const は C# のコンパイル時定数として問題なくインライン化される)。
//
// 付与内容 (キャラ解放 / 広告削除) の実処理は PlayerState.AddPurchasedProduct の switch 文が正であり、
// ここでは二重管理を避けるため実処理ロジックは持たない。GrantDescription は UI/ドキュメント表示専用。

using EscapeNine.Core;
using EscapeNine.Runtime;

namespace EscapeNine.Runtime.IAP
{
    public static class IapProductIds
    {
        public const string Wizard = PlayerState.ProductWizard;
        public const string Elf = PlayerState.ProductElf;
        public const string Knight = PlayerState.ProductKnight;
        public const string RemoveAds = PlayerState.ProductRemoveAds;

        /// <summary>Unity IAP 導入時にカタログへ登録する4商品の一覧 (Swift: ProductID.allCases 相当)。</summary>
        public static readonly string[] All = { Wizard, Elf, Knight, RemoveAds };

        /// <summary>
        /// productId → 付与内容の対応 (表示・ドキュメント用のみ)。実際の付与処理は
        /// PlayerState.AddPurchasedProduct の switch 文が正であり、ここは説明目的でしかない
        /// (ロジックの二重管理を避けるため、この文字列は判定に使用しないこと)。
        /// </summary>
        public static string GrantDescription(string productId)
        {
            switch (productId)
            {
                case Wizard: return "魔法使いキャラクター解放";
                case Elf: return "エルフキャラクター解放";
                case Knight: return "ナイトキャラクター解放";
                case RemoveAds: return "広告削除";
                default: return "不明な商品";
            }
        }

        /// <summary>
        /// 本物の IAP SDK 導入前のフォールバック価格表示 (Swift: StoreKitService.priceString(for:) の
        /// プロダクト未取得時フォールバック、および既存 ShopScreen.AdRemovalPriceText と同一の値)。
        /// キャラ価格は Core.GameConfig.PremiumCharacterPrice を正とする (値の複製禁止)。
        /// </summary>
        public static string FallbackPrice(string productId)
        {
            return productId == RemoveAds
                ? "¥480"
                : "¥" + GameConfig.PremiumCharacterPrice;
        }
    }
}
