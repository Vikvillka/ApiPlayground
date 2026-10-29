---
title: 'Refactor the guarded outbound path without weakening its policy'
type: 'refactor'
ticket: '6'
created: '2026-10-28'
status: done
baseline_revision: '5b561d7f7c659a80dcc4b2ad7f0095189a85f27e'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The single guarded outbound executor combines request validation, DNS resolution and validation, address pinning, handler construction, dispatch tracking, bounded response reading, and failure classification in one method. That makes security-sensitive ordering harder to inspect and safely maintain.

**Approach:** Refactor only demonstrated cohesion seams in the existing guarded dispatch path, keeping one production transport and preserving every approved policy/security invariant and observable dispatch outcome. Prefer small private helpers with explicit values over new public abstractions, configuration, or transport paths.

## Boundaries & Constraints

**Always:** Preserve production `PublicInternetOnly` policy; reject an entire DNS answer set if any address is disallowed; pin the actual connection to an address from the validated answer set; retain requested host/port checks; disable proxy and redirects; preserve exact test-only loopback endpoint/hostname restrictions; maintain 5-second connect and 30-second total deadlines across DNS, dispatch, and body reads; preserve caller-cancellation vs timeout classification, `DispatchStarted` history eligibility, response metadata, and the 1 MiB cap for declared and streamed bodies. Keep exactly one registered guarded executor and preserve the `IOutboundRequestExecutor` contract. Do not modify policy values or API contracts.

