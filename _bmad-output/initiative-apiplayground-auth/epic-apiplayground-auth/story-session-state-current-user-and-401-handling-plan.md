---
title: 'Session state, current user, and 401 handling'
type: 'feature'
ticket: '7'
created: '2026-11-04'
status: built
route: 'full'
risk: 'low'
context: ['_bmad-output/initiative-apiplayground-auth/architecture-apiplayground-auth/architecture-apiplayground-auth.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The workspace is visible to everyone, and the browser does not know who is signed in. An expired session leaves the user looking at a workspace whose requests fail.

**Approach:** Add a session store that restores the current user on load, shows a gate in front of the workspace, and returns the visitor to the auth screen on sign-out or on a 401 from any API call.

## Boundaries & Constraints

**Always:** Signed-out visitors see only login and registration. Signed-in users see their id and can log out. A 401 from a protected call returns to the auth screen with "Please sign in again.". Login and registration errors never expire the session.

**Never:** No change to the backend endpoints. No new outbound request path. No session token stored in the browser.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Load, valid cookie | `GET /api/auth/me` returns 200 | Workspace shown; header shows the id | None |
| Load, no cookie | `me` returns 401 | Auth screen shown | Not an error |
| Load, server down | `me` fails | Auth screen shown | Treated as signed out |
| Expired session | Protected call returns 401 | Workspace unmounted; auth screen with "Please sign in again." | None |
| Login from gate | Correct credentials | Workspace shown for that user | Wrong credentials stay on the form |
| Log out | Logout returns 204 | Auth screen shown; workspace state cleared | Failure shows an alert and keeps the workspace |

## Tasks & Acceptance

**Execution:**
- [x] `frontend/src/stores/SessionStore.ts` -- session status, restore, sign in, expire, log out
- [x] `frontend/src/components/SessionGate/SessionGate.tsx` -- gate, provider, and 401 hook
- [x] `frontend/src/components/AuthScreen/AuthScreen.tsx` -- full-page login and registration screen (replaces the 1.6 dialog)
- [x] `frontend/src/components/TopBar/TopBar.tsx` -- signed-in id and Log out action from the session store
- [x] `frontend/src/api/apiClient.ts` -- 401 hook, excluding auth endpoints
- [x] `frontend/src/api/authApi.ts` -- `fetchCurrentUserId`, typed `logIn`
- [x] `frontend/src/main.tsx` -- gate wraps `App`
- [x] Tests: `AuthScreen.test.tsx`, `SessionGate.test.tsx`, `App.test.tsx` wrapper

**Acceptance Criteria:**
- Given no valid session, when the app loads, then only the auth screen is shown.
- Given a valid session, when the app loads, then the workspace is shown with the user's id.
- Given an expired session, when a protected call returns 401, then the auth screen shows "Please sign in again.".
- Given a signed-in user, when they log out, then the auth screen is shown and per-user state is cleared.
- Given a failed logout, when the request fails, then an alert is shown and the workspace stays.

**Deviations from the 1.6 plan:**
- The auth screen is full-page, not a dialog, per the UX document. The frozen 1.6 text "auth dialog opened from the top bar" is superseded by this ticket. Needs confirmation.
- The header shows the user id until `me` returns an email. The UX asks for the email; the backend `me` returns only the id.
- Sign-out and expiry reset the workspace, because the RootStore unmounts. The app has no router, so the URL is not preserved.

**Verification record (2026-11-04):**
- `npm test` from `frontend/`: 87 passed, 0 failed (10 files)
- `npm run build` from `frontend/`: OK (`tsc --noEmit` and Vite build, existing chunk-size warning only)
