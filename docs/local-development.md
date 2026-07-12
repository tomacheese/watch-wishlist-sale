# Local Development

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/) (.NET 10)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) (the `func` command)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (Azure Storage local emulator)

## Starting Azurite

Durable Functions persists orchestration execution history and entity state to Azure Storage (Blob / Queue / Table) (see `durableTask.storageProvider.type: "AzureStorage"` in [`host.json`](../host.json)). By running Azurite locally, you can complete the setup without provisioning a Storage account on Azure.

```bash
# If installing via npm
npm install -g azurite

# Start (by default, data is saved to the current directory)
azurite
```

Once started, three endpoints (Blob on port `10000`, Queue `10001`, and Table `10002` by default) will be listening. `__blobstorage__` / `__queuestorage__` directories will be created, but these are not managed by Git.

## Preparing Configuration Files

Create [`local.settings.json`](../local.settings.json). For details, see [configuration.md](configuration.md).

## `func start`: Running Locally

```bash
# Start Azurite in another terminal
azurite

# Start the Functions host
func start
```

If startup is successful, a list of Functions such as `RunCrawler` / `CrawlerOrchestrator` / `GetWishlistAppIdsActivity` will be displayed in the console, and the host will be listening. `RunCrawler` operates on a `[TimerTrigger("0 0 * * * *")]` schedule (at minute 0 every hour), so for testing, manually trigger it using the method in the next section.

## Manually Triggering a Function

```bash
curl -X POST "http://localhost:7071/admin/functions/RunCrawler" \
  -H "Content-Type: application/json" \
  -d '{ "input": "" }'
```

This starts `RunCrawler`, which calls `client.ScheduleNewOrchestrationInstanceAsync` and begins the orchestration.

## Checking Orchestration Execution Status

```bash
# instanceId is in the format "{CrawlerOrchestrator}-{profileId}" (e.g., CrawlerOrchestrator-76561198072825180)
curl "http://localhost:7071/runtime/webhooks/durabletask/instances/CrawlerOrchestrator-<STEAM_PROFILE_ID value>"
```

You can check the status using the `runtimeStatus` field in the response.

| `runtimeStatus` | Meaning |
|---|---|
| `Pending` | Waiting to start |
| `Running` | In progress |
| `Completed` | Completed successfully |
| `Failed` | Terminated due to failure |

## Debugging Tips

- **Discord notifications are not sent on first run**: While `NotificationSnapshot.isFirstRun` is `true`, notifications are skipped and only the state is recorded. If you want to reset the state and reproduce the first-run behavior, delete Azurite's data directories (`__blobstorage__` / `__queuestorage__`) and restart.
- **Orchestrations with the same instance ID are reused**: Using the singleton pattern, if an instance in `Running`/`Pending` state exists, new runs are skipped. If nothing happens when manually triggering, first check the current instance status.
- **Be aware of rate limits on external APIs**: Steam, IsThereAnyDeal, and CheapShark have rate limits. When manually triggering multiple times for testing, space out the requests.
