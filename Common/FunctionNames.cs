namespace WatchWishlistSale.Common;

/// <summary>
/// Constants for the names of the Durable Functions functions.
/// </summary>
public static class FunctionNames
{
    /// <summary>Name of the timer-triggered crawler function.</summary>
    public const string RunCrawler = "RunCrawler";

    /// <summary>Name of the wishlist watching orchestrator function.</summary>
    public const string CrawlerOrchestrator = "CrawlerOrchestrator";

    /// <summary>Name of the activity that gets the app IDs on the wishlist.</summary>
    public const string GetWishlistAppIdsActivity = "GetWishlistAppIdsActivity";

    /// <summary>Name of the activity that gets Steam app details.</summary>
    public const string GetAppDetailsActivity = "GetAppDetailsActivity";

    /// <summary>Name of the activity that extracts apps on sale.</summary>
    public const string FilterSaleAppsActivity = "FilterSaleAppsActivity";

    /// <summary>Name of the activity that gets the historical lowest price of an app.</summary>
    public const string GetLowestPriceActivity = "GetLowestPriceActivity";

    /// <summary>Name of the activity that sends sale notifications through the Discord webhook.</summary>
    public const string SendDiscordNotificationActivity = "SendDiscordNotificationActivity";

    /// <summary>Name of the Durable Entity that manages the notified sale state.</summary>
    public const string NotificationStateEntity = "NotificationStateEntity";
}
