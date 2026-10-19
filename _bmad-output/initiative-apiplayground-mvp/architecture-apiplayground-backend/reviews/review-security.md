# Security-Critical Architecture Review

**Review date:** 2026-10-19
**Verdict:** Needs clarification before Story 2 dispatch tests and before Story 3 enables UI-to-API dispatch. The current Story 1 baseline is consistent with its no-dispatch scope, and no outbound target-dispatch implementation was found in the current application code. The spine has sound safety intent, but some boundaries are ambiguous or stricter than the approved scope.

## Scope and evidence

Compared the architecture spine with the canonical SPEC and MVP scope, initiative, epic and its ticket sequence, Story 1's approved plan, UX browser/API context, and current checked-in source. Relevant current code includes the API's health-only route, localhost launch profile, React scaffold, test-host health test, and in-process controlled-target harness. No outbound product endpoint or production target HTTP client exists in this baseline.

## Findings

### High — Separate the approved test target from product destination policy and production dispatch

**Evidence:** AD-4 says Story 2 may dispatch only in focused tests to the owner-approved controlled target, after the guard exists, while UI/product dispatch remains disabled until Story 3. Story 2's ticket separately requires policy approval and tests against the approved target; Story 3 connects the approved guard to the UI. The epic explicitly places the outbound-safety foundation before the guarded GET tracer.

**Risk:** “Controlled target” could mean an in-process fake, a locally hosted test server, or an externally reachable owner-approved endpoint. “Approved target” could also be read as the only destination allowed by the product policy, rather than a test fixture distinct from the still-to-be-decided production allow/deny policy. These readings affect whether tests exercise the real guard and transport, whether test allowances leak into runtime configuration, and whether Story 2 accidentally creates a production dispatch surface. The spine's intended sequencing is sound but does not state these separations precisely.

**Recommendation:** State explicitly that Story 1's harness self-test is readiness-only and has zero dispatch; Story 2's dispatching tests are policy-specific tests using only the separately owner-approved controlled test target, through the same guarded implementation intended for production (with a test-controlled transport/target where appropriate); test-only destination allowances cannot become production defaults; and no user-triggerable/API or UI dispatch is enabled until Story 3. Keep product destination policy and test-target approval as distinct decisions. Do not require any actual dispatch in Story 1.

### High — Local binding and browser-origin protections are unresolved security gates, not just topology choices

**Evidence:** AD-6 requires local-only exposure and says not to expose an unauthenticated request-triggering API to the local network by default. It defers exact host binding, proxy-versus-CORS origin, and “any local-origin protections” until before UI dispatch. The initiative requires the application to run locally and says public/remote hosting and internet access to the application are not required; it does not specify browser-origin protections. The current README starts the API on `localhost` and Vite on `127.0.0.1`; the API currently exposes only `/health`.

**Risk:** A loopback-only API can still be invoked by a webpage opened in the user's browser. CORS alone is not a general request-forgery control, especially for simple requests or other browser request mechanisms. When an unauthenticated endpoint can trigger outbound requests, an attacker-controlled page could turn a local user's browser into a trigger even though the service is not reachable from the LAN. “Any local-origin protections” does not define which threat is addressed or what condition must pass before Story 3.

**Recommendation:** Make the pre-Story-3 decision an explicit browser-to-loopback threat-model gate. Specify the required origin/host/request protections and how they are verified, including how the chosen proxy or CORS arrangement interacts with them. Do not treat CORS configuration by itself as proof of protection. Preserve the approved no-auth/local-first scope unless the owner separately approves authentication; the review does not recommend adding an unrequested login feature. Keep the current baseline health-only and non-dispatching.

### High — Story 9's “independent” safety verification is not enforceable as written

**Evidence:** The SPEC, initiative, and epic require tests to independently verify SSRF protections, timeouts, and response-size limits. Story 2's ticket says the closing suite is outside Story 2's own tests. Story 9 adds independent approved-target cases and a separate human confirmation. AD-5 says Story 9 “retains an independent safety-test suite,” but gives no boundary for independence; it also permits one test project and shared fakes/controlled targets.

**Risk:** A team could satisfy the spine by merely rerunning Story 2's implementation tests or reusing the same assertions and fixtures, which would not provide the separately required closeout check. Conversely, interpreting “independent” as requiring a separate project, tool, or external service would add scope not required by the ticket.

