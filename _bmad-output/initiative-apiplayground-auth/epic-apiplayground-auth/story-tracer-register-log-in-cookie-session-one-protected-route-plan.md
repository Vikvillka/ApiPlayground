---
title: 'Tracer: register, log in, cookie session, one protected route'
type: 'feature'
ticket: '1'
created: '2026-11-04'
status: built
baseline_revision: '948580a77778f0f23e436b38cbceff4151381cf9'
route: 'full'
route_source: 'pinned'
risk: 'high'
review: 'thorough'
review_source: 'pinned'
lenses_ran: ['security-auth-flow', 'matrix-coverage', 'dependency-direction']
review_loop_iteration: 0
context: ['_bmad-output/initiative-apiplayground-auth/architecture-apiplayground-auth/architecture-apiplayground-auth.md', '_bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The API has no user accounts. Every route is anonymous, so per-user data cannot be separated. This story proves the auth stack end to end on the thinnest slice.

**Approach:** Add ASP.NET Core Identity (Infrastructure only, `Guid` keys, no roles), register and login and logout endpoints under `/api/auth`, an httpOnly `SameSite=Strict` cookie session with about 8 hours sliding lifetime, a default-deny fallback policy, and extend the existing request guard from the tracer route to all `/api/*`. One protected route proves the 401 and 200 behavior.

## Boundaries & Constraints

**Always:** Identity types stay in Infrastructure. Dependencies run inward (Api references Application and Infrastructure; Application references Domain). The request guard (Host, Origin, `X-ApiPlayground-Request`, Sec-Fetch-Site) is kept as is and now also covers `/api/*`. The API binds to loopback only. Anonymous access is limited to register, login, and `/health`. Unauthenticated calls return 401 ProblemDetails. Password minimum is 8 characters, no composition rules (decision 2026-11-04). Passwords are stored only as hashes. Login failure returns one generic message. Migrations are created only with `dotnet ef migrations add`.

**Decisions:** Local development keeps the `postgres` superuser in `ConnectionStrings__Postgres` (decided 2026-11-04). No environment change.

**Never:** No OAuth. No password recovery, roles, or lockout. Do not change existing tracer controller behavior or the outbound request path. Do not add a second outbound HTTP path. Do not relax the Host, Origin, or header checks. Do not change environment settings or connection strings in the repo.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Register then login | Valid email and password of 8+ characters | 200 on login and an httpOnly `SameSite=Strict` cookie is set | No error expected |
| Protected route without cookie | GET to the protected route, no cookie | 401 ProblemDetails | Not an error page |
| Protected route with cookie | GET with the cookie from login | 200 | No error expected |
| Logout | POST logout with the cookie | Cookie cleared; the same cookie is rejected afterwards (401) | No server revocation (per AD-2) |
| Wrong credentials | Known email, wrong password, or unknown email | Same generic 401 message for both | No account leak |
| Guard on `/api/*` | Request with a foreign Origin or missing `X-ApiPlayground-Request` | Rejected by guard, as for the tracer route today | Existing guard response |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Api/ApiPlayground.Api.csproj` -- add `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.14
- `backend/src/ApiPlayground.Api/Program.cs` -- builder services (Identity, cookie options, fallback policy) and guard call at the start of the pipeline
- `backend/src/ApiPlayground.Api/Endpoints/TracerEndpoints.cs` -- `UseRequestGuard()`; today it targets the tracer route, extend the path check to `/api/*`. Do not change the checks themselves
- `backend/src/ApiPlayground.Api/Controllers/ApiControllerBase.cs` -- reuse `BadRequestProblem()` and related helpers for ProblemDetails
- `backend/src/ApiPlayground.Infrastructure/Data/PlatformDbContext.cs` -- Identity model mapping; `IdentityUser<Guid>` store
- `backend/src/ApiPlayground.Infrastructure/DependencyInjection.cs` -- register Identity stores and managers next to existing repositories (pattern at lines 32-33)
- `backend/src/ApiPlayground.Application/Services/GetTracerUseCase.cs` -- style reference for new use cases (input record, async `ExecuteAsync`, typed result)
- `backend/src/ApiPlayground.Infrastructure/Data/Migrations/` -- existing migrations (`20261018000000_InitialPlatformBaseline` through `20261029125342_DomainRequestEntities`); the generated Identity migration goes here, created only by `dotnet ef migrations add`
- `backend/tests/ApiPlayground.Tests/MigrationContinuityTests.cs` -- migration id constants (lines 10-13) and `ExpectedMigrationIds` (line 14) must include the new id
- `backend/tests/ApiPlayground.Tests/Story9DisposableDatabase.cs` and `Story9CloseoutTests.cs` -- existing disposable-database helper that creates a restricted app role; reuse it for the PostgreSQL auth tests instead of creating a new database setup
- `backend/src/ApiPlayground.Application/Interfaces/` -- home for the new auth port, next to `IRequestHistoryRepository.cs`
- `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` -- `WebApplicationFactory<Program>` pattern (line 256) for auth round-trip tests
- `frontend/vite.config.ts` -- proxy already correct; do not change

## Tasks & Acceptance

**Execution:**
- [ ] `backend/src/ApiPlayground.Api/ApiPlayground.Api.csproj` -- add the Identity EF Core package at 10.0.14 -- Identity is the chosen store (AD-1)
- [ ] `backend/src/ApiPlayground.Infrastructure/DependencyInjection.cs` and `PlatformDbContext.cs` -- register Identity with `Guid` keys, no roles -- AD-1
- [ ] `backend/src/ApiPlayground.Application` -- add an auth port and register, login, logout, and current-user use cases -- keeps the endpoint thin and the dependency direction intact
- [ ] `backend/src/ApiPlayground.Api/Controllers/AuthController.cs` -- routes `/api/auth/register`, `/api/auth/login`, `/api/auth/logout` -- AD-3, AD-4
- [ ] `backend/src/ApiPlayground.Api/Program.cs` -- cookie options (httpOnly, `SameSite=Strict`, about 8h sliding), default-deny fallback policy, anonymous allowlist -- AD-2, AD-4
- [ ] `backend/src/ApiPlayground.Api/Endpoints/TracerEndpoints.cs` -- extend guard coverage to `/api/*` without changing the checks -- AD-3
- [ ] one protected route under `/api/` -- returns 200 with an authenticated user, 401 otherwise -- proves the fallback policy
- [ ] `backend/src/ApiPlayground.Infrastructure/Data/Migrations/` -- generate the Identity migration with `dotnet ef migrations add` only -- AGENTS.md rule
- [ ] `backend/tests/ApiPlayground.Tests/` -- add auth tests covering the I/O matrix on PostgreSQL -- verify line from ticket

**Acceptance Criteria:**
- Given a registered user, when they log in, then an httpOnly `SameSite=Strict` cookie is set and the protected route returns 200 with it.
- Given no cookie, when the protected route is called, then the response is 401 ProblemDetails.
- Given a wrong password or an unknown email, when login is called, then both return the same generic message.
- Given a logged-in user, when logout is called, then the same cookie is rejected on the protected route.
- Given a request to any `/api/*` route with a foreign Origin, when it arrives, then the guard rejects it as before.

## Implementation Notes

## Design Notes

Identity's default cookie handling is a close fit for httpOnly and sliding expiry. Set `SameSite=Strict` explicitly. The fallback policy must be default-deny, with `[AllowAnonymous]` only on register, login, and `/health`, so that new controllers are protected unless they opt out.

## Verification

**Commands:**
- `dotnet tool restore` (in `backend/`) -- expected: tools restored
- `dotnet build backend/ApiPlayground.sln` -- expected: build succeeds with no errors
- `dotnet test backend/ApiPlayground.sln --configuration Release` -- expected: all tests pass, including the new auth tests on PostgreSQL

**Manual checks (if no CLI):**
- Env: `ConnectionStrings__Postgres` set in the shell, and `APIPLAYGROUND_TEST_ADMIN_CONNECTION` targets database `postgres`. The API does not read `.env`.

