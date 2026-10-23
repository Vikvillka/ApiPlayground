---
title: 'Inspect HTTP response details'
type: 'feature'
ticket: '5'
created: '2026-10-22'
status: done
baseline_revision: '21033e1b9c150e8213178c2d006bc262bac43205'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md'
  - '_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/DESIGN.md'
  - '_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Users can send configured requests but cannot inspect complete response details, especially response headers, or switch between headers and body.

**Approach:** Expand the response contract and viewer to present observed HTTP status, elapsed time, response headers, and body with accessible Body and Headers tabs, Body selected by default, and readable JSON formatting. Preserve the distinction between HTTP error responses, policy blocks, timeouts, and other transport failures without unsupported diagnostic claims.

## Boundaries & Constraints

**Always:** Reuse the existing guarded outbound executor and captured `OutboundResult`; do not add a transport. Preserve public-only production policy, exact loopback-only test target, response-size/deadline limits, 4 KiB API request envelope, 2048-character URL limit, and Story 3 persistence smoke behavior. Display the target's HTTP status independently from the API status; HTTP 4xx/5xx remains an observed response, not a transport failure. Preserve multi-valued response headers. Keep request editing behavior and Body/Headers state separate. Default response tab to Body for a new response and retain prior-result labeling while sending. Format JSON for readability without a raw-mode toggle. Textual status and error cues must remain accessible and not rely on color alone.

**Never:** Add History, Collections, authentication, request scripting/schema validation, raw-response mode, alternate transports, response-header editing, or unsupported root-cause diagnosis. Do not expose secrets or change outbound security policy.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| HTTP success | Controlled response with status, elapsed time, repeated headers, JSON body | Show observed status/time; Body tab selected and JSON formatted; Headers tab exposes all values | No error expected |
| HTTP error | Controlled 4xx/5xx response with headers/body | Display it as a completed HTTP response with target status, elapsed time, headers, and body | Do not classify as transport failure or infer cause |
| Transport failure | Timeout or other executor failure | Show accessible distinct failure state, not response tabs with fabricated HTTP response details | State only observed failure; do not claim cause |
| Policy rejection | Executor rejects before dispatch | State request was not sent; do not show it as an HTTP response | Preserve policy distinction |
| Response tab switch | A response with body and headers | Switch displayed content; Body is selected by default for each new response | Keep request tabs unaffected |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Application/Outbound/OutboundResult.cs` — source contract already carries nullable target status, elapsed time, case-insensitive multi-value headers, body, and failure. Reuse it.
- `backend/src/ApiPlayground.Infrastructure/Outbound/GuardedTargetHttpClient.cs` — captures response and content headers, status, elapsed time, and bounded body. Preserve redirects, timeout, response cap, and guarded transport behavior.
- `backend/src/ApiPlayground.Application/Outbound/GetTracerUseCase.cs` — treats `Failure.None` HTTP responses, including 4xx/5xx, as completed responses and retains persistence smoke check; preserve this behavior.
- `backend/src/ApiPlayground.Api/Program.cs` — endpoint currently emits status, elapsed, body, failure and request-sent but omits response headers. Extend payload with captured headers while keeping API status mapping and request guards unchanged.
- `frontend/src/tracer.ts` — `TracerResponse` contract currently lacks response headers; restore a multi-value header shape consistent with API output and preserve failure handling.
- `frontend/src/App.tsx` and `frontend/src/App.css` — response area currently shows summary/body only. Add independent response-tab state, accessible tab semantics and header rendering; retain existing request tabs and format JSON via existing formatter.
- `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` — controlled loopback success tests verify selected request values but need response header and HTTP error assertions; extend using the owned exact loopback policy.
- `backend/tests/ApiPlayground.Tests/Outbound/ControlledLoopbackServer.cs` — existing Kestrel harness supports deterministic status, headers and body; no external network tests.
- `frontend/src/App.test.tsx` — currently checks basic body visibility; add status/time, header values, tab default/switch, formatted JSON, HTTP-error response, and transport/policy state tests.
- `_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md`, `ux-apiplayground/DESIGN.md`, and `ux-apiplayground/EXPERIENCE.md` — authoritative CAP-2 and response/accessibility behavior.

## Tasks & Acceptance

**Execution:**
- [x] `backend/src/ApiPlayground.Api/Program.cs` — serialize captured multi-value response headers with status, elapsed time, body and existing failure fields; do not change response/API status semantics.
- [x] `frontend/src/tracer.ts`, `frontend/src/App.tsx`, and `frontend/src/App.css` — represent headers and implement accessible Body/Headers response tabs, default Body selection, readable JSON, and distinct non-response failure states.
- [x] `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` and `frontend/src/App.test.tsx` — cover the matrix with controlled loopback and UI tests, including 4xx response vs timeout/policy failure.
- [x] Run frontend tests/build, complete backend suite with PostgreSQL integration configuration, `git diff --check`, and independent review; record evidence here. Verification passed; review finding was fixed and covered by a keyboard-navigation/ARIA relationship regression assertion.

**Acceptance Criteria:**
- Given a controlled HTTP response, when it arrives, then the response pane shows the target's status and elapsed time and provides all captured header values and body.
- Given the target returns HTTP 4xx or 5xx, when the response is displayed, then it remains an HTTP response with its observed details rather than a transport failure.
- Given response JSON, when Body is selected, then it is formatted for reading without a raw-mode toggle; Body is selected by default for a new response and Headers tab switches to captured headers.
- Given timeout, other transport failure, or policy rejection, when the request ends, then the UI communicates the distinct outcome accessibly and does not invent response details or unsupported causes; policy rejection explicitly says not sent.
- Given request input tabs and response tabs, when either is switched, then the two independent tab states do not affect one another.

## Implementation Notes

- Added the captured outbound multi-value headers to the tracer API response without changing API HTTP status mapping.
- Added independent accessible response tabs. Successful HTTP results show the observed status/time and formatted body or each response header value; policy and transport failures remain separate from HTTP response details.
- Added a controlled loopback 429 endpoint assertion and UI tests for success/error headers, formatted JSON, tabs, policy rejection, and transport failure.

## Plan Change Log

## Review Triage Log

| Verdict | Evidence | Route |
|---|---|---|
| medium | `frontend/src/App.tsx`: tabs used tab ARIA roles without arrow-key navigation or explicit tab/panel relationships. Verified: initially only click handlers existed and tab panels had no `aria-labelledby`; fixed with arrow/Home/End navigation, roving tab focus, `aria-controls`/`aria-labelledby`, and focused UI assertions. | patch |

## Design Notes

Represent header values as arrays so repeated response header values survive serialization. Render header names and each value as observed; do not combine arrays into a fabricated single value.

## Verification

**Commands:**
- `npm test` from `frontend` — passed, 16 tests across 2 files.
- `npm run build` from `frontend` — passed TypeScript checking and production Vite build.
- `dotnet test backend/ApiPlayground.sln --configuration Release` — passed, 86 tests, 0 failed, 0 skipped. Integration suite completed; outbound tests use controlled test-owned targets.
- `git diff --check` — passed with no whitespace errors.
- Post-review-fix `npm test` — passed, 21 tests across 2 files; `npm run build` passed TypeScript checking and Vite production build.
- Post-review-fix `dotnet test backend/ApiPlayground.sln --configuration Release` — passed (exit code 0); no backend files changed by the review fix. `git diff --check` passed.