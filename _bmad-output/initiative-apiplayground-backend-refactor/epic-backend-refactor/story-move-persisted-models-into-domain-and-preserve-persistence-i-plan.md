---
title: 'Move persisted models into Domain and preserve persistence identity'
type: 'refactor'
ticket: '4'
created: '2026-10-28'
status: 'built'
baseline_revision: '8c0f193c2b338a5d659f8c46d4c98b0aa4c93545'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 2
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Persisted records are declared beside the Infrastructure-owned `PlatformDbContext` and currently have API CLR namespaces, while Domain is empty. Moving storage-shaped types without domain behavior would create an artificial ownership boundary and may destabilize EF model identities.

**Approach:** Inventory persisted models and their consumers, then establish an evidence-based ownership decision. Keep types that are only EF persistence records in Infrastructure; move a type into Domain only when it has demonstrable application/domain meaning independent of storage. Preserve the full CLR identity `ApiPlayground.Api.Data.PlatformDbContext`, existing migration IDs/history, schema, and persisted-data semantics.

## Boundaries & Constraints

**Always:** Keep Domain free of EF Core and Infrastructure references. Keep `PlatformDbContext`, EF mappings, migrations, PostgreSQL adapters, and storage-only records in Infrastructure. Preserve the context's exact CLR name and current migration assembly/history. Inventory entity semantics first; do not invent domain rules or move types merely to make Domain non-empty. Verify model-diff and migration continuity against fresh and already-migrated disposable databases. Generate any approved migration only with `dotnet ef migrations add`.

