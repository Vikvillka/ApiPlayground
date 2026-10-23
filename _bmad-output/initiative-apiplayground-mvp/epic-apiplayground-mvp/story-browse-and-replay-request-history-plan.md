---
title: 'Browse and replay request history'
type: 'feature'
ticket: '6'
created: '2026-10-23'
status: 'built'
baseline_revision: '58ffd1962d5d66b020849e9344db2f1d5fb4ee32'
route: 'full'
route_source: 'pinned'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md'
  - '_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Requests cannot be found or repeated after they are sent, so users must reconstruct prior configurations manually.

**Approach:** Persist immutable records of eligible dispatched attempts, show them in a History surface, and let users load a record into the composer or explicitly replay it through the existing guarded request path. A History write failure must not prevent or misrepresent the request outcome.

**Decision:** On load, redact-sensitive query values are replaced with empty values and disabled parameter rows; their names/order remain visible for user re-entry. Redacted headers follow the same blank-and-disabled behavior.

## Boundaries & Constraints

**Always:** Keep EF Core, migrations, and persistence implementations in Infrastructure; expose use-case-focused ports/results from Application and map them in API. Keep History records immutable and load/replay into a detached editable request. Store only attempts whose guarded execution reached the dispatch boundary; include HTTP responses of any status and failures occurring after dispatch starts, but exclude validation and pre-dispatch policy rejection. Distinguish user-visible dispatch state from whether a remote service is known to have received bytes. Persist method, URL/query, ordinary headers and body needed to reproduce the submitted request. Omit values for `Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie`, and headers whose case-insensitive names contain `api-key`, `apikey`, or `token`; redact query parameter values whose case-insensitive names contain `token`, `api-key`, `apikey`, `secret`, or `password`. Show omitted header/parameter names blank and disabled when loaded. Retain entries until the local database is reset; do not add automatic expiry or delete controls. Persistence failure must not block dispatch; communicate that the History entry was not saved. Selecting a History record never sends, clears the prior response, and resets response selection to Body. Replay uses the existing `IOutboundRequestExecutor`/guard. Preserve the public-only production policy, the 4 KiB API request envelope, 2048-character URL limit, response/deadline limits, local-only topology, and Story 3 persistence smoke behavior. Automated target dispatch tests may use only the owned ephemeral loopback server with exact test-only policy.

