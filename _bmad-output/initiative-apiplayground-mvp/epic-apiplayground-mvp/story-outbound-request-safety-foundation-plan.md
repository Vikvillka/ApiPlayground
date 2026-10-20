---
title: 'Outbound request safety foundation'
type: 'feature'
ticket: '2'
created: '2026-10-20'
status: 'ready-for-dev'
baseline_revision: '9b39ea0ef409751f5365504531da6a426ed762a9'
route: 'full'
route_source: 'pinned'
risk: 'high'
review: ''
review_source: ''
lenses_ran: []
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md'
  - '_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md'
  - '_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/mvp-scope.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Outbound target requests need a single guarded path that enforces owner-approved destination policy, SSRF/DNS-rebinding protections, redirect behavior, timeouts, and response-size limits before any product endpoint can dispatch.

**Approach:** Add one Application-owned outbound port and one fail-closed Infrastructure adapter using the approved HTTP(S), public-address-only policy. Verify the guard with focused tests against a self-hosted controlled HTTP server bound to a random loopback port; do not expose product/API/UI dispatch (ticket 1.3 owns the tracer).

**Decision (2026-10-20):** Permit HTTP and HTTPS schemes only, and only publicly routable destinations. Reject localhost, loopback, private, link-local, and other non-public/special-use addresses. Resolve and inspect all DNS A/AAAA answers; pin the connection to a validated address to prevent rebinding. Disable automatic redirects. Enforce a 5-second connect timeout, 30-second total request deadline including response-body reads, and a 1 MiB (1,048,576-byte) maximum response body. Tests must create and stop their own controlled loopback HTTP server on a random free port; never use real external APIs and never weaken the production guard to enable tests. This loopback target is test-only and is not a product default. **Implementation gate:** the public-only policy is compatible with a test-only loopback target only if the same production guard receives a narrowly scoped, explicit test policy that admits exactly the server's ephemeral loopback endpoint; no global bypass, production default change, or arbitrary loopback allowance is permitted. If this cannot be enforced and proven, stop before dispatch-capable tests and ask the owner rather than weaken the guard.

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Application/` — declare one outbound-execution port; no HTTP transport dependencies or target policy implementation belongs here.
- `backend/src/ApiPlayground.Infrastructure/DependencyInjection.cs` — persistence is currently registered here; add fail-closed guard registration without enabling any API route.
- `backend/src/ApiPlayground.Infrastructure/Outbound/` — reserved by `ArchitectureBoundaryTests` as the sole location for the future guarded adapter; directory/file does not yet exist.
- `backend/tests/ApiPlayground.Tests/` — single `net10.0` xUnit test project with ASP.NET Core test-host support. Existing `InProcessControlledTarget` fake asserts zero dispatch and is not a socket server; preserve that no-dispatch harness and add narrowly scoped adapter tests using a test-owned loopback listener.
- `backend/src/ApiPlayground.Api/Program.cs` — currently only `/health`; preserve it without any request-sending endpoint. Ticket 1.3 owns product/UI tracer wiring.
- `_bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md` — AD-4 requires a single guarded dispatch path, fail-closed explicit policy, test-only dispatch to the approved target through the production guard, and product dispatch disabled until Story 3.
- `_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/mvp-scope.md` — requires SSRF protections, request timeout, and bounded response size; does not prescribe thresholds.
- `_bmad-output/initiative-apiplayground-mvp/epic-apiplayground-mvp/tickets.toml` — ticket 2 verification contract: allow/deny, DNS/rebinding, redirects, timeout, response-size, and no UI dispatch.

## Tasks & Acceptance

**Execution:**
- [ ] `backend/src/ApiPlayground.Application/` — define the minimal outbound port and transport-independent result/error outcomes needed by later stories; keep product request DTOs and endpoints out of scope.
- [ ] `backend/src/ApiPlayground.Infrastructure/Outbound/` — implement a single guarded HTTP(S) adapter with explicit policy, strict public-address validation for every DNS answer, connection pinning, no automatic redirects, bounded connect/total deadlines and streaming body cap; fail closed on missing/invalid policy.
- [ ] `backend/src/ApiPlayground.Infrastructure/DependencyInjection.cs` — register exactly one guarded adapter and require explicit approved policy; do not register alternate dispatch clients, proxies, or API routes.
- [ ] `backend/tests/ApiPlayground.Tests/` — add policy/edge-case tests and a test-owned ephemeral loopback server fixture; exercise production guard code with a narrowly scoped test policy for that exact endpoint, preserving existing zero-dispatch harness assertions.
- [ ] `backend/tests/ApiPlayground.Tests/ArchitectureBoundaryTests.cs` — extend boundary assertions as needed to guarantee that no runtime direct dispatch path is introduced outside the single guarded adapter.
- [ ] `backend/src/ApiPlayground.Api/Program.cs` — verify the safety adapter is not exposed as an HTTP request-sending endpoint; leave product dispatch disabled for ticket 1.3.

**Acceptance Criteria:**
- Given missing or invalid outbound policy, when Infrastructure is configured, then guard resolution fails closed and no unguarded dispatch path is registered.
- Given a destination URI, when its scheme or any resolved A/AAAA address is disallowed, then the adapter rejects it before opening a target connection.
- Given a hostname whose DNS answers change or include a prohibited address, when connection occurs, then the adapter connects only to the validated pinned address or rejects the request; it never re-resolves freely during connection.
- Given a redirect response, when received, then automatic redirect following does not occur and no second target is contacted.
- Given delayed connection/response or oversized streamed body, when the approved test server exercises the guard, then the corresponding 5-second connect deadline, 30-second end-to-end deadline, or 1 MiB body limit is enforced, including cancellation during body reads.
- Given the ticket 2 implementation, when runtime API routes and dependencies are inspected, then no product request-sending endpoint, UI dispatch path, or second outbound transport exists.

## Implementation Notes

## Plan Change Log

## Review Triage Log

## Design Notes

Keep hostname-based HTTP authority and TLS certificate/SNI validation while connecting sockets only to validated numeric addresses. Disable ambient proxy behavior unless an explicitly policy-aware proxy is designed; a proxy can move DNS resolution and connection outside the guard. With `ResponseHeadersRead`, the total deadline must be explicitly carried through streaming body reads; `HttpClient.Timeout` alone does not cover that phase. `Content-Length` is only an early-rejection hint: count bytes while streaming and terminate at the configured cap. The loopback test exception must be an explicit test-only policy passed through the same production adapter, constrained to the exact ephemeral endpoint; it must not become a production fallback or global guard bypass.

## Verification

**Commands:**
- `dotnet build backend/ApiPlayground.sln --configuration Release` — expected: all runtime projects and tests compile.
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter FullyQualifiedName~Outbound` — expected: tests cover scheme/address allow-deny, all DNS answer validation and pinning, redirects, connect and total deadlines, response streaming cap, and test-policy isolation.
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter FullyQualifiedName~ArchitectureBoundaryTests` — expected: single transport boundary remains enforced and no product dispatch path was added.
- `git diff --check` — expected: no whitespace errors.
