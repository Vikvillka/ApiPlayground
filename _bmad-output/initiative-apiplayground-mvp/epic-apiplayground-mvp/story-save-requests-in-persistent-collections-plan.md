---
title: 'Save requests in persistent collections'
type: 'feature'
ticket: '7'
created: '2026-10-25'
status: 'built'
baseline_revision: '4a1de9de79290af096952db56d10933288ae7338'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md'
  - '_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/EXPERIENCE.md'
  - '_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/DESIGN.md'
  - '_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Users must reconstruct frequently reused requests because History is for sent attempts, not deliberately saved reusable requests.

**Approach:** Let users explicitly save the current request into a named persistent collection, then find and load it later without sending. Offer an editable method/path title suggestion and allow choosing or creating a collection in the save flow.

## Boundaries & Constraints

**Always:** Keep EF Core, migrations, and persistence in Infrastructure; define use-case ports/results in Application, and map API DTOs without exposing persistence entities. Persist collections and saved requests in the existing local PostgreSQL store for reuse across work sessions. Make inline collection creation and saving the request atomic. Save only on explicit user action; selecting a saved request loads a detached editor state, never dispatches, and clears the stale response. Preserve method, URL/query, enabled headers, and body needed to restore the request. Apply the same sensitive-header omission policy as Story 6: omit values for exact names `Authorization`, `Proxy-Authorization`, `Cookie`, and `Set-Cookie`, plus case-insensitive header names containing `api-key`, `apikey`, or `token`; load those values blank and disabled for re-entry. A failed save leaves the dialog and values available for retry and reports failure. Keep current request size/URL bounds, local-only topology, and guarded outbound path unchanged.

