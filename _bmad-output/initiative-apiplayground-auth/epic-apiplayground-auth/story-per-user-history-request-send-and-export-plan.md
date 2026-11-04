---
title: 'Per-user history, request send, and export'
type: 'feature'
ticket: '5'
created: '2026-11-04'
status: built
route: 'full'
risk: 'medium'
context: ['_bmad-output/initiative-apiplayground-auth/architecture-apiplayground-auth/architecture-apiplayground-auth.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** History rows have an `OwnerId`, and the send path already records history under the caller's id. But the history table had no query filter, so a future query could read another user's entries. Export carries no stored data, but it must still be reachable only by signed-in users.

**Approach:** Add the owner scope filter to `RequestHistoryEntity`, the same pattern as ticket 4. The Postgres history repository sets the scope before each query and save. Send already passes the owner first. Export takes no stored data, so it needs no owner filter; the global fallback authorization policy rejects anonymous calls to it.

## Boundaries & Constraints

**Always:** Other users' history behaves as not found (404), never forbidden. Owner id is passed explicitly from `ICurrentUser`. Export stays a pure function of the request body.

**Never:** No `IgnoreQueryFilters` on history. No schema change, so no migration. No second outbound request path.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| List as user B | User A has history entries | User B gets an empty list | None |
| Find user A's entry as user B | Entry id owned by user A | Null, so the controller returns 404 | None |
| Read by id with another user's scope | Entry id owned by user A, scope is user B | Null | None |
| Query with no owner scope | Scope not set | Zero history rows | None |
| Send as user A | Authenticated user A | History row written with `OwnerId` = A | Save failure sets `historySaved` false |
| Export anonymously | No session cookie | Rejected by the fallback policy | 401 |

## Tasks & Acceptance

**Execution:**
- [x] `PlatformDbContext.cs` -- query filter on `RequestHistoryEntity` using `OwnerScope`
- [x] `PostgresRequestHistoryRepository.cs` -- set `OwnerScope` in `SaveAsync`, `ListAsync`, and `FindAsync`
- [x] `RequestHistoryIsolationTests.cs` -- two-user history isolation test and no-scope test on PostgreSQL
- [x] `MigrationContinuityTests.cs` -- the JSON round-trip test scopes its context to the row's owner

**Acceptance Criteria:**
- Given user B, when user B lists history, then user A's entries are not returned.
- Given user A's entry id, when user B reads it, then the result is null and no row is returned.
- Given no owner scope, when history is queried, then no rows are returned.
- Given user A sends a request, when the history row is saved, then its `OwnerId` is user A.
- Given the change, when the full suite runs, then all tests pass.

**Notes:**
- Export does not read stored data. Its anonymous rejection comes from the fallback authorization policy. The anonymous-rejection suite that enumerates every route is ticket 8.
- The `MigrationContinuityTests` change only sets the context owner. Its JSON-mapping assertions are unchanged.
- The HTTP-level 404 for history is covered by code review. No HTTP-level test was added in this ticket.

**Verification record (2026-11-04):**
- `dotnet build ApiPlayground.sln --configuration Release` from `backend/`: OK, 0 errors
- `dotnet test ApiPlayground.sln --configuration Release --no-build`: 203 passed, 0 failed (one run earlier had a flaky `StartupConfigurationTests` failure that did not recur)
