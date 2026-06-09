using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Wishlist;

/// <summary>
/// Steam Web API (IWishlistService/GetWishlist/v1) のレスポンス全体
/// </summary>
public class WishlistApiResponse
{
    /// <summary>レスポンス本体</summary>
    [JsonPropertyName("response")]
    public WishlistResponseBody? Response { get; set; }
}

/// <summary>
/// ウィッシュリスト API のレスポンス本体
/// </summary>
public class WishlistResponseBody
{
    /// <summary>ウィッシュリストに登録されているアイテム一覧</summary>
    [JsonPropertyName("items")]
    public List<WishlistItem> Items { get; set; } = [];
}

/// <summary>
/// ウィッシュリストの 1 アイテム
/// </summary>
public class WishlistItem
{
    /// <summary>Steam アプリ ID</summary>
    [JsonPropertyName("appid")]
    public long AppId { get; set; }

    /// <summary>ウィッシュリスト内の優先度</summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    /// <summary>ウィッシュリストに追加された日時 (UNIX タイムスタンプ)</summary>
    [JsonPropertyName("date_added")]
    public long DateAdded { get; set; }
}
