# Adversarial Review — ApiPlayground Backend Architecture Spine

**Verdict: Revisions required.** The spine identifies the right architectural boundaries, but several critical contracts are deferred to independently authored stories without enough shared rules to ensure those stories converge. The pairs below are counterexamples: both stories in each pair can satisfy the current spine and relevant ticket/SPEC language while producing incompatible behavior or implementation. The spine therefore describes intent more reliably than it prevents divergence.

## Scope and evidence

Reviewed the architecture spine and its referenced/canonical contract material: initiative ticket index, epic ticket index and description, Story 1 plan, canonical SPEC and both companions (`mvp-scope.md`, `stack.md`), plus the existing baseline API, EF Core context/migration/snapshot, and test-project references. The approved epic ticket set contains Stories 1–9; it does **not** contain the architecture-boundary story that the spine says is scheduled between Stories 1 and 2. Story 1 is frozen as the accepted single-project baseline and explicitly disallows product request dispatch.

## Findings

### F-1 — Critical: Request/result contract has no canonical shape or evolution rule

**Why the rule fails:** AD-2 says Story 3 establishes a minimal cross-layer contract and Stories 4–5 extend it without competing contracts, but leaves exact fields and routes to the stories. The concluding contract sentence lists semantic information, not field names, types, nullability, encoding, or compatibility requirements. “Extend” is not an enforceable schema rule.

**Compliant counterexample pair:**

- **Story 3 implementation A:** Application accepts `ExecuteRequest { Method, Url }` and returns `ExecutionResult { StatusCode, Body }`; API DTOs use `method`, `url`, `status`, and `body`.
- **Story 3 implementation B:** Application accepts `RequestInput { HttpMethod, Uri }` and returns `CallResult { Status, Content }`; API uses `httpMethod`, `uri`, `statusCode`, and `content`.
- Each can send the controlled GET and display the minimal result required by Story 3, and each respects API/Application separation. Story 4 can extend its own chosen shape with query/header/body fields, while Story 5 can independently return headers and elapsed time. The two stories can each pass their own acceptance checks yet disagree on the request and result contract the frontend consumes. An especially subtle valid mismatch: elapsed time measured around the whole Application use case versus only the transport call; `Headers` as a case-insensitive map versus an ordered list preserving duplicates; body as a string versus an arbitrary JSON value.

**Correction:** Add a single normative contract section (or a checked-in contract artifact owned by the spine) that names the Application request/result models, field types/optionality, header representation and duplicate behavior, body representation, timing boundary/unit, and how the HTTP DTO maps to them. Require Stories 3–5 to extend those same models additively and update the same contract/test fixtures; forbid parallel “minimal” and “full” result types for the same execution. Specify whether frontend/API wire compatibility is maintained across story increments. Routes may remain story-level only if acceptance tests consume the published contract rather than independently invented fields.

### F-2 — High: Mutation ownership and request snapshot consistency are deferred, allowing replay/history/collection drift

**Why the rule fails:** The spine says Story 6 defines the canonical snapshot and mutation ownership, and Story 7 extends it, but supplies no invariant about which layer owns the authoritative request, how edits/save/load map to it, or whether history and collections share the same representation. The ticket says History preserves ordinary fields and replay uses the guarded path; Collections extends stored representation and selection loads without sending. Neither defines mutation semantics or snapshot consistency.

**Compliant counterexample pair:**

- **Story 6 implementation A:** History stores an immutable snapshot of the exact dispatched request (including normalized URL and enabled rows only); replay loads it into an editable form, and subsequent edits do not change the history record.
- **Story 6 implementation B:** History stores a mutable request draft (including disabled rows); replay references that record, and editing a loaded request updates the history entry. Both can “store and list dispatched attempts,” load without sending, and replay through the guard.
- **Story 7 implementation A:** A collection request is a copied snapshot; editing a loaded collection request changes only the editor until explicit save.
- **Story 7 implementation B:** A collection item is a live mutable entity; editing loaded values immediately changes the saved item. Both satisfy persistent availability, selection without dispatch, and retry-on-save-failure in their own tests.

The same UI action can thus mutate persisted History in one implementation and not another, or silently overwrite a saved Collection versus requiring an explicit save. Also unresolved: whether a failed dispatch attempt stores the user-entered request or a normalized/guard-transformed request, and whether replay can faithfully reconstruct repeated headers and enabled/disabled rows.

