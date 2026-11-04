---
title: 'Refactor sweep'
type: 'refactor'
ticket: '9'
created: '2026-11-04'
status: built
route: 'full'
risk: 'low'
context: ['_bmad-output/initiative-apiplayground-auth/architecture-apiplayground-auth/architecture-apiplayground-auth.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `AuthEndpointTests.cs` duplicated the database, factory, request, and cookie helpers that the anonymous-rejection and two-user suites already shared in `ProtectedRouteHarness.cs`.

**Approach:** Cleanup only. Point `AuthEndpointTests` at the shared harness and remove its private copies. No behavior change.

## Boundaries & Constraints

**Always:** Test assertions and expected values stay the same. Missing `APIPLAYGROUND_TEST_ADMIN_CONNECTION` still fails.

**Never:** No application code change. No change to frontend code or to environment settings.

## Tasks & Acceptance

**Execution:**
- [x] `backend/tests/ApiPlayground.Tests/ProtectedRouteHarness.cs` -- add `Credentials`; expose `ReadSessionCookie` as internal; reuse `Credentials` in `RegisterAndLogInAsync`
- [x] `backend/tests/ApiPlayground.Tests/AuthEndpointTests.cs` -- use the shared harness; remove private `CreateDatabaseAsync`, `CreateFactory`, `CreateRequest`, `WithCookie`, `ReadSessionCookie`, and the local cookie and password constants

**Acceptance Criteria:**
- Given the refactor, when `dotnet build backend/ApiPlayground.sln` runs, then it succeeds.
- Given the refactor, when `dotnet test backend/ApiPlayground.sln --configuration Release` runs, then all 214 tests pass unchanged.

**Deviations from plan:**
- The end-to-end suite is not part of this ticket's check. It is covered by ticket 10. No frontend code changed.
