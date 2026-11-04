---
title: 'End-to-end scenario UJ-1 in the browser'
type: 'feature'
ticket: '10'
created: '2026-11-04'
status: built
route: 'full'
risk: 'medium'
context: ['_bmad-output/initiative-apiplayground-auth/architecture-apiplayground-auth/architecture-apiplayground-auth.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** No browser-level test proves that two users on one local instance keep their collections isolated. The Story 9 journey ran without sign-in.

**Approach:** Add a Playwright spec for UJ-1 (two users, one instance). Add shared sign-in helpers for E2E specs. Run specs with one worker so the Story 9 API restart cannot overlap other specs. Wire the Story 9 browser host with the same auth setup as `Program.cs`.

## Boundaries & Constraints

**Always:** The E2E password is a fixed test-only value on disposable databases. The suite must fail when isolation is broken.

**Never:** No application behavior change. No change to environment settings or database roles. No real passwords in the repo.

## Tasks & Acceptance

**Execution:**
- [x] `backend/tests/ApiPlayground.Tests/Story9BrowserHostRunnerTests.cs` -- mirror `Program.cs` auth wiring (problem details, authorization fallback, identity session, auth endpoints, guard, controllers)
- [x] `frontend/e2e/auth-helpers.ts` -- shared `registerAndSignIn`, `logIn`, `logOut` helpers
- [x] `frontend/e2e/uj1-two-users.spec.ts` -- two users save collections and stay isolated
- [x] `frontend/e2e/story9-mvp.spec.ts` -- sign in before the journey
- [x] `frontend/e2e/story9-runner.ts` -- include the UJ-1 spec and set `workers: 1`

**Acceptance Criteria:**
- Given the local stack with two users, when `npm run test:e2e` runs, then the Playwright suite passes.
- Given collection owner scoping is broken in the backend, when `npm run test:e2e` runs, then UJ-1 fails. Verified with a temporary break in the collections repository and EF query filter, then reverted.

**Deviations from plan:**
- None.
