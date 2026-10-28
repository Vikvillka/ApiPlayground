# Technical Requirements — Complete Backend Refactoring

## Purpose and scope

Refactor the existing, working ApiPlayground backend for readability, cohesion, maintainability, reuse, and consistent architecture. This is not a feature rewrite. Scope covers all four runtime projects (`Api`, `Application`, `Domain`, `Infrastructure`), backend tests, backend CI interaction, and operational configuration where relevant. Include the guarded outbound transport in the quality review; preserve its current security policy and externally observable dispatch behavior.

## Required architecture

- Preserve inward dependency direction: Domain has no project dependencies; Application depends on Domain; Infrastructure implements inner-layer ports and depends on Application/Domain; API is the HTTP adapter and composition root.
- Domain owns meaningful persisted application entities/models and domain rules. Keep EF Core and infrastructure dependencies out of Domain. Do not invent entities or rules solely to make the layer appear populated.
- Application owns use cases/workflow orchestration, application contracts, validation responsibilities, and ports needed by inner workflows. Avoid coupling it to ASP.NET Core or EF Core.
- Infrastructure owns `DbContext`, EF mappings/migrations, PostgreSQL adapters, and outbound transport implementation.
- Preserve the full CLR identity `ApiPlayground.Api.Data.PlatformDbContext` for migration/design-time compatibility, even if the implementation remains physically owned by Infrastructure.
- API owns controllers, HTTP request/response contracts, HTTP-specific validation/translation, middleware, and composition-root registration. Controllers delegate application work; they do not contain business or persistence logic.
- Use abstractions, DI, records, mapping layers, and parameter objects only where they establish a real boundary, improve cohesion/testability, or reduce meaningful duplication.

## HTTP endpoint migration

- Attribute-routed ASP.NET Core controllers are mandatory for application endpoints; Minimal API endpoint mappings are not the standard.
- Keep `GET /health` as the infrastructure health probe outside this migration.
- Migrate the current tracer, history, and collection routes to controllers.
- Preserve route templates, HTTP methods, request/response JSON shape, status codes, guard behavior, origin/host checks, body-size checks, and middleware ordering by default.
- Add/retain API contract tests for every migrated action and its security guard behavior.

## Models, DTOs, mapping, and validation

- Keep Domain models, application contracts, persistence representations, and API wire models at explicit boundaries; never expose persistence or domain models accidentally.
- Use immutable records for suitable data-transfer values. Keep one primary class per source file when it improves navigation.
- Centralize mapping where repetition or contract safety warrants it. Evaluate explicit mapping versus AutoMapper/another package against actual mapping volume; do not add a package before a reasoned decision.
- Establish a coherent validation design, placing rules in the layer that owns them and sharing only genuinely common rules. Preserve current validation outcomes and messages unless a separately approved contract change says otherwise.

## Error handling

- Use a consistent strategy for expected application failures, validation failures, unexpected failures, and cancellation.
- Standardize validation, application, and unexpected error response bodies on RFC 9457 Problem Details. Preserve existing HTTP status codes unless separately approved.
- Preserve existing guard rejection bodies, bodyless not-found responses, and rich tracer-failure outcome bodies; these are outside the approved Problem Details body change.
- Evaluate centralized ASP.NET Core exception handling and `IExceptionHandler`; avoid duplicate broad catches and swallowed failures.
- Do not leak exception details, database/provider names, credentials, or other sensitive values.
- Problem Details is an approved intentional wire-contract change. Do not expose internal exception details; keep each endpoint's existing status code by default.

## DI, configuration, and logging

- Keep composition and registrations explicit and organized by layer/responsibility; no service locator or direct construction of infrastructure adapters inside application workflows.
- Use strongly typed options and startup validation for related operational settings where that provides value. Avoid hard-coded environment-specific values where they should be configurable.
- Do not make the outbound security policy casually configurable or weaken its production defaults. Preserve the current public-only policy, redirect behavior, DNS validation/pinning, deadlines, and response cap unless separately approved.
- Repository inspection found no NLog package integration or NLog calls/configuration. Add and configure NLog during implementation if this remains true; use structured, useful logs and appropriate levels, include trace/correlation context where available, and never log secrets, authorization/cookie headers, sensitive query values, or request credentials.

## Persistence and migrations

