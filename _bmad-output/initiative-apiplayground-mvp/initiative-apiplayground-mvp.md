---
type: initiative
title: ApiPlayground MVP
parent: none
covers: [CAP-1, CAP-2, CAP-3, CAP-4]
after: []
assignee: ""
risk: high
---

# ApiPlayground MVP

## Description

Deliver the ApiPlayground MVP defined by the canonical spec: a straightforward HTTP API client for configuring and sending requests, inspecting responses, and reusing requests from history and collections. The initiative includes the application and the safeguards required for outbound requests. It also supports the author's goal of learning the BMAD process by taking a project from idea to a working MVP.

## Outcome

Developers can check an API endpoint, inspect the response, and reuse a saved request in a later work session; the spec's success signal and independent safety tests show whether the MVP works as intended.

## Requirements

The canonical spec is the requirement source for this initiative. Its capability identifiers CAP-1 through CAP-4 are listed in `covers` and define the work owned by the epic.

## Done when

1. A user can send GET, POST, PUT, PATCH, and DELETE requests with the configured URL, query parameters, headers, and JSON body.
2. A user can inspect a response's HTTP status, elapsed time, headers, and body.
3. A user can find and resend a request from history and reuse a saved collection request in a later work session.
4. Independent tests verify outbound SSRF protections, timeouts, and response-size limits, and the integrated MVP runs locally on the author's computer with the React UI, ASP.NET Core backend, and PostgreSQL available locally; remote hosting and internet access to the application are not required.

## Boundaries

One product boundary covering the web UI, ASP.NET Core API, PostgreSQL persistence, and guarded outbound HTTP requests. This initiative excludes automatic, definitive identification of every error's true cause, as stated in the spec's non-goals. The tracer path is a user configuring a GET request in the UI, the backend sending it with safety protections, and the response appearing in the UI.

## References

- spec — _bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md, Capabilities, Constraints, Non-goals, and Success signal
- implementation context — _bmad-output/initiative-apiplayground-mvp/spec-apiplayground/stack.md

## Notes

- Decision (2026-10-12): Keep the MVP in one epic for the single owner and cohesive product outcome.
- Decision (2026-10-12): Use the UI GET → guarded backend request → visible response as the tracer path.
- Decision (2026-10-12): Put the platform baseline in the first stories of the epic rather than creating a separate epic.
- Decision (2026-10-12): Set initiative risk to high because an outbound-request security mistake could have significant impact.
- Decision (2026-10-12): The MVP must run locally on the author's computer, including the React UI, ASP.NET Core backend, and PostgreSQL; remote hosting and internet access to the application are not required.
- Open question: Which developer audience and concrete scenarios beyond the author's own use should define the target?
- Open question: What evidence or scenarios would validate a simplicity advantage over Swagger or Postman?
- Open question: Is comparing request attempts in MVP scope, or are history and replay sufficient?
- Open question: What URL, parameter, header, body, and authentication validation behavior belongs in MVP?
- Open question: Which exact SSRF protections, timeout policies and thresholds, response-size limits, and tests are required? Settle these before implementation of the affected stories.
- Open question: What objective usability check can demonstrate low-friction use without inventing a time target?
