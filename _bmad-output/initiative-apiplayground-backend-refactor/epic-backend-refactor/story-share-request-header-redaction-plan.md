---
title: 'Share request-header redaction'
type: 'refactor'
ticket: '1'
created: '2026-10-28'
status: done
baseline_revision: 'aa53e5d477da05ccba495d8c6bca6d509c09624c'
route: 'full'
route_source: 'pinned'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** History snapshot creation and saved-collection request saving independently implement the same sensitive-header value redaction and redacted-name collection. Keeping these transformation loops separate risks inconsistent handling as the shared sensitivity rules evolve.

**Approach:** Use one Application-owned header-redaction transformation for both flows, preserving current sensitive-header classification, case-insensitive header lookup, blanked values, redacted-name metadata, and all caller-specific validation, query sanitization, and response behavior.

## Boundaries & Constraints

**Always:** Preserve the existing `RequestHistoryRedaction.IsSensitiveHeader()` rules and case-insensitive header map behavior. Sensitive values remain blank and their original names remain in `RedactedHeaders`; ordinary values remain unchanged. Preserve query URL sanitization, request validation, snapshots, persistence, DTOs, and wire behavior. Keep the helper in Application without introducing dependencies.

**Never:** Do not broaden or narrow sensitive-name classification, change query-value redaction, merge the history and collection workflows, change API responses or persistence schema, expose sensitive values, or touch unrelated controller/transport paths.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Sensitive header | Authorization, Proxy-Authorization, Cookie, Set-Cookie, or matching API-key/token header | Value is empty and original name is recorded as redacted | No sensitive value is retained |
| Ordinary header | Non-sensitive name and value | Value is preserved and name is not redacted | No error |
| Header casing | Sensitive name with any casing | Classification remains case-insensitive in history and saved requests | No error |
| Query sanitization | URL with sensitive and ordinary query parameters | Existing sanitizer blanks only sensitive query values in both flows | Preserve existing URL format and redaction behavior |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Application/Outbound/RequestHistory.cs` — `RequestHistoryRedaction` owns current header sensitivity rules and URL query sanitizer; history snapshot creation currently duplicates the map transformation here.
- `backend/src/ApiPlayground.Application/Outbound/RequestCollections.cs` — `SaveRequestUseCase.SaveAsync()` validates collection-specific request fields, then duplicates header redaction; keep validation and query sanitizer calls unchanged.
- `backend/tests/ApiPlayground.Tests/RequestHistoryTests.cs` — history snapshot redaction, body, and query-value expectations; add cases for the full sensitive-header set and casing while retaining existing assertions.
- `backend/tests/ApiPlayground.Tests/RequestCollectionTests.cs` — collection redaction, query behavior, save validation, and persistence assertions; retain field/persistence checks and exercise same shared classification.
- `_bmad-output/initiative-apiplayground-backend-refactor/spec-backend-refactor/technical-requirements.md` — require evidenced duplication removal without changing behavior or adding speculative layers.

## Tasks & Acceptance

**Execution:**
- [x] `RequestHistory.cs` — add a shared header-map transformation that returns sanitized values and redacted names, and use it in history snapshot creation.
- [x] `RequestCollections.cs` — replace only the duplicated transformation loop with the shared Application helper; preserve preceding validation and all remaining save behavior.
- [x] `RequestHistoryTests.cs` and `RequestCollectionTests.cs` — add additive coverage for sensitive header names and case-insensitive classification across both consumers; retain query, ordinary-header, and persistence assertions.

**Acceptance Criteria:**
- Given the same header map, when history and collection flows sanitize it, then both produce identical sanitized header values and redacted-name lists.
- Given any currently sensitive header name, when either flow snapshots it, then its value is empty and its name is reported as redacted.
- Given an ordinary header, when either flow snapshots it, then its value is preserved and it is not reported as redacted.
- Given a request URL with sensitive query parameters, when either workflow sanitizes the URL, then the existing query redaction behavior is unchanged.
- Given invalid collection request input, when collection validation runs, then existing validation failures occur before persistence and are unchanged.

## Design Notes

The shared rules already exist in `RequestHistoryRedaction.IsSensitiveHeader()`, and both workflows already call the same `SanitizeUrl()`. Extract only the repeated header-map transformation. Keep collection validation before sanitization and leave history’s nullable-header handling and metadata projection intact. History reports query names while saved requests do not; this story does not unify those separate contracts.

## Implementation Notes

Added `RequestHistoryRedaction.SanitizeHeaders()` and routed history snapshot and collection save through it. Added matching assertions for Authorization, Proxy-Authorization, Cookie, Set-Cookie, API-key/token names in mixed casing, and ordinary header preservation. Collection validation, query sanitization, DTOs, persistence and existing behavior remain unchanged.

Verification: focused redaction tests passed (13/13); CI-equivalent backend suite excluding the opt-in browser runner passed (137/137); Release build and `git diff --check` passed.

## Plan Change Log


## Review Triage Log

- Quick review: no findings. Shared transformation preserves case-insensitive classification, blank values, original redacted names, ordinary values, and caller-specific flow behavior; focused and full backend suites passed.

## Verification

**Commands:**
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName~RequestHistoryTests|FullyQualifiedName~RequestCollectionTests"` — expected: focused redaction, validation, and persistence tests pass.
- `dotnet test backend/ApiPlayground.sln --configuration Release --filter "FullyQualifiedName!~Story9BrowserHostRunnerTests"` — expected: full CI-equivalent backend suite passes.
- `dotnet build backend/ApiPlayground.sln --configuration Release` — expected: Release solution build succeeds.
- `git diff --check` — expected: clean diff.
