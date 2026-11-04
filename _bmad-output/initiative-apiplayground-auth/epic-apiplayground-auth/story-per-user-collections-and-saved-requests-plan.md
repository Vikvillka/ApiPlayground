---
title: 'Per-user collections and saved requests'
type: 'feature'
ticket: '4'
created: '2026-11-04'
status: built
route: 'full'
risk: 'medium'
context: ['_bmad-output/initiative-apiplayground-auth/architecture-apiplayground-auth/architecture-apiplayground-auth.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Collections and saved requests have an `OwnerId` (ticket 3), but reads and writes rely only on each repository method remembering the filter. Nothing stops a future query from leaking another user's rows.

**Approach:** Add an owner scope to `PlatformDbContext` with EF global query filters on `RequestCollectionEntity` and `SavedRequestEntity`. The Postgres repository sets the scope from its owner parameter before each query. A query without a scope returns no rows. Saving into a collection that belongs to another user, or that does not exist, throws `CollectionNotFoundException`, which the controller maps to 404. The history filter is ticket 1.5.

## Boundaries & Constraints

**Always:** Other users' records behave as not found (404), never forbidden. Dependencies run inward. Owner id is passed explicitly from `ICurrentUser`.

**Never:** No `IgnoreQueryFilters` on these entities. No schema change, so no migration. No history filter here (1.5).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| List as user B | User A has collections | User B gets an empty list | None |
| Save into another user's collection | `collectionId` owned by user A, caller is user B | No row written; A's collection unchanged | `CollectionNotFoundException` -> 404 |
| Save into a missing collection | Unknown `collectionId` | No row written | `CollectionNotFoundException` -> 404 |
| Query with no owner scope | Scope not set | Zero collection and saved-request rows | None |
| Read by id with another user's scope | Id owned by user A, scope is user B | Null | None |

## Tasks & Acceptance

**Execution:**
- [x] `PlatformDbContext.cs` -- `OwnerScope` property; query filters on `RequestCollectionEntity` and `SavedRequestEntity`
- [x] `PostgresRequestCollectionRepository.cs` -- set `OwnerScope` in `ListAsync` and `SaveAsync`; throw `CollectionNotFoundException` for a foreign or missing collection
- [x] `Application/Outbound/CollectionNotFoundException.cs` -- new exception
- [x] `ApiControllerBase.cs` -- `NotFoundProblem` helper
- [x] `TracerCollectionsController.cs` -- map `CollectionNotFoundException` to 404
- [x] `RequestCollectionIsolationTests.cs` -- three Postgres two-user tests

**Acceptance Criteria:**
- Given user B, when user B lists collections, then user A's collections and saved requests are not returned.
- Given user A's collection id, when user B saves into it, then the save throws `CollectionNotFoundException`, no row is written, and user A's collection is unchanged.
- Given no owner scope, when collections or saved requests are queried, then no rows are returned.
- Given the change, when the full suite runs, then all tests pass.

**Notes:**
- No update or delete routes exist for collections or saved requests in the MVP. The query filter covers any future route of that kind.
- The 404 HTTP mapping is covered by compilation and code review. No HTTP-level test was added in this ticket.

**Verification record (2026-11-04):**
- `dotnet build ApiPlayground.sln --configuration Release` from `backend/`: OK, 0 warnings, 0 errors
- `dotnet test ApiPlayground.sln --configuration Release --no-build`: 201 passed, 0 failed
