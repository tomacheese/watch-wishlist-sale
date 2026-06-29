using WatchWishlistSale.Models.Pricing;
using WatchWishlistSale.Models.Wishlist;

namespace WatchWishlistSale.Models.Notification;

/// <summary>
/// Discord に通知する 1 アプリ分のセール情報
/// </summary>
/// <param name="app">アプリ詳細情報</param>
/// <param name="lowestPrice">過去最安値情報 (取得できなかった場合は null)</param>
public record SaleNotification(AppDetails app, LowestPriceResult? lowestPrice);
