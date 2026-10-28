# Current Backend State and Evidence-Based Gaps

This companion records observations from the repository audit. A gap is not automatically a runtime defect; recommendations require scope/acceptance review.

## Architecture baseline

- Four runtime projects already exist with the intended project-reference direction: Domain has no project references; Application references Domain; Infrastructure references Application and Domain; API references Application and Infrastructure.
- The Domain project contains no hand-authored `.cs` source. The current persisted entities are declared in `PlatformDbContext.cs` under the API namespace.
- The API uses Minimal APIs. `Program.cs` invokes `MapTracerEndpoints`; `TracerEndpoints.cs` combines request-guard middleware, route definitions, validation, application-result-to-HTTP mapping, and anonymous response DTO projection.
- `PlatformDbContext` is physically located in Infrastructure but has CLR identity `ApiPlayground.Api.Data.PlatformDbContext`. Existing architecture approval explicitly preserves that identity. Migration source namespaces and generated metadata are not fully uniform.
- Infrastructure owns EF Core/Npgsql providers and migration files, but the API project also directly references EF Design and EF Relational packages.
- Application owns use cases and persistence/outbound ports. Infrastructure implements PostgreSQL stores and the guarded outbound client.

## Concrete concerns observed

- Header redaction is performed in both history snapshot creation and collection save flow; API response mapping also applies redaction when projecting headers.
- Tracer routes duplicate responsibilities and use anonymous objects for several wire responses; response DTO projection is embedded in the endpoint class.
- Request validation is split between API and Application. Some rules overlap, while API-only and collection-only requirements differ; blanket consolidation risks behavior drift.
- Collection routes catch broad exceptions and return 503 responses. The tracer use case catches and suppresses history/persistence exceptions. No centralized exception-handler implementation was found. Cancellation and error semantics need characterization before restructuring.
- `GetTracerUseCase` performs the tracer persistence smoke check after successful outbound execution. The Infrastructure implementation performs a database round-trip check. Its successful-response failure semantics are observable and require an explicit preservation decision before changing/removing it.
- Connection configuration is read from the standard configuration provider. Infrastructure DI accepts a nullable connection string. The production outbound policy is constructed in `Program.cs`; security-related policy and body/host/origin controls include fixed values.
- Repository search found no NLog package reference, NLog configuration, or NLog logging calls in backend source. NLog integration is authorized by the user if the absence is confirmed during implementation.
- Existing tests cover routes/guards, validation, outbound safety, migration continuity, persistence, architecture boundaries, and response mapping. CI runs frontend and backend jobs; backend CI excludes the opt-in browser-host runner. The dedicated Story 9 CI job was removed by user direction.

## Evidence locations

- Project references and API package references: `backend/src/ApiPlayground.*/*.csproj`.
- Composition and endpoint behavior: `backend/src/ApiPlayground.Api/Program.cs`, `TracerEndpoints.cs`.
- Application workflows/contracts: `backend/src/ApiPlayground.Application/Outbound/*.cs`.
- Persistence and entities: `backend/src/ApiPlayground.Infrastructure/Data/PlatformDbContext.cs`, `Data/Migrations/`.
- Infrastructure adapters and policy: `backend/src/ApiPlayground.Infrastructure/Outbound/`.
- Contract, architecture, safety, and migration tests: `backend/tests/ApiPlayground.Tests/`.
- CI: `.github/workflows/ci.yml`.
- Existing architecture decisions and CLR identity compatibility: `_bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md` and the completed backend-boundary story plan.

## Verification evidence at planning time

The audit recorded a successful local Release build, 111/111 tests using the backend CI filter, and 112/112 in the full backend suite. The opt-in `Story9BrowserHostRunnerTests` returned early when its runner-directory environment variable was absent; these backend test counts are not evidence of a browser E2E run. CI does not currently require the dedicated Story 9 browser E2E job.
