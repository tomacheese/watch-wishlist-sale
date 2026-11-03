using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;
using WatchWishlistSale.Common;
using WatchWishlistSale.Entities;
using WatchWishlistSale.Models.Notification;
using WatchWishlistSale.Models.Pricing;
using WatchWishlistSale.Models.Wishlist;

namespace WatchWishlistSale.Orchestrations;

/// <summary>
/// Orchestrator that watches the wishlist for sales and notifies Discord of price drops not yet notified.
/// </summary>
public static class WatchWishlistOrchestrator
{
    /// <summary>
    /// Retry policy for activities that call external APIs.
    /// Transient errors (network issues, rate limits) are retried with exponential backoff, up to 3 attempts in total.
    /// </summary>
    private static readonly TaskOptions ActivityRetryOptions = TaskOptions.FromRetryPolicy(new RetryPolicy(
        maxNumberOfAttempts: 3,
        firstRetryInterval: TimeSpan.FromSeconds(5),
        backoffCoefficient: 2.0));

    /// <summary>
    /// Maximum number of activities run concurrently in one fan-out.
    /// To respect external API rate limits, items are processed in chunks of this size
    /// with a wait between chunks instead of fanning out all at once
    /// (the wait is defined per callee by <see cref="AppDetailsFanOutInterval"/> and
    /// <see cref="LowestPriceFanOutInterval"/>).
    /// </summary>
    private const int MaxFanOutConcurrency = 3;

    /// <summary>
    /// Wait between fan-out chunks of GetAppDetailsActivity.
    /// Spreads the requests out so the average rate stays under the Steam store appdetails API rate limit.
    /// </summary>
    private static readonly TimeSpan AppDetailsFanOutInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Wait between fan-out chunks of GetLowestPriceActivity.
    /// Spreads the requests out to stay under the IsThereAnyDeal rate limit,
    /// leaving headroom because one activity run can issue more than one HTTP request.
    /// </summary>
    private static readonly TimeSpan LowestPriceFanOutInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Orchestrator entry point. Runs the whole flow: fetch the wishlist, collect app details, filter sale apps,
    /// fetch lowest prices, send the Discord notification and update the notification state.
    /// </summary>
    /// <param name="context">Orchestration execution context.</param>
    /// <returns>A task that completes when the orchestration finishes.</returns>
    [Function(FunctionNames.CrawlerOrchestrator)]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ILogger logger = context.CreateReplaySafeLogger(nameof(WatchWishlistOrchestrator));

        var profileId = context.GetInput<string>()
            ?? throw new InvalidOperationException("Steam profile id is required as orchestrator input");
        logger.LogInformation("Starting crawler orchestrator.");

        List<long> appIds = await context.CallActivityAsync<List<long>>(FunctionNames.GetWishlistAppIdsActivity, profileId, ActivityRetryOptions);
        logger.LogInformation("Got {AppIdsCount} app ids.", appIds.Count);

        // 2. Fetch the details of each app ID in parallel (fan-out).
        // Fanning out everything at once could hit the external API rate limit, so fan out/in in chunks of
        // MaxFanOutConcurrency and wait AppDetailsFanOutInterval between chunks to keep the effective rate below the limit.
        List<AppDetails?> appDetailsResults = [];
        long[][] appIdChunks = [.. appIds.Chunk(MaxFanOutConcurrency)];
        for (var chunkIndex = 0; chunkIndex < appIdChunks.Length; chunkIndex++)
        {
            IEnumerable<Task<AppDetails?>> appDetailsTasks = appIdChunks[chunkIndex].Select(appId =>
                context.CallActivityAsync<AppDetails?>(FunctionNames.GetAppDetailsActivity, appId, ActivityRetryOptions));
            appDetailsResults.AddRange(await Task.WhenAll(appDetailsTasks));

            var isLastChunk = chunkIndex == appIdChunks.Length - 1;
            if (!isLastChunk)
            {
                await context.CreateTimer(context.CurrentUtcDateTime.Add(AppDetailsFanOutInterval), CancellationToken.None);
            }
        }

