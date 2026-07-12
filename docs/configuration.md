# Configuration Reference

## `local.settings.json`

Configuration file loaded only during local execution (in production Azure environment, "Application Settings" is used instead and is not subject to deployment by GitHub Actions). Excluded by `.gitignore`, so it does not exist immediately after cloning the repository. Create it manually using the following content as reference.

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "STEAM_PROFILE_ID": "<Steam's SteamID64 (numeric)>",
    "DISCORD_WEBHOOK_URL": "<Discord Webhook URL>",
    "ITAD_API_KEY": "<IsThereAnyDeal API key>"
  }
}
```

## Environment Variables

| Key | Description | Reference |
|---|---|---|
| `AzureWebJobsStorage` | Connection string to Storage used by Durable Functions for persisting state. `"UseDevelopmentStorage=true"` specifies using local Azurite | Common to Azure Functions runtime |
| `FUNCTIONS_WORKER_RUNTIME` | Worker runtime. Specify `"dotnet-isolated"` for .NET's isolated worker model | Common to Azure Functions runtime |
| `STEAM_PROFILE_ID` | SteamID64 (numeric) of the account whose wishlist to monitor. A numeric ID is required, not a custom URL name (vanity name) | [`Triggers/Crawler.cs`](../Triggers/Crawler.cs) |
| `DISCORD_WEBHOOK_URL` | Webhook URL of the Discord channel to send notifications to | [`Activities/SendDiscordNotification.cs`](../Activities/SendDiscordNotification.cs) |
| `ITAD_API_KEY` | [IsThereAnyDeal](https://isthereanydeal.com/) API key used to retrieve historical lowest prices | [`Activities/GetLowestPrice.cs`](../Activities/GetLowestPrice.cs) |

The SteamID64 for `STEAM_PROFILE_ID` can be looked up using tools like [steamid.io](https://steamid.io/).

## Production Environment Configuration

On Azure, these values are configured as "Application Settings" in the Function App. Deployment via [`.github/workflows/azure-functions-deploy.yml`](../.github/workflows/azure-functions-deploy.yml) targets only the code; application setting values themselves are not included in the deployment target.
