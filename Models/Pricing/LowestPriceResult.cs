namespace WatchWishlistSale.Models.Pricing;

/// <summary>
/// 最安値の取得結果
/// </summary>
/// <param name="AppId">Steam アプリ ID</param>
/// <param name="Price">過去最安値</param>
/// <param name="Currency">通貨コード (例: "JPY", "USD")</param>
/// <param name="Source">取得元 ("ITAD" または "CheapShark")</param>
public record LowestPriceResult(long AppId, decimal Price, string Currency, string Source);
