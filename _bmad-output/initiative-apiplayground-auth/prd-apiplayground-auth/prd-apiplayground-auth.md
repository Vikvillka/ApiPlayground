---
title: ApiPlayground Authentication PRD
status: final
created: 2026-11-04
updated: 2026-11-04
---

# ApiPlayground Authentication PRD

Source: [brief](../brief-apiplayground-auth/brief-apiplayground-auth.md)

## Vision

Give ApiPlayground user accounts so each person's saved requests, collections, and request history are separate and persist across sessions.

## Users and journey

Vika is an ordinary user. There are no roles. The users are trusted, but the data of one user must never be visible to another.

UJ-1: Vika opens the app and registers with email and password. She is signed in and sees an empty workspace. She sends requests, creates collections, and builds up a request history. She logs out. Later she logs in again and finds her collections and history exactly as she left them. Another user, Petya, registered on the same instance, sees none of it.

## Scope

In: registration, login, logout, session persistence, per-user ownership of collections, saved requests, and history.
Out: OAuth or any third-party login, password reset, roles and permissions, email verification, profile management.

## Functional requirements

### Accounts
- FR-1: A visitor can register with a unique email and a password.
- FR-2: Registration with an email that is already taken is rejected with a clear message that does not break the form.
- FR-3: Passwords are never stored or logged in plain text.

### Sessions
- FR-4: A registered user can log in with email and password; invalid credentials are rejected with a generic message that does not reveal whether the email exists.
- FR-5: After login the session persists across page reloads via an httpOnly cookie, until logout or expiry.
- FR-6: A logged-in user can log out; the session is no longer valid afterwards.
- FR-7: The UI shows the current user and a logout action; unauthenticated visitors see only login and registration.

### Data ownership
- FR-8: Every collection, saved request, and history entry belongs to exactly one user, set at creation.
- FR-9: Every read, list, and write of these records is limited to the authenticated user's own records. Requests for another user's record behave as not found.
- FR-10: All data and dispatch routes (collections, history, request send, export) reject unauthenticated calls.
- FR-11: Existing data is discarded when ownership is introduced; there is no backfill.

## Non-functional requirements

- NFR-1: Existing loopback-only binding and Host, Origin, and header checks are unchanged.
- NFR-2: Cookie must be httpOnly; other cookie and CSRF protections are decided in architecture.
- NFR-3: Authentication failures do not leak whether an account exists.
- NFR-4: Automated tests cover isolation between two users and the unauthenticated rejection of every protected route.

## Success metrics

- Two users on one instance never see each other's data (verified by tests through UI and direct API calls).
- Vika's data is intact after logout and login.
- Counter-metric: sign-in or registration must not add friction to the existing request-send flow beyond the one-time login.

## Open items

- [ASSUMPTION] No password recovery for the MVP; a forgotten password means a new account.
- [DECIDED] Session lifetime, cookie rules, and CSRF approach are set in the architecture spine (AD-2, AD-3). Password minimum is 8 characters with no composition rules (decision 2026-11-04).
- [DECIDED] Request-send and export routes require login (FR-10).
