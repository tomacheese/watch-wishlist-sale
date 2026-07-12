# watch-wishlist-sale

A serverless application that monitors your Steam wishlist and sends Discord notifications when apps go on sale and drop in price.

## Features

- Polls Steam wishlist once per hour
- Detects apps on sale (and that have dropped in price since the last notification)
- Sends Discord notifications with information compared against historical lowest price (via IsThereAnyDeal)
- Persists notification state using Durable Entities to prevent duplicate notifications

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) (for local execution)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (for local execution, Azure Storage emulator)
- Discord Webhook URL / Steam SteamID64 / IsThereAnyDeal API key

## Setup

```bash
git clone https://github.com/tomacheese/watch-wishlist-sale.git
cd watch-wishlist-sale
dotnet restore
```

You must create `local.settings.json`. For details, see [docs/local-development.md](docs/local-development.md).

## Configuration

Key environment variables are listed below. For details, see [docs/configuration.md](docs/configuration.md).

| Key | Description |
|---|---|
| `AzureWebJobsStorage` | Connection string to Storage used for persisting Durable Functions state |
| `FUNCTIONS_WORKER_RUNTIME` | Worker runtime (`dotnet-isolated`) |
| `STEAM_PROFILE_ID` | SteamID64 of the account whose wishlist to monitor |
| `DISCORD_WEBHOOK_URL` | Webhook URL of the Discord channel to send notifications to |
| `ITAD_API_KEY` | IsThereAnyDeal API key |

## Architecture

For processing flow and folder structure, see [docs/architecture.md](docs/architecture.md).

## Deployment

GitHub Actions automatically deploys to Azure Functions triggered by a push to the `main`/`master` branch. For details, see [docs/deployment.md](docs/deployment.md).

## License

This project is published under the [MIT](LICENSE) license.
