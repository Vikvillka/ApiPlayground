---
title: 'Backend Clean Architecture boundaries'
type: 'refactor'
ticket: '10'
created: '2026-10-19'
status: 'built'
baseline_revision: '63e7378be057a8acf973d13eb1dd20ede5ec6726'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md'
  - '_bmad-output/initiative-apiplayground-mvp/epic-apiplayground-mvp/tickets.toml'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The backend baseline puts ASP.NET Core, EF Core/Npgsql persistence, and the initial migration in one API project, so upcoming stories cannot independently follow the approved Clean Architecture boundaries.

**Approach:** Establish Domain, Application, Infrastructure, and Api runtime projects with the spine's permitted references. Move EF context and the existing migration stream into Infrastructure without changing the initial migration ID or `PlatformDbContext` full type identity; retain the single test project and add boundary plus isolated PostgreSQL continuity verification.

**Decision (2026-10-20):** Verify a subsequent Infrastructure migration by generating and applying a temporary continuation migration only in an isolated test workspace; do not add a permanent no-op migration to the repository.

## Boundaries & Constraints

**Always:** Preserve the Story 1 baseline behavior and no-dispatch harness. Preserve the uncommitted user change in `Program.cs` that removes the fallback database credential; do not reset or stage unrelated work. Domain has no project dependencies; Application references Domain; Infrastructure references Application and Domain; Api references Application and Infrastructure only for composition-root wiring. Keep `PlatformDbContext` as `ApiPlayground.Api.Data.PlatformDbContext` unless a tested compatibility path requires otherwise. Keep migration ID `20261018000000_InitialPlatformBaseline`, one Infrastructure migration assembly/history stream, local host PostgreSQL support without Docker, CI PostgreSQL service, and fail (not skip) integration tests when configured PostgreSQL is unavailable. Use only unique disposable databases for migration tests, never the owner's existing `apiplayground` database. Do not read `.env`, alter `_bmad/render/`, or enable/request any outbound HTTP dispatch.

**Never:** Add product endpoints, outbound request execution, domain data tables, authentication, new test projects, destructive operations on the owner's database, migration-history renames/forks, or unrelated frontend/product work.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fresh disposable database | Unique test database with no EF history | Infrastructure applies the original migration once and records its original ID | Test fails with a clear connection/setup error; no skip |
| Story 1-migrated disposable database | Unique DB initialized with Story 1 migration history | Infrastructure recognizes the same migration as applied and does not apply a duplicate baseline | Test fails if identity/history diverges; cleanup is restricted to generated DB name |
| Runtime dependency boundary | Four runtime project files and source tree | Project references match spine; no direct target HTTP dispatch exists outside the guarded adapter boundary | Automated test fails with offending project/file |

</frozen-after-approval>

## Code Map

- `backend/ApiPlayground.sln` — currently contains only Api and the single Tests project; add Domain and Infrastructure projects and Application.
- `backend/src/ApiPlayground.Api/ApiPlayground.Api.csproj` — currently owns EF Design and Npgsql references; move persistence packages to Infrastructure and keep only required API dependencies.
- `backend/src/ApiPlayground.Api/Program.cs` — current composition root; preserve user's current environment-only connection configuration and register Infrastructure through its DI entry point.
- `backend/src/ApiPlayground.Api/Data/PlatformDbContext.cs` — move physically into Infrastructure while retaining namespace/type identity.
- `backend/src/ApiPlayground.Api/Data/Migrations/` — move initial migration and snapshot into Infrastructure; retain migration ID and snapshot model/version, configure EF migration assembly.
- `backend/tests/ApiPlayground.Tests/` — retain the single test project; add architecture/reference and PostgreSQL migration-continuity tests.
- `backend/.config/dotnet-tools.json` — current EF tool pin 10.0.0; verify compatible servicing version with EF/Npgsql pins.
- `.github/workflows/ci.yml` — backend job currently has no PostgreSQL service; provision PostgreSQL 17 and pass a non-secret isolated test-admin connection.
- `README.md` — local setup expects host PostgreSQL and currently documents the removed API fallback; update only required config/tool/test instructions.
- `backend/src/ApiPlayground.Api/Properties/launchSettings.json` — current localhost profile; preserve loopback exposure.

## Tasks & Acceptance

