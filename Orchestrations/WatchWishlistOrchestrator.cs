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
/// ウィッシュリストのセール状況を監視し、未通知の値下げを Discord に通知するオーケストレーター
/// </summary>
public static class WatchWishlistOrchestrator
{
    /// <summary>
    /// 外部 API 呼び出しを伴うアクティビティの再試行ポリシー。
    /// 一時的なエラー (ネットワーク不調やレート制限など) に対し、間隔を空けながら最大 3 回まで試行する。
    /// </summary>
    private static readonly TaskOptions ActivityRetryOptions = TaskOptions.FromRetryPolicy(new RetryPolicy(
        maxNumberOfAttempts: 3,
        firstRetryInterval: TimeSpan.FromSeconds(5),
        backoffCoefficient: 2.0));

    /// <summary>
    /// Fan-out 時に同時実行するアクティビティの最大数。
    /// 外部 API のレート制限を考慮し、件数が多い場合でも全件を一度に Fan-out せず、
    /// この件数ずつチャンクに分け、チャンクの間に待機を挟みながら順番に処理する
    /// (待機時間は呼び出し先ごとに <see cref="AppDetailsFanOutInterval"/> /
    /// <see cref="LowestPriceFanOutInterval"/> で個別に定義している)。
    /// </summary>
    private const int MaxFanOutConcurrency = 3;

    /// <summary>
    /// GetAppDetailsActivity の Fan-out チャンク間で空ける待機時間。
    /// 呼び出し先の Steam ストア appdetails API は実測で約 200 リクエスト / 5 分
    /// (= 1 リクエストあたり平均約 1.5 秒) という厳しいレート制限がかかっているため、
    /// MaxFanOutConcurrency (3) 件のチャンクを約 5 秒間隔で送出することで、
    /// 平均レートを制限値未満 (約 0.6 req/sec) に抑える。
    /// </summary>
    private static readonly TimeSpan AppDetailsFanOutInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// GetLowestPriceActivity の Fan-out チャンク間で空ける待機時間。
    /// 呼び出し先の IsThereAnyDeal API のレート制限は約 1000 リクエスト / 5 分
    /// (= 約 3.3 req/sec) だが、このアクティビティ 1 回につき最大 2 回の HTTP リクエストが
    /// 発生し得るため、MaxFanOutConcurrency (3) 件のチャンク (= 最大 6 リクエスト) を
    /// 約 2 秒間隔で送出し、余裕を持たせている。
    /// </summary>
    private static readonly TimeSpan LowestPriceFanOutInterval = TimeSpan.FromSeconds(2);

