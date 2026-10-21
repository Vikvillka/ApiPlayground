---
title: 'Guarded GET tracer'
type: 'feature'
ticket: '3'
created: '2026-10-20'
status: built
baseline_revision: 'e9f1fb05c10d4e36e5abd67e98f46adb1ba9afef'
route: 'full'
route_source: 'auto'
risk: 'high'
review: ''
review_source: ''
lenses_ran: []
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md'
  - '_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md'
  - '_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/DESIGN.md'
  - '_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The local React scaffold cannot make a user-configured GET through the API, so the MVP's first visible request/response flow is missing. Enabling browser-triggered dispatch without request-origin protections would expose the local API to cross-site request abuse.

**Approach:** Connect the cold-open GET workspace to one API/Application use case and the existing guarded outbound port, showing a minimal observed result while keeping the full request editor, response viewer, and History out of scope. Use a same-origin Vite development proxy and strict loopback API Host/Origin checks; test browser-to-API protections and the guarded GET with an explicitly scoped test-owned loopback server, while runtime outbound policy remains the already approved public-internet-only policy.

**Decision (2026-10-20):** The UI reaches the API through the Vite same-origin development proxy; no direct cross-origin browser API configuration or permissive CORS policy is added. The request-triggering API accepts only `Host: localhost:5000` and the exact configured UI Origin `http://127.0.0.1:5173`; `localhost:5173`, alternate Host spelling/ports, missing Origin, `null` Origin, and unrecognized Origin are rejected. No cookie authentication is introduced; the route requires JSON and a non-simple custom request header in addition to these checks. The browser-triggered product path uses the public-only outbound policy from ticket 2. Automated controlled-GET tests may substitute only the existing exact-endpoint test policy and test-owned ephemeral loopback server through DI; no loopback target is allowed in normal runtime configuration.

</frozen-after-approval>

## Code Map

- `frontend/src/App.tsx` — placeholder UI; replace only with the initial GET workspace and minimal request/result states, reusing the cold-open layout and design tokens.
- `frontend/src/main.tsx`, `frontend/src/platform.ts`, `frontend/vite.config.ts`, `frontend/package.json` — app bootstrap, app name, Vite/Vitest configuration and scripts; add same-origin `/api` proxy (preserve browser Origin and target Host) and focused tests without adding a UI framework.
- `backend/src/ApiPlayground.Api/Program.cs` — currently maps only `/health`; compose guard, request-protection configuration/middleware, and tracer route here without exposing a second outbound transport.
- `backend/src/ApiPlayground.Application/Outbound/` — existing `IOutboundRequestExecutor`, `OutboundRequest`, and `OutboundResult`; add a focused GET tracer use case and transport-neutral result mapping, leaving the guard as the only dispatch path.
- `backend/src/ApiPlayground.Infrastructure/DependencyInjection.cs` and `Outbound/OutboundPolicy.cs` — explicit guarded executor registration and `PublicInternetOnly()` policy. API production DI must register this policy; tests can replace it only with the existing internal test policy through test assembly access.
- `backend/src/ApiPlayground.Api/Data/PlatformDbContext.cs` and `backend/src/ApiPlayground.Infrastructure/` — persistence context and migrations exist, but no marker port/model exists. Implement the required non-sensitive smoke marker through an Application-owned port; scope insert/read/delete to one transaction and avoid a permanent product table unless existing provider behavior prevents a safe temporary-table check.
- `backend/tests/ApiPlayground.Tests/HealthEndpointTests.cs`, `Outbound/ControlledLoopbackServer.cs`, and `Outbound/OutboundSafetyTests.cs` — reuse `WebApplicationFactory`, ephemeral Kestrel target, and transport policy tests; add endpoint Host/Origin/header rejection and end-to-end guarded GET/persistence assertions.
- `_bmad-output/initiative-apiplayground-mvp/epic-apiplayground-mvp/tickets.toml` — ticket 1.3 verification includes cold-open state, browser protections, controlled GET, policy rejection/not-sent result, and temporary marker lifecycle; do not edit the ticket leaf.

## Tasks & Acceptance

**Execution:**
- [x] `frontend/vite.config.ts` — proxy `/api` to the documented loopback API address; keep browser calls same-origin and avoid permissive CORS.
- [x] `backend/src/ApiPlayground.Api/Program.cs` and application request-protection code — register production public-only guard and exact configured Host/Origin/JSON/custom-header validation; reject missing, `null`, and untrusted origins before invoking Application.
- [x] `backend/src/ApiPlayground.Application/` and `backend/src/ApiPlayground.Api/Program.cs` — define and expose one minimal GET tracer use case/route through `IOutboundRequestExecutor`; preserve HTTP response versus pre-dispatch policy rejection versus timeout/transport outcomes.
- [x] `backend/src/ApiPlayground.Application/` and `backend/src/ApiPlayground.Infrastructure/` — implement a transactional non-sensitive persistence smoke marker insert/read/delete using an Application-owned port and no permanent product data table if a transaction-scoped PostgreSQL temporary table suffices.
- [x] `frontend/src/App.tsx` and frontend tests — implement initial GET, blank URL, disabled Send, sending/result/error states, and a minimal observed status/body result; no full tabs, methods editor, History, or Collections.
- [x] `backend/tests/ApiPlayground.Tests/` and `frontend/src/` tests — verify Host/Origin/CSRF-style request controls, no dispatch on rejection, exact test-target GET through the production guard, marker lifecycle, and cold-open UI behavior; tests must not contact public services.
- [x] `README.md` — document local Vite proxy/API configuration and safe startup without adding secrets or alternate dispatch paths.

