---
title: 'Share only common request validation'
type: 'refactor'
ticket: '3'
created: '2026-10-28'
status: 'built'
baseline_revision: 'b9f3e735579c117c0a268845471193873ee780b3'
route: 'full'
route_source: 'pinned'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick', 'security-contract-audit']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Tracer dispatch and saved-collection workflows repeat a few genuinely common request-validation predicates, but their surrounding rules, validation order, and HTTP outcomes differ. Broad consolidation risks changing which errors users see and how workflow failures are reported.

**Approach:** Share only the proven common supported-method, absolute HTTP(S) URL shape, and case-insensitive duplicate-header checks while keeping caller-specific validation, ordering, messages, HTTP mapping, cancellation, dispatch/history outcomes, and persistence smoke-check placement unchanged.

## Boundaries & Constraints

**Always:** Preserve exact accepted methods (`GET`, `POST`, `PUT`, `PATCH`, `DELETE`), HTTP(S) URI requirements, credential/fragment rejection, and case-insensitive duplicate detection. Each workflow keeps its existing validation order, blank/length handling, error message, whitespace-body semantics, tracer managed-header rules, request/response contract, and exception mapping. Preserve `GetTracerUseCase` dispatch/history/smoke-check sequencing and cancellation behavior.

**Never:** Do not merge the workflows, centralize all validation, move validation to a different layer, alter public messages/statuses/body shape, change persistence smoke-check location/trigger/semantics, rewrite cancellation/error handling, or modify outbound transport, API routes, persistence schema, or frontend contracts.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Supported method | Exact uppercase supported method | Both use cases accept the common method predicate; caller-specific later checks still apply | Preserve caller mapping |
| Unsupported method | Unsupported or lowercase method | Both reject at their existing validation point with their existing message | Tracer HTTP 400; collection `ArgumentException` mapped to existing 400 |
| URL shape | Absolute HTTP(S) URI without credentials or fragment | Shared predicate agrees across callers; blank/length checks and messages remain local | Preserve error precedence and message |
| Duplicate headers | Names equal ignoring case | Existing callers reject duplicates; collection blank-name and tracer managed-header behavior remain separate | Preserve caller-specific error mapping |
| Body/cancellation/smoke-check | Whitespace body or cancellation/failure around dispatch and persistence check | Existing body semantics, dispatch/history outcome, and per-request smoke-check result remain unchanged | Preserve 499/502/504/rich-result mappings and existing cancellation propagation |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Api/TracerRequestController.cs` — HTTP-owned tracer validation and HTTP response translation. Preserve binding, validation order, messages, status/body mapping, and keep only shared pure predicates at the current call sites.
- `backend/src/ApiPlayground.Application/Outbound/RequestCollections.cs` — `SaveRequestUseCase.SaveAsync()` owns collection-specific validation, whitespace-body behavior, persistence call, and application errors; preserve order and collection-specific rules.
- `backend/src/ApiPlayground.Application/Outbound/GetTracerUseCase.cs` — dispatch/history/smoke-check orchestration; do not move the smoke-check or alter cancellation and failure semantics.
- `backend/src/ApiPlayground.Infrastructure/Outbound/PostgresTracerPersistenceSmokeCheck.cs` — per-request transactional round-trip; relocation is not safe here because it would change timing and response effects.
- `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` — existing assertions cover tracer validation messages/no-dispatch, failure mappings, and persistence/history outcomes.
- `backend/tests/ApiPlayground.Tests/RequestCollectionTests.cs` — collection redaction, invalid request rejection, and persistence behavior; retain collection-specific validation expectations.
- `backend/tests/ApiPlayground.Tests/TracerPersistenceSmokeCheckTests.cs` — successful round-trip/rollback contract; keep placement and behavior unchanged.
- `backend/tests/ApiPlayground.Tests/Outbound/OutboundSafetyTests.cs` — actual caller cancellation coverage at DNS/body stages; no transport changes are intended.
- `_bmad-output/initiative-apiplayground-backend-refactor/spec-backend-refactor/technical-requirements.md` — require coherent ownership and behavior preservation; smoke-check semantics are protected.

## Tasks & Acceptance

**Execution:**
- [x] `RequestCollections.cs` or a narrowly scoped Application validation file — expose only shared pure predicates for supported methods, common HTTP(S) URI shape, and case-insensitive duplicate header names; do not own messages, ordering, or endpoint-specific conditions.
- [x] `TracerRequestController.cs` and `RequestCollections.cs` — use the shared predicates at the existing validation points without moving checks or changing remaining caller logic.
- [x] `TracerEndpointTests.cs` and `RequestCollectionTests.cs` — retain and extend characterization of precedence, messages, whitespace-only bodies, blank/managed headers, and no-dispatch/no-persistence on rejection as needed.
- [x] `TracerPersistenceSmokeCheckTests.cs` and `GetTracerUseCase.cs` — make no behavior/placement change; verify cancellation, dispatch/history and smoke-check outcomes remain covered and unchanged.

**Acceptance Criteria:**
- Given supported and unsupported method values, when either workflow validates them, then both share the same method predicate while retaining their existing messages, order, and error mapping.
- Given a URL with invalid common URI shape, when either workflow validates it, then the common predicate agrees while each caller preserves its blank/length checks, validation precedence, and message.
- Given header names that collide case-insensitively, when either workflow validates them, then both reject duplicates while preserving collection blank-name and tracer managed-header behavior.
- Given whitespace-only bodies or multiple simultaneous invalid inputs, when either workflow validates them, then the existing caller-specific result and validation precedence are unchanged.
- Given cancellation, outbound failure, history persistence failure, or smoke-check failure, when tracer dispatch runs, then dispatch/history eligibility and persistence verification semantics remain unchanged.

## Design Notes

The safe common seams are pure predicates only: method membership, common absolute HTTP(S) URI shape, and case-insensitive duplicate header detection. Collection treats any nonempty GET/DELETE body as invalid and skips parsing whitespace-only JSON; tracer has different whitespace semantics. Tracer also rejects managed headers and maps validation to HTTP responses, whereas collection combines blank/duplicate header rejection and maps `ArgumentException` to its response. Keep those differences at the caller.

The smoke-check currently runs after outbound dispatch and history handling on successful outbound results; it affects the rich result payload. Moving it to startup/health/background work changes timing and observable outcomes, so it remains in `GetTracerUseCase`.

## Implementation Notes

Implemented a pure shared `RequestValidation` helper in the Application outbound namespace for exact supported methods, common HTTP(S) URI shape, and case-insensitive duplicate header names. The tracer controller and collection use these predicates at their original validation points; caller-owned messages, order, blank and length checks, body/header behavior, mapping, and tracer orchestration remain local. Added caller-facing characterization for supported/unsupported methods, URL shape and precedence, duplicate/blank/managed headers, whitespace bodies, and persistence prevention.

Focused story tests passed (72/72); CI-equivalent backend suite excluding the opt-in browser runner passed (158/158); outbound/history suites passed (58/58); Release build and `git diff --check` passed. Two independent quick and contract-focused reviews found no issues. No smoke-check relocation or behavior change was made.

## Plan Change Log


## Review Triage Log

- Quick review: no findings. Complete diff, including the shared helper, was reviewed; all stated acceptance criteria and behavior-preservation boundaries are met.
- Independent validation-contract audit: no findings. Existing caller order/messages/body/header semantics and tracer error mappings remain intact; `GetTracerUseCase` and smoke-check behavior are unchanged.

## Verification

**Commands:**
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName~TracerEndpointTests|FullyQualifiedName~RequestCollectionTests|FullyQualifiedName~TracerPersistenceSmokeCheckTests"` — expected: focused validation, workflow, and smoke-check tests pass; PostgreSQL setup is required for the smoke-check integration test.
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName!~Story9BrowserHostRunnerTests"` — expected: complete CI-equivalent backend suite passes.
- `dotnet build backend/ApiPlayground.sln --configuration Release` — expected: Release solution build succeeds.
- `git diff --check` — expected: clean diff.
