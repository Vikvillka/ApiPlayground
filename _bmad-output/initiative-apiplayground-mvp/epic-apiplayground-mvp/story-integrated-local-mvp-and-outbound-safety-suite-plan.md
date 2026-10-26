---
title: 'Integrated local MVP and outbound-safety suite'
type: 'feature'
ticket: '9'
created: '2026-10-26'
status: done
baseline_revision: '9b0b8d4d1afb0fea94301a242f5f4b417dac0d11'
route: 'full'
route_source: 'pinned'
risk: 'high'
review: 'thorough'
review_source: 'pinned'
lenses_ran: [blind-hunter, edge-case-hunter, verification-gap, intent-alignment]
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md'
  - '_bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md'
  - '_bmad-output/initiative-apiplayground-mvp/epic-apiplayground-mvp/story-outbound-request-safety-foundation-plan.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The MVP's request, response, History, and Collection capabilities have been implemented separately, but the complete local UI/API/PostgreSQL path has not been verified across API restart, and a distinct closeout suite has not independently verified the approved outbound safeguards.

**Approach:** Verify a real browser journey through the local UI, API, guarded outbound adapter, and disposable PostgreSQL, including response inspection, History replay, and Collection reuse after an API restart. Add a separately runnable Story 9 safety suite through the same guarded adapter and exact test-owned ephemeral target; a person separately confirms and records results.

## Boundaries & Constraints

**Always:** Preserve the production policy: HTTP/HTTPS only, public routability, validation of every DNS A/AAAA answer and pinned connection, no automatic redirects, 5-second connect timeout, 30-second end-to-end deadline including body reads, and 1 MiB response cap. Automated dispatch may reach only a test-owned ephemeral loopback endpoint through the same guard and its exact scoped test policy. Use existing migrations and disposable PostgreSQL databases with generated `apg_mig_*` names and unique `apg_role_*` logins. Admin connection targets `postgres` and is used only for harness-validated database/role lifecycle; document required admin privileges (CREATEDB plus permission to create/drop its disposable application role and create/drop databases). Create the DB admin-owned for cleanup; app login is `LOGIN NOCREATEDB NOCREATEROLE NOSUPERUSER NOREPLICATION NOBYPASSRLS`, has no memberships, and receives only target-database `CONNECT` and `public` schema `USAGE`/`CREATE` (no database-level `CREATE`). Use a fresh random credential for each run, held only in memory/process environment and never logged or written to disk; only app credentials enter API/migration process. PostgreSQL `PUBLIC` may still permit connections elsewhere, and `CREATEDB` is cluster-wide, so validate generated names in the harness and target the API connection only at the disposable database; do not claim stronger server-side scoping than PostgreSQL provides. Keep production independent of test code and run closeout separately from Story 2 tests. Record human confirmation outside automated pass/fail.