**Acceptance Criteria:**
- Given a fresh UI, when it opens, then GET is selected, URL is blank, Send is disabled, and the response invites URL entry.
- Given an untrusted Host/Origin, absent or `null` Origin, non-JSON or missing custom-header request, when the tracer route is called, then it rejects before invoking the outbound port and records no send.
- Given an allowed same-origin browser request, when a controlled GET is exercised in tests, then it passes through the single guard to the exact test-owned loopback endpoint and the UI can show a minimal observed result; production policy remains public-only.
- Given a policy rejection, when returned by the outbound guard, then the UI says the request was not sent and does not present it as a target response.
- Given the smoke persistence check, when it runs, then a non-sensitive marker is inserted, read back, and removed atomically, leaving no marker or permanent product table behind.
- Given a normal runtime deployment, when dependencies/routes are inspected, then no unguarded HTTP transport, alternate dispatch route, permissive CORS rule, or test-only loopback policy is available.

## Open Questions

## Implementation Notes

- Added the Application tracer use case and persistence port. Infrastructure implements the smoke check using a transaction-scoped PostgreSQL temporary table and removes the marker before commit.
- API composition root registers `PublicInternetOnly()` and one POST tracer endpoint. Exact Host/Origin, JSON content type, and custom header are validated before endpoint execution; policy rejection is distinct from timeout/transport failure.
- Tracer endpoint now enforces a 4 KiB request-body limit (including chunked bodies) and a 2048-character URL limit before outbound execution. Vite rewrites the proxied Host to `localhost:5000` while preserving the browser Origin. UI clears previous results before a new send and locks URL/submission during the request.
- The browser request protections are attached to the matched tracer endpoint metadata rather than an exact path string, so ASP.NET route path variants cannot bypass the guard.
- Vite binds to `127.0.0.1` and proxies `/api` to `http://localhost:5000`; the React page has a minimal GET-only workspace matching cold-open and displays observed response or not-sent/failure states.
- Backend endpoint tests use the existing internal exact-endpoint policy and owned loopback server; security rejection tests assert the outbound port is never invoked. Frontend tests use mocked fetch and never access external APIs.
- The real PostgreSQL smoke-check adapter is exercised against a uniquely named disposable database; the test checks the transaction-scoped temp table is absent in the same PostgreSQL session after commit.
- Build scope remains ticket 1.3 only; no History, full methods editor, response tabs, or product persistence schema was added.

## Execution Record

- Frontend tests passed: 5/5; Vite/TypeScript production build passed.
- Backend suite passed after request/persistence additions; the real smoke adapter test passed against a disposable PostgreSQL database. All dispatch coverage uses the owned ephemeral loopback target; no public services are contacted.

## Plan Change Log

- Review remediation (2026-10-21): bounded request bodies and URL length; corrected proxy Host rewriting; added real disposable-PostgreSQL smoke-check coverage and stale/concurrent UI submission coverage. Frozen intent and security decisions unchanged.

## Review Triage Log

- Finding: Vite forwarded a Host rejected by the API allowlist. Verdict: fixed. Route: proxy `changeOrigin: true`; endpoint regression test covers accepted target Host plus exact browser Origin. Note: test validates the API guard values, not an end-to-end running Vite process.
- Finding: tracer JSON bodies and URLs were unbounded. Verdict: fixed. Route: 4 KiB Kestrel request-body feature plus Content-Length precheck; 2048-character URL maximum. Evidence: oversized body returns 413 and oversized URL returns 400 without executor calls.
- Finding: actual PostgreSQL temp-table smoke-check lifecycle lacked integration coverage. Verdict: fixed. Route: adapter test uses unique disposable DB and checks `pg_temp` relation absence in same session after commit.
- Finding: failed send could leave stale result. Verdict: fixed. Route: clear result at new send; sequential success/failure UI test verifies only current error remains.
- Finding: overlapping submissions could race. Verdict: fixed. Route: submit guard plus disabled URL input during in-flight request; UI test verifies second submit does not dispatch another fetch.
- Finding: path case/trailing-slash variants could bypass path-specific middleware. Verdict: fixed. Route: mark the mapped tracer endpoint with guard metadata and apply middleware checks to the matched endpoint; regression tests cover trailing slash and uppercase path.
- Independent follow-up noted the disposable DB test must check the same PostgreSQL session because temporary tables are session-scoped. Verdict: fixed by keeping the test DbContext connection open and checking `pg_temp` after adapter completion.

## Design Notes

The browser-origin check, Host allowlist, JSON requirement, and custom header form layered protections rather than relying on CORS. Test-only loopback authorization is only available to the test assembly and must remain injected into the same guard implementation used at runtime. A successful HTTP error status is still an observed target response; only policy rejection means the request was not sent.

## Verification

**Commands:**
- `npm test` and `npm run build` from `frontend` — 5 tests pass; production build passes.
- `dotnet test backend/ApiPlayground.sln --configuration Debug` — 71 tests passed, 0 failed, 0 skipped, including endpoint security, route variants, use-case, disposable PostgreSQL persistence lifecycle, and existing safety tests. A Release rebuild was blocked by the user's running API process holding Release assemblies; final complete suite passed in Debug.
- `dotnet build backend/ApiPlayground.sln --configuration Release` — all backend projects compile.
- `git diff --check` — no whitespace errors.

**Manual checks:**
- Start the API and Vite proxy locally; verify the initial workspace and only a configured allowed request can invoke the API route.
