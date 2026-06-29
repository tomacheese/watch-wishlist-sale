using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WatchWishlistSale.Common;

namespace WatchWishlistSale.Triggers;

/// <summary>
/// 毎時実行されるタイマートリガーから WatchWishlistOrchestrator を起動する Function
/// </summary>
public class Crawler(IConfiguration configuration, ILogger<Crawler> logger)
{
    /// <summary>
    /// タイマートリガーのエントリーポイント。毎時 0 分に起動し、WatchWishlistOrchestrator を開始する。
    /// 同一プロフィールに対するオーケストレーターの多重起動を防ぐためシングルトンパターンを使用する。
    /// </summary>
    /// <param name="myTimer">タイマートリガーの情報 (スケジュール状態を含む)</param>
    /// <param name="client">Durable Functions のクライアント。オーケストレーター起動に使用する</param>
    /// <returns>処理完了を表す非同期タスク</returns>
    [Function(FunctionNames.RunCrawler)]
    public async Task RunCrawler(
        [TimerTrigger("0 0 * * * *")] TimerInfo myTimer,
        [DurableClient] DurableTaskClient client)
    {
        logger.LogInformation("Timer trigger function executed at: {executionTime}", DateTime.Now);
        if (myTimer.ScheduleStatus is not null)
        {
            logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }

        var profileId = configuration["STEAM_PROFILE_ID"]
            ?? throw new InvalidOperationException("STEAM_PROFILE_ID is not configured");

        // プロフィール ID ごとにインスタンス ID を固定し、同一プロフィールに対するオーケストレーターの多重起動を防ぐ (シングルトンパターン)
        var instanceId = $"{FunctionNames.CrawlerOrchestrator}-{profileId}";
        OrchestrationMetadata? existingInstance = await client.GetInstanceAsync(instanceId);
        if (existingInstance is { RuntimeStatus: OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending })
        {
            logger.LogInformation(
                "Orchestration is already running (status: {status}). Skipping this run.",
                existingInstance.RuntimeStatus);
            return;
        }

        await client.ScheduleNewOrchestrationInstanceAsync(
            FunctionNames.CrawlerOrchestrator,
            profileId,
            new StartOrchestrationOptions(InstanceId: instanceId));
        logger.LogInformation("Started orchestration for wishlist monitoring.");
    }
}
