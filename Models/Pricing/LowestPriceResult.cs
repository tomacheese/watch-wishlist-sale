namespace WatchWishlistSale.Models.Pricing;

/// <summary>
/// 最安値の取得結果
/// </summary>
/// <param name="appId">Steam アプリ ID</param>
/// <param name="price">過去最安値</param>
/// <param name="currency">通貨コード (例: "JPY", "USD")</param>
/// <param name="source">取得元 ("ITAD" または "CheapShark")</param>
public record LowestPriceResult(long appId, decimal price, string currency, string source);
