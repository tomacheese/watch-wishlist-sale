# Copilot code review instructions

This is a .NET 10 Azure Durable Functions app (isolated worker) that crawls a Steam wishlist and notifies Discord of price drops. Review C# changes against the points below.

## Durable Functions correctness (highest priority)

- Flag non-deterministic code inside the orchestrator (`Orchestrations/`): `DateTime.Now`/`DateTime.UtcNow`, `Guid.NewGuid()`, random, environment reads, direct HTTP/file I/O, or `Task.Delay`. Such work belongs in an activity or must use the Durable-provided context APIs.
- Orchestrators should only coordinate activities and use durable timers/context; business logic and external calls belong in `Activities/`.
- Watch for changes that break the singleton pattern in `Triggers/Crawler.cs` (fixed instance ID per profile, skip when an instance is already `Running`/`Pending`) — losing it allows overlapping runs.
- Preserve the "first run skips notification" behavior: the entity (`Entities/NotificationStateEntity.cs`) reports `isFirstRun` on its first call, and the orchestrator skips Discord notification while it is true. Flag changes that break either side.

## Conventions enforced in this repo

- Function names must use `Common/FunctionNames.cs` constants, not string literals.
- HTTP access must go through the injected `IHttpClientFactory`; flag any `new HttpClient()`.
- Every public type/member needs an XML doc comment (`///`) — the build warns otherwise.
- Code comments and XML docs are written in Japanese; log and exception messages in English. Do not flag Japanese comments as an issue.
- Prefer `record` types for models/DTOs. Nullable reference types are enabled — flag suppressed or dishonest nullability (`!` without justification).

## Security & configuration

- Flag any hardcoded secrets or environment-specific values (Discord webhook URLs, `ITAD_API_KEY`, `STEAM_PROFILE_ID`); these must be read via `IConfiguration`.
- `local.settings.json` must never be added to the repo.
- Confirm external HTTP responses are checked (`IsSuccessStatusCode`) and deserialization results are null-checked before use, since Steam/ITAD/CheapShark responses are untrusted.

## Do not flag

- CRLF line endings, 4-space indentation, and allman braces — these are enforced by `.editorconfig`.
- Japanese-language comments and documentation.
- The learning-oriented long-form docs under `docs/`.
