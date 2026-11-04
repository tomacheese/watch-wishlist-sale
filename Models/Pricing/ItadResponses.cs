using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Pricing;

/// <summary>Response of games/lookup/v1.</summary>
public class ItadLookupResponse
{
    /// <summary>Whether ITAD has a game for the given Steam app ID.</summary>
    [JsonPropertyName("found")]
    public bool Found { get; set; }

    /// <summary>ITAD game information. Null when <see cref="Found"/> is false.</summary>
    [JsonPropertyName("game")]
    public ItadGame? Game { get; set; }
}

/// <summary>ITAD game identification.</summary>
public class ItadGame
{
    /// <summary>ITAD's internal game ID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

/// <summary>Response element of games/storelow/v2 (lowest prices of one game).</summary>
public class ItadStoreLowEntry
{
    /// <summary>ITAD's internal game ID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Lowest prices per shop.</summary>
    [JsonPropertyName("lows")]
    public IList<ItadLow> Lows { get; } = [];
}

/// <summary>Lowest price entry for one shop.</summary>
public class ItadLow
{
    /// <summary>Shop identification.</summary>
    [JsonPropertyName("shop")]
    public ItadShop Shop { get; set; } = new();

    /// <summary>Lowest price amount and currency.</summary>
    [JsonPropertyName("price")]
    public ItadPrice Price { get; set; } = new();
}

/// <summary>ITAD shop identification.</summary>
public class ItadShop
{
    /// <summary>ITAD's internal shop ID.</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

/// <summary>ITAD price information (amount and currency code).</summary>
public class ItadPrice
{
    /// <summary>Amount.</summary>
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    /// <summary>Currency code (for example "JPY", "USD").</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;
}
