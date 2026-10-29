---
title: 'Organize DI and configuration and add safe NLog'
type: 'refactor'
ticket: '9'
created: '2026-10-29'
status: 'in-review'
baseline_revision: '2188e670f3986623e2076ae68a2357e458af2e0b'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Service registration is split between `Program.cs` and `ApiPlayground.Infrastructure/DependencyInjection.cs` without a clear owner. The `Postgres` connection string is passed as a nullable string with no startup validation, so misconfiguration fails late. The backend has no NLog package, configuration, or calls; logging uses only the built-in provider.

**Approach:** Make registration ownership explicit, validate the required operational setting at startup, and add NLog with structured, safe log output. Decisions (owner, 2026-10-29):
- Typed options: `PostgresOptions` bound from `ConnectionStrings:Postgres`, validated with `ValidateOnStart` (non-empty check).
- NLog: code-only setup in `Program.cs` with minimum levels and a console target. No `nlog.config`, no `appsettings` section.
- Log targets: console only. No file target.
- Request logging: no built-in ASP.NET Core HTTP logging. Keep the existing `ApiLog` messages only.

## Boundaries & Constraints

**Always:** Keep Domain free of infrastructure references. Keep EF migrations generated only with `dotnet ef migrations add`. Keep existing `ApiLog` message parameters limited to exception type names and routing metadata. Keep all existing statuses, routes, and guard behavior unchanged.

