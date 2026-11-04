using WatchWishlistSale.Models.Pricing;
using WatchWishlistSale.Models.Wishlist;

namespace WatchWishlistSale.Models.Notification;

/// <summary>
/// Sale information of one app to notify to Discord.
/// </summary>
/// <param name="app">App details.</param>
/// <param name="lowestPrice">Lowest price information (null if it could not be fetched).</param>
public record SaleNotification(AppDetails app, LowestPriceResult? lowestPrice);
