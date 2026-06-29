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
    [Function(FunctionNames.FilterSaleAppsActivity)]
    public List<AppDetails> FilterSaleAppsActivity([ActivityTrigger] List<AppDetails> appDetails)
    {
        logger.LogInformation("Filtering sale apps from {appDetailsCount} app details", appDetails.Count);

        // 販売中 & 割引中のアプリ
        List<AppDetails> saleApps = [.. appDetails.Where(app =>
        {
            if (app.PriceOverview is null)
            {
                // 価格情報がない => 未発売 or 販売終了
                return false;
            }

            // 割引率が 0 => 割引なし
            return app.PriceOverview.DiscountPercent != 0;
        })];

        logger.LogInformation("Found {saleAppsCount} sale apps", saleApps.Count);
        return saleApps;
    }
}
