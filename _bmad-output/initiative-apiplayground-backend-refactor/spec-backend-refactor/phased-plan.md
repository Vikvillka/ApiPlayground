# Proposed Refactoring Phases — Planning Draft

This plan is a phase outline for later ticket inception, not approved implementation tickets. Keep each eventual story small enough for one build/review cycle and give each a focused verification command.

## Phase 1 — Baseline and contract characterization

Inventory every current route, HTTP verb, request/response field, status/error response, guard condition, application validation outcome, persistence semantic, configuration key, and outbound policy invariant. Keep `GET /health` as an infrastructure health probe, not an application controller. Strengthen tests for any behavior not asserted before changing its owner. Record current schema/migration IDs and the persistence smoke-check semantics.

**Gate:** existing Release build/backend suite pass; new characterization tests capture the approved baseline; no behavior changes.

## Phase 2 — Domain and persistence ownership

Identify persisted entities and meaningful domain rules; place them in Domain in cohesive files without EF dependencies. Keep `PlatformDbContext` physically owned by Infrastructure and preserve its full CLR identity. Separate persistence configuration only where it improves ownership. Check EF model/snapshot continuity and generate a migration with `dotnet ef migrations add` only if a reviewed model change requires one.

**Gate:** architecture reference tests, fresh and existing-history migration-continuity tests, persisted-data/schema assertions, full relevant integration tests.

## Phase 3 — Application use cases and ports

Clarify workflow contracts, port ownership, validation placement, cancellation propagation, and expected versus unexpected failures. Reduce exception swallowing and broad catches only while preserving outcomes and HTTP-visible semantics. Evaluate the persistence smoke check's placement without removing or changing its observed semantics absent approval.

**Gate:** application tests plus endpoint contract tests for all changed outcomes; no infrastructure/framework dependency leaks inward.

## Phase 4 — Controller-based HTTP adapter

Migrate all current application endpoints from Minimal APIs to attribute-routed controllers; keep `GET /health` as the infrastructure health probe. Keep HTTP guard middleware and ordering, request-size/host/origin checks, routes, methods, serialization shape, status codes, and validation behavior. Establish thin actions delegating to application use cases.

**Gate:** controller integration tests exercise each existing route, guard, valid path, and representative failure contract; route inventory diff is empty.

## Phase 5 — API contracts, mapping, validation, and errors

Introduce named API contracts where they clarify the wire boundary, centralize explicit mapping, and consolidate genuinely shared validation. Implement one coherent internal error strategy and centralized exception handling. Standardize validation, application, and unexpected error bodies on the owner-approved RFC 9457 Problem Details contract while preserving existing status codes by default. Keep guard rejection bodies, bodyless not-found responses, and rich tracer-failure outcome bodies unchanged.

**Gate:** contract snapshots/assertions for each affected route, shared-rule parity tests, sanitization checks, cancellation/error tests.

## Phase 6 — DI, configuration, NLog, and modern C# cleanup

Organize dependency registrations, replace justified hard-coded operational settings with typed options and startup validation, and add/configure NLog if still absent. Improve structured logs and trace context while excluding credentials and sensitive request values. Apply focused cleanup for nullability, async/cancellation, resource lifetime, naming, formatting, and analyzers.

**Gate:** startup/configuration tests, safe-log tests or reviewable capture tests, Release build and all applicable unit tests.

## Phase 7 — Infrastructure and guarded transport maintainability

Refactor PostgreSQL stores and outbound policy/transport in isolated slices. Treat guarded transport as high risk: preserve public-only production policy, all DNS-answer checks and address pinning, redirect prohibition, connect/total deadlines, cancellation classification, response-size cap, dispatch tracking, and exact test-only loopback policy. Do not add a second transport path.

**Gate:** focused outbound safety suite, architecture scanner, persistence tests, existing public contract tests; security-sensitive changes reviewed independently.

## Phase 8 — Integrated closeout

Run Release build, full backend suite with disposable PostgreSQL, architecture checks, migration-continuity checks, existing formatting/analyzer checks if configured, relevant frontend contract/build checks, and independent browser E2E when it covers changed integration behavior. Do not add a new mandatory formatting CI gate, restore Story 9 as a mandatory CI job, or alter existing backend test selection. Review the final diff and document any separately approved contract/schema changes.

**Gate:** all required checks pass with no weakened tests or unapproved behavior/schema changes; user reviews the completed story/phase before marking it done.

## Dependency notes

- Phase 1 precedes all behavior-sensitive work.
- Domain/persistence boundaries and use-case contracts inform controller and DTO design.
- Controller migration depends on characterization of current HTTP behavior; the approved Problem Details scope excludes existing guard, not-found, and rich tracer-failure bodies.
- NLog/options can proceed after current configuration and redaction constraints are inventoried.
- Guarded transport work can be sequenced independently only if stories do not collide on common configuration or transport contracts; otherwise add explicit prerequisites.
- Final closeout waits on every implementation phase.