- Keep schema and persisted-data semantics stable by default. If a reviewed refactoring need requires a schema change, explain data and migration continuity impact before implementing it.
- The user permits new EF migrations only when generated through the EF tooling command (for example `dotnet ef migrations add`); never hand-author migration source files.
- Preserve existing migration IDs, the single Infrastructure migration stream/history, existing data, and `PlatformDbContext` CLR identity. Verify fresh and already-migrated disposable databases plus generated continuation migration behavior.
- Review the tracer persistence smoke check: preserve its observable success/failure semantics unless a separately reviewed decision approves a change; investigate whether its placement can be improved safely.

## C# and code organization

- Use async I/O and propagate `CancellationToken` where supported; honor cancellation rather than converting it to unrelated failures.
- Maintain clear nullable contracts, resource/service lifetimes, thread safety, and efficient persistence/query behavior.
- Prefer readable modern C# over novelty. Remove dead code, redundant conditions, magic values, unused parameters, and unnecessary complexity when behavior is covered.
- Keep files and folders aligned with the actual responsibilities of the existing solution. Do not introduce generic utility catch-alls, speculative inheritance, excessive wrappers, or patterns for terminology's sake.
- Evaluate formatters/analyzers and warning policy as quality gates; any new tools/packages require a concrete benefit and approved scope.

## Behavior and safety preservation

- Do not intentionally change product behavior, business rules, routes, methods, request/response contracts, authorization/origin protection, validation outcomes, integration behavior, or persisted-data semantics without explicit approval.
- Preserve all outbound SSRF protections, approved-target constraints, timeout/cancellation behavior, redirect policy, dispatch tracking, and response-size safeguards.
- Report suspected existing defects separately; do not silently bundle unrelated fixes.
- Do not delete, weaken, skip, or hide failing tests to make refactoring pass.

## Quality gates and acceptance

Each implementation ticket must have a focused, runnable verification check and, where needed, regression tests before refactoring begins. At minimum:

1. Release build succeeds with no new unexplained compiler/analyzer warnings.
2. Existing backend tests and all relevant new regression/integration tests pass; PostgreSQL tests remain fail-not-skip when configured prerequisites are unavailable.
3. API contract tests show route, method, guard, payload, status, and error behavior are preserved unless the owner approved a specific change.
4. Architecture tests enforce project-reference direction, HTTP/application boundaries, and a single guarded outbound path.
5. Migration-continuity checks preserve migration identity and data/schema expectations; any approved migration is generated by EF tooling and tested against disposable databases.
6. Formatting/analyzer checks pass where configured; no quality gate is weakened.
7. Final diff contains no unrelated files or unapproved contract changes.

The dedicated Story 9 CI job remains removed. Do not restore browser E2E as a mandatory CI gate or add a new mandatory formatting CI gate in this refactor; retain the independently runnable E2E procedure and run it separately when relevant changes warrant it.

## Proposed implementation phases (not tickets)

1. **Baseline and contracts:** inventory routes, payloads, errors, validation, configuration, safety invariants, schema/migrations; strengthen characterization tests for behavior touched later.
2. **Domain and persistence ownership:** identify real persisted entities/domain rules, separate entity files, move ownership without EF leaking inward, preserve context CLR identity and migration history.
3. **Application boundaries:** clarify use-case contracts/ports, validation ownership, cancellation and failure semantics, and remove swallowed/duplicated workflow logic without changing observable behavior.
4. **Controller migration:** migrate application Minimal API routes to attribute-routed controllers; preserve guard middleware and exact wire contracts, and keep `GET /health` as an infrastructure health probe.
5. **Contracts, mapping, and errors:** establish justified API DTOs/mappings and unified internal validation/error handling; apply approved Problem Details bodies only to validation, application, and unexpected failures, retaining existing guard, not-found, and rich tracer-failure bodies.
6. **Configuration, DI, and NLog:** organize registrations, introduce justified typed options/startup checks, and configure safe structured logging.
7. **Infrastructure and outbound maintainability:** simplify persistence and guarded transport in isolated, security-reviewed slices with existing safety tests.
8. **Final quality sweep:** run Release build, full backend suite, architecture/migration checks, relevant independent browser E2E, format/analyzers, and final diff review.

These phases are a planning outline only. Do not start implementation or treat the existing draft ticket breakdown as approved until the user reviews and approves the spec.
