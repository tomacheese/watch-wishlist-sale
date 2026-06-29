using System.Diagnostics.CodeAnalysis;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WatchWishlistSale.Common;
using WatchWishlistSale.Models.Wishlist;

namespace WatchWishlistSale.Activities;

/// <summary>
/// アプリ詳細一覧から、現在販売中 (価格情報があり、かつ割引中) のアプリのみを抽出する Activity。
/// 「前回通知時から価格が変わっていないか」の判定は NotificationStateEntity の状態が必要となるため、
/// ここでは行わずオーケストレーター側で行う。
/// </summary>
public class FilterSaleApps(ILogger<FilterSaleApps> logger)
{
    /// <summary>
    /// アクティビティのエントリーポイント。アプリ詳細一覧からセール中のアプリだけを抽出して返す。
    /// </summary>
    /// <param name="appDetails">アプリ詳細情報の一覧</param>
    /// <returns>現在割引中のアプリ詳細情報の一覧</returns>
    [Function(FunctionNames.FilterSaleAppsActivity)]
    [SuppressMessage("Design", "CA1002", Justification = "Durable Functions Activity の入出力は List<T> を直接 JSON シリアライズするため変更不可。")]
    public List<AppDetails> FilterSaleAppsActivity([ActivityTrigger] List<AppDetails> appDetails)
    {
        ArgumentNullException.ThrowIfNull(appDetails);
        logger.LogInformation("Filtering sale apps from {AppDetailsCount} app details", appDetails.Count);

        List<AppDetails> saleApps = [.. appDetails.Where(app =>
        {
            if (app.PriceOverview is null)
            {
                // 価格情報がない => 未発売 or 販売終了
                return false;
            }

            return app.PriceOverview.DiscountPercent != 0;
        })];

        logger.LogInformation("Found {SaleAppsCount} sale apps", saleApps.Count);
        return saleApps;
    }
}
