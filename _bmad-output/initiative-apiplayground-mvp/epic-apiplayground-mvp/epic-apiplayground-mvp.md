---
type: epic
title: ApiPlayground MVP
parent: initiative-apiplayground-mvp
covers: [CAP-1, CAP-2, CAP-3, CAP-4]
after: []
assignee: ""
risk: high
---

# ApiPlayground MVP

## Description

Deliver a working and maintainable MVP of ApiPlayground: a user can configure and send an HTTP request, inspect its response, find and resend a request from history, and reuse saved requests in collections across work sessions. Outbound requests must follow the safety requirements in the canonical spec. This epic owns the complete product path across the UI, backend, and persistence.

## Outcome

Developers can check an API endpoint and reuse a saved request in a later work session; the spec's success signal and independent safety tests show whether the MVP works as intended.

## Done when

1. The integrated application supports the request configuration and execution in CAP-1 and response inspection in CAP-2.
2. A user can find and resend a prior request and can reuse a saved collection request in a later work session, as defined by CAP-3 and CAP-4.
3. Independent tests verify the implemented SSRF protections, timeout behavior, and response-size limits.
4. The integrated MVP runs locally on the author's computer with the React UI, ASP.NET Core backend, and PostgreSQL available locally; remote hosting and internet access to the application are not required.

## Boundaries

This epic owns the single-repository product path across the React and TypeScript UI, ASP.NET Core Web API, PostgreSQL persistence, and guarded outbound HTTP requests. It does not promise definitive identification of every error's cause. The first demonstrable path is a user configuring a GET request in the UI, the backend sending it with safety protections, and the response appearing in the UI. Platform scaffold, environments, CI, deployment, and operations are part of the opening stories in this epic.

## References

- parent — _bmad-output/initiative-apiplayground-mvp/initiative-apiplayground-mvp.md, scope and boundaries
- spec — _bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md, Capabilities, Constraints, Non-goals, and Success signal
- implementation context — _bmad-output/initiative-apiplayground-mvp/spec-apiplayground/stack.md

## Notes

- Decision (2026-10-12): Keep all four capabilities in one epic for one owner and one cohesive product outcome.
- Decision (2026-10-12): Demonstrate the request path first with a GET request through the UI and guarded backend to a visible response.
- Decision (2026-10-12): Include the platform baseline in the first stories of this epic.
- Decision (2026-10-12): Use high risk because incorrect outbound-request security behavior could have significant impact.
- Decision (2026-10-12): Deliver the MVP for local use on the author's computer, including the React UI, ASP.NET Core backend, and PostgreSQL; remote hosting and internet access to the application are not required.
- Decision (2026-10-18): For this epic, approve the safety-first prefix `Local platform baseline` → `Outbound request safety foundation` → `Guarded GET tracer` as an explicit exception to the installed ticket workflow's default tracer-first Set rule. The first successful end-to-end tracer remains the guarded UI → backend → GET → visible minimal result path; no outbound request may be dispatched until the project owner approves the allowed destinations and security policies. This exception does not change the SPEC, UX, or MVP scope.
- Decision (2026-10-18): The breakdown order is platform baseline, outbound safety foundation, guarded GET tracer, complete request methods and inputs, response details, History, Collections, Refactor sweep, then the integrated local MVP and independent outbound-safety suite. Story 1 owns local service, database migration, and no-dispatch test-harness setup; Story 2 owns the backend safeguards; Story 3 connects the approved guard to the UI; later stories extend the request/result and persistence contracts. The Refactor sweep waits for Stories 1–7, and the closing suite follows it.
- Decision (2026-10-19): Add `Backend Clean Architecture boundaries` after completed Story 1 and before Story 2. It establishes the four approved backend projects, preserves compatibility for existing and fresh databases, adds boundary verification, and does not alter Story 1 or enable outbound dispatch. Story 2 depends on this boundary story; the safety-first sequence remains platform baseline → architecture boundary → outbound safety → guarded GET.
- Decision (2026-10-25): For Story 9, reuse Story 2's owner-approved outbound policy and controlled-target gate: HTTP/HTTPS only; publicly routable addresses only; validate every A/AAAA answer and pin the connection; disable automatic redirects; enforce 5-second connect and 30-second total deadlines including response-body reads; cap response bodies at 1 MiB. Automated dispatch is limited to a test-owned ephemeral loopback server through the same guard and narrowly scoped test policy; never contact a real external API or weaken production policy. Story 2's plan is the detailed source of this approval.
- Decision (2026-10-25): Story 7 reuses the owner-approved Story 6 sensitive-header classification recorded in the Story 7 plan; the ticket's former unknown is resolved.
- Open question: Sensitive-header classification must be decided before History or Collections persist request values; sensitive values must not be stored before that decision.
- Open question: Settle the exact SSRF protections, timeout policy and thresholds, response-size limits, and tests before implementing the affected stories.
- Open question: Define the target developer scenarios, simplicity evidence, comparison scope, request-validation scope, and objective usability check without adding unsupported assumptions.