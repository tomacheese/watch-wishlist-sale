using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Pricing;

/// <summary>
/// Result of a lowest price lookup.
/// </summary>
/// <param name="AppId">Steam app ID.</param>
/// <param name="Price">Historical lowest price.</param>
/// <param name="Currency">Currency code (for example "JPY", "USD").</param>
/// <param name="Source">Data source ("ITAD" or "CheapShark").</param>
public record LowestPriceResult(
    [property: JsonPropertyName("appId")] long AppId,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("source")] string Source);