**Never:** Add an alternate transport, weaken outbound policy, make History records editable in place, send on selection, record validation failures or policy rejections, persist omitted sensitive-header values, add collection saving, authentication UI, request scripting/schema validation, or claim a failed request reached its target without evidence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| HTTP response | Guarded request returns any HTTP status | Record the submitted request and observed response summary; History remains ordered and replayable | HTTP 4xx/5xx is still an HTTP response |
| Post-dispatch failure | Guarded dispatch has started, then timeout/transport/response-size/cancellation outcome | Record the immutable request attempt and available outcome evidence | Do not infer the remote service received it |
| Pre-dispatch rejection | Invalid request or guard policy rejects before dispatch | No History record | Preserve validation/policy response; policy explicitly says not sent |
| Sensitive inputs | Dispatched request contains a sensitive header or query parameter | Persist names/structure but redact selected values; ordinary inputs remain available | On load, redacted rows/parameters are blank and disabled |
| History persistence failure | Database insert fails for an eligible attempt | Request result is still returned and UI reports the attempt was not saved | Do not turn dispatch into API failure or claim it was stored |
| Load and replay | User selects a History record, then optionally presses Send | Load fields without dispatch; clear stale response; explicit replay goes through the guard | Redacted sensitive rows stay disabled until re-entered |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Application/Outbound/OutboundRequest.cs` and `OutboundResult.cs` — existing single request/result transport contract. `OutboundResult` currently lacks an explicit dispatch-start marker; add a testable marker at the guarded `HttpClient.SendAsync` boundary so DNS/policy failures are not mislabeled as dispatched. Keep HTTP 4xx/5xx represented as `Failure.None` with observed status.
- `backend/src/ApiPlayground.Application/Outbound/GetTracerUseCase.cs` — current use case runs only the temporary smoke check after successful HTTP. Separate smoke behavior from durable History recording; never let History persistence change outbound outcome.
- `backend/src/ApiPlayground.Application/Outbound/IOutboundRequestExecutor.cs` — sole dispatch port to reuse. Do not construct another client or bypass policy for replay.
- `backend/src/ApiPlayground.Infrastructure/Data/PlatformDbContext.cs`, `DependencyInjection.cs`, and `Data/Migrations/` — EF context, Npgsql registration, and single migration stream. Add History entity/mapping and migration in Infrastructure, preserving existing migration identity/history.
- `backend/src/ApiPlayground.Api/Program.cs` — current guarded tracer route validates requests and maps executor results. Extend the request contract to retain base URL and parameter rows if necessary for faithful form restoration; add list/load API mapping without exposing EF types. Keep existing Host/Origin/request-size protections on dispatch.
- `frontend/src/App.tsx`, `App.css`, `tracer.ts` — composer currently holds request rows locally and submits a flattened final URL plus enabled header dictionary. Add collapsed-by-default History navigation, list/load behavior, explicit replay through the existing submit path, redaction-aware disabled rows, and clear/reset response state on load.
- `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs`, `MigrationContinuityTests.cs`, and `ControlledLoopbackServer.cs` — endpoint guard, disposable-PostgreSQL migration checks, and owned loopback harness. Add coverage for dispatch eligibility, storage-failure independence, sanitized persistence, list/load mapping, and replay guard use.
- `frontend/src/App.test.tsx` — existing validation, dispatch, response, and state tests. Extend for History list/load/replay, no implicit send, stale-response clearing, redacted headers, and persistence failure notice.
- `_bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md` and `ux-apiplayground/EXPERIENCE.md` — immutable snapshots, detached drafts, dispatch/persistence ordering, History sidebar behavior, and resolved header decision govern implementation.

## Tasks & Acceptance

**Execution:**
- [x] `backend/src/ApiPlayground.Application/Outbound/` — define an Application-owned History snapshot/port/use case and explicit dispatch-start result signal; document that dispatch begins at the guarded call to `HttpClient.SendAsync`, and that History insert failure cannot replace the outbound result.
- [x] `backend/src/ApiPlayground.Infrastructure/Data/` and `DependencyInjection.cs` — persist sanitized immutable attempt snapshots, provide stable newest-first ordering with a deterministic tie-break, add Infrastructure migration and registrations, and keep EF exceptions behind the port.
- [x] `backend/src/ApiPlayground.Api/Program.cs` and request DTOs — record eligible executions, expose History listing/loading, preserve current request guard and response status semantics, and support complete request restoration within existing size/URL bounds.
- [x] `frontend/src/App.tsx`, `App.css`, and `tracer.ts` — add keyboard-accessible collapsed History UI; load without sending and clear old response; display unsaved-history status; explicit resend uses the normal protected submit path.
- [x] `backend/tests/ApiPlayground.Tests/` — cover migration, sensitive-value omission, stable ordering, HTTP error and dispatch-started failure inclusion, validation/policy exclusion, and History persistence failure without lost dispatch result using controlled tests.
- [x] `frontend/src/App.test.tsx` — verify list/load/replay, no send on selection, redacted header presentation, cleared prior response, and accessible persistence-failure message.
- [x] Run frontend tests/build, full backend Release suite with required PostgreSQL integration configuration, `git diff --check`, and independent quick review; record evidence and triage here.

**Acceptance Criteria:**
- Given an eligible dispatched request, when it completes with an HTTP response or a post-dispatch failure, then History contains an immutable sanitized snapshot and available observed outcome.
- Given validation or policy rejects a request before dispatch, when the result is returned, then it is absent from History and retains its existing error semantics.
- Given History persistence fails, when an eligible request is sent, then the outbound outcome is still returned and the UI clearly reports that its History entry was not saved.
- Given a History item is selected, when it is loaded, then all restorable request fields appear in the editor, omitted sensitive headers are blank and disabled, no request is sent, and any prior response is cleared.
- Given a loaded item, when the user explicitly sends it, then dispatch uses the existing guarded path and the prior History entry remains unchanged.
- Given persisted attempts have equal timestamps, when History is listed, then ordering is deterministic and stable.

## Design Notes

- Dispatch eligibility uses an explicit marker immediately before the guarded `SendAsync` call; DNS/policy failures remain excluded, while connection/post-dispatch failures are recorded without claiming the target received bytes.
- Sensitive query values are blanked in the persisted URL; an ordered redacted-name list lets the UI restore those query rows blank and disabled. History stores outcome summary but no response headers/body.
- History listing currently returns all retained rows newest-first, with the GUID as a deterministic secondary sort key; list/load endpoints reuse the existing local Host/Origin/custom-header protections.
- UI adds a collapsed History control, detached no-send load, stale-result clearing, re-entry for redacted values, and replay only through the standard Send path.


## Decision Log

- Decision (2026-10-23): Retain History entries until local database reset; no expiry or user-facing deletion controls in this story.
- Decision (2026-10-23): Redact query values for case-insensitive parameter names containing `token`, `api-key`, `apikey`, `secret`, or `password`; load those parameter rows blank and disabled. Other query values remain available for replay.

## Verification

**Implementation evidence:** frontend suite passes (24 tests); TypeScript/Vite production build succeeds; backend Release suite passes (94 tests, 0 failed, 0 skipped) with the workspace PostgreSQL test role; migration discovery lists `20261018000000_InitialPlatformBaseline` and `20261023000000_RequestHistory` (applied-state lookup unavailable because this command had no application connection string). `git diff --check` is clean after review fixes. Quick review completed; all findings were corrected and regression-tested.

## Review Triage Log

- `patch` — `frontend/src/App.tsx` request-tab panel relationship finding: declared `aria-controls` IDs had no matching request panel IDs; add the IDs and `aria-labelledby` relationships. Implemented and covered by the post-fix frontend build/tests.
- `patch` — `frontend/src/App.tsx` History selection during an in-flight send could let the send completion restore a stale response after History selection cleared it. Disable History item selection while sending; a regression test verifies the item is unavailable until send completes.
- `patch` — `backend/src/ApiPlayground.Infrastructure/Outbound/GuardedTargetHttpClient.cs` used elapsed connection-deadline state to classify any later transport exception as a connection timeout. Restrict that classification to the connection callback’s explicit expiration marker or a `TimeoutException`, preserving transport-failure semantics after connection.
- `false` — backend verification evidence finding: final configured Release run exited 0 after all 94 tests, with 0 failures and 0 skips; PostgreSQL migration tests ran using `.env` test role. The earlier failure was due to a deliberately incorrect connection value during triage and is superseded.

## Code Review

### 2026-10-23

**Code review complete.** 0 `decision-needed`, 3 `patch`, 0 `defer`, 1 rejected.

- [x] [Review][Patch] Prevent History selection while a send is in flight [frontend/src/App.tsx:186]
- [x] [Review][Patch] Connect request tab controls to their panels [frontend/src/App.tsx:194]
- [x] [Review][Patch] Preserve transport-failure classification after connect deadline elapses [backend/src/ApiPlayground.Infrastructure/Outbound/GuardedTargetHttpClient.cs:162]

**Rejected**
- `false` — The verification record did establish a passing backend Release run; subsequent final run confirmed 94 passed, 0 failed, and 0 skipped.

**Commands:**
- `npm test` and `npm run build` from `frontend` — expected: all UI tests pass and TypeScript/Vite production build succeeds.
- `dotnet test backend/ApiPlayground.sln --configuration Release` — expected: all unit/API and configured PostgreSQL integration tests pass; missing required test database configuration is a failure, not a skip.
- `dotnet ef migrations list --project src/ApiPlayground.Infrastructure/ApiPlayground.Infrastructure.csproj --startup-project src/ApiPlayground.Api/ApiPlayground.Api.csproj` from `backend` — expected: the existing migration plus the History migration are discovered in the same Infrastructure stream.
- `git diff --check` — expected: no whitespace errors.
