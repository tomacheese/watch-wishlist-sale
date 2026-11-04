namespace WatchWishlistSale.Models.Pricing;

/// <summary>
/// Result of a lowest price lookup.
/// </summary>
/// <param name="appId">Steam app ID.</param>
/// <param name="price">Historical lowest price.</param>
/// <param name="currency">Currency code (for example "JPY", "USD").</param>
/// <param name="source">Data source ("ITAD" or "CheapShark").</param>
public record LowestPriceResult(long appId, decimal price, string currency, string source);
