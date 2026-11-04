---
title: ApiPlayground Authentication Brief
status: draft
created: 2026-11-04
updated: 2026-11-04
---

# ApiPlayground Authentication

## Problem

ApiPlayground is a local-only API playground with no concept of a user. All saved data is shared. To support several people on one machine, each with their own collections and requests, the app needs identity.

## Goal

Several users can use one local ApiPlayground instance, and each sees only their own data.

## Scope (MVP)

In:
- Registration with email and password.
- Login and logout.
- Sessions via httpOnly cookie.
- Ownership: every persisted user record belongs to a user and is only visible to that user.

Out:
- OAuth or any third-party identity provider (dropped by the user).
- Password reset (no mail server locally; the user declined an alternative).
- Roles and permissions.

## Constraints

- The API stays loopback-only; existing Host, Origin, and header checks remain.
- Existing data is test data and is wiped when ownership is introduced; no backfill.
- Backend: ASP.NET Core controllers, EF Core migrations generated with `dotnet ef migrations add`. Frontend: React, MobX, MUI.

## Success

- A new user can register, log in, create data, log out, and log back in to find it.
- A second user never sees the first user's data, via UI or direct API calls.
- Unauthenticated requests to protected routes are rejected.

## Open questions (for PRD and architecture)

- Forgotten password has no recovery path. Is that acceptable for the MVP? (Assumed yes.)
- Password hashing and cookie settings (SameSite, CSRF protection) are architecture decisions.
- Session lifetime and logout-everywhere behavior.
- Which entities are user-owned (to be confirmed against the current domain model).
