---
id: SPEC-backend-refactor
companions:
  - technical-requirements.md
  - current-state-and-gaps.md
  - phased-plan.md
sources: []
---

> **Canonical contract.** This SPEC and its companions define the proposed complete backend code-quality refactoring. Detailed constraints and acceptance gates are in `technical-requirements.md`; evidence-based findings are in `current-state-and-gaps.md`; the candidate phase sequence is in `phased-plan.md`.

# ApiPlayground Complete Backend Refactoring

## Why

The existing ApiPlayground backend works and has passing regression coverage, but its implementation does not consistently realize the approved layer ownership and maintainability goals: the Domain project is empty, the HTTP adapter uses Minimal APIs despite the owner's controller requirement, endpoint and error responsibilities are concentrated, and logging integration is absent. A phased refactoring is needed to make the whole backend easier to understand, test, and extend without turning code-quality work into an unapproved functional rewrite.

## Capabilities

- **CAP-1**
  - **intent:** Backend responsibilities are cohesive, readable, maintainable, and reusable across the whole codebase, eliminating evidenced duplication without speculative abstractions.
  - **success:** Each phase improves explicitly identified structures, keeps classes and methods focused, and passes focused regression tests and the backend suite without unexplained quality regressions.
- **CAP-2**
  - **intent:** All application HTTP endpoints use ASP.NET Core attribute-routed controllers as the standard HTTP adapter.
  - **success:** Current Minimal API endpoints are migrated while routes, methods, request/response contracts, status/error behavior, and guard protections remain unchanged unless an exact change is separately approved.
- **CAP-3**
  - **intent:** API validation and failure handling follow a coherent, centralized strategy with shared abstractions only where rules genuinely overlap.
  - **success:** Validation, expected application failures, unexpected failures, and cancellation are handled consistently; validation, application, and unexpected error bodies use RFC 9457 Problem Details with existing status codes preserved by default, while guard rejection, bodyless not-found, and rich tracer-failure bodies retain their established formats; sensitive details remain hidden.
- **CAP-4**
  - **intent:** Infrastructure owns persistence technology and implements inner-layer ports while layer dependencies remain directed inward.
  - **success:** Domain and Application do not depend on EF Core or Infrastructure; Infrastructure owns the context, mappings, migrations, and adapters; migration continuity and the `ApiPlayground.Api.Data.PlatformDbContext` CLR identity are preserved.
- **CAP-5**
  - **intent:** Domain contains the persisted application entities/models and meaningful domain rules around which the application is built.
  - **success:** Existing persisted semantics are represented in cohesive Domain types without EF/Infrastructure dependencies or invented abstractions, and the approved schema/data baseline remains intact unless a specific migration is approved.
- **CAP-6**
  - **intent:** Domain, application, persistence, and HTTP transfer contracts have explicit boundaries and maintainable mappings.
  - **success:** API responses do not accidentally expose domain or persistence models; justified request/response DTOs, immutable records, and explicit testable mappings preserve existing JSON contracts.
- **CAP-7**
  - **intent:** Dependency registration, operational configuration, and backend logging have clear ownership and safe behavior.
  - **success:** Meaningful settings use justified strongly typed options/startup checks; NLog is installed/configured if absent; structured logs add useful context without secrets, credentials, or sensitive request values.
- **CAP-8**
  - **intent:** Backend code follows maintainable modern C#/.NET practices and measurable automated quality gates.
  - **success:** Logical changes preserve nullability, async/cancellation, resource lifetime, thread safety, and efficient persistence behavior; Release build, tests, architecture boundaries, formatting, and configured analyzers pass.
- **CAP-9**
  - **intent:** The guarded outbound HTTP policy and transport remain a single secure, maintainable dispatch path.
  - **success:** Refactoring retains the current production public-only policy, all DNS-answer validation and connection pinning, redirect prohibition, connect/total deadlines, cancellation and dispatch semantics, response cap, and exact test-only loopback boundary, verified by the safety suite.

## Constraints

- This is a code-quality refactoring of a working application, not a feature rewrite. Do not silently change business rules, API behavior, integrations, or persisted-data semantics.
- ASP.NET Core attribute-routed controllers are mandatory for application endpoints; Minimal APIs are not the endpoint standard.
- Keep `GET /health` as the infrastructure health probe; the mandatory controller requirement applies to application endpoints.
- Preserve the full CLR identity `ApiPlayground.Api.Data.PlatformDbContext` and existing migration IDs/history. Infrastructure remains the physical owner of the context and migration stream.
- A consistent validation/error-handling strategy is required. Standardize validation, application, and unexpected error response bodies on RFC 9457 Problem Details as an explicitly approved API contract change; preserve current HTTP status codes unless separately approved. Keep existing guard rejection bodies, bodyless not-found responses, and rich tracer-failure outcome bodies unchanged.
- Schema changes are permitted only when a specific refactoring need is reviewed and approved. Any approved EF migration must be generated using `dotnet ef migrations add`; migration source files must never be hand-authored.
- Preserve current persistence smoke-check observable success/failure semantics unless a separately reviewed change is approved; evaluate safe relocation before replacement or removal.
- Preserve all current outbound security and API guard behavior. Refactor the guarded transport in isolated, regression-tested slices; do not introduce a second transport or weaken production policy.
- Introduce interfaces, layers, DTOs, mapping frameworks, CQRS, utilities, wrappers, analyzers, or packages only when evidence shows a concrete cohesion, boundary, testability, operational, or maintenance benefit.
- Keep secrets out of source control and logs; do not log authorization/cookie headers, sensitive query values, request credentials, or raw exception details to clients.
- Do not delete, skip, weaken, or hide tests to make refactoring pass. Record suspected pre-existing defects separately rather than bundling them silently.
- Keep the dedicated Story 9 CI job removed; browser E2E is not a mandatory CI gate for this initiative, though it remains independently runnable for relevant integration verification.
- Do not add a new mandatory formatting CI gate; retain existing CI gate scope.

## Non-goals

- New product capabilities, endpoint additions, or unrelated bug fixes.
- Unapproved changes to API routes, methods, payloads, status codes, validation outcomes, error responses, authorization/guarding, or persisted-data semantics.
- Replacing the existing outbound safety policy or adding another dispatch implementation.
- Requiring browser E2E in CI or restoring the removed Story 9 CI job.
- Adding frameworks, abstractions, layers, or dependencies solely to satisfy terminology or reduce a few lines of code.

## Success signal

The backend is organized around clear Domain, Application, Infrastructure, and controller-based API responsibilities, with safe operational logging and consistent internal validation/error handling. The Release build and all applicable backend regression, integration, architecture, migration-continuity, and focused outbound-safety tests pass; existing API and data behavior is preserved except for changes the owner has explicitly reviewed and approved.

## Open Questions

- Which persisted models and invariants should become Domain-owned types, especially where current EF entities are anemic persistence records? Confirm during Phase 1 inventory; do not invent business rules.
- Should the existing tracer persistence smoke check remain in the successful request path, or may a later reviewed design relocate it while preserving current response/failure semantics?
- For any proposed schema migration, what exact domain/persistence need justifies it and what existing-data compatibility evidence is required before approval?