**Never:** Contact real external APIs, weaken production guard, add a runtime test-policy switch or another transport, print/persist credentials, target the application database with test-admin privileges, hand-author EF migrations, broaden product scope, or treat automated success as human confirmation. Never reuse an existing app role: create must fail on collision; remove the newly created login during cleanup. Do not give the app role cluster-level attributes or explicit privileges outside the disposable database. PostgreSQL `CREATEDB` and default `PUBLIC CONNECT` are not prefix restrictions: validate generated database and role identifiers before DDL and operate only on those exact per-run names.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|----------------------------|----------------|
| Local request journey | Browser request to owned target; app DB migrated | UI shows observed status, elapsed time, headers/body; dispatched attempt appears in History | Surface actual failure only |
| History replay | Select persisted attempt | Loading sends nothing; explicit replay creates a new attempt with restored values | Policy rejection is not sent or archived |
| Restart and Collection reuse | Save request; restart API against same disposable DB | Saved item remains available, loads fields and clears stale response without sending | Fail on lost data or implicit dispatch |
| Policy-rejected API request | Submit a disallowed local destination | Guard rejects before connection; History unchanged and target hits remain zero | Fail if a target connection or History row appears |
| Guard and transport limits | Mixed DNS answers, redirect, delayed connect/body, or streamed response at/over cap | Validate/pin every answer; do not follow redirect; enforce exact 5s/30s/1 MiB policy | Assert rejection/failure classification and zero second-target hits |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Api/Program.cs` — fixed production policy, guards, dispatch and persistence routes; reuse pipeline in test Kestrel without runtime test setting.
- `backend/src/ApiPlayground.Infrastructure/Outbound/OutboundPolicy.cs` and `GuardedTargetHttpClient.cs` — sole guard/transport; reuse exact-target test policy only in tests.
- `backend/tests/ApiPlayground.Tests/Outbound/ControlledLoopbackServer.cs` and `OutboundSafetyTests.cs` — ephemeral target and deterministic DNS/connector cases.
- `backend/tests/ApiPlayground.Tests/MigrationContinuityTests.cs` — admin lookup, disposable DB lifecycle and migration helper; extend role provisioning/cleanup.
- `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs`, `RequestCollectionTests.cs`, migrations — route/persistence patterns; PostgreSQL History durability and API restart remain uncovered.
- `backend/tests/ApiPlayground.Tests/ArchitectureBoundaryTests.cs` — existing boundary scans; add production DI/policy assertions.
- `frontend/src/App.tsx`, `tracer.ts`, `vite.config.ts` — dispatch, inspection, replay and no-send restore through fixed local Host/origin.
- Frontend manifests, `.github/workflows/ci.yml`, `README.md` — pinned browser tooling, separate E2E command/CI and safe instructions.

## Tasks & Acceptance

**Execution:**
- [x] `backend/src/ApiPlayground.Api/Program.cs` and test-host launcher in the existing test project — host real routes on Kestrel; production stays `PublicInternetOnly()`, launcher composes only exact target policy and DB stores without production runtime switches.
- [x] `backend/tests/ApiPlayground.Tests/MigrationContinuityTests.cs` and DB fixture — validate/quote generated DB/role IDs; admin on `postgres` must be `LOGIN NOSUPERUSER CREATEDB CREATEROLE NOREPLICATION NOBYPASSRLS`; preflight/assert attributes. Create admin-owned DB/login (fail on collision); assert no role membership is granted. App login: `LOGIN NOCREATEDB NOCREATEROLE NOSUPERUSER NOREPLICATION NOBYPASSRLS`, no memberships. Grant only target DB `CONNECT`, public-schema `USAGE`/`CREATE`. Assert `current_database()`/`current_user` and role ACLs. API/migrations get app credentials only. Guaranteed partial-failure cleanup drops exact DB then exact new role; document exact-ID recovery if terminated.
- [x] `backend/tests/ApiPlayground.Tests/Story9CloseoutTests.cs` — separately filter allow/deny, all-answer DNS/pinning, no-follow redirect, 5s connect, 30s total including reads, and 1 MiB boundary; loopback only.
- [x] `frontend/e2e/story9-mvp.spec.ts` and runner — own startup/readiness/shutdown of task 1's test-host launcher and child-process tree in guaranteed cleanup on pass, failure or cancellation; orchestrate DB/API/Vite/browser and verify fixed fixture response, History replay, API rejection with unchanged History/zero target hits, save/restart/reuse without implicit send.
- [x] `ArchitectureBoundaryTests.cs` — retain boundary scans; assert fixed production policy, no test setting and one guarded adapter.
- [x] Frontend manifests, browser config, CI, `README.md` — pin browser, add E2E script/CI and safe instructions without secrets.
- [x] Run isolated closeout/E2E, frontend build/tests, Release backend suite and migration continuity; require separate human confirmation, recorded below.

**Acceptance Criteria:**
- Given CI or local prerequisites, when the browser command runs, then its fixture returns HTTP 200, `application/json`, and `{"ok":true,"marker":"story9"}`; assertions compare those exact UI values and request method, URL, headers, and body with the stored History values, and the runner leaves no host/Vite child processes on pass, failure or cancellation.
- Given an existing History attempt, when browser selects then replays it, then selection adds zero target hits/History rows and replay adds exactly one of each, with identical recorded request values.
- Given saved Collection data and an API restart, when browser reloads the same DB, then the item persists, restores request fields, clears stale response, and causes zero target hits.
- Given a disallowed API target, when closeout submits it, then the guard rejects pre-connect, History is unchanged, and the owned target has zero hits.
- Given automated suite success, when a person separately reviews results, then confirmation is recorded apart from test output; otherwise story remains unconfirmed.

## Implementation Notes

- Extracted request guard and route mapping into `backend/src/ApiPlayground.Api/TracerEndpoints.cs` so the production API and a real Kestrel browser host share routes without enabling a production test-policy switch.
- Added disposable PostgreSQL database/login lifecycle with exact generated identifiers, validated non-superuser `CREATEDB`/`CREATEROLE` admin, limited app grants, in-memory random credentials, pool-specific clearing, and ordinary exact database drop. Pool clearing avoids requiring forbidden `pg_signal_backend` privileges.
- The existing persistence smoke check used a PostgreSQL TEMP table, but the approved app-role contract intentionally denies database TEMP. It now performs its round trip using a random exact-pattern public-schema table and rolls the transaction back; the regression test asserts no generated table remains.
- Added same-origin Fetch Metadata handling to the fixed local API request guard because browser proxy reads omit `Origin`. The exception requires exact `Host`, `Sec-Fetch-Site: same-origin`, GET/POST as appropriate, JSON for POST, and the request marker; other mutations still require the configured Vite origin.
- Added the real-browser Story 9 runner/spec, fixed the timing assertion to match separate DOM status spans, registered the persistence check once in the test host, and verified dispatch, response inspection, replay, rejection, and collection durability across API restart.
- Added CI and README instructions for a separately runnable safety suite and independent manual sign-off. Human confirmation has not been recorded.
- Hardened partial fixture initialization: any exception during DB/role setup now runs exact-resource cleanup; the original failure is rethrown, or aggregated with an explicit cleanup failure. Removed a duplicate app-role membership query.
- Root-level Playwright/npm manifests created during early setup were removed; Playwright/tsx remain pinned in `frontend/package.json`. Playwright output is ignored under `frontend/test-results/`.
- Verification: isolated Story9CloseoutTests passed 9/9; browser E2E passed 1/1, including a final rerun after backend cleanup hardening; full Release backend suite passed 111/111; the affected Story 9 closeout, migration-continuity, and persistence-smoke-check suites passed 15/15. The final frontend rerun passed 27/27 tests and the production TypeScript/Vite build. That rerun exposed a flaky dialog-focus assertion: replacing the passive effect with `useLayoutEffect` fixed focus during the opening commit. `git diff --check` passes. Initial test runs also exposed and fixed the local-origin guard, role/TEMP privilege mismatch, and two test assertions.
- Story 9 implementation and review are built; the user's explicit acceptance is recorded below as the separate human sign-off. Automated test success alone is not treated as sign-off.

## Plan Change Log

## Review Triage Log

| Pass | Verdict | Evidence / disposition |
|---|---|---|
| Independent plan review 1 | 1 high, 4 medium, 1 low | Added executable production DI/policy checks; separated admin and app DB roles; added API-level rejection/History assertion; specified exact safety thresholds; named independent test commands; removed response-tab claim not observable after item load/reload. |
| Independent plan review 2 | 1 medium | Clarified admin/app database ownership, schema grants, migration identity, prefix validation and cleanup boundary; no runtime admin credentials. |
| Build review — blind hunter | 10 requested; 2 relevant observations, 8 false/unactionable | False: SQL interpolation uses generated regex-validated IDs; CI jobs have independent service containers; Content-Type display and infinite host wait were explicitly declined; several claims contradicted by actual source. Low: redundant no-membership SQL query was removed. Smoke-check rollback aborts the transaction and PostgreSQL recovers it after connection/process loss. False: the intentionally partial tracer payload DTO ignores unknown API properties and does not fail. |
| Build review — edge case hunter | 1 accepted; remaining findings false | Medium, patch, fixed: catch-all cleanup now runs for any setup failure including PostgreSQL `PostgresException`; original and cleanup exceptions are preserved. Other claims were false: source files exist; SQL uses harness-generated regex-validated identifiers; CI service containers are job-isolated; JSON formatting is handled; declined host-wait item is not actioned. |
| Build review — verification gap | No qualifying gaps | Confirmed the standard backend CI excludes the parent-controlled runner and Story 9 CI invokes `npm run test:e2e`, which sets the runner directory; URL/safety/timeout/cap, DB ACLs, smoke rollback, and actual browser flow have executed. Missing browser-visible Content-Type assertion is explicitly declined. The same-origin Fetch Metadata fallback is auxiliary to the explicit-origin path and is not required by the tested Vite proxy flow. |
| Build review — intent alignment | No unresolved divergence | Implementation matches production/test policy split, disposable-role constraints, real browser journey, isolated suite, and distinct manual confirmation. The user's subsequent explicit acceptance is recorded below. |
| Explicit user dispositions | 2 findings declined | User explicitly declined adding timeout/cancellation for the browser host shutdown loop and declined adding a browser assertion for visible `Content-Type`; neither is changed or treated as blocking. |

## Design Notes

Story 9 is a separate suite/report boundary, not another transport/project. CI bootstrap may use postgres only to create the admin; harness uses only `LOGIN NOSUPERUSER CREATEDB CREATEROLE NOREPLICATION NOBYPASSRLS` on `postgres`, asserting role flags and no membership grants. `CREATEROLE` allows it to create and drop only roles it created. API/migrations get app connection only. Document exact-ID recovery, never broad prefix cleanup. PostgreSQL cannot prefix-limit `CREATEDB` or override default `PUBLIC CONNECT`; validate identifiers and use exact target DB. Reuse DB across restart; generate schema changes with `dotnet ef migrations add`.

## Verification

**Commands:**
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter FullyQualifiedName~Story9CloseoutTests` — expected: independent guarded cases pass without external dispatch.
- `npm run test:e2e` from `frontend` — expected: browser completes dispatch, response inspection, History replay, policy rejection and post-restart Collection reuse.
- `npm test` and `npm run build` from `frontend` — expected: existing tests and production build pass.
- `dotnet test backend/ApiPlayground.sln --configuration Release` with admin/app disposable-DB configuration — expected: full suite passes with no skipped DB tests.
- Migration continuity tests and `git diff --check` — expected: migration stream remains intact and no whitespace errors.

**Manual checks:**
- The confirmer, date, local verification reference and safety result are recorded below; no secrets.

**Human confirmation record:**
- Confirmer: user, via explicit chat approval (“все устраивает”; personal name not provided).
- Date: 2026-10-26.
- Verification run reference: local verification completed in this session; no CI run ID was issued.
- Safety result: user accepted the completed Story 9 results: closeout/browser/backend/frontend checks passed; automated dispatch used only the test-owned ephemeral loopback target, with no external API calls. No unresolved blockers were raised.
