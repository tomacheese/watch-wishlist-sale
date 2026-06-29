namespace WatchWishlistSale.Common;

/// <summary>
/// Durable Functions の各種 Function 名を定義する定数クラス
/// </summary>
public static class FunctionNames
{
    public const string RunCrawler = "RunCrawler";
    public const string CrawlerOrchestrator = "CrawlerOrchestrator";
    public const string GetWishlistAppIdsActivity = "GetWishlistAppIdsActivity";
    public const string GetAppDetailsActivity = "GetAppDetailsActivity";
    public const string FilterSaleAppsActivity = "FilterSaleAppsActivity";
    public const string GetLowestPriceActivity = "GetLowestPriceActivity";
    public const string SendDiscordNotificationActivity = "SendDiscordNotificationActivity";
    public const string NotificationStateEntity = "NotificationStateEntity";
}
