---
title: 'Characterize the contract and migrate tracer dispatch to a controller'
type: 'refactor'
ticket: '2'
created: '2026-10-28'
status: done
baseline_revision: 'bc708d2cb01af9334876cc997c7b69caef5fd878'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Application endpoints currently use Minimal APIs, while the approved architecture requires attribute-routed controllers. The tracer dispatch endpoint also combines HTTP validation and response translation with routing, making its behavior easy to change accidentally during migration.

**Approach:** Characterize the existing `POST /api/tracer/get` contract, then move only this application action to an attribute-routed controller. Keep it delegated through the existing `GetTracerUseCase` and registered guarded outbound executor; preserve validation, guard, dispatch, smoke-check, status, and response semantics.

## Boundaries & Constraints

**Always:** Keep route `POST /api/tracer/get`, middleware order, guard behavior, accepted fields, validation messages/statuses, outbound policy, dispatch/history behavior, persistence smoke-check result and failure behavior, cancellation outcomes, and response fields/statuses unchanged. The controller must use the existing application use case; outbound dispatch is not called directly from the API action. Keep `GET /health` and all other routes unchanged in this story.

**Never:** Do not migrate other endpoints, standardize error bodies in this story, change the Problem Details contract, alter schema/migrations, modify frontend contracts, weaken tests/guards, add a new outbound path, or refactor the smoke check.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Valid dispatch | JSON with valid HTTP(S) URL and supported method, authorized request headers | Existing rich tracer-result JSON and HTTP status; use the registered guarded executor, preserve dispatch and history results | No new error handling; existing use-case classification remains |
| Invalid request values | Missing/blank/too-long URL, unsupported method, invalid URI, forbidden body/method pairing, malformed JSON body, duplicate or managed headers | Existing validation response body/message and HTTP 400; outbound executor is not invoked | Preserve current validation precedence and response shape |
| Guard rejection | Invalid host/origin/content type/request marker or oversized body on tracer path | Existing 403 JSON or bodyless 413 and status; no dispatch | Preserve path-prefix and metadata guard behavior |
| Outbound policy rejection | Request passes API validation but the guarded executor rejects destination | Existing rich tracer outcome body and HTTP 403 | Preserve policy-failure fields and status |
| Outbound timeout/transport failure | Executor returns existing timeout or transport failure outcome | Existing rich result body and 504/502/499 status mapping | Preserve cancellation vs timeout distinction |
| Successful target response | Guarded executor returns a response, including a target HTTP error status | Application response remains HTTP 200 with the target status and existing result fields; smoke-check semantics unchanged | History persistence failure remains reported by existing result, without replacing dispatch outcome |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Api/Program.cs` — composition root; currently registers services, builds the app, installs `UseRequestGuard()`, then calls `MapTracerEndpoints()`. Add MVC services and controller mapping without changing guard ordering.
- `backend/src/ApiPlayground.Api/TracerEndpoints.cs` — owns guard middleware and current mappings; keep `UseRequestGuard()`, `GET /health`, and unrelated route mappings, remove only the `POST /api/tracer/get` mapping and retain/move its request record as needed.
- `backend/src/ApiPlayground.Application/Outbound/GetTracerUseCase.cs` — existing application boundary for validation-adjacent dispatch/history/smoke-check behavior; controller must delegate through `ExecuteAsync()`.
- `backend/src/ApiPlayground.Infrastructure/DependencyInjection.cs` and `Outbound/GuardedTargetHttpClient.cs` — registered guarded transport and policy implementation; do not bypass or alter in this story.
- `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` — WebApplicationFactory coverage for guard, request validation, dispatch, statuses, and response fields; extend for the migrated action and binding edge cases.
- `backend/tests/ApiPlayground.Tests/TracerPersistenceSmokeCheckTests.cs` — preserves the smoke-check contract; no smoke-check code change is intended.
- `backend/tests/ApiPlayground.Tests/Story9BrowserHostRunnerTests.cs` — separately built host currently invokes `MapTracerEndpoints()`; update its endpoint registration to discover/map controllers while keeping this runner opt-in and excluded from CI.
- `backend/tests/ApiPlayground.Tests/ArchitectureBoundaryTests.cs` — scans runtime source for direct outbound dispatch and checks dependency boundaries; the controller remains subject to these checks.
- `frontend/src/tracer.ts` — current request/response contract; no change is expected.

## Tasks & Acceptance

**Execution:**
- [ ] `TracerEndpoints.cs` — preserve guard middleware and unrelated route maps while removing only the tracer-dispatch Minimal API route; keep request guard coverage on the path prefix and endpoint metadata as appropriate.
- [ ] `Program.cs` — register controller services and map attribute-routed controllers after the request guard, preserving existing route behavior and health mapping.
- [ ] `TracerRequestController.cs` — add a thin attribute-routed action for `POST /api/tracer/get`, retaining request validation, status/JSON projection, and delegation to `GetTracerUseCase` without direct transport access.
- [ ] `TracerEndpointTests.cs` — characterize route, guard, validation (including malformed/empty/null/wrong-type JSON binding cases), valid dispatch, target-error response, failure status mapping, and result fields; assert rejected requests do not dispatch.
- [ ] `Story9BrowserHostRunnerTests.cs` — register/map controllers in its isolated test host so the opt-in browser runner remains functional.
- [ ] `ApiPlayground.Api.csproj` — no package change expected; verify the existing Web SDK supplies MVC/controller support.

**Acceptance Criteria:**
- Given a valid tracer request, when it is posted to `/api/tracer/get`, then the same outbound executor is used and the existing status and JSON fields are returned.
- Given any existing API validation failure, when the request is posted, then the existing HTTP 400 body/message and validation precedence are preserved and dispatch does not begin.
- Given an invalid tracer guard condition or oversized request, when the request is posted, then the existing 403 JSON or bodyless 413 response occurs before the action and no dispatch begins.
- Given policy rejection, timeout, cancellation, transport failure, or a target response, when the use case returns its outcome, then the existing endpoint status mapping and rich result body remain unchanged.
- Given a successful outbound response and smoke-check result, when dispatch completes, then history eligibility and persistence-smoke-check outcome are unchanged.
- Given another application route or `GET /health`, when it is requested, then its route and response remain unchanged by this story.

## Implementation Notes


## Plan Change Log


## Review Triage Log


## Design Notes

Controller model binding can introduce automatic behavior (especially with `[ApiController]`) that differs from Minimal API binding. Preserve observable request behavior deliberately: either avoid automatic API-controller responses where they would alter the contract, or add explicit tests and match the existing 400/status/body semantics before relying on framework defaults. Keep the existing guard path-prefix check; controller metadata alone is insufficient for malformed or unmatched tracer paths.

## Verification

**Commands:**
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName~TracerEndpointTests|FullyQualifiedName~TracerPersistenceSmokeCheckTests|FullyQualifiedName~ArchitectureBoundaryTests"` — expected: all focused tests pass; database-dependent tests require `APIPLAYGROUND_TEST_ADMIN_CONNECTION` when configured.
- `dotnet build backend/ApiPlayground.sln --configuration Release` — expected: successful Release build.
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName!~Story9BrowserHostRunnerTests"` — expected: CI-equivalent backend suite passes when PostgreSQL test prerequisites are configured.