**Never:** No schema change or new migration. No change to outbound policy, DNS/address pinning, redirect handling, or the guard's logic (ticket 6 scope). No logging of secrets, connection strings, `Authorization` or `Cookie` headers, query values, or request bodies. No new DI container.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| HAPPY_PATH | Valid `ConnectionStrings:Postgres` | App starts; NLog writes structured entries; routes unchanged | No error expected |
| MISSING_CONN | Connection string absent or empty | Startup fails before serving requests with a clear configuration message that does not echo the value | Fail fast at host start |
| SENSITIVE_IN_LOG | Request with auth header and sensitive query value | Captured log contains neither value | Redacted by construction (fields not logged) |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Api/Program.cs` -- composition root; calls `AddInfrastructure`, `AddGuardedOutbound`, `AddTracerPersistenceSmokeCheck`, and sets up `UseExceptionHandler` and `UseRequestGuard`; no logging configuration.
- `backend/src/ApiPlayground.Infrastructure/DependencyInjection.cs` -- registers `PlatformDbContext`, the history and collection stores, and the outbound and smoke-check services; takes a nullable connection string without validation. Reuse its extension-method pattern.
- `backend/src/ApiPlayground.Api/ApiPlayground.Api.csproj` -- `UserSecretsId` set; no NLog package; no `appsettings*.json` present.
- `backend/src/ApiPlayground.Api/Properties/launchSettings.json` -- local profile; do not change its URL or environment values.
- `backend/src/ApiPlayground.Api/ApiLog.cs` -- `LoggerMessage` definitions; reuse this pattern for any new messages.
- `backend/src/ApiPlayground.Api/UnhandledExceptionHandler.cs`, `TracerCollectionsController.cs` -- `ILogger` consumers; keep their message contracts.
- `backend/src/ApiPlayground.Api/TracerEndpoints.cs` -- `UseRequestGuard` extension; must not log headers or query values.
- `backend/src/ApiPlayground.Infrastructure/Outbound/` -- guarded transport; do not change behavior (ticket 6 scope).
- `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs`, `Story9CloseoutTests.cs` -- host wiring with `AddSingleton` overrides and `AddLogging()`; must keep compiling and passing.

## Tasks & Acceptance

**Execution:**
- [x] `backend/src/ApiPlayground.Infrastructure/DependencyInjection.cs` -- add `PostgresOptions` with `ValidateOnStart` and non-empty check; consolidate registration ownership -- rationale: CAP-7 ownership
- [x] `backend/src/ApiPlayground.Api/Program.cs` -- move registration calls to one owner per concern; add code-only NLog setup with console target and minimum levels -- rationale: CAP-7 logging setup
- [x] `backend/src/ApiPlayground.Api/ApiPlayground.Api.csproj` -- add the NLog package (no ASP.NET HTTP logging integration) -- rationale: NLog absent today
- [x] `backend/src/ApiPlayground.Api/ApiLog.cs` -- add any new `LoggerMessage` entries needed for startup and configuration logs, with no sensitive parameters -- rationale: structured context
- [x] `backend/tests/ApiPlayground.Tests/` -- add startup-validation tests and a captured-log sensitivity test -- rationale: proves redaction

**Acceptance Criteria:**
- Given no `Postgres` connection string, when the host starts, then startup fails with a message that does not include any configured value.
- Given NLog is configured, when a request carries an `Authorization` header and a sensitive query value, then no captured log entry contains either value.
- Given the change is complete, when the Release build and backend tests run, then both pass with no migration or schema change.

## Implementation Notes

- Added `PostgresOptions` (`ConnectionString`, fixed `ValidationMessage` that does not echo the value); `AddInfrastructure(IServiceCollection, IConfiguration)` binds `ConnectionStrings:Postgres` with `Validate` and `ValidateOnStart`. The `PlatformDbContext` options resolve from `IOptions<PostgresOptions>`. Migrations assembly unchanged.
- `Program.cs`: `ClearProviders()` then `AddNLog` with a code-only `LoggingConfiguration` (one console target, JSON layout with an explicit allowlist of time, level, logger, message, exception type). Rules: `Microsoft.Hosting.Lifetime` Info (final), `Microsoft.*` and `System.*` Warn (final), `*` Info. The `AddGuardedOutbound(OutboundPolicy.PublicInternetOnly())` line is unchanged.
- No new `LoggerMessage` was added to `ApiLog.cs`. A startup log would not run before validation fails at host start.
- Test hosts do not load user secrets or `.env`, so `TestConfigurationDefaults` sets a placeholder `ConnectionStrings__Postgres` env var (only when empty). The startup tests override it with `UseSetting`.
- `Story9BrowserHostRunnerTests` now sets `ConnectionStrings:Postgres` in configuration before `AddInfrastructure(builder.Configuration)`.
- New tests in `StartupConfigurationTests.cs`: 3 missing-value cases (null, empty, whitespace) and a captured-log sensitivity test (Authorization header, sensitive query value, throwing executor). Results: 186 passed in the non-browser suite, 0 failed. The 4 new tests pass.
- Conflict: Verification says the log is written to a file, but Approach says console only. Followed console only (frozen intent). No file target added.
- Residuals accepted by owner (option a, 2026-10-29): (1) registration ownership is not fully consolidated, because use-case registrations stay in `Program.cs` (Api/Infrastructure reference cycle); (2) the sensitivity test uses a capturing provider rather than the NLog layout. Both need follow-up tickets. The Execution checkbox for registration consolidation is therefore only partly met.

## Plan Change Log

## Review Triage Log
- Registration still split (Program.cs keeps use-case registrations): verdict medium, patch not applied. Use-case types sit in the Api namespace and Infrastructure cannot reference Api. Owner decision (a, 2026-10-29): accepted as residual; follow-up ticket needed to consolidate ownership.
- Log test does not run through NLog: verdict medium, patch not applied. Capturing provider replaces NLog output in the test host. Owner decision (a, 2026-10-29): accepted as residual; follow-up ticket needed to cover the NLog layout in tests.
- Captured-log test missing status assertion and proof of path: verdict medium, patched. Test now asserts the InvalidOperationException entry exists.
- Npgsql category unfiltered at Info: verdict medium, patched. Added Npgsql Warn rule.
- Missing-value test lacks configured-value absence check: verdict false. No configured value exists in the missing case to echo.
- TestConfigurationDefaults process-wide env var: verdict low, rejected. Tests pass; the fix adds complexity.
- Plan Verification wording (file target): verdict low, fixed in plan.
- ApiLog task checkbox overstated: verdict low, rejected. No new entries are needed and the plan wording allows that.
- Binding wording (manual Configure vs binding): verdict low, rejected. Behaviour is equivalent.
- Diff snapshot showed stale status and mojibake: verdict false. Snapshot was taken before the frontmatter update; the on-disk plan is current.

## Design Notes

Startup validation runs through options validation so failures surface before the first request. NLog setup stays in one place to keep registration ownership clear.

## Verification

**Commands:**
- `dotnet build backend/ApiPlayground.sln --configuration Release` -- expected: 0 errors
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName!~Story9BrowserHostRunnerTests"` -- expected: all pass; logging is console-only (no file target)
- `git status --short` -- expected: no migration or schema file changed

**Manual checks (if no CLI):**
- Captured log output for a request with an auth header and sensitive query contains neither value
