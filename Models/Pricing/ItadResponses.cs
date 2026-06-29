using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Pricing;

/// <summary>games/lookup/v1 のレスポンス</summary>
public class ItadLookupResponse
{
    /// <summary>指定した Steam アプリ ID に対応するゲームが ITAD に存在するかどうか</summary>
    [JsonPropertyName("found")]
    public bool Found { get; set; }

    /// <summary>対応するゲームの ITAD ゲーム情報。<see cref="Found"/> が false の場合は null</summary>
    [JsonPropertyName("game")]
    public ItadGame? Game { get; set; }
}

/// <summary>ITAD のゲーム識別情報</summary>
public class ItadGame
{
    /// <summary>ITAD 内部のゲーム ID</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

/// <summary>games/storelow/v2 のレスポンス要素 (ゲーム 1 件分の過去最安値一覧)</summary>
public class ItadStoreLowEntry
{
    /// <summary>ITAD 内部のゲーム ID</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>ショップごとの過去最安値一覧</summary>
    [JsonPropertyName("lows")]
    public IList<ItadLow> Lows { get; } = [];
}

/// <summary>特定ショップにおける過去最安値エントリ</summary>
public class ItadLow
{
    /// <summary>ショップの識別情報</summary>
    [JsonPropertyName("shop")]
    public ItadShop Shop { get; set; } = new();

    /// <summary>過去最安値の金額と通貨</summary>
    [JsonPropertyName("price")]
    public ItadPrice Price { get; set; } = new();
}

/// <summary>ITAD のショップ識別情報</summary>
public class ItadShop
{
    /// <summary>ITAD 内部のショップ ID</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

/// <summary>ITAD の価格情報 (金額と通貨コード)</summary>
public class ItadPrice
{
    /// <summary>金額</summary>
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    /// <summary>通貨コード (例: "JPY", "USD")</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;
}
