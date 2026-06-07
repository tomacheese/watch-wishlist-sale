using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Wishlist;

/// <summary>
/// Steam ストア API (appdetails) が返すセール価格情報
/// </summary>
public class PriceOverview
{
    /// <summary>通貨コード (例: "JPY")</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    /// <summary>定価 (通貨の最小単位の 100 倍。例: 1,000 円なら 100000)</summary>
    [JsonPropertyName("initial")]
    public int Initial { get; set; }

    /// <summary>割引後価格 (通貨の最小単位の 100 倍)</summary>
    [JsonPropertyName("final")]
    public int Final { get; set; }

    /// <summary>割引率 (%)。割引が無い場合は 0</summary>
    [JsonPropertyName("discount_percent")]
    public int DiscountPercent { get; set; }
}

/// <summary>
/// Steam ストア API (appdetails) から取得するアプリ詳細情報のうち、本機能で利用するフィールドのみを表すモデル
/// </summary>
public class AppDetails
{
    /// <summary>アプリの種別 (game, dlc, music など)</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>アプリ名</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Steam アプリ ID</summary>
    [JsonPropertyName("steam_appid")]
    public long SteamAppId { get; set; }

    /// <summary>セール価格情報。未発売・販売終了などで価格が無い場合は null</summary>
    [JsonPropertyName("price_overview")]
    public PriceOverview? PriceOverview { get; set; }
}

/// <summary>
/// Steam appdetails API のレスポンスにおける、1 アプリ分の結果
/// </summary>
public class AppDetailsResult
{
    /// <summary>取得に成功したかどうか</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>アプリ詳細情報 (success が false の場合は null)</summary>
    [JsonPropertyName("data")]
    public AppDetails? Data { get; set; }
}