**Never:** Do not rename or relocate `PlatformDbContext`; do not hand-author migrations; do not alter schema, data semantics, migration IDs, or migration history without a specific reviewed need and approval. Do not create speculative domain abstractions or claim mutable EF records contain domain invariants when the code does not support that conclusion.

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Domain/ApiPlayground.Domain.csproj` — currently has no project dependencies; the project should own any genuine domain entities without EF references.
- `backend/src/ApiPlayground.Infrastructure/ApiPlayground.Infrastructure.csproj` — already references Domain and owns EF Core/Npgsql dependencies; no project-reference changes are expected.
- `backend/src/ApiPlayground.Infrastructure/Data/PlatformDbContext.cs` — declares `ApiPlayground.Api.Data.PlatformDbContext` and three mutable EF entity records (`RequestHistoryEntity`, `RequestCollectionEntity`, `SavedRequestEntity`) alongside fluent schema, index, and relationship mapping. Keep context and mapping here; extract only entity declarations if their move is proven safe.
- `backend/src/ApiPlayground.Infrastructure/Outbound/PostgresRequestHistoryStore.cs` and `PostgresRequestCollectionStore.cs` — construct EF entities and map them to Application snapshots; update imports only if entity namespace changes.
- `backend/src/ApiPlayground.Infrastructure/DependencyInjection.cs` — configures Infrastructure's migration assembly; preserve its current behavior.
- `backend/src/ApiPlayground.Infrastructure/Data/Migrations/` — contains the three existing migration IDs, designer metadata, and model snapshot. Entity CLR identities appear in the snapshot/designer; compare generated model changes and do not accept destructive or schema-changing operations without approval.
- `backend/tests/ApiPlayground.Tests/ArchitectureBoundaryTests.cs` — asserts dependency direction and exact context CLR identity.
- `backend/tests/ApiPlayground.Tests/MigrationContinuityTests.cs` — verifies migration IDs/history, fresh and existing database paths, and following migration discovery; it contains source-path/namespace assumptions that may require updates if entity definitions move.
- `backend/tests/ApiPlayground.Tests/LegacyStoryOneInitialPlatformBaseline.cs`, `RequestCollectionTests.cs`, `Story9BrowserHostRunnerTests.cs`, `Story9CloseoutTests.cs`, and `TracerPersistenceSmokeCheckTests.cs` — exercise context construction, persistence behavior, and the controlled test host; preserve their contracts.
- Persisted model inventory: `RequestHistoryEntity` stores immutable request history evidence; `RequestCollectionEntity` and `SavedRequestEntity` represent a collection relationship. The current entity classes contain no methods or demonstrated domain invariants; validation/redaction reside in Application. Determine whether namespace-only/assembly movement provides meaningful Domain ownership without EF identity churn, and leave an entity in Infrastructure if no genuine Domain ownership is justified by evidence.

## Tasks & Acceptance

**Execution:**
- [x] Inventory each persisted entity, its actual behavior/invariants, consumers, and EF model identity before changing files; document an evidence-based decision for each entity.
- [x] Keep storage-only EF entity declarations in Infrastructure; move a declaration into Domain only if concrete domain behavior/meaning is evidenced, without adding EF references to Domain.
- [x] Keep `PlatformDbContext`, fluent mappings, migration files, migration assembly, and PostgreSQL stores in Infrastructure; update only references justified by an approved ownership change.
- [x] Compare the final EF model against the baseline using EF tooling; no pending schema changes are allowed unless separately approved. Do not create a migration for a code-only ownership change.
- [x] Confirm existing architecture and migration-continuity assertions protect context identity and migration history; add further tests only if an actual model ownership change warrants them.
- [x] Verify architecture, migration-continuity/persistence, and CI-equivalent backend tests, plus Release build and `git diff --check`.

**Acceptance Criteria:**
- Given the persisted entity inventory, when ownership is resolved, then every moved type has evidence of application/domain ownership, or storage-only records remain in Infrastructure; no new business rule is invented.
- Given the updated project graph, when architecture checks run, then Domain has no EF/Infrastructure dependency and all runtime references remain inward.
- Given Infrastructure remains the persistence owner, when the API and tests resolve `PlatformDbContext`, then its full name remains `ApiPlayground.Api.Data.PlatformDbContext` and EF discovers the existing Infrastructure migration stream.
- Given a fresh database or the legacy initial migration baseline, when existing continuity tests run, then the current migration stream applies/recognizes each existing migration exactly once. If a future model change affects a database already at the full current history, add direct preservation/data assertions for that case before implementation.
- Given no approved schema need, when EF model-diff tooling runs, then there are no pending model changes and no new migration is produced.
- Given history and collection persistence tests, when they run, then existing save/load, relationship, redaction, ordering, and smoke-check behavior is unchanged.

## Design Notes

The existing entity classes are mutable EF-shaped records with no demonstrated methods/invariants. Repository evidence does not show behavior/rules beyond persistence shape, so all three stay beside the Infrastructure-owned context; an empty Domain project is preferable to a false ownership boundary. No schema or migration changes are warranted. If a later story identifies genuine domain behavior, it must first establish a distinct domain type and explicit mapping instead of relocating EF records by namespace alone.

## Implementation Notes

No runtime source change is justified: all current entities are persistence records without demonstrated domain behavior and remain with `PlatformDbContext` in Infrastructure. The code diff is intentionally empty; the checked-in plan records this evidence-based ownership outcome. No schema or migration changes are made.

## Verification

**Commands:**
- `dotnet build backend/ApiPlayground.sln --configuration Release` — passed.
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName~ArchitectureBoundaryTests"` — passed.
- `dotnet ef migrations has-pending-model-changes --project backend/src/ApiPlayground.Infrastructure/ApiPlayground.Infrastructure.csproj --startup-project backend/src/ApiPlayground.Api/ApiPlayground.Api.csproj` — passed; no model changes.
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName~MigrationContinuityTests|FullyQualifiedName~RequestCollectionTests|FullyQualifiedName~TracerPersistenceSmokeCheckTests"` — passed with configured disposable PostgreSQL prerequisites.
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName!~Story9BrowserHostRunnerTests"` — passed.
- `git diff --check` — passed.

## Review Triage Log

- 1 finding: medium, bad_plan. The move into Domain lacked evidence of domain ownership; declarations contain persistence fields/navigation only and no demonstrated invariants. Re-derived with all storage records retained in Infrastructure; no schema/migration change. Plan now explicitly rejects namespace-only relocation.
- 1 finding: medium, bad_plan. The prior verification description overstated coverage of a database at full current migration history. Existing tests cover fresh DB, legacy initial migration recognition, and generated continuation, not existing current-history data. Corrected acceptance criteria to match tested cases and require direct current-history preservation tests before any future model-changing move.
