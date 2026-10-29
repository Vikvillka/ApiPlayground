---
title: 'Migrate remaining application routes to controllers and explicit contracts'
type: 'refactor'
ticket: '7'
created: '2026-10-29'
status: done
baseline_revision: '37842910e0c8833af3b0bfc7d004f1044b80b855'
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

**Problem:** History and collection application endpoints remain Minimal API handlers with anonymous response projections, while application routes must use attribute-routed controllers and maintainable explicit HTTP contracts. Migrating them can change JSON binding, serialization, error mapping, and the request guard unless their existing behavior is characterized and preserved.

**Approach:** Move the four remaining history/collection routes to thin attribute-routed controllers with justified request/response DTOs and explicit mappings. Preserve routes, HTTP methods, JSON fields, statuses, error behavior, guard middleware, application use-case ownership, and the separate infrastructure health probe.

## Boundaries & Constraints

**Always:** Preserve `GET /api/tracer/history`, `GET /api/tracer/history/{id:guid}`, `GET /api/tracer/collections`, and `POST /api/tracer/collections/requests`. Keep bodyless history not-found responses; preserve collection `200` and `201` results, validation `{error}` 400s, and sanitized 503 responses. Keep history-store exception propagation distinct from collection catches. Preserve header/query redaction metadata and field casing/shapes. Keep `GET /health` returning its existing healthy JSON. Retain `UseRequestGuard()` ordering, `/api/tracer` path-prefix coverage, 4096-byte body cap, host/origin/fetch metadata/content-type/request-marker rules, and 403/413 bodies. Controllers delegate to existing use cases and do not access persistence directly.

