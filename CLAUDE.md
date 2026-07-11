# CLAUDE.md

## Overview

`WatchWishlistSale` is a serverless batch app built on **Azure Durable Functions** (.NET 10, isolated worker model). It runs hourly, crawls a Steam wishlist, detects apps that are on sale and have dropped in price since the last notification, and posts them to Discord. Lowest-ever prices are fetched from IsThereAnyDeal / CheapShark.

## Development commands

- `dotnet restore` — restore dependencies.
- `dotnet build --configuration Release` — build. Run before committing; CI runs the same. Keep the build warning-free (StyleCop.Analyzers and `GenerateDocumentationFile` warnings are treated as review blockers).
- `func start` — run locally. Requires **Azurite** (Storage emulator) running in another terminal and a `local.settings.json` file (see below).
- Manually trigger the timer without waiting for the top of the hour:
  `POST http://localhost:7071/admin/functions/RunCrawler` with body `{ "input": "" }`.

There is no test project. Verify changes by building and by running the flow locally against Azurite (see `docs/06-local-development.md`).

## Architecture

Durable Functions splits work into three roles. Data flows Trigger → Orchestrator → Activities, with an Entity holding cross-run state.

- `Triggers/` — entry points. `Crawler.cs` is the hourly `TimerTrigger` that starts the orchestration.
- `Orchestrations/` — `WatchWishlistOrchestrator.cs`. Coordinates activities (fan-out/fan-in). **Must stay deterministic**: no `DateTime.Now`, random, direct I/O, or non-durable async — call activities for anything non-deterministic.
- `Activities/` — the actual work (HTTP calls, filtering, Discord notification). One class per activity.
- `Entities/` — `NotificationStateEntity.cs`, a Durable Entity persisting notified-sale state across runs.
- `Models/` — DTOs grouped by domain: `Wishlist/`, `Pricing/`, `Notification/`.
- `Common/FunctionNames.cs` — central constants for every Function name.
- `Program.cs` — DI/host setup (registers `IHttpClientFactory`, OpenTelemetry).

Deeper design and a code walkthrough live in `docs/` (learning-oriented, Japanese).

## Coding conventions

- **Language**: code comments and XML doc comments in **Japanese** (match existing files); log/exception messages in **English**.
- **XML docs required**: `GenerateDocumentationFile` is on, so every public type/member needs a `///` summary or the build warns.
- **Function names**: reference `FunctionNames.*` constants, never string literals, for `[Function(...)]` attributes and orchestration/activity/entity calls.
- **HTTP**: get clients from the injected `IHttpClientFactory` using a named client (`CreateClient(nameof(TheClass))`); do not `new HttpClient()`.
- **Models**: prefer `record` types; `Nullable` and `ImplicitUsings` are enabled — annotate nullability honestly and rely on implicit usings.
- **Formatting** (`.editorconfig`, enforced): 4-space indent, **CRLF** line endings, UTF-8, final newline, allman braces. JSON/YAML/XML use 2-space indent.

## Secrets & configuration

- Local config is `local.settings.json` at the repo root — **gitignored, never commit it**. It holds `STEAM_PROFILE_ID` (numeric SteamID64, not a vanity name), `DISCORD_WEBHOOK_URL`, and `ITAD_API_KEY`. In Azure these come from Application Settings.
- Never hardcode webhook URLs, API keys, or profile IDs; read them via `IConfiguration`.
- Deployment uses OIDC (`azure/login`); no long-lived Azure credentials are stored in the repo.

## Documentation update rules

- Adding/renaming a Function → update `Common/FunctionNames.cs` and any relevant `docs/` walkthrough.
- Changing local setup, required settings keys, or the run/debug flow → update `docs/06-local-development.md`.
- Changing the orchestration flow or architecture → update `docs/03-architecture-and-flow.md`.

## Commits

Conventional Commits; descriptions in **Japanese** (matches history). Example: `feat: セール判定に最安値比較を追加`.
