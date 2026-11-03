using System.Text.Json.Serialization;

namespace WatchWishlistSale.Models.Wishlist;

/// <summary>
/// Whole response of the Steam Web API (IWishlistService/GetWishlist/v1).
/// </summary>
public class WishlistApiResponse
{
    /// <summary>Response body.</summary>
    [JsonPropertyName("response")]
    public WishlistResponseBody? Response { get; set; }
}

/// <summary>
/// Body of the wishlist API response.
/// </summary>
public class WishlistResponseBody
{
    /// <summary>Items on the wishlist.</summary>
    [JsonPropertyName("items")]
    public IList<WishlistItem> Items { get; } = [];
}

/// <summary>
/// One wishlist item.
/// </summary>
public class WishlistItem
{
    /// <summary>Steam app ID.</summary>
    [JsonPropertyName("appid")]
    public long AppId { get; set; }

    /// <summary>Priority within the wishlist.</summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    /// <summary>When the item was added to the wishlist (UNIX timestamp).</summary>
    [JsonPropertyName("date_added")]
    public long DateAdded { get; set; }
}
