---
title: 'Complete request methods and inputs'
type: 'feature'
ticket: '4'
created: '2026-10-21'
status: done
baseline_revision: 'e13bc530a5c0e1dc44adc0e022d9b6aa6552e83e'
route: 'full'
route_source: 'auto'
risk: medium
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md'
  - '_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/DESIGN.md'
  - '_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The current request workspace can send only a simple GET URL, so users cannot configure the methods and inputs required by CAP-1. This story adds method selection, query parameters, request headers, and JSON body while retaining the single guarded outbound path.

**Approach:** Extend the shared Application/API request contract and the UI to send GET, POST, PUT, PATCH, and DELETE with configured URL, enabled query/header rows, and optional valid JSON. Preserve URL query values and append enabled Params including repeated names; reject duplicate enabled header names before dispatch; permit JSON body only for POST, PUT, and PATCH. Keep request/response panes, validate before dispatch, prevent duplicate sends, and make any prior result explicitly identifiable during a new send.

## Boundaries & Constraints

**Always:** All target dispatch must use the existing Application outbound port and guarded Infrastructure adapter. Preserve Story 3 Host/Origin/JSON/custom-header checks, public-only production policy, 4 KiB API request limit, 2048-character URL bound, distinct policy rejection/transport/HTTP outcomes, and temporary persistence smoke behavior unless repo evidence proves a necessary contract-preserving adjustment. Automated outbound coverage may use only an owned ephemeral loopback target through the exact test-only policy. Keep the same-origin Vite proxy and local-only topology. Preserve entered values while switching Params, Headers, and Body tabs; rows default enabled and disabled rows must not be sent. Existing URL query items are preserved and enabled Params append using standard URL encoding, including repeated keys. Duplicate enabled header names block dispatch; disabled duplicates do not. JSON body is permitted only for POST, PUT, and PATCH. Keep request controls keyboard operable, visibly focused, and the request/response layout stacked on narrow desktop.

**Never:** Add alternate HTTP transports, weaken policy, introduce auth editors, History, Collections, response Headers tab/full response viewer, scripting/schema validation, remote services, or persistence models beyond Story 3's smoke check. Do not broaden application request size limits beyond the existing 4 KiB API payload cap.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Configured request | One of five methods, valid HTTP(S) URL, enabled rows, optional valid JSON | Guarded target observes selected method, merged query, enabled headers, and body exactly as configured | Existing guarded failure mapping is preserved |
| Disabled rows | Params/headers include disabled rows | Disabled values are absent from the target request | No error |
| Invalid URL or JSON | Malformed URL or syntactically invalid JSON body | No outbound dispatch; relevant input receives accessible validation feedback | Keep editor values for correction |
| Request in flight | A previous response exists and a new request starts | Prevent duplicate dispatch and retain previous result with explicit previous-request label | Current request's failure is shown without misattributing the prior result |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Application/Outbound/OutboundRequest.cs` — existing method/URI/header/body/content-type port DTO; preserve as the single dispatch contract.
- `backend/src/ApiPlayground.Application/Outbound/GetTracerUseCase.cs` — current URL-only GET use case and persistence smoke check; extend/generalize for selected methods and inputs while preserving Story 3 outcome mapping and smoke semantics.
- `backend/src/ApiPlayground.Api/Program.cs` — current `/api/tracer/get` request DTO, size/URL limits, Host/Origin/content-type/custom-header gate, and result mapping; adapt endpoint contract and keep guard effective for every method.
- `backend/src/ApiPlayground.Infrastructure/Outbound/GuardedTargetHttpClient.cs` — validates and dispatches all five approved methods with headers/body already represented by `OutboundRequest`; do not add a second transport.
- `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` and `Outbound/ControlledLoopbackServer.cs` — API host guard tests and ephemeral server; add exact target assertions for method/query/header/body, disabled values, invalid inputs, and no-dispatch cases.
- `frontend/src/App.tsx` and `frontend/src/App.css` — current GET-only form and two-panel UI; add method selector, persistent Params/Headers/Body tabs, enabled key/value rows, JSON editor, validation, prior-result label, and accessible responsive layout consistent with UX tokens.
- `frontend/src/tracer.ts` and `frontend/src/App.test.tsx` — existing fetch contract and component tests; generalize request payload and cover serialization, validation, loading, duplicate prevention, and retained result.
- `frontend/vite.config.ts` — preserves same-origin proxy and exact API host behavior; change only if needed to support the same protected route.
- `_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md`, `ux-apiplayground/EXPERIENCE.md`, and `ux-apiplayground/DESIGN.md` — CAP-1, request control behavior, and visual/accessibility requirements.

