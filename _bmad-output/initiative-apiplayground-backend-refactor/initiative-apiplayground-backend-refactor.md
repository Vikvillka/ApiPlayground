---
type: initiative
title: ApiPlayground Backend Refactoring
parent: none
covers: [CAP-1, CAP-2, CAP-3, CAP-4, CAP-5, CAP-6, CAP-7, CAP-8, CAP-9]
after: []
assignee: ""
risk: medium
---

# ApiPlayground Backend Refactoring

## Description

Refactor the complete working backend for clearer layer ownership, maintainability, safe operations, and measurable verification. Preserve established behavior and persisted-data semantics except for explicitly approved contract changes.

## Outcome

The backend is organized around meaningful Domain, Application, Infrastructure, and controller-based API responsibilities; the guarded outbound path remains safe; and the agreed regression gates show that approved scope is integrated without unapproved behavior or data changes.

## Requirements

The canonical requirement source is the [backend refactoring spec](spec-backend-refactor/spec-backend-refactor.md), which defines CAP-1 through CAP-9; read all listed companions.

## Done when

1. CAP-1 through CAP-9 are delivered or explicitly deferred with the user's agreement.
2. The Release build, full applicable backend tests, architecture checks, migration-continuity tests, and relevant contract/safety verification pass.
3. API routes, methods, status codes, security guards, business rules, and persisted-data semantics remain stable; validation, application, and unexpected error bodies use approved RFC 9457 Problem Details, while guard, not-found, and rich tracer-failure bodies retain their existing formats.
4. The backend is integrated and releasable in the supported product; no new deployment requirement is introduced.

## Boundaries

One bounded refactoring epic owns Domain, Application, Infrastructure, API, backend tests, and existing backend CI gates. Frontend changes are limited to compatibility with the approved error contract and its tests. No product features; no mandatory browser E2E CI job. The health probe remains an infrastructure endpoint; application endpoints use attribute-routed controllers.

## References

- spec — _bmad-output/initiative-apiplayground-backend-refactor/spec-backend-refactor/spec-backend-refactor.md, CAP-1 through CAP-9 and Constraints
- requirements — _bmad-output/initiative-apiplayground-backend-refactor/spec-backend-refactor/technical-requirements.md
- current state — _bmad-output/initiative-apiplayground-backend-refactor/spec-backend-refactor/current-state-and-gaps.md
- phase outline — _bmad-output/initiative-apiplayground-backend-refactor/spec-backend-refactor/phased-plan.md
- architecture — _bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md, layer ownership

## Notes

- Decision (2026-10-27): Keep this work in a separate initiative; do not reopen or modify the completed MVP epic.
- Decision (2026-10-27): Use one bounded backend-refactoring epic and deliver the candidates as small stories.
- Decision (2026-10-28): The comprehensive approved spec supersedes the earlier narrow CAP-1–CAP-4 scope; include guarded outbound transport in this epic with high-risk isolated verification.
- Decision (2026-10-28): RFC 9457 Problem Details applies to validation, application, and unexpected errors; keep current guard rejection, bodyless not-found, and rich tracer-failure response bodies and statuses.
- Decision (2026-10-28): Keep GET /health as the infrastructure health probe outside the application controller migration.
- Decision (2026-10-28): Do not add a new mandatory formatting CI gate or restore the dedicated Story 9 CI job.
- Decision (2026-10-28): No separate platform-baseline epic is needed: this is a brownfield refactor of the existing deployed/local product, and no new scaffold, environment, deployment, or operations capability is in scope.
