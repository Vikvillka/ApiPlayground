---
title: 'Isolation and anonymous-rejection test suites'
type: 'test'
ticket: '8'
created: '2026-11-04'
status: done
route: 'full'
risk: 'low'
context: ['_bmad-output/initiative-apiplayground-auth/architecture-apiplayground-auth/architecture-apiplayground-auth.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Protection and isolation are asserted only per controller. Nothing checks, over real HTTP, that every protected route rejects anonymous callers, that a forged cookie is rejected, or that one user cannot reach another user's collections or history.

**Approach:** Add HTTP-level test suites on a shared PostgreSQL harness. Cover the anonymous 401 contract on every protected route, forged cookies, and two-user isolation through the API.

## Boundaries & Constraints

**Always:** Tests use the disposable database harness and `APIPLAYGROUND_TEST_ADMIN_CONNECTION`. Missing configuration fails the test; it never skips. Each two-user client disables cookie handling so one user's cookie cannot leak into another's requests.

**Never:** No application code change. No change to database roles or the test admin target.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Anonymous call to a protected route | No cookie, 8 routes | 401 `application/problem+json` | Test fails on any other status |
| Forged session cookie | Random cookie value on collections and history | 401 | Test fails on any other status |
| User B lists or saves into user A collections | Valid cookie for B | User A data unchanged, B sees no A data | Test fails on leak |
| User B lists or reads user A history | Valid cookie for B, history seeded for A | B sees none of A's entries | Test fails on leak |

## Tasks & Acceptance

**Execution:**
- [x] `backend/tests/ApiPlayground.Tests/ProtectedRouteHarness.cs` -- shared HTTP harness (database, factory, register and log in, cookie helpers)
- [x] `backend/tests/ApiPlayground.Tests/AnonymousRejectionTests.cs` -- 401 theory over 8 protected routes and forged-cookie fact
- [x] `backend/tests/ApiPlayground.Tests/TwoUserHttpIsolationTests.cs` -- two-user isolation over HTTP for collections and history

**Acceptance Criteria:**
- Given an anonymous caller, when they hit any protected route, then the response is 401 with a problem body.
- Given a forged session cookie, when it is sent to collections or history, then the response is 401.
- Given two signed-in users, when user B reads or writes, then user A's collections and history are not visible or changed.

**Deviations from plan:**
- The `WebApplicationFactory` client keeps a cookie jar, so the second login overwrote the first user's cookie. The two-user clients use `HandleCookies = false`. This was a harness fix, not an application change.
- The harness helpers duplicate private helpers in `AuthEndpointTests.cs`. Consolidation is deferred to ticket 9.

**Validation:** New suites 11 of 11 passed. Full backend suite `dotnet test backend/ApiPlayground.sln --configuration Release` 214 of 214 passed.