**Never:** Add alternate target transports, persist sensitive values, save automatically on send, send on selection, expose EF entities, add unrelated collection management/bulk features, or allow EF exceptions to leak through API responses.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Save to existing collection | Configured request, editable suggested title, selected collection | Request is saved into that collection and appears in its list | Do not claim success until persistence completes |
| Create collection inline | Configured request, new collection name, title | Collection and request are saved atomically and available after restart | Roll back both if either write fails |
| Save failure | Dialog contains unsaved request values | Dialog remains open with values intact and retry is available | Show textual failure; do not discard the request |
| Sensitive headers | Request includes owner-classified sensitive headers | Names may be retained for restoration, but sensitive values are never stored and load blank/disabled | Require user re-entry before those headers can be enabled |
| Load saved request | User selects a collection item | Restore method, title context, URL/params, headers, and body; clear stale response | Never dispatch implicitly |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Application/Outbound/RequestHistory.cs` — reuse its application-owned request snapshot shape, port/use-case conventions, and central redaction implementation only after the owner confirms policy; keep collection-specific save semantics separate from immutable History attempts.
- `backend/src/ApiPlayground.Infrastructure/Data/PlatformDbContext.cs` and `Data/Migrations/` — add collection and saved-request entities/mappings in Infrastructure. Generate schema changes with `dotnet ef migrations add` from `backend` after model changes; preserve the existing migration stream.
- `backend/src/ApiPlayground.Infrastructure/Outbound/PostgresRequestHistoryStore.cs` and `DependencyInjection.cs` — reuse Npgsql/EF persistence style and DI registration; implement a focused collection store with transaction-scoped create-and-save behavior.
- `backend/src/ApiPlayground.Api/Program.cs` — follow History route guard and explicit Application-to-wire DTO mapping. Add collection list/save endpoints while retaining request body and local-origin protections.
- `frontend/src/App.tsx`, `App.css`, and `tracer.ts` — extend the collapsed History area to show Collections, add an accessible save dialog with editable method/path suggestion and existing/new collection choice, preserve current form on failure, and load items through the established no-send restoration behavior.
- `backend/tests/ApiPlayground.Tests/RequestHistoryTests.cs`, `TracerEndpointTests.cs`, `MigrationContinuityTests.cs`, and `frontend/src/App.test.tsx` — follow established redaction, API host, PostgreSQL migration, and UI testing patterns. Add collection persistence/reload, atomic creation, failure/retry, redaction, load/no-send, and keyboard/focus coverage.
- `_bmad-output/initiative-apiplayground-mvp/epic-apiplayground-mvp/tickets.toml` and its CAP-4 spec/UX references — canonical ticket intent, persistence-across-work-session requirement, and save-dialog interaction contract.

## Tasks & Acceptance

**Execution:**
- [x] `backend/src/ApiPlayground.Application/Outbound/` — define collection/save-request contracts and use cases that validate and preserve the configured request without coupling Application to EF.
- [x] `backend/src/ApiPlayground.Infrastructure/Data/` and `DependencyInjection.cs` — implement persistent collection/request records and atomic inline create-plus-save; generate migration with `dotnet ef migrations add` and verify existing migration continuity.
- [x] `backend/src/ApiPlayground.Api/Program.cs` — expose guarded collection list/save operations and saved-request mapping while keeping entities internal and errors non-sensitive.
- [x] `frontend/src/App.tsx`, `App.css`, and `tracer.ts` — implement the collapsed Collections surface, accessible save dialog, title suggestion, existing/new collection selection, retry-preserving failure state, and no-send item loading.
- [x] `backend/tests/ApiPlayground.Tests/` — verify persistence across fresh store/context instances, atomic creation, failed-save behavior, migration continuity, and confirmed sensitive-value omission.
- [x] `frontend/src/App.test.tsx` — verify save flow, keyboard/focus behavior, retained values on failure/retry, request restoration, stale-response clearing, and no implicit dispatch.
- [x] Run frontend tests/build, full backend Release suite with PostgreSQL configuration, migration checks, `git diff --check`, and quick review.

**Acceptance Criteria:**
- Given a configured request and existing collection, when the user confirms saving, then the named request appears in that collection and remains available in a later work session.
- Given the user creates a collection while saving, when persistence succeeds, then both collection and request exist; if persistence fails, neither partial result is exposed as saved.
- Given save persistence fails, when the operation returns, then the dialog retains its inputs, reports failure, and allows retry without losing the configured request.
- Given a saved collection item is selected, when it loads, then request fields are restored and the prior response is cleared without sending.
- Given a configured sensitive header, when the request is saved and later loaded, then its value was not persisted and is presented blank/disabled for re-entry.

## Implementation Notes

- Added persistent PostgreSQL collections and saved requests with atomic inline creation, explicit list/save API routes, and a save/load UI that never dispatches on selection.
- Reused History redaction for sensitive headers and sanitized sensitive query values before persisting collection URLs.
- Quick review found that collection URLs could retain query secrets and that list failures could expose persistence exceptions. Both were fixed, with regression tests for redacted URLs and sanitized 503 responses.

## Plan Change Log

- Decision (2026-10-25): User approved reusing Story 6's sensitive-header policy for Collections to keep History and saved-request behavior consistent.

## Review Triage Log

| Verdict | Evidence | Route |
|---|---|---|
| medium | Collection saves retained sensitive query values in the persisted URL; the save use case now reuses History URL redaction, and tests assert the returned and reloaded URL contain empty sensitive values. | patch |
| medium | Collection listing let store exceptions escape the route; it now returns a generic 503, with an endpoint test asserting exception details are absent. | patch |

## Design Notes

The History record is an immutable attempt and should not become a collection item by aliasing its database row. Collections represent explicitly saved request configurations with an editable title and collection membership. Inline creation and save must share one transaction so retry cannot leave an empty collection after request persistence fails.

## Verification

**Results:**
- `npm test` from `frontend` — passed, 27 tests across 2 files.
- `npm run build` from `frontend` — passed TypeScript checking and production Vite build.
- `dotnet test tests/ApiPlayground.Tests/ApiPlayground.Tests.csproj -c Release` from `backend`, with local test environment configured — passed, 99 tests, 0 failed, 0 skipped; PostgreSQL migration continuity separately passed, 4 tests.
- `dotnet ef migrations list` from `backend` discovered `20261018000000_InitialPlatformBaseline`, `20261023000000_RequestHistory`, and generated `20261025165909_RequestCollections`; applied-state lookup could not connect to the app database, while disposable-PostgreSQL migration tests passed.
- `git diff --check` against the Story 6 baseline — passed with no whitespace errors.
- Quick review findings were fixed and regression-tested; no findings deferred.