**Never:** Do not change API paths, methods, statuses, response fields, null/malformed binding behavior, guard policy, validation, exception/cancellation semantics, persistence/smoke-check behavior, or frontend contracts. Do not add a `Location` header or alter the existing `201` body response. Do not change `GET /health` into an application controller; do not remove guard middleware or rely on controller metadata alone for path-prefix protection. Do not implement Problem Details standardization here; it belongs to its separate story.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| List history | Guard-approved GET | `200` JSON array with existing history fields and redacted header/query data | Preserve current unhandled store-exception behavior |
| Find history | Valid or malformed GUID path | Existing `200` entry or bodyless `404`; constrained-route mismatch remains `404` | No new error envelope |
| List collections | Guard-approved GET, store succeeds/fails | Existing `200` JSON array or generic sanitized `503` Problem Details | Keep existing broad collection catch and message |
| Save collection request | Valid request; validation or persistence failure | Existing `201` JSON body; `400 {error}` for `ArgumentException`; generic sanitized `503` for other exceptions | Preserve request binding and exception mapping, including cancellation behavior |
| Guard and health | Missing guard headers, oversized body, or GET `/health` | Existing 403 JSON / bodyless 413; health remains `200 {status: healthy}` | Guard runs before matched API action; health stays exempt |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Api/TracerEndpoints.cs` — owns `UseRequestGuard()`, `/api/tracer` path-prefix fallback, health mapping, four Minimal API mappings and anonymous DTO projections. Keep the guard and health; remove only the four application route mappings and obsolete request record/projections once replacements exist.
- `backend/src/ApiPlayground.Api/Program.cs` — controllers are already registered/mapped after `UseRequestGuard()`; preserve composition and middleware order.
- `backend/src/ApiPlayground.Api/TracerRequestController.cs` — established dispatch-controller pattern and existing error/DTO style; keep it focused on dispatch.
- `backend/src/ApiPlayground.Application/Outbound/RequestHistory.cs` — history snapshots and history use-case contract.
- `backend/src/ApiPlayground.Application/Outbound/RequestCollections.cs` — collection request/snapshot/use-case contracts and validation; reuse `SaveRequestCommand` rather than exposing it as an HTTP DTO.
- `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` — existing guard, history/listing, collection save/load, and sanitized collection failure coverage; extend for history-by-id, detailed response contract, binding edge cases, and guard coverage on moved endpoints.
- `backend/tests/ApiPlayground.Tests/HealthEndpointTests.cs` — asserts current health status/body; retain unchanged and include in focused verification.
- `backend/tests/ApiPlayground.Tests/Story9BrowserHostRunnerTests.cs` — opt-in browser host registers/maps controllers already; verify its endpoint composition remains valid, without making it mandatory in CI.
- `frontend/e2e/story9-mvp.spec.ts` — browser integration journey exercises collection/history contracts; no frontend change expected.
- `_bmad-output/initiative-apiplayground-backend-refactor/spec-backend-refactor/technical-requirements.md` — mandates attribute-routed application controllers, explicit boundaries, behavior-preserving DTO mappings, and excludes `/health`.

## Tasks & Acceptance

**Execution:**
- [x] `TracerHistoryController.cs` and justified API contract/mapping file — implement both history GET actions using `RequestHistoryUseCase`; preserve list/item JSON shape, header redaction projection, GUID route constraint, and bodyless not-found.
- [x] `TracerCollectionsController.cs` and API request/response contracts — implement collection list/save using `SaveRequestUseCase`; preserve request binding, validation ownership, current anonymous JSON shape, status codes, and sanitized exception mapping.
- [x] `TracerEndpoints.cs` — retain guard middleware, metadata, `/health`, and only the middleware/metadata required by migrated actions; remove duplicate application Minimal API mappings and obsolete DTO projections/request record.
- [x] `TracerEndpointTests.cs` and `HealthEndpointTests.cs` — assert every route, method, contract, status, redaction shape, not-found, error mapping, guard rejection/body limit, and unchanged health result. Keep tests additive.
- [x] `Story9BrowserHostRunnerTests.cs` — confirm the isolated opt-in host still discovers and maps the migrated controllers; do not restore an E2E CI job.

**Acceptance Criteria:**
- Given a guard-approved history list or lookup request, when the route executes, then its current method, status, JSON shape, redaction data, and bodyless not-found behavior are preserved.
- Given collection list/save success, validation failure, or storage failure, when the route executes, then current JSON fields, statuses, sanitized messages, exception distinctions, and request-binding behavior are preserved.
- Given any moved route without required guard headers/origin/host or with an oversized body, when it reaches the API, then the existing guard rejects it before controller execution with the same status/body.
- Given `GET /health`, when requested, then it remains the infrastructure health probe returning `200` and `{ "status": "healthy" }`.
- Given the registered application route inventory, when routing is inspected, then every application route uses attribute-routed controllers and no duplicate Minimal API mapping remains.

## Design Notes

The four API responses currently use anonymous projections; use explicit response contracts only where they clarify the wire boundary. A small shared header DTO for `name`, `value`, and `redacted` is justified because history and saved requests already emit that same shape; keep their surrounding response records separate. Preserve the `redactedHeaders` list in both. The collection-save HTTP DTO remains distinct from `SaveRequestCommand` and maps into it. Avoid `[ApiController]` automatic binding/error behavior unless tests demonstrate exact parity for null, malformed, missing, and wrong-type bodies. History store exceptions propagate; collection routes catch and sanitize; do not normalize them in this migration. `/health` remains a minimal infrastructure probe.

## Implementation Notes

Added `TracerHistoryController` and `TracerCollectionsController` with explicit response contracts in `TracerContracts.cs`; collection POST deserializes using HTTP JSON options to retain Minimal API binding outcomes without `[ApiController]`. `TracerEndpoints` now retains only the request guard and `/health`; composition order is unchanged. Expanded route tests for lookup, full history/saved-request shape and redaction, invalid body binding, guard rejections/body cap, sanitized collection-save failures, history exception propagation, and controller-only route inventory. The opt-in browser host still registers controllers and calls `MapControllers()`; it remains excluded from the mandatory full-suite run.

Verification: focused route/health tests passed (83/83); CI-equivalent backend suite passed (179/179, excluding opt-in `Story9BrowserHostRunnerTests`); Release solution build succeeded; `git diff --check` was clean.

## Plan Change Log


## Review Triage Log

Quick review completed: no findings.

## Verification

**Commands:**
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName~TracerEndpointTests|FullyQualifiedName~HealthEndpointTests"` — expected: all route, guard, binding, response-contract, and health tests pass.
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName!~Story9BrowserHostRunnerTests"` — expected: complete CI-equivalent backend suite passes; configured PostgreSQL prerequisites are required.
- `dotnet build backend/ApiPlayground.sln --configuration Release` — expected: Release solution build succeeds.
- `git diff --check` — expected: clean diff.

Recorded results: focused route/health tests 83/83 passed; CI-equivalent backend tests 179/179 passed with the opt-in browser runner excluded; Release build succeeded; diff check clean.
