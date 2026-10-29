---
title: 'Standardize validation and application errors on Problem Details'
type: 'refactor'
ticket: '8'
created: '2026-10-29'
status: done
baseline_revision: 'd3fa2264b39801f606ad3aa3d52fa98fee0c9e31'
route: 'full'
route_source: 'pinned'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Application validation and unexpected-failure responses still mix ad hoc `{error}` objects, empty 400 bodies, collection-specific Problem Details, and unhandled framework errors. Consumers need a consistent, safe RFC 9457 contract without losing the existing statuses or intentionally distinct guard, not-found, and rich tracer-failure outcomes.

**Approach:** Standardize validation, application-failure, and unexpected-error response bodies on Problem Details, retaining current status codes and safe details. Add centralized exception handling for otherwise unhandled errors, update frontend error parsing to consume `detail`, and characterize preserved response classes.

## Boundaries & Constraints

**Always:** Use `application/problem+json` Problem Details for in-scope validation, application, and unexpected errors, following RFC 9457 fields; retain current HTTP statuses by default. Keep guard responses exactly as `{error}` 403 and bodyless 413; keep bodyless history 404 and malformed/null request-binding 400 behavior if classified as binding rather than validation; preserve tracer outbound rich-result fields and statuses (including policy rejection 403 and 499/502/504 failures). Sanitize unexpected errors: no exception, provider, credential, or sensitive request details in client responses. Keep existing validation order/messages as safe Problem Details `detail`; keep collection storage details sanitized. Frontend should display useful `detail` and remain compatible with existing `error` responses and rich tracer outcomes. Preserve routes, request behavior, persistence, guard ordering, and cancellation semantics except error body standardization explicitly approved here.

