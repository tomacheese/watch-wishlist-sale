using System.Text.Json.Serialization;
using WatchWishlistSale.Models.Pricing;
using WatchWishlistSale.Models.Wishlist;

namespace WatchWishlistSale.Models.Notification;

/// <summary>
/// Sale information of one app to notify to Discord.
/// </summary>
/// <param name="App">App details.</param>
/// <param name="LowestPrice">Lowest price information (null if it could not be fetched).</param>
public record SaleNotification(
    [property: JsonPropertyName("app")] AppDetails App,
    [property: JsonPropertyName("lowestPrice")] LowestPriceResult? LowestPrice);
