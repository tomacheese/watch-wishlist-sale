namespace WatchWishlistSale.Common;

/// <summary>
/// Durable Functions の各種 Function 名を定義する定数クラス
/// </summary>
public static class FunctionNames
{
    /// <summary>タイマートリガーで起動するクローラー Function の名前</summary>
    public const string RunCrawler = "RunCrawler";

    /// <summary>ウィッシュリスト監視オーケストレーター Function の名前</summary>
    public const string CrawlerOrchestrator = "CrawlerOrchestrator";

    /// <summary>ウィッシュリストの App ID 一覧を取得する Activity Function の名前</summary>
    public const string GetWishlistAppIdsActivity = "GetWishlistAppIdsActivity";

    /// <summary>Steam アプリ詳細情報を取得する Activity Function の名前</summary>
    public const string GetAppDetailsActivity = "GetAppDetailsActivity";

    /// <summary>セール中のアプリを抽出する Activity Function の名前</summary>
    public const string FilterSaleAppsActivity = "FilterSaleAppsActivity";

    /// <summary>アプリの過去最安値を取得する Activity Function の名前</summary>
    public const string GetLowestPriceActivity = "GetLowestPriceActivity";

    /// <summary>Discord Webhook 経由でセール通知を送信する Activity Function の名前</summary>
    public const string SendDiscordNotificationActivity = "SendDiscordNotificationActivity";

    /// <summary>通知済みセール状態を管理する Durable Entity Function の名前</summary>
    public const string NotificationStateEntity = "NotificationStateEntity";
}
