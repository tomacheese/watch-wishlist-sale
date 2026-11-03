using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Pricing;

/// <summary>Response element of CheapShark games (search by steamAppID).</summary>
public class CheapSharkGameLookup
{
    /// <summary>CheapShark's internal game ID.</summary>
    [JsonPropertyName("gameID")]
    public string GameId { get; set; } = string.Empty;
}

/// <summary>Response of CheapShark games (by id).</summary>
public class CheapSharkGameInfo
{
    /// <summary>Lowest price information. Null if unavailable.</summary>
    [JsonPropertyName("cheapestPriceEver")]
    public CheapSharkCheapestPrice? CheapestPriceEver { get; set; }
}

/// <summary>Lowest price details returned by CheapShark.</summary>
public class CheapSharkCheapestPrice
{
    /// <summary>Lowest price as a USD string (for example "4.99").</summary>
    [JsonPropertyName("price")]
    public string Price { get; set; } = string.Empty;
}
