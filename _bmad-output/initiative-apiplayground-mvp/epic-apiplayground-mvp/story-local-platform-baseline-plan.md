---
title: 'Local platform baseline'
type: 'feature'
ticket: '1'
created: '2026-10-18'
status: 'built'
baseline_revision: '053bc00a1ab848d9bcd4a3e9bea2734f7c89cb3c'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/stack.md'
  - '_bmad-output/initiative-apiplayground-mvp/epic-apiplayground-mvp/epic-apiplayground-mvp.md'
  - '_bmad-output/initiative-apiplayground-mvp/epic-apiplayground-mvp/tickets.toml'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The repository contains no application scaffold, local service setup, database migrations, application checks, CI workflow, or reusable test harness, so there is no repeatable foundation for later ApiPlayground stories.

**Approach:** Establish the local React + TypeScript, ASP.NET Core Web API, and PostgreSQL baseline with documented startup, migrations, minimum build/test checks, CI, and a no-dispatch harness self-test.

## Boundaries & Constraints

**Always:** Keep the baseline local-first; use the already installed and running host PostgreSQL 17 service; use EF Core migrations; use Vite + Vitest for the React/TypeScript build and tests, and xUnit with ASP.NET Core test-host support for backend/harness tests; make fresh PostgreSQL initialization reproducible; provide an example-only environment file and keep local secrets/data out of version control; the harness self-test must not dispatch an outbound request; follow the existing stack and .gitignore conventions.

**Never:** Implement API request sending, enable any outbound dispatch, select or assume outbound security policies, build request/response product UI, deploy publicly, or add product capabilities beyond this platform baseline.

**Decision (2026-10-18):** Use the already installed and running host PostgreSQL 17 service; do not require Docker Compose for local setup.

**Decision (2026-10-18):** Use EF Core migrations for repeatable database initialization.

**Decision (2026-10-18):** Use Vite + Vitest for the React/TypeScript build and tests, and xUnit with ASP.NET Core test-host support for backend and harness tests.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fresh local setup | Clone at the approved revision; required local prerequisites available | Documented setup starts the local services and applies database migrations | Instructions identify missing prerequisites and provide a recoverable failure message |
| Harness self-test | Local test harness with its built-in controlled target | Harness proves it is ready without dispatching any request | Fails with an actionable diagnostic if the test setup is unavailable |
| CI validation | Push or pull request on the repository's configured CI provider | Minimum frontend/backend build and test checks run without secrets or outbound requests | Failed check reports the failing command and returns a failing status |

</frozen-after-approval>

## Code Map

- `_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/stack.md` — approved technology context only; no architecture choice beyond React + TypeScript, ASP.NET Core Web API/C#, and PostgreSQL.
- `_bmad-output/initiative-apiplayground-mvp/epic-apiplayground-mvp/epic-apiplayground-mvp.md` — local-only delivery boundary and baseline/tracer sequencing decision; preserve the approved safety-first exception.
- `_bmad-output/initiative-apiplayground-mvp/epic-apiplayground-mvp/tickets.toml` — Story 1 description and verification; no outbound dispatch in this story.
- `.gitignore` — existing ignores for .NET build output, Node dependencies/build output, environment secrets with `.env.example` allowlisted, and local PostgreSQL data.
- `skills-lock.json` — BMAD skill lock only, not an application package manifest.
- Repository inspection found no application source, .NET project, frontend package manifest, migrations, CI workflow, or app tests; repository remote is GitHub.
- Local environment has .NET SDK 10.0.302, Node 24.18.0, npm 12.2.0, and a running PostgreSQL 17 Windows service with `psql`/`pg_isready`; Docker is not installed. Prefer reusing the existing PostgreSQL service unless the owner chooses otherwise.

## Tasks & Acceptance

**Execution:**
- [x] `frontend/package.json`, `frontend/package-lock.json`, and Vite/Vitest configuration — establish the minimal React + TypeScript build and test commands without selecting a UI component library.
- [x] `backend/ApiPlayground.sln`, API project, and xUnit test project — establish the minimal ASP.NET Core Web API build and test projects using the approved test host.
- [x] `.env.example` and local service configuration — provide non-secret connection settings for the existing host PostgreSQL 17 service.
- [x] Backend database configuration and initial EF Core migration — make fresh local database initialization reproducible and repeatable.
- [x] Test harness project/files — add an in-process controlled test double and a self-test that verifies harness readiness without sending an outbound request.
- [x] `README.md` — document prerequisites, configuration, local startup, fresh database setup, build/test commands, and troubleshooting.
- [x] `.github/workflows/ci.yml` — run minimum frontend/backend build and test checks on GitHub Actions without secrets or outbound requests.
- [x] `.gitignore` — existing ignore rules already cover the selected tooling's generated artifacts; no changes were needed.

**Acceptance Criteria:**
- Given a fresh checkout and the documented prerequisites, when a developer follows the local setup instructions, then React, the API, and PostgreSQL can be started locally and the documented health/setup checks succeed.
- Given a fresh local PostgreSQL instance, when the documented migration command runs, then the initial schema is created reproducibly and the command can be run again without corrupting it.
- Given the controlled-target harness, when its self-test runs, then it reports readiness without making an outbound request.
- Given a push or pull request to the GitHub repository, when CI runs, then minimum frontend/backend build and test checks execute without secrets or outbound requests and report failure through the CI status when a check fails.

## Implementation Notes

- The initial EF Core migration was applied to a fresh, isolated PostgreSQL 17 database, then applied again successfully; the EF migration history table exists. The disposable test clusters did not use or modify the installed PostgreSQL service.
- Verification passed: frontend `npm ci`, production build, Vitest (1 test), and `npm audit` (0 vulnerabilities); backend Release build and xUnit (3 tests), with no vulnerable NuGet packages.
- Quick review found one issue in the initial harness self-test: readiness was hard-coded in a private test-only type. The in-process controlled target is now a shared test helper with configuration-derived readiness; tests cover configured and unconfigured states with zero dispatch.
- The GitHub Actions workflow was inspected but not executed remotely; no push or remote operation was performed.


## Plan Change Log


## Review Triage Log

- **Finding:** The harness self-test used a private target whose readiness was hard-coded true, so it could pass without establishing a configured, reusable controlled target. **Verdict:** medium — the approved verification requires a reusable ready target, which later controlled-target tests cannot consume. **Route:** patch — the smallest fix is to extract the in-process target into a shared test helper and derive readiness from its configuration; the self-test can validate both configured and unconfigured states without dispatch.


## Design Notes

The platform baseline must be usable without opening an outbound request path. The harness should test its own readiness against a local/in-process controlled test double; any test that dispatches toward an approved destination belongs behind the later safety-policy approval gate.

## Verification

**Commands:**
- `npm ci` and the Vite/Vitest frontend scripts — expected: deterministic dependency install and passing build/test checks.
- `dotnet build` and `dotnet test` — expected: backend and harness projects compile and tests pass.
- The documented EF Core migration command — expected: schema initializes successfully on the host PostgreSQL 17 service and rerunning the command is safe.
- GitHub Actions workflow checks — expected: frontend/backend checks pass without secrets or outbound requests.

**Manual checks:**
- Follow the README from a fresh checkout and confirm local services start without public hosting or internet access to the application.
- Confirm the harness self-test emits no outbound request and does not require a security-policy decision.
