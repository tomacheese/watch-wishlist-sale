# AGENTS.md

## Language

- Write commit descriptions, PR/Issue titles and bodies, code comments and XML doc comments, and documents in English.
- Exceptions: end-user-facing notification text (the Japanese Discord notification strings in `Activities/SendDiscordNotification.cs`), Japanese data patterns and fixtures, quotations, and technical identifiers.
- Log and exception messages are in English.

## Overview

`WatchWishlistSale` is a serverless batch app built on Azure Durable Functions (.NET 10, isolated worker model). It runs hourly, scans a Steam wishlist, detects apps that are on sale and cheaper than at the last notification, and notifies Discord. Historical lowest prices come from IsThereAnyDeal (ITAD) with CheapShark as a fallback.

## Development commands

- `dotnet restore` restores dependencies.
- `dotnet build --configuration Release` builds. Run it before committing; CI runs the same build. Keep build warnings at zero (StyleCop.Analyzers and `GenerateDocumentationFile` warnings are review blockers).
- `func start` runs locally. It needs Azurite running in another terminal and a `local.settings.json` (see `docs/local-development.md` and `docs/configuration.md`).
- To fire the timer without waiting for the hour: `POST http://localhost:7071/admin/functions/RunCrawler` with body `{ "input": "" }`.

There is no test project. Verify changes with the build and a local run against Azurite.

## Architecture

Data flows Trigger -> Orchestrator -> Activities, and a Durable Entity keeps state across runs.

- `Triggers/Crawler.cs`: hourly `TimerTrigger` that starts the orchestration. It uses a fixed instance ID per profile (`CrawlerOrchestrator-<STEAM_PROFILE_ID>`) and skips the run if that instance is `Running` or `Pending`.
- `Orchestrations/WatchWishlistOrchestrator.cs`: coordinates the activities (fan-out/fan-in in chunks with timers between chunks to respect rate limits).
- `Activities/`: the actual work (HTTP calls, filtering, Discord notification), one class per activity.
- `Entities/NotificationStateEntity.cs`: Durable Entity holding the notified price per app ID, keyed by profile ID.
- `Models/`: DTOs grouped by domain (`Wishlist/`, `Pricing/`, `Notification/`).
- `Common/FunctionNames.cs`: constants for all function names.
- `Program.cs`: host setup (`IHttpClientFactory`, OpenTelemetry; the Azure Monitor exporter is enabled only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set).

See `docs/` for the walkthrough.

## Conventions

- The orchestrator must be deterministic: no `DateTime.Now`/`UtcNow`, `Guid.NewGuid()`, random numbers, direct I/O, environment reads, or non-durable async such as `Task.Delay`. Put non-deterministic work in activities and use `context.CurrentUtcDateTime` and `context.CreateTimer`.
- Keep the "first run skips notification" behavior intact: `NotificationStateEntity.GetSnapshot` reports `isFirstRun`, and the orchestrator skips Discord while it is true but still records state. Do not break only one side.
- Keep the per-profile singleton check in `Crawler.cs`; removing it allows overlapping runs.
- Use `FunctionNames.*` constants instead of string literals for function names.
- Get HTTP clients from the injected `IHttpClientFactory` (`CreateClient(nameof(TheClass))`); never `new HttpClient()`.
- Public types and members need `///` XML doc comments (`GenerateDocumentationFile` is on, so missing ones warn).
- Prefer `record` for models. `Nullable` and `ImplicitUsings` are enabled; annotate nullability honestly and avoid unjustified `!`.
- Check `IsSuccessStatusCode` and null deserialization results for external HTTP responses (Steam, ITAD, CheapShark).
- Formatting is enforced by `.editorconfig`: 4-space indent, CRLF, UTF-8, final newline, Allman braces; 2 spaces for JSON/YAML/XML.

## Secrets and configuration

- `local.settings.json` is gitignored and must never be committed. It holds `STEAM_PROFILE_ID` (numeric SteamID64, not a vanity name), `DISCORD_WEBHOOK_URL` and `ITAD_API_KEY`. On Azure these come from application settings.
- Never hard-code webhook URLs, API keys or profile IDs; read them through `IConfiguration`.
- Deployment uses OIDC (`azure/login`); do not store long-lived Azure credentials in the repository.

## Documentation updates

- When adding or renaming a function, update `Common/FunctionNames.cs` and the related `docs/` pages.
- When setup, configuration keys or run/debug steps change, update `docs/local-development.md` and `docs/configuration.md`.
- When the orchestration flow or architecture changes, update `docs/architecture.md`.
