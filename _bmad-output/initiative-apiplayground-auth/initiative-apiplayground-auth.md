---
tracker_id: ""
key: ""
type: initiative
title: "Authentication: accounts, sessions, and per-user data"
parent: none
covers: [FR-1, FR-2, FR-3, FR-4, FR-5, FR-6, FR-7, FR-8, FR-9, FR-10, FR-11, NFR-1, NFR-2, NFR-3, NFR-4]
after: []
assignee: ""
risk: high
---

# Authentication: accounts, sessions, and per-user data

## Description

ApiPlayground becomes a multi-user app. Each person registers with an email and password, signs in with an httpOnly cookie session, and works only with their own collections, saved requests, and request history. The PRD owns the requirements and the success metrics; the architecture spine owns the decisions. This initiative delivers them for the local deployment.

## Outcome

Two people on one instance each keep their own requests and history across sessions, and no one can see or change another person's data.

## Requirements

The numbered requirements are in the PRD (FR-1 to FR-11, NFR-1 to NFR-4), cited by id in `covers` and by each epic's `covers`. Requirements are not repeated here.

## Done when

1. Two users on one instance never see each other's collections, saved requests, or history, verified by automated tests through the UI and direct API calls.
2. Every data and dispatch route rejects unauthenticated calls, verified by a test that enumerates those routes.
3. A returning user finds collections and history exactly as left after logout and login.
4. Sign-in and registration add no friction to the request-send flow beyond the one-time login.

## Boundaries

Backend identity, sessions, request guard, and per-user ownership of data; frontend login, registration, logout, and current-user display. Not OAuth or any third-party login, not password reset, not roles, not email verification, not profile management. Tracer path: one user registers, signs in, sends a request, and sees it in their own history.

- Touch point: request guard in `backend/src/ApiPlayground.Api/Program.cs` — extended to all `/api/*`; owner: epic-apiplayground-auth
- Touch point: frontend API layer and vite proxy `frontend/vite.config.ts` — cookie and same-origin requests; owner: epic-apiplayground-auth

## References

- spec — _bmad-output/initiative-apiplayground-auth/prd-apiplayground-auth/prd-apiplayground-auth.md, sections Functional requirements, Non-functional requirements, Success metrics
- constraint — _bmad-output/initiative-apiplayground-auth/architecture-apiplayground-auth/architecture-apiplayground-auth.md, AD-1 to AD-7
- constraint — _bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md, inherited invariants AD-1 to AD-6
- brief — _bmad-output/initiative-apiplayground-auth/brief-apiplayground-auth/brief-apiplayground-auth.md

## Notes

- Decision: OAuth and any third-party login are out of scope (user, 2026-11-04).
- Decision: one epic for the whole initiative; backend and frontend share one owner and one release (user, 2026-11-04).
- Decision: existing collections, saved requests, and history are discarded at migration; there is no backfill (PRD FR-11, architecture AD-6).
- Assumption: no password recovery for the MVP; a forgotten password means a new account (PRD open items).
- Open question: password rules and account lockout are deferred by the architecture; the epic's entries that touch registration wait on them.