**Correction:** State entity ownership and lifecycle invariants: an attempt is immutable and records the submitted logical request; replay/load creates a detached editable draft; a collection item changes only through an explicit save/update operation; History and Collections use one canonical request-value representation but distinct persistence entities/semantics. Define which normalization is applied before snapshotting and preserve user-meaningful values. Require tests proving edit-after-load does not mutate History and that collection mutation occurs only at the specified save boundary. Decide and specify the field set and sensitive-value omission/redaction behavior before persistence is implemented.

### F-3 — High: EF Core persistence rules do not constrain port granularity, transaction semantics, or failure behavior consistently

**Why the rule fails:** AD-3 correctly locates EF Core and forbids a generic repository, but “use-case-focused persistence ports” and “Story 6 defines ... persistence behavior” do not define the use-case atomicity boundary, ordering relative to dispatch, read/write guarantees, or error contract. Story 6 explicitly makes failed History persistence non-blocking for dispatch, while Story 7 requires failed saves to retain form values and allow retry; their implementations can diverge on whether persistence is attempted before or after dispatch and what “saved” means.

**Compliant counterexample pair:**

- **Story 6 implementation A:** Dispatch first, then insert the attempt; on insert failure return the successful/failed outbound result plus an unsaved-history indicator.
- **Story 6 implementation B:** Insert a pending attempt first, dispatch, then update it; on DB outage continue dispatch and report unsaved history. Both honor “does not block dispatch” and can show an unsaved entry, but leave different persisted states after process termination or update failure, and differ in what a History list may show.
- **Story 7 implementation A:** Save collection request and collection creation in one transaction, so either both exist or neither does.
- **Story 7 implementation B:** Create collection and then save item in separate operations; if item save fails, an empty collection remains. Both retain the form and allow retry, but differ in durable state after failure.

Additional legal variance includes returning EF entities from a port versus application-owned records, `SaveChanges` per entity versus per use case, and list ordering/pagination with no stable tie-break. “No generic repository” alone does not settle these contracts.

**Correction:** Define application-owned persistence port semantics per operation (inputs/outputs, atomicity, ordering and stable identifiers), with Infrastructure translating EF failures into a technology-neutral result/error. Specify History's dispatch/persist ordering and pending/partial-state policy, plus Collection save/create atomicity and idempotent retry behavior. Require stable History ordering (including equal timestamps) and state the persistence consistency expectations. Preserve the explicit non-blocking dispatch rule without leaking `DbUpdateException` or EF types to Application/API.

### F-4 — High: Error semantics are distinguished conceptually but not represented as a shared result contract

**Why the rule fails:** AD-2 says to keep target HTTP responses distinct from local validation/policy rejection, timeout, transport failure, and cancellation, and not claim unsupported causes. It does not say how the distinction is encoded across Application, HTTP, and UI, what outcomes count as a dispatched attempt for Story 6, or whether an HTTP non-2xx response is success or failure. Story 5 requires timeout and other transport outcomes remain distinct; Story 6 stores HTTP and post-dispatch transport attempts but excludes validation and pre-dispatch blocks. These are classifications without a canonical discriminator/taxonomy.

**Compliant counterexample pair:**

- **Story 5 implementation A:** Returns a successful execution result with `HttpStatus=503` for an HTTP 503, while timeout is `Outcome=TimedOut` and no status.
- **Story 5 implementation B:** Returns `Outcome=HttpError` for 503, and transport exceptions are encoded as `Outcome=NetworkError` with an optional status. Both display observed status/timing/headers/body and keep timeout distinct from an HTTP response.
- Story 6 can count B's `HttpError` and post-dispatch failures as attempts, while another conforming consumer may treat only “successful” Application results as attempts. Similarly, a cancellation after dispatch begins may be excluded as cancellation in one implementation and persisted as a transport attempt in another. Both can satisfy the wording unless the result taxonomy and dispatch boundary are specified.

**Correction:** Define a closed or extensible outcome model with separate `HttpResponse` (any status), `PolicyRejectedBeforeDispatch`, `ValidationRejected`, `TimedOut`, `TransportFailed`, and `Cancelled` cases; state which carry an observed HTTP response and whether dispatch began. Define the dispatch-start boundary used by History inclusion. Make diagnostic evidence fields optional and prohibit inferring DNS, TLS, server, or other causes absent direct evidence. Map each case to API status/body consistently, while keeping an upstream HTTP status in the result rather than treating it as the API endpoint's own HTTP status. Add a shared table-driven mapping test used by Stories 3, 5, and 6.

### F-5 — Critical: The required architecture transition is not in the approved ticket sequence, and migration preservation is underspecified