**Execution:**
- [x] `backend/src/ApiPlayground.Domain/`, `Application/`, `Infrastructure/` — add minimal `net10.0` projects and enforce approved dependency direction; Infrastructure provides a clear DI registration entry point and migration assembly configuration.
- [x] `backend/src/ApiPlayground.Api/Data/` and Infrastructure persistence files — move context/migrations/snapshot without changing migration ID or context CLR identity; update compatible EF Core/Npgsql servicing pins and local EF tool pin.
- [x] `backend/src/ApiPlayground.Api/Program.cs` and project files — wire Api to Application/Infrastructure; preserve the current no-fallback connection-string behavior and user edits.
- [x] `backend/ApiPlayground.sln` and `backend/tests/ApiPlayground.Tests/` — add projects and automated project-reference/direct-dispatch boundary tests; add safe, fail-not-skip migration continuity tests using generated unique databases and guaranteed cleanup.
- [x] `.github/workflows/ci.yml` and `README.md` — provision PostgreSQL 17 for integration tests; document the required test-admin configuration and local host-Postgres workflow without Docker or credential defaults.

**Acceptance Criteria:**
- Given the four runtime projects, when references are inspected/tested, then they match AD-1 and the test project remains outside runtime dependencies.
- Given the runtime source tree, when boundary tests run, then direct target dispatch is rejected outside the single future guarded adapter location and no dispatch path is added now.
- Given fresh and Story-1-migrated disposable databases, when Infrastructure migrations run, then the initial migration is recorded exactly once with unchanged ID and no second history stream is introduced.
- Given a following Infrastructure migration, when it is discovered and applied in the isolated verification flow, then it uses the same migration assembly/history stream.
- Given missing/unavailable configured PostgreSQL in CI integration tests, when tests run, then they fail rather than silently skip; local setup remains host-PostgreSQL-compatible and Docker-free.

## Implementation Notes

- Implemented four-project Clean Architecture topology and moved the existing context/migration stream to Infrastructure while preserving the original migration ID and `PlatformDbContext` CLR identity.
- Preserved the user's environment-only connection configuration; no outbound target HTTP dispatch or product endpoint was added.
- Migration continuity coverage exercises fresh DB, Story 1 migration history, and an actual temporary continuation migration generated/applied in an isolated workspace.
- Review patches expanded forbidden dispatch API matching (including JSON convenience APIs), normalized line endings in temporary source mutation, and corrected README test-setup ordering.
- Verification: Release solution build succeeds; full backend suite passes (9/9); architecture boundary tests pass after the final dispatch scanner patch (2/2); `git diff --check` succeeds. Earlier focused migration suite passed 4/4 against local PostgreSQL.


## Plan Change Log


## Review Triage Log

- `medium / patch` — `ArchitectureBoundaryTests.cs`: the original forbidden API set missed common `HttpClient` methods, allowing some direct dispatch calls to evade the boundary guard. Expanded the pattern set; subsequently added JSON convenience methods after final review. Verified architecture tests pass (2/2).
- `medium / patch` — `MigrationContinuityTests.cs`: temporary model mutation depended on exact newline style and could fail on CRLF sources. Normalize line endings before replacement; focused and migration continuity tests pass.
- `medium / patch` — `README.md`: test invocation appeared before required PostgreSQL test-admin setup, so following the documented sequence failed. Reordered setup before test commands; full suite passes.
- `medium / patch` — `ArchitectureBoundaryTests.cs`: final review identified omitted `GetFromJsonAsync`/`GetFromJsonAsAsyncEnumerable` dispatch extensions. Added both patterns and reran architecture boundary tests (2/2).


## Design Notes

Migration identity and the `PlatformDbContext` CLR identity are separate compatibility surfaces: keep both stable while moving assembly ownership. Migration tests must only create/drop uniquely named disposable databases and must never point at the developer's existing local application database.

## Verification

**Commands:**
- `dotnet restore backend/ApiPlayground.sln` — expected: updated project graph and package pins restore successfully.
- `dotnet build backend/ApiPlayground.sln --configuration Release` — expected: all four runtime projects and tests build.
- `dotnet test backend/ApiPlayground.sln --configuration Release` with configured PostgreSQL test-admin connection — expected: baseline, architecture, and migration-continuity tests pass; database-dependent cases fail if PostgreSQL is unavailable and never skip.
- `dotnet ef migrations list` from `backend` — expected: Infrastructure is the single discoverable migration assembly and retains `20261018000000_InitialPlatformBaseline`.
- `git diff --check` — expected: no whitespace errors; review diff confirms user-owned changes and protected files remain untouched.
