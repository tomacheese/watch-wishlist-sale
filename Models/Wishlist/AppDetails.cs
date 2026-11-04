using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Wishlist;

/// <summary>
/// Sale price information returned by the Steam store API (appdetails).
/// </summary>
public class PriceOverview
{
    /// <summary>Currency code (for example "JPY").</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    /// <summary>List price, in 100 times the smallest currency unit (for example 100000 for 1,000 yen).</summary>
    [JsonPropertyName("initial")]
    public int Initial { get; set; }

    /// <summary>Discounted price, in 100 times the smallest currency unit.</summary>
    [JsonPropertyName("final")]
    public int Final { get; set; }

    /// <summary>Discount percentage. 0 when there is no discount.</summary>
    [JsonPropertyName("discount_percent")]
    public int DiscountPercent { get; set; }
}

/// <summary>
/// App details from the Steam store API (appdetails), limited to the fields this app uses.
/// </summary>
public class AppDetails
{
    /// <summary>App type (game, dlc, music, and so on).</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>App name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Steam app ID.</summary>
    [JsonPropertyName("steam_appid")]
    public long SteamAppId { get; set; }

    /// <summary>Sale price information. Null when there is no price, such as unreleased or no longer sold.</summary>
    [JsonPropertyName("price_overview")]
    public PriceOverview? PriceOverview { get; set; }
}

/// <summary>
/// Result for one app in the Steam appdetails API response.
/// </summary>
public class AppDetailsResult
{
    /// <summary>Whether the fetch succeeded.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>App details (null when success is false).</summary>
    [JsonPropertyName("data")]
    public AppDetails? Data { get; set; }
}