**Why the rule fails:** AD-1 calls four projects “adopted,” and the Design Paradigm/Deferred sections claim a dedicated architecture-boundary story is scheduled after Story 1 and before Story 2. However, the epic ticket index has Story 2 directly after Story 1; there is no transition ticket, acceptance criteria, or verification for it. Story 1's frozen plan requires its initial migration and single API project. Current migration metadata identifies `ApiPlayground.Api.Data.PlatformDbContext`; simply moving files and renaming the context/namespace can affect EF discovery and model snapshot association even if the migration ID string remains unchanged. “Preserving migration identity and history” does not define how to prove an already-initialized database remains recognized, nor whether the context type is allowed to change.

**Compliant counterexample pair:**

- **Transition implementation A:** Move the existing context/migration to Infrastructure and retain the same migration ID, but alter context metadata/snapshot and run `database update` only against a fresh database. Fresh setup passes; an existing database may not be recognized as applied or future migrations may be associated with a different context/history.
- **Transition implementation B:** Keep the old context type/namespace as a compatibility shell and put the new context in Infrastructure, but generate/use a second migration assembly/history stream. Existing databases still start, but schema changes can be tracked twice or diverge.

Both can claim to preserve the visible migration name/history and establish four projects, because there is no transition-specific migration invariant or acceptance test. Worse, Story 2's `after=[1]` does not enforce a missing architecture step.

**Correction:** Reconcile the spine with the actual ticket order: create and approve a transition ticket between Stories 1 and 2, or explicitly defer AD-1 and keep Stories 2–N on the single-project baseline until that ticket exists. The transition ticket must state whether context CLR identity changes, the canonical Infrastructure migration assembly/context/history table, and how legacy databases are handled. Verify both a fresh database and a database migrated by the baseline: migration IDs remain applied exactly once, no destructive/repeated baseline operation occurs, and the next generated migration is discoverable and applies through the canonical context. Include solution/project references, API composition root, design-time EF factory/tool invocation, test references, and CI in its acceptance criteria. Do not claim the transition is scheduled until the ticket index encodes its dependency.

### F-6 — High: Local-only operational rules leave runtime exposure and CI/database behavior open

**Why the rule fails:** AD-6 says local topology and local-only binding are required, but explicitly leaves exact host binding, browser-origin mechanism and local-origin protections open until UI dispatch is enabled. “Must remain local-only” does not select an enforceable binding or prevent a later story from using `0.0.0.0`/`+` for convenience. It also says the boundary story records how CI/local tests obtain PostgreSQL, but no such story is present. Story 1 plan confirms CI baseline checks are required, while its verification only promises frontend/backend checks without secrets/outbound requests; the architecture's persistence integration test requirement does not establish a CI service or connection source.

**Compliant counterexample pair:**

- **Story 3 implementation A:** API listens only on loopback and Vite proxies to it; cross-origin protections are not needed for that topology.
- **Story 3 implementation B:** API listens on all interfaces and enables permissive CORS for the Vite origin; it is reachable from the LAN, even though the intended UX is local. Both can make the local UI-to-API request work and comply with the unresolved topology bullet.
- **Infrastructure/CI implementation A:** PostgreSQL integration tests use a CI-managed PostgreSQL service and run migrations before tests.
- **Implementation B:** Persistence tests are skipped in CI unless a developer-provided connection string exists; local README uses host PostgreSQL. Both can pass current unspecified CI and local setup language while leaving different verification coverage and failure modes.

**Correction:** Before enabling UI dispatch, set an enforceable default: loopback-only API binding, the supported UI/API origin strategy (prefer proxy or explicitly allowlisted development origin), and the CSRF/origin/Host behavior relevant to an unauthenticated local request-triggering API. Specify a negative test proving another interface cannot reach the API. Define a deterministic CI PostgreSQL 17 provision/connection/migration/test strategy that needs no secret or Docker prerequisite for local development; keep CI-specific provisioning distinct from local requirements. Make the integration suite fail rather than silently skip when its configured CI database is absent.

## Cross-cutting recommendation

The spine should separate **invariants** (must be common across all stories) from **story-owned choices** (details that may vary) and attach a verification mechanism to each invariant. For each deferred contract, either make the choosing story a real dependency in the ticket graph or define an explicit default that downstream stories must follow. At minimum, add shared contract fixtures/tests for request/results and error outcomes, lifecycle/ownership tests for persistence, and fresh-plus-upgraded migration and loopback exposure checks. Without these, the current rules prevent some dependency violations but do not reliably prevent independent implementations from diverging.
