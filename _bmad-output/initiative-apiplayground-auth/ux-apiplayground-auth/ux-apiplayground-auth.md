---
title: ApiPlayground Authentication UX
status: final
created: 2026-11-04
updated: 2026-11-04
---

# ApiPlayground Authentication UX

Source: [PRD](../prd-apiplayground-auth/prd-apiplayground-auth.md). Visual identity comes from the MVP [DESIGN.md](../../initiative-apiplayground-mvp/ux-apiplayground/DESIGN.md) and is not changed here.

## Screens

- **Register** (FR-1, FR-2): email and password fields, one submit. Visitor who is not signed in lands here by default. A taken email shows a clear message under the email field and keeps the typed values.
- **Log in** (FR-4): email and password fields, one submit. Bad credentials show one generic message ("Email or password is incorrect"), the same for unknown emails. Link to register.
- **Signed-in shell** (FR-5, FR-7): header shows the current user's email and a Log out action. Collections, history, and request send appear only here.
- **Log out** (FR-6): one click, returns to the log in screen. No confirmation.

## Visitor view

Unauthenticated visitors see only Register and Log in. No collections, history, or request-send controls are rendered, not just disabled.

## States

- Submitting: the submit button shows progress and is disabled. Fields keep their values.
- Session expired or missing (401 from the API): return to Log in with the message "Please sign in again". Do not lose the URL the user was on.
- Reload while signed in: the session persists, no flash of the visitor view.

## Accessibility

- Every field has a visible label. Errors are linked to their field and announced to screen readers.
- Keyboard: Tab order follows the form; Enter submits.
- Password field is masked, with no reveal toggle in MVP.

## Out of scope

- Password recovery (PRD open item: a forgotten password means a new account).
- Password strength meter; the rule is minimum 8 characters, nothing else.
