---
type: epic
title: Backend Refactoring
parent: initiative-apiplayground-backend-refactor
covers: [CAP-1, CAP-2, CAP-3, CAP-4, CAP-5, CAP-6, CAP-7, CAP-8, CAP-9]
after: []
assignee: ""
risk: medium
---

# Backend Refactoring

## Description

Refactor the complete backend into cohesive Domain, Application, Infrastructure, and controller-based API responsibilities while preserving established behavior, data, and outbound security. The approved backend refactoring spec and its companions are the requirement source.

## Outcome

The full approved backend refactoring scope is integrated and releasable in the supported product; its regression and security checks pass, and no unapproved behavior or data change has been introduced.

## Requirements

- CAP-1: Cohesive, maintainable backend responsibilities and removal of evidenced duplication.
- CAP-2: Attribute-routed controllers for application HTTP endpoints.
- CAP-3: Consistent validation and error handling with the approved Problem Details scope.
- CAP-4: Infrastructure-owned persistence with preserved context identity and migration continuity.
- CAP-5: Evidence-based Domain ownership of persisted application models and meaningful rules.
- CAP-6: Explicit API/application/domain/persistence contracts and justified mappings.
- CAP-7: Clear DI/configuration ownership and safe NLog integration.
- CAP-8: Maintainable modern C# and passing measurable quality gates.
- CAP-9: Maintainable guarded outbound transport with all existing security controls preserved.

## Done when

1. CAP-1 through CAP-9 are delivered or explicitly deferred with the user's agreement.
2. The Release build, full applicable backend suite, architecture checks, migration continuity, API contracts, and outbound safety checks pass.
3. Attribute-routed controllers serve every application endpoint; GET /health remains the infrastructure health probe.
4. The approved Problem Details response-body change is implemented only for validation, application, and unexpected errors; existing HTTP statuses, guard bodies, bodyless not-found responses, rich tracer-failure bodies, business behavior, and persisted-data semantics are preserved.
5. Backend changes are integrated and releasable in the supported product without imposing a new deployment requirement.

## Boundaries

One owner and one backend-refactoring outcome across Domain, Application, Infrastructure, API, tests, and existing backend CI gates. Frontend work is limited to approved error-contract compatibility and tests. No product features; no mandatory browser E2E CI job. Preserve the existing smoke-check semantics, context CLR identity, migration stream, outbound safety policy, and removal of the Story 9 CI job.

## References

- parent — _bmad-output/initiative-apiplayground-backend-refactor/initiative-apiplayground-backend-refactor.md, requirements and scope
- spec — _bmad-output/initiative-apiplayground-backend-refactor/spec-backend-refactor/spec-backend-refactor.md, CAP-1 through CAP-9
- requirements — _bmad-output/initiative-apiplayground-backend-refactor/spec-backend-refactor/technical-requirements.md
- current state — _bmad-output/initiative-apiplayground-backend-refactor/spec-backend-refactor/current-state-and-gaps.md
- phase outline — _bmad-output/initiative-apiplayground-backend-refactor/spec-backend-refactor/phased-plan.md
- architecture — _bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md, layer ownership and boundaries

## Notes

- Decision (2026-10-28): The approved full-backend spec supersedes the original narrow epic; guarded outbound transport is included as a separate high-risk lane.
- Decision (2026-10-28): Preserve the full CLR identity ApiPlayground.Api.Data.PlatformDbContext; do not rename it to align its namespace with Infrastructure.
- Decision (2026-10-28): Standardize only validation, application, and unexpected errors on RFC 9457 Problem Details; preserve guard rejection, bodyless not-found, and rich tracer-failure response bodies and current statuses.
- Decision (2026-10-28): Keep GET /health as an infrastructure health probe, not an application controller.
- Decision (2026-10-28): Do not add dotnet format --verify-no-changes as a mandatory backend CI gate; do not restore the Story 9 CI job.
- Decision (2026-10-28): Use the existing smoke-check result and failure semantics as the baseline; changing or removing them requires separate approval.
- Open question: Which existing persisted models and invariants merit Domain ownership? Resolve by evidence-based inventory in the persistence story; do not invent rules.
- Open question: Can the persistence smoke-check placement be improved without changing its current observable result or failure semantics? Evaluate during application-workflow work; any behavior change requires separate approval.
- Open question: Any schema migration requires a specific approved refactoring justification and existing-data compatibility evidence; generate it only with dotnet ef migrations add.
- Decision (2026-10-28): User approved the replacement story set and build order; preserve existing identities 1–5 and use new IDs 6–10 for the additional stories.
- Decision (2026-10-28): The tracer-dispatch controller is the tracer bullet; Domain/persistence and guarded-transport are separate lanes after it, with application workflow/controller/error/DI work sequenced by actual code dependencies.
- Decision (2026-10-28): The Refactor sweep waits on all implementation entries; integrated closeout follows it. Relevant browser E2E remains separately runnable, never a mandatory CI gate.