**Recommendation:** Preserve the existing single-test-project decision, but define independence at the verification level: Story 9 owns a distinct closeout suite/cases and execution/reporting outside Story 2's own test set, exercises the settled approved cases against the integrated guarded path, and records the separately required human confirmation. Do not imply that independence requires a new project or a second production dispatch path. Test cases must remain limited to owner-approved targets and policy decisions.

### Medium — The absolute local-only exposure rule tightens scope beyond the cited product decision

**Evidence:** AD-6 says binding/API exposure “must remain local-only.” The initiative and epic say the MVP runs locally on the owner's computer and that public/remote hosting and internet access to the application are not required. Those statements establish local delivery and exclude a requirement for public hosting; they do not explicitly prohibit deliberate LAN access.

**Risk:** The spine turns “not required” into “prohibited,” potentially blocking an owner-approved local-network use without a source requirement. This is stricter than necessary for the safe default and should not be silently treated as an accepted product policy.

**Recommendation:** Keep loopback-only as the default supported/safe configuration and retain the rule against exposing an unauthenticated request-triggering API to the LAN by default. Phrase broader LAN prohibition as a decision/open question unless the owner confirms that the MVP must be inaccessible from other devices on the local network. Public/remote hosting remains out of scope.

### Medium — The four-project migration is described as a scheduled prerequisite, but no such ticket exists

**Evidence:** The spine's Design Paradigm and Deferred section require a dedicated architecture-boundary story after Story 1 and before Story 2, creating four runtime projects and moving persistence. The approved epic ticket list orders Story 2 directly after Story 1; it contains no architecture-boundary ticket. Story 1's approved plan accepts the single-project baseline and does not schedule that migration.

**Risk:** This silently inserts work and changes the approved build order. Because the spine makes the migration a prerequisite to Story 2, it can delay the safety foundation and make an unapproved structural refactor appear already authorized. The four-project design itself is a proposed architecture choice, not a requirement in the SPEC or initiative.

**Recommendation:** Either treat the four-project structure/migration as a proposed choice and implement the guarded path within the approved sequence, or have the owner approve and ticket the migration before describing it as scheduled or mandatory. Do not alter Story 1 or perform outbound dispatch to accommodate the architecture transition.

## Confirmed alignment / no finding

- **Story 1 no-dispatch scope:** AD-4 correctly says Story 1 adds no request-sending endpoint and its harness only verifies readiness. Story 1's plan explicitly forbids dispatch and outbound policy selection. The current `Program.cs` maps only `/health`; `InProcessControlledTarget` increments its count only if invoked, and the self-tests assert readiness with a zero count. No conflict or current dispatch bypass was found.
- **Guarded single path:** A single Application-owned outbound port with one Infrastructure guard is consistent with the SPEC's required SSRF protection, timeout, and response-size limit. The boundary is appropriate; resolve the test/production distinctions described above before dispatch is introduced.
- **Secrets:** AD-6's rule to keep local credentials/data out of version control and supply connection settings via runtime configuration aligns with Story 1's example-only environment file and ignore rules. The tracked `.env.example` contains a conspicuous placeholder, not an actual credential, and current API code reads the connection string from configuration. Continue ensuring test/CI configuration contains no local credentials. No committed live secret was identified in the reviewed source.
- **Policy ownership:** The spine appropriately leaves destination policy, DNS/rebinding and redirect behavior, timeout/response-size thresholds, and sensitive-header classification to owner decisions rather than inventing values. Keep those decisions explicit and resolved at the ticket-defined gates; do not infer that the controlled test target settles the product's allowed-destination policy.

## Source references

- Canonical requirements: `spec-apiplayground.md` (Capabilities, Constraints, Success signal, Open Questions) and `mvp-scope.md` (Request-safety requirements).
- Sequencing and approval gates: `initiative-apiplayground-mvp.md`, `epic-apiplayground-mvp.md`, and `epic-apiplayground-mvp/tickets.toml` (Stories 1–3 and 9).
- No-dispatch baseline: `epic-apiplayground-mvp/story-local-platform-baseline-plan.md` (Never, harness matrix, and Design Notes).
- Implementation evidence: `backend/src/ApiPlayground.Api/Program.cs`, `backend/src/ApiPlayground.Api/Properties/launchSettings.json`, `backend/tests/ApiPlayground.Tests/InProcessControlledTarget.cs`, `backend/tests/ApiPlayground.Tests/HarnessSelfTest.cs`, `README.md`, `.env.example`, and `.gitignore`.
