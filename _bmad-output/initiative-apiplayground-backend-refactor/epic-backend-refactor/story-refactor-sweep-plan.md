---
title: "Refactor sweep"
ticket: 5
status: in-progress
---

# Plan: Refactor sweep (ticket 1.5)

## Owner decisions

1. Include review residuals of tickets 1.1 to 1.9 in this sweep.
2. Remove the per-request persistence round trip entirely; the database is treated as always correct on this path. Remove `ITracerPersistenceSmokeCheck`, its PostgreSQL implementation, its DI registration, its tests, and `persistenceVerified` from the use case result and the tracer JSON response. No raw SQL remains.
3. Do not convert the exception handler to middleware; keep `AddExceptionHandler<UnhandledExceptionHandler>()` to preserve exception ordering.
4. Rename `*Store` to `*Repository` for the request history and collection contracts and their PostgreSQL implementations.

## Scope

- Remove the persistence round trip and `persistenceVerified` (Application, Infrastructure, Api, tests).
- Move `IsPublicAddress` out of `OutboundPolicy` into `NetworkAddressClassifier`.
- Extract `OutboundResult Failed(...)` into `OutboundResults`.
- Rename `*Store` types and files to `*Repository`.
- Move `ToResponse` mappers out of controllers into extension methods under `Mappers/`.
- Split `TracerContracts.cs` and `TracerRequest` so each record lives in its own file under `Contracts/`.
- Group Api source files into `Controllers/`, `Contracts/`, `Mappers/`, `Logging/`, `Errors/`, and `Endpoints/`. Namespaces stay `ApiPlayground.Api`.
- Move `CreateNLogConfiguration` into a `LoggingBuilder` extension (`AddNLogConsole`).
- Make the tracer controller branching readable (blank lines, early returns, helper methods).

## Out of scope

- Contract changes beyond removing `persistenceVerified`.
- Exception handling middleware conversion.
- Schema or migration changes.

## Verification

- Release build of `backend/ApiPlayground.sln` has 0 errors.
- Backend tests pass excluding `Story9BrowserHostRunnerTests`.
- Frontend tests pass after the `persistenceVerified` contract removal.
- No remaining references to `SmokeCheck`, `persistenceVerified`, or the renamed `*Store` types.