        List<AppDetails> appDetailsList = [.. appDetailsResults.OfType<AppDetails>()];
        logger.LogInformation(
            "Got {AppDetailsCount} app details ({FailedCount} failed)",
            appDetailsList.Count,
            appDetailsResults.Count - appDetailsList.Count);

        List<AppDetails> saleApps = await context.CallActivityAsync<List<AppDetails>>(FunctionNames.FilterSaleAppsActivity, appDetailsList);
        logger.LogInformation("Got {SaleAppsCount} sale apps", saleApps.Count);

        // 4. Get the notification history from the notification state entity.
        EntityInstanceId entityId = new(FunctionNames.NotificationStateEntity, profileId);
        NotificationSnapshot snapshot = await context.Entities.CallEntityAsync<NotificationSnapshot>(entityId, NotificationStateEntity.OperationGetSnapshot);

        // 5. Keep only apps whose price changed since the last notification, or that newly went on sale.
        List<AppDetails> targetApps = [.. saleApps.Where(app =>
        {
            var currentPrice = app.PriceOverview!.Final / 100m;
            return !snapshot.notifiedPrices.TryGetValue(app.SteamAppId, out var notifiedPrice) || notifiedPrice != currentPrice;
        })];
        logger.LogInformation("Got {TargetAppsCount} apps to notify", targetApps.Count);

        // 6. Fetch the lowest prices of the apps to notify in parallel (fan-out).
        // Same as above: chunked, waiting LowestPriceFanOutInterval between chunks.
        List<LowestPriceResult?> lowestPrices = [];
        AppDetails[][] targetAppChunks = [.. targetApps.Chunk(MaxFanOutConcurrency)];
        for (var chunkIndex = 0; chunkIndex < targetAppChunks.Length; chunkIndex++)
        {
            IEnumerable<Task<LowestPriceResult?>> lowestPriceTasks = targetAppChunks[chunkIndex].Select(app =>
                context.CallActivityAsync<LowestPriceResult?>(FunctionNames.GetLowestPriceActivity, app.SteamAppId, ActivityRetryOptions));
            lowestPrices.AddRange(await Task.WhenAll(lowestPriceTasks));

            var isLastChunk = chunkIndex == targetAppChunks.Length - 1;
            if (!isLastChunk)
            {
                await context.CreateTimer(context.CurrentUtcDateTime.Add(LowestPriceFanOutInterval), CancellationToken.None);
            }
        }

        List<SaleNotification> notifications = [.. targetApps.Select((app, index) => new SaleNotification(app, lowestPrices[index]))];

        // 7. On the first run the whole wishlist would be a notification target, so only record the state and skip Discord.
        if (snapshot.isFirstRun)
        {
            logger.LogInformation("First run detected. Recording state without sending Discord notification for {Count} apps", notifications.Count);
        }
        else
        {
            await context.CallActivityAsync(FunctionNames.SendDiscordNotificationActivity, notifications, ActivityRetryOptions);
        }

        foreach (AppDetails app in targetApps)
        {
            var currentPrice = app.PriceOverview!.Final / 100m;
            await context.Entities.CallEntityAsync(entityId, NotificationStateEntity.OperationSetNotified, new NotifiedEntry(app.SteamAppId, currentPrice));
        }

        // 9. Remove notification records of apps whose sale has ended.
        foreach (var notifiedAppId in snapshot.notifiedPrices.Keys)
        {
            if (saleApps.Any(app => app.SteamAppId == notifiedAppId))
            {
                continue;
            }

            logger.LogInformation("❌ Removing notification record for app id: {AppId}", notifiedAppId);
            await context.Entities.CallEntityAsync(entityId, NotificationStateEntity.OperationRemoveNotified, notifiedAppId);
        }

        logger.LogInformation("✅ Done");
    }
}
