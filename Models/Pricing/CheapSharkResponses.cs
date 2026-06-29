using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Pricing;

/// <summary>CheapShark games (steamAppID 検索) のレスポンス要素</summary>
internal class CheapSharkGameLookup
{
    /// <summary>CheapShark 内部のゲーム ID</summary>
    [JsonPropertyName("gameID")]
    public string GameId { get; set; } = string.Empty;
}

/// <summary>CheapShark games (id 指定) のレスポンス</summary>
internal class CheapSharkGameInfo
{
    /// <summary>過去最安値情報。取得できない場合は null</summary>
    [JsonPropertyName("cheapestPriceEver")]
    public CheapSharkCheapestPrice? CheapestPriceEver { get; set; }
}

/// <summary>CheapShark が返す過去最安値の詳細</summary>
internal class CheapSharkCheapestPrice
{
    /// <summary>過去最安値 (USD 建て文字列。例: "4.99")</summary>
    [JsonPropertyName("price")]
    public string Price { get; set; } = string.Empty;
}
