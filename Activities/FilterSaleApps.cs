using System.Diagnostics.CodeAnalysis;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WatchWishlistSale.Common;
using WatchWishlistSale.Models.Wishlist;

namespace WatchWishlistSale.Activities;

/// <summary>
/// Activity that extracts only the apps currently on sale (having price information and discounted) from the app details.
/// Whether the price changed since the last notification needs the NotificationStateEntity state,
/// so that check is done in the orchestrator instead.
/// </summary>
public class FilterSaleApps(ILogger<FilterSaleApps> logger)
{
    /// <summary>
    /// Activity entry point. Returns only the apps on sale from the app details.
    /// </summary>
    /// <param name="appDetails">List of app details.</param>
    /// <returns>Details of the apps currently discounted.</returns>
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
                // No price information: not yet released or no longer sold.
                return false;
            }

            return app.PriceOverview.DiscountPercent != 0;
        })];

        logger.LogInformation("Found {SaleAppsCount} sale apps", saleApps.Count);
        return saleApps;
    }
}
