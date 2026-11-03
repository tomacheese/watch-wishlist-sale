using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WatchWishlistSale.Common;

namespace WatchWishlistSale.Triggers;

/// <summary>
/// Timer-triggered function that starts <c>WatchWishlistOrchestrator</c> once per hour.
/// </summary>
public class Crawler(IConfiguration configuration, ILogger<Crawler> logger)
{
    /// <summary>
    /// Timer trigger entry point. Runs at minute 0 of every hour and starts the orchestrator,
    /// using a fixed instance ID per profile so that runs for the same profile never overlap.
    /// </summary>
    /// <param name="myTimer">Timer trigger information, including the schedule status.</param>
    /// <param name="client">Durable Functions client used to start the orchestration.</param>
    /// <returns>A task that completes when the run has been handled.</returns>
    [Function(FunctionNames.RunCrawler)]
    public async Task RunCrawler(
        [TimerTrigger("0 0 * * * *")] TimerInfo myTimer,
        [DurableClient] DurableTaskClient client)
    {
        ArgumentNullException.ThrowIfNull(myTimer);
        ArgumentNullException.ThrowIfNull(client);
        logger.LogInformation("Timer trigger function executed at: {ExecutionTime}", DateTime.Now);
        if (myTimer.ScheduleStatus is not null)
        {
            logger.LogInformation("Next timer schedule at: {NextSchedule}", myTimer.ScheduleStatus.Next);
        }

        var profileId = configuration["STEAM_PROFILE_ID"]
            ?? throw new InvalidOperationException("STEAM_PROFILE_ID is not configured");

        // Fix the instance ID per profile (singleton pattern) to prevent overlapping orchestrations for the same profile.
        var instanceId = $"{FunctionNames.CrawlerOrchestrator}-{profileId}";
        OrchestrationMetadata? existingInstance = await client.GetInstanceAsync(instanceId);
        if (existingInstance is { RuntimeStatus: OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending })
        {
            logger.LogInformation(
                "Orchestration is already running (status: {Status}). Skipping this run.",
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