    [Function(FunctionNames.CrawlerOrchestrator)]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(WatchWishlistOrchestrator));

        string profileId = context.GetInput<string>()
            ?? throw new InvalidOperationException("Steam profile id is required as orchestrator input");
        logger.LogInformation("Starting crawler orchestrator.");

        // 1. ウィッシュリストの App ID 一覧を取得
        List<long> appIds = await context.CallActivityAsync<List<long>>(FunctionNames.GetWishlistAppIdsActivity, profileId, ActivityRetryOptions);
        logger.LogInformation("Got {appIdsCount} app ids.", appIds.Count);

        // 2. 各 App ID の詳細情報を並列取得 (Fan-out)
        // 全件を一度に Fan-out すると外部 API のレート制限に抵触しかねないため、
        // MaxFanOutConcurrency 件ずつチャンクに分け、チャンクの間に AppDetailsFanOutInterval だけ
        // 待機を挟みながら Fan-out/Fan-in することで、実効的なリクエストレートを制限値未満に抑える
        List<AppDetails?> appDetailsResults = [];
        long[][] appIdChunks = [.. appIds.Chunk(MaxFanOutConcurrency)];
        for (int chunkIndex = 0; chunkIndex < appIdChunks.Length; chunkIndex++)
        {
            IEnumerable<Task<AppDetails?>> appDetailsTasks = appIdChunks[chunkIndex].Select(appId =>
                context.CallActivityAsync<AppDetails?>(FunctionNames.GetAppDetailsActivity, appId, ActivityRetryOptions));
            appDetailsResults.AddRange(await Task.WhenAll(appDetailsTasks));

            bool isLastChunk = chunkIndex == appIdChunks.Length - 1;
            if (!isLastChunk)
            {
                await context.CreateTimer(context.CurrentUtcDateTime.Add(AppDetailsFanOutInterval), CancellationToken.None);
            }
        }
        List<AppDetails> appDetailsList = appDetailsResults.OfType<AppDetails>().ToList();
        logger.LogInformation(
            "Got {appDetailsCount} app details ({failedCount} failed)",
            appDetailsList.Count,
            appDetailsResults.Count - appDetailsList.Count);

        // 3. 販売中 & 割引中のアプリを抽出
        List<AppDetails> saleApps = await context.CallActivityAsync<List<AppDetails>>(FunctionNames.FilterSaleAppsActivity, appDetailsList);
        logger.LogInformation("Got {saleAppsCount} sale apps", saleApps.Count);

        // 4. 通知状態エンティティ (旧実装の notified.json 相当) から、前回までの通知状況を取得
        EntityInstanceId entityId = new(FunctionNames.NotificationStateEntity, profileId);
        NotificationSnapshot snapshot = await context.Entities.CallEntityAsync<NotificationSnapshot>(entityId, NotificationStateEntity.OperationGetSnapshot);

        // 5. 「前回通知時から価格が変わった」または「新たにセール対象になった」アプリのみを抽出
        List<AppDetails> targetApps = saleApps.Where(app =>
        {
            decimal currentPrice = app.PriceOverview!.Final / 100m;
            return !snapshot.NotifiedPrices.TryGetValue(app.SteamAppId, out decimal notifiedPrice) || notifiedPrice != currentPrice;
        }).ToList();
        logger.LogInformation("Got {targetAppsCount} apps to notify", targetApps.Count);

        // 6. 通知対象アプリの過去最安値を並列取得 (Fan-out)
        // こちらも同様に、チャンクに分けて LowestPriceFanOutInterval だけ待機を挟みながら Fan-out/Fan-in する
        List<LowestPriceResult?> lowestPrices = [];
        AppDetails[][] targetAppChunks = [.. targetApps.Chunk(MaxFanOutConcurrency)];
        for (int chunkIndex = 0; chunkIndex < targetAppChunks.Length; chunkIndex++)
        {
            IEnumerable<Task<LowestPriceResult?>> lowestPriceTasks = targetAppChunks[chunkIndex].Select(app =>
                context.CallActivityAsync<LowestPriceResult?>(FunctionNames.GetLowestPriceActivity, app.SteamAppId, ActivityRetryOptions));
            lowestPrices.AddRange(await Task.WhenAll(lowestPriceTasks));

            bool isLastChunk = chunkIndex == targetAppChunks.Length - 1;
            if (!isLastChunk)
            {
                await context.CreateTimer(context.CurrentUtcDateTime.Add(LowestPriceFanOutInterval), CancellationToken.None);
            }
        }

        List<SaleNotification> notifications = targetApps
            .Select((app, index) => new SaleNotification(app, lowestPrices[index]))
            .ToList();

        // 7. 初回実行時はウィッシュリスト全体が通知対象になってしまうため、Discord への送信は行わず状態の記録のみ行う
        if (snapshot.IsFirstRun)
        {
            logger.LogInformation("First run detected. Recording state without sending Discord notification for {count} apps", notifications.Count);
        }
        else
        {
            await context.CallActivityAsync(FunctionNames.SendDiscordNotificationActivity, notifications, ActivityRetryOptions);
        }

        // 8. 通知状態を更新 (新規・価格変更分を記録)
        foreach (AppDetails app in targetApps)
        {
            decimal currentPrice = app.PriceOverview!.Final / 100m;
            await context.Entities.CallEntityAsync(entityId, NotificationStateEntity.OperationSetNotified, new NotifiedEntry(app.SteamAppId, currentPrice));
        }

        // 9. セールが終了し対象から外れたアプリの通知記録を削除
        foreach (long notifiedAppId in snapshot.NotifiedPrices.Keys)
        {
            if (saleApps.Any(app => app.SteamAppId == notifiedAppId))
            {
                continue;
            }
            logger.LogInformation("❌ Removing notification record for app id: {appId}", notifiedAppId);
            await context.Entities.CallEntityAsync(entityId, NotificationStateEntity.OperationRemoveNotified, notifiedAppId);
        }

        logger.LogInformation("✅ Done");
    }
}
