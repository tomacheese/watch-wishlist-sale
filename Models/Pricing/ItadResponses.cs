using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Pricing;

/// <summary>games/lookup/v1 のレスポンス</summary>
internal class ItadLookupResponse
{
    [JsonPropertyName("found")]
    public bool Found { get; set; }

    [JsonPropertyName("game")]
    public ItadGame? Game { get; set; }
}

internal class ItadGame
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

/// <summary>games/storelow/v2 のレスポンス要素</summary>
internal class ItadStoreLowEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("lows")]
    public List<ItadLow> Lows { get; set; } = [];
}

internal class ItadLow
{
    [JsonPropertyName("shop")]
    public ItadShop Shop { get; set; } = new();

    [JsonPropertyName("price")]
    public ItadPrice Price { get; set; } = new();
}

internal class ItadShop
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

internal class ItadPrice
{
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;
}