**Never:** Do not change HTTP statuses or business/validation rules; do not convert guard, bodyless not-found, or rich tracer-failure responses to Problem Details. Do not expose internal exception messages, stack traces, database/provider names, secrets, or credentials. Do not normalize cancellation into a different status/body beyond established current handling. Do not add a new API endpoint, feature, mandatory E2E CI gate, or formatting CI gate. Avoid `[ApiController]` auto-validation if it changes current binding outcomes; do not duplicate exception catches or silently swallow faults.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Tracer validation | Guard-approved invalid request; valid JSON with invalid fields | Existing `400` status and message in RFC 9457 Problem Details `detail` | No dispatch; retain validation order |
| Collection validation | Invalid saved-request command | Existing `400`, Problem Details with safe validation detail | No internal details |
| Collection persistence failure | Store fails on list/save | Existing `503` and sanitized Problem Details detail | No provider/exception leakage |
| Unhandled application failure | History store or unexpected controller exception | Existing framework status (normally `500`) with generic sanitized Problem Details | Central handler logs safely; no raw exception response |
| Excluded outcomes | Guard rejects, history ID absent/malformed, outbound rich failure | Existing 403 `{error}`, bodyless 413/404, or rich tracer JSON/status unchanged | Excluded from global response rewriting |
| Frontend error display | Problem Details or legacy `{error}` response | Show `detail` or `error`, falling back to HTTP status text | Preserve rich tracer response handling |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Api/TracerRequestController.cs` — `Get()` owns ordered request validation and empty malformed/null binding 400s; convert validation responses, not outbound rich results. Preserve 499/502/504 and policy 403 payloads.
- `backend/src/ApiPlayground.Api/TracerCollectionsController.cs` — `Save()` handles malformed/null JSON, validation `ArgumentException`, and broad sanitized 503; `List()` also maps failures to 503. Centralize reusable Problem Details creation without changing these statuses/details or cancellation catch semantics unintentionally.
- `backend/src/ApiPlayground.Api/TracerHistoryController.cs` and `Program.cs` — history store failures currently escape; add a sanitized exception boundary for unexpected errors while preserving `Find()`'s bodyless 404 and existing guard middleware order.
- `backend/src/ApiPlayground.Api/TracerEndpoints.cs` — `UseRequestGuard()` creates excluded 403/413 bodies before controller execution; do not rewrite these responses.
- `frontend/src/tracer.ts` — `sendTracerRequest()` currently reads only `error` for non-rich failures; `saveTracerRequest()` already accepts `error`/`detail`; history/collection lists only report HTTP status. Extract a safe shared response-message parser as justified.
- `frontend/src/App.tsx` and `frontend/src/App.test.tsx` — user-facing alerts consume thrown messages; update/add tests for Problem Details `detail` and rich tracer response behavior.
- `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` — existing validation, collection failure, guard, binding, history exception, and outbound failure tests; update error-contract assertions and add sanitized unexpected-error coverage while retaining exclusions.
- `frontend/package.json` — `test` and `build` scripts provide validation; no new dependencies expected.
- `_bmad-output/initiative-apiplayground-backend-refactor/spec-backend-refactor/technical-requirements.md` and `.../epic-backend-refactor.md` — approved RFC 9457 scope: validation/application/unexpected only; preserve statuses and guard, not-found, rich tracer bodies.

## Tasks & Acceptance

**Execution:**
- [x] `TracerRequestController.cs` and API error contract/helper — return RFC 9457 Problem Details for every validation response with current 400 and safe existing detail; retain empty 400 for malformed/null body binding unless tests/evidence classify differently.
- [x] `TracerCollectionsController.cs` and central API exception handling/composition — standardize validation and sanitized 503 responses, centralize unexpected errors safely, preserve collection details/statuses and exclusions; avoid duplicate broad catches.
- [x] `Program.cs` and relevant middleware/error helper — register/configure the handler so unhandled application failures return sanitized Problem Details at existing status without intercepting guard or bodyless route-miss outcomes.
- [x] `frontend/src/tracer.ts` and `App.test.tsx` — parse RFC 9457 `detail` for tracer and collection failures, retain legacy `error`/fallbacks and rich tracer handling.
- [x] `TracerEndpointTests.cs` — verify RFC content type/fields/status and safe details for validation, collection failures and unexpected errors; retain exact guard, bodyless 404/413, binding and rich outbound response assertions.

**Acceptance Criteria:**
- Given an in-scope validation error, when the API responds, then status is unchanged and the body is RFC 9457 Problem Details with a safe useful detail.
- Given an unexpected exception, when the request is processed, then the existing error status is retained, the body is sanitized Problem Details, and internal exception text is not disclosed.
- Given an excluded guard rejection, missing history resource, or rich outbound failure, when it occurs, then its current response body shape and status remain unchanged.
- Given a Problem Details response from tracer or collection operations, when the frontend receives it, then the user sees its `detail`; legacy `error` and rich tracer payloads remain supported.
- Given the backend and frontend verification commands, when they run, then focused API tests, backend CI-equivalent tests, frontend tests, and frontend build pass.

## Implementation Notes

- Added `ApiControllerBase` (`BadRequestProblem`, `ServiceUnavailableProblem`) for application Problem Details. Validation 400s in `TracerRequestController` and `TracerCollectionsController` use it; messages and order are unchanged.
- Added `UnhandledExceptionHandler` (`AddExceptionHandler`/`UseExceptionHandler`, placed before `UseRequestGuard()`). It returns 500 with a generic Problem Details body and logs only the exception type, method, path, and trace id via `ApiLog`. A cancelled request returns `false` so cancellation semantics are unchanged.
- Kept bodyless binding 400s, guard 403 `{error}` and 413, bodyless history 404, and rich tracer outcomes unchanged. Route misses stay bodyless (covered by `Unknown_non_tracer_routes_remain_bodyless_not_found`).
- Collection `Save()` and `List()` 503s return sanitized Problem Details. Collection cancellation behavior is preserved (the broad catch still maps to 503).
- Frontend `tracer.ts` reads bodies as text, so empty and non-JSON bodies do not throw. Messages come from `detail`, then legacy `error`, then the existing HTTP-status fallback.
- Residual risk: `ArgumentException.Message` is still returned as the 400 `detail` for collection save. Non-validation `ArgumentException` text could reach clients (pre-existing behavior).
- Residual risk: an earlier `dotnet run --no-build` exited with code 1. It was not investigated; the test and build runs passed.


## Plan Change Log

- Implementation pass: all five Execution tasks completed. Backend build 0 errors and 0 warnings. Targeted backend tests 84 passed. Full backend suite excluding `Story9BrowserHostRunnerTests` 182 passed. Frontend `npm test -- --run` 30 passed in 2 files. Frontend `npm run build` succeeded. `git diff --check` clean. No commits, staging, or migrations.
- Review pass (quick lens): 5 findings triaged. Patched rows 2 (`BadHttpRequestException` returns `false` so framework status is kept) and 3 (null-body guard in `saveTracerRequest`). Rows 1 deferred as pre-existing; rows 4 and 5 rejected as low. Re-verified: backend build 0 errors; backend suite excluding `Story9BrowserHostRunnerTests` 182 passed; frontend tests 30 passed; frontend build succeeded; `git diff --check` clean.


## Review Triage Log

| # | Lens | Finding | Verdict | Route | Evidence / Action |
|---|------|---------|---------|-------|-------------------|
| 1 | quick | Collection save returns `ArgumentException.Message` as 400 `detail`, which may expose non-literal text | medium | defer | Pre-existing: the old code returned `BadRequest(new { error = exception.Message })` with the same exposure. The change keeps the existing residual risk already recorded in Implementation Notes. Application validation throws literal messages; the leak needs a non-validation `ArgumentException` escaping the store. Revisit in a separate ticket that narrows the validation exception type. |
| 2 | quick | `UnhandledExceptionHandler` maps every exception to 500, including `BadHttpRequestException` (413) from an oversized chunked body | medium | patch | Verified in code: the guard checks only `ContentLength > 4096`, so chunked bodies pass. Kestrel's body-size limit throws `BadHttpRequestException` during read. The new handler converted it to 500, a regression this change introduced. Patch: `if (exception is BadHttpRequestException) return false;` restores framework status handling. Not covered by a test: `TestServer` does not enforce the Kestrel body limit. |
| 3 | quick | `saveTracerRequest` returns `null` for an empty or non-JSON 2xx body; UI shows "Request saved" | low | patch | Verified in `tracer.ts`: `readBody` returns `null`, and the function returned `body as SavedRequest` without a check. Before the change, `response.json()` threw. Patch: throw when `body` is not an object. Kept under the low rule because the fix is a one-line guard. |
| 4 | quick | Cancellation branch in `UnhandledExceptionHandler` has no test | low | reject | Code preserves the established cancellation behavior (returns `false`). The finding is a missing test, not a defect. Adding coverage is more than a direct correction. |
| 5 | quick | `Response.StatusCode = 500` is set before `TryWriteAsync`; status can change before rethrow | low | reject | Needs `TryWriteAsync` to return `false` or the response to have started. The default JSON writer does not return `false` here. Fixing it needs a new `HasStarted` branch, which is more than a direct correction. |

Routing summary: no `bad_plan` or `intent_gap`. Patches for rows 2 and 3 applied directly by the owner-session agent (the implementation subagent was not re-addressable). Loopback count stays 0.

## Design Notes

Prefer ASP.NET Core's supported Problem Details and exception-handler mechanisms, but explicitly limit behavior to intended response classes. `AddProblemDetails()` alone does not transform explicit `BadRequest(...)` results or catch exceptions; verify the exception boundary is configured. MVC JSON formatting and `HttpJsonOptions` used for manual body deserialization are distinct. Keep malformed/null request binding behavior characterized. Do not use blanket status-code-page rewriting that could turn route misses or guard responses into Problem Details. Logs must not include raw exception details or sensitive request content.

## Verification

**Commands:**
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName~TracerEndpointTests|FullyQualifiedName~HealthEndpointTests"` — expected: API error classes and exclusions pass.
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName!~Story9BrowserHostRunnerTests"` — expected: full CI-equivalent backend suite passes; configured PostgreSQL prerequisites are required.
- `dotnet build backend/ApiPlayground.sln --configuration Release` — expected: Release solution build succeeds.
- `npm test -- --run` from `frontend` — expected: frontend tests pass.
- `npm run build` from `frontend` — expected: TypeScript and Vite production build succeed.
- `git diff --check` — expected: clean diff.

**Results (after review patches):**
- `dotnet build backend/ApiPlayground.sln --configuration Release` — 0 errors.
- Backend suite excluding `Story9BrowserHostRunnerTests` — 182 passed, 0 failed.
- Frontend `npm test -- --run` — 30 passed in 2 files.
- Frontend `npm run build` — succeeded.
- `git diff --check` — clean.
- Not run: `Story9BrowserHostRunnerTests` (excluded by project default).
- Not covered by a test: the `BadHttpRequestException` pass-through (Kestrel body limit) and the null-body save guard.