**Never:** Do not create a second HTTP client/transport, weaken address classification, accept partial DNS sets, resolve again during connection, enable redirects/proxies, alter timeouts/cap, change failure mapping or dispatch boundary semantics, expose test policy in production, or weaken/delete safety assertions. Any additional required tests must be additive.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Rejected request/policy | Invalid URI/method/headers, disallowed literal, mismatched test endpoint | PolicyRejected before connector invocation; dispatch not started | Preserve diagnostics and rejection precedence |
| DNS validation and pinning | DNS set contains all allowed addresses, or any disallowed/mixed answer | Mixed set rejected before connection; valid set pins one validated address and uses original host/port | Resolver timeout/cancellation remains distinct from transport/policy rejection |
| Guarded connect | Validated destination, redirects/proxy disabled | Connect only to pinned IP after verifying callback host/port | 5-second connect timeout and 30-second total timeout retain classification |
| Dispatch and cancellation | Cancellation before dispatch, during connect, send, or response read | Preserve `DispatchStarted` boundary and report caller cancellation as Cancelled; total/connect deadline as Timeout | Do not convert caller cancellation into timeout or transport failure |
| Bounded response | Declared length above 1 MiB or streamed body exceeds cap | ResponseTooLarge; no more than 1 MiB retained; preserve target status/headers and dispatch-started state | Keep response cap checks for both known and unknown lengths |
| Successful/target error response | Valid response, including non-2xx target status and repeated headers | Return full bounded body, target status and combined response headers; no redirect follow | Preserve existing result data and elapsed-time semantics |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Infrastructure/Outbound/GuardedTargetHttpClient.cs` — the sole production transport and primary refactor target. `ExecuteAsync()` currently performs validation, resolve/check/pin, handler/callback creation, request dispatch, response header/body processing, and failure classification. Private helpers may clarify these stages; do not change their order or boundary semantics.
- `backend/src/ApiPlayground.Infrastructure/Outbound/OutboundPolicy.cs` — immutable policy and address classifier; owns production public-address allow logic and exact internal loopback test endpoint. Treat every policy value and range check as frozen.
- `backend/src/ApiPlayground.Infrastructure/DependencyInjection.cs` — validates and registers exactly one `IOutboundRequestExecutor`; production API composes `PublicInternetOnly`. Do not add a production policy switch or second executor.
- `backend/src/ApiPlayground.Application/Outbound/OutboundResult.cs` and `GetTracerUseCase.cs` — define result fields and interpret `DispatchStarted` for history eligibility; preserve outcomes and semantics.
- `backend/tests/ApiPlayground.Tests/Outbound/OutboundSafetyTests.cs` — tests address ranges, method/URI/header rejection, IPv6 selection, timeout, exact test-only policy, mixed DNS answers, redirects, response cap, total-body deadline, cancellation, and no default executor.
- `backend/tests/ApiPlayground.Tests/Story9CloseoutTests.cs` — independent closeout assertions for real API production policy, DNS rebinding/pinning, mixed-answer rejection, redirects, deadlines, response cap, and DI composition. Keep all assertions intact.
- `backend/tests/ApiPlayground.Tests/ArchitectureBoundaryTests.cs` — enforces single transport and inward references; the guard executor remains the only runtime direct-dispatch exception.
- `backend/src/ApiPlayground.Infrastructure/Outbound/AssemblyInfo.cs` — grants test access to internal seams; avoid expanding public surface.

## Tasks & Acceptance

**Execution:**
- [x] Refactor `GuardedTargetHttpClient.ExecuteAsync()` into a small number of cohesive private stages only where ordering and result semantics remain explicit; avoid speculative interfaces/classes.
- [x] Consolidate any proven duplicate validation only if rejection precedence, reason, and no-dispatch behavior remain identical.
- [x] Keep DNS whole-set validation and connection pinning visibly coupled; ensure handler callback uses only the validated pinned address while preserving URI host/port verification.
- [x] Keep bounded streaming behavior and known-length fast rejection equivalent, including target status/headers and `DispatchStarted` in oversize outcomes.
- [x] Add targeted regression assertions only for uncovered invariants reached by changed stages; never remove or weaken the existing outbound or independent closeout suite.
- [x] Review the final transport diff against every security invariant and run focused outbound safety, architecture, closeout, and full CI-equivalent backend verification.

**Acceptance Criteria:**
- Given any existing rejection case, when the executor validates it, then it returns the same PolicyRejected result before DNS/connection/dispatch as appropriate.
- Given a DNS hostname, when all addresses are validated, then connection uses a pinned validated IP and verifies the requested host and port; a mixed answer set never connects.
- Given an HTTP response, when redirect/proxy behavior is configured, then redirects remain disabled, proxy use remains disabled, and only the single guarded connection executes.
- Given caller cancellation or connect/total deadline expiry at each stage, when execution stops, then failure classification and `DispatchStarted` match the existing contract.
- Given known-length or streamed content above 1 MiB, when the response is read, then the executor returns `ResponseTooLarge` with retained status/headers and does not buffer beyond the cap.
- Given successful and target non-success responses, when execution completes, then target status, headers, body, elapsed time, and dispatch eligibility remain unchanged.
- Given production DI composition, when the API starts, then exactly one guarded executor uses `PublicInternetOnly` and test-only loopback policy remains inaccessible to production composition.

## Design Notes

The executor's natural seams are validation, DNS vetting/address selection, handler/connect setup, dispatch, bounded body read, and result mapping. A useful extraction carries explicit values (validated URI, pinned IP, deadline/cancellation state) rather than re-reading DNS or recreating policy decisions. Rejection ordering is observable through diagnostics and `DispatchStarted`; helper extraction must preserve it. Review independently checks the security-sensitive transport diff beyond tests.

## Implementation Notes

Implementation will stay within `GuardedTargetHttpClient.cs` and additive coverage in `OutboundSafetyTests.cs`. Extract DNS validation/pinned-address selection, guarded handler construction, and dispatch/response reading into private stages with explicit URI, validated address, deadline, and dispatch-boundary state. Preserve exception/result mapping and the exact point where `DispatchStarted` becomes true. Keep test-host hostname/port mismatch rejection before DNS resolution because the hostname check is not redundant with endpoint validation. Add assertions for DNS not being queried on a mismatched test endpoint, declared-length oversize metadata, and successful target-error response metadata/repeated headers. Do not edit the frozen intent, policy, DI, application contract, or existing closeout/architecture tests.

Verification: focused security/architecture/closeout suites passed (62/62); CI-equivalent backend suite excluding the explicitly opt-in browser host runner passed (135/135); Release solution build passed; `git diff --check` was clean.


## Plan Change Log


## Review Triage Log

- Initial quick review: no findings. Independent security audit verified that removing the test-host hostname/port guard let a mismatched claimed test hostname reach DNS resolution before later rejection, contrary to rejection precedence and the exact test-only restriction. Verdict: low; route: patch. Restored the pre-DNS guard and added coverage proving mismatched ports do not invoke DNS. Both final quick and security-focused re-reviews found no remaining issues.


## Verification

**Commands:**
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName~OutboundSafetyTests|FullyQualifiedName~ArchitectureBoundaryTests|FullyQualifiedName~Story9CloseoutTests"` — expected: all focused security and boundary suites pass; PostgreSQL-dependent cases require configured test credentials.
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName!~Story9BrowserHostRunnerTests"` — expected: CI-equivalent backend suite passes.
- `dotnet build backend/ApiPlayground.sln --configuration Release` — expected: successful Release build.
- `git diff --check` — expected: clean diff.
