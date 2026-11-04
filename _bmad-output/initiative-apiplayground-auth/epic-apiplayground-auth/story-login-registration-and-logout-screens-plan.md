---
title: 'Login, registration, and logout screens'
type: 'feature'
ticket: '6'
created: '2026-11-04'
status: built
route: 'full'
risk: 'low'
context: ['_bmad-output/initiative-apiplayground-auth/architecture-apiplayground-auth/architecture-apiplayground-auth.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The backend already has `register`, `login`, `logout`, and `me` endpoints, but the browser has no way to use them. A visitor cannot create an account, log in, or log out from the UI.

**Approach:** Add a small auth client on the existing request layer (`apiFetch`) and an auth dialog opened from the top bar. The dialog switches between log in and registration. The top bar gets a Log out action. Errors stay inside the dialog. The session itself is not stored in the browser; the httpOnly cookie carries it. Session state, current user display, and 401 handling are ticket 7.

## Boundaries & Constraints

**Always:** All calls go through `apiFetch` on the same-origin proxy. Error text comes from the server's Problem Details `detail`. The password minimum is 8 characters on registration; no composition rules. Login accepts any non-empty password.

**Never:** No session or user state in MobX yet. No gate that hides the workspace (ticket 7). No change to the backend endpoints. No new outbound request path.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Register new account | Valid email, password of 8+ characters | 201; dialog switches to log in with a notice | None |
| Register duplicate email | Existing email | Server detail shown inside the form; input kept | Form stays usable |
| Register short password | Fewer than 8 characters | Submit disabled | None |
| Log in valid | Correct credentials | 200; dialog closes; cookie set by server | None |
| Log in invalid | Wrong credentials | Server's generic detail shown inside the form | Dialog stays open |
| Log out | Any state | 204; "Logged out." notice | Network or server failure shows the error in the top bar |

## Tasks & Acceptance

**Execution:**
- [x] `frontend/src/api/authApi.ts` -- `registerAccount`, `logIn`, `logOut` through `apiFetch`
- [x] `frontend/src/components/AuthDialog/AuthDialog.tsx` -- log in and registration forms with in-form errors
- [x] `frontend/src/components/TopBar/TopBar.tsx` -- Log in and Log out actions
- [x] `frontend/src/components/AuthDialog/AuthDialog.test.tsx` -- Vitest tests for each screen state and logout

**Acceptance Criteria:**
- Given a visitor, when they register with a new email, then the account is created and the form asks them to log in.
- Given an existing email, when they register, then the duplicate message appears in the form and the form keeps the email.
- Given wrong credentials, when they log in, then the generic message appears and the dialog stays open.
- Given correct credentials, when they log in, then the dialog closes.
- Given a signed-in browser, when the user clicks Log out, then the logout request is sent and the result is shown.

**Notes:**
- The session survives a page reload because the cookie is httpOnly and set by the server. Nothing in the frontend needs to restore it in this ticket.
- Ticket 7 will track the signed-in state and decide what the signed-out visitor sees.

**Verification record (2026-11-04):**
- `npm test` from `frontend/`: 82 passed, 0 failed (9 files)
- `npm run build` from `frontend/`: OK (existing chunk-size warning only)
