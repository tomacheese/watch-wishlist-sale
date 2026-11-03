# Review instructions for GitHub Copilot

See `AGENTS.md` for the project overview, conventions and safety rules. Review C# changes with these focus points.

## Durable Functions correctness (highest priority)

- Flag non-deterministic code in the orchestrator (`Orchestrations/`): `DateTime.Now`/`UtcNow`, `Guid.NewGuid()`, random numbers, environment variable reads, direct HTTP/file I/O, `Task.Delay`. It belongs in activities or must use the Durable context APIs.
- Business logic and external calls belong in `Activities/`, not the orchestrator.
- Flag changes that break the singleton pattern in `Triggers/Crawler.cs` (fixed per-profile instance ID, skip when already `Running`/`Pending`).
- Keep the "first run skips notification" behavior: `Entities/NotificationStateEntity.cs` reports `isFirstRun` and the orchestrator skips Discord while it is true. Flag changes that break only one side.

## Repository conventions

- Function names use the `Common/FunctionNames.cs` constants, not string literals.
- HTTP access goes through the injected `IHttpClientFactory`; flag `new HttpClient()`.
- Public types and members need XML doc comments (`///`).
- Prefer `record` for models; flag suppressed or dishonest nullability (unjustified `!`).

## Security and configuration

- Flag hard-coded secrets or environment-specific values (Discord webhook URL, `ITAD_API_KEY`, `STEAM_PROFILE_ID`); they must come from `IConfiguration`.
- `local.settings.json` must never be added to the repository.
- Steam/ITAD/CheapShark responses are untrusted: check `IsSuccessStatusCode` and null deserialization results.

## Do not flag

- CRLF line endings, 4-space indent, Allman braces (enforced by `.editorconfig`).
- Long-form learning documents under `docs/`.
