---
type: epic
title: "Accounts, sessions, and per-user data"
parent: initiative-apiplayground-auth
covers: [FR-1, FR-2, FR-3, FR-4, FR-5, FR-6, FR-7, FR-8, FR-9, FR-10, FR-11, NFR-1, NFR-2, NFR-3, NFR-4]
assignee: ""
risk: high
---

# Accounts, sessions, and per-user data

## Description

A visitor registers, signs in, and gets a cookie session that survives reloads. Every collection, saved request, and history entry belongs to one user and is reachable only by that user. Every data and dispatch route requires login. Existing data is discarded at migration. The architecture spine (AD-1 to AD-7) sets the decisions this epic builds on.

## Outcome

Registered users keep their own requests and history across sessions, and a second user on the same instance sees none of it.

## Requirements

The requirements are the PRD's numbered lines FR-1 to FR-11 and NFR-1 to NFR-4, cited by id in `covers`. Requirements are not repeated here.

## Done when

1. Registration, login, session persistence across reloads, logout, and the current-user display work end to end in the UI on the local stack (FR-1 to FR-7).
2. Collections, saved requests, history, request send, and export are owned per user, and every one of their routes rejects unauthenticated calls (FR-8 to FR-10).
3. The two-user isolation suite and the anonymous-rejection suite run against PostgreSQL and fail when the database is missing; neither is skipped (NFR-4, architecture AD-7).
4. The migration discards the three data tables and adds the required owner to each, with no backfill (FR-11, AD-6); the existing unit and end-to-end suites still pass.

## Boundaries

The backend identity, sessions, request guard, and ownership in the API and Infrastructure projects, plus the frontend login, registration, and logout screens. Not OAuth, not password reset, not roles, not email verification, not profile management. Other epics: none; the initiative has one epic.

## References

- spec — _bmad-output/initiative-apiplayground-auth/prd-apiplayground-auth/prd-apiplayground-auth.md, sections Functional requirements and Non-functional requirements
- architecture — _bmad-output/initiative-apiplayground-auth/architecture-apiplayground-auth/architecture-apiplayground-auth.md, AD-1 to AD-7
- parent backend spine — _bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md, inherited invariants AD-1 to AD-6

## Notes

- Decision: one epic for the initiative (user, 2026-11-04).
- Assumption: password rules, session lifetime (about 8 hours sliding), and the CSRF approach follow the architecture spine until the user changes them.
- Decision: password minimum is 8 characters with no composition rules; lockout is out of scope for the MVP (user, 2026-11-04).
- Decision: the refactor sweep (entry 9) and the end-to-end scenario (entry 10) are in the breakdown (user, 2026-11-04).
- Waits on: nothing outside this epic.
