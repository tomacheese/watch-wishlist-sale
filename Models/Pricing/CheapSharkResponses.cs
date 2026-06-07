using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Pricing;

/// <summary>CheapShark games (steamAppID 検索) のレスポンス要素</summary>
internal class CheapSharkGameLookup
{
    [JsonPropertyName("gameID")]
    public string GameId { get; set; } = string.Empty;
}

/// <summary>CheapShark games (id 指定) のレスポンス</summary>
internal class CheapSharkGameInfo
{
    [JsonPropertyName("cheapestPriceEver")]
    public CheapSharkCheapestPrice? CheapestPriceEver { get; set; }
}

internal class CheapSharkCheapestPrice
{
    [JsonPropertyName("price")]
    public string Price { get; set; } = string.Empty;
}