## Tasks & Acceptance

**Execution:**
- [ ] `backend/src/ApiPlayground.Application/Outbound/` and `backend/src/ApiPlayground.Api/Program.cs` — accept the five methods and configured inputs through one application contract and the existing protected route; validate URL, methods, JSON, and request-size bounds before dispatch.
- [ ] `frontend/src/App.tsx`, `frontend/src/App.css`, and `frontend/src/tracer.ts` — implement method selection, Params/Headers/Body controls, enabled row add/remove, JSON validation, request serialization, prior-response label, keyboard access, and responsive behavior.
- [ ] `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` and `frontend/src/App.test.tsx` — prove all methods and configured values through the owned controlled target; prove disabled rows and invalid URL/JSON do not dispatch; cover UI state and serialization.
- [ ] Run frontend tests/build, full backend suite with PostgreSQL integration configuration, diff checks, and independent review; record evidence and triage in this plan.

## Execution Evidence

- Added final URL length checks in the browser and API; API rejects lengths above 2048 before dispatch. Added a backend test for the inclusive 2048-character boundary.
- Params are appended by the client to the final URL with `URLSearchParams`, retaining repeated keys, existing query items, and disabled-row exclusion while keeping the JSON envelope compact.
- UI no longer exposes response Headers, which belongs to Story 5. API rejects case-insensitive duplicate header names and transport-managed Host/Content-Length/Content-Type.
- Frontend verification after the review fix: `npm test` — 11 tests passed; `npm run build` — passed.
- Backend verification after the review fix: `dotnet test backend/ApiPlayground.sln --configuration Release` — passed (all configured tests; includes controlled loopback outbound tests and PostgreSQL integration configuration).
- `git diff --check` — passed.
- Independent quick review found one accessibility issue; fixed and covered by the added cross-tab validation-message test.

**Acceptance Criteria:**
- Given a configured request using GET, POST, PUT, PATCH, or DELETE, when it is sent, then the controlled target receives the selected method and enabled URL/query/header/body values through the existing guard.
- Given a disabled parameter or header row, when the request is sent, then that row is excluded without deleting its editor value.
- Given an invalid absolute HTTP(S) URL or invalid JSON body, when Send is attempted, then the UI identifies the field error and the outbound executor is not called.
- Given a request is in flight, when the user attempts another send, then no duplicate dispatch occurs; any earlier response remains visible with a clear previous-request label.
- Given desktop or narrow desktop width and keyboard-only use, when the user configures and sends a request, then controls have labels, logical tab/focus order, visible focus, and both request and response remain reachable side-by-side or stacked as appropriate.

## Implementation Notes

## Decision Log

- Decision (2026-10-21): Append enabled Params to any query already present in the URL and preserve repeated parameter keys as repeated query pairs.
- Decision (2026-10-21): Reject duplicate enabled Header names before dispatch, matching `OutboundRequest`'s current one-value-per-name contract; disabled duplicates do not block sending.
- Decision (2026-10-21): Permit JSON bodies only for POST, PUT, and PATCH; GET and DELETE send no body. Keep the existing 4 KiB API envelope limit.

## Review Triage Log

- Finding 1 — low: Confirmed. When validation fails in a non-selected request tab, Send stays disabled and the inline error is hidden with that tab. The patch now shows an accessible explanation and associates it with Send.

## Design Notes

The approved outbound DTO models headers as a single string per name. Retain the current 4 KiB API request-envelope limit; do not broaden limits speculatively.

## Verification

**Commands:**
- `npm test` and `npm run build` from `frontend` — all frontend tests pass and production bundle builds.
- `dotnet test backend/ApiPlayground.sln --configuration Release` — all backend and configured PostgreSQL integration tests pass without public-target dispatch.
- `git diff --check` — no whitespace errors.

## Code Review

### 2026-10-22

**Code review complete.** 0 `decision-needed`, 1 `patch`, 0 `defer`, 0 rejected. The patch was applied and reverified.

- [x] [Review][Patch] Explain disabled Send when another tab has invalid input [frontend/src/App.tsx:42]