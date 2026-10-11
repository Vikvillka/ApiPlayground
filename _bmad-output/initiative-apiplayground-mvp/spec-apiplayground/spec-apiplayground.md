---
id: SPEC-apiplayground
companions:
  - mvp-scope.md
  - stack.md
sources:
  - ../../brainstorm-apiplayground/brainstorm-apiplayground.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# ApiPlayground

## Why

ApiPlayground explores the hypothesis that developers want a straightforward way to verify API endpoints without learning a complex interface or handling unnecessary settings for routine checks. It aims to let them configure and send a request, inspect its response, and reuse saved requests with low friction. The target audience and any advantage over Swagger or Postman remain hypotheses, not validated facts. This pet project is also the author's practical path to learning BMAD by taking a project from idea to working MVP.

## Capabilities

- **CAP-1**
  - **intent:** A user can configure and send an HTTP request to check an API endpoint.
  - **success:** A user can select GET, POST, PUT, PATCH, or DELETE; set a URL and applicable query parameters, headers, and JSON body; and send a request whose method and values match the selection.
- **CAP-2**
  - **intent:** A user can inspect the response to an HTTP request.
  - **success:** After an endpoint responds, the user can inspect its HTTP status, elapsed time, response headers, and response body.
- **CAP-3**
  - **intent:** A user can find and repeat a previously sent request.
  - **success:** A sent request appears in history; the user can select it and resend it with the same method, URL, parameters, headers, and body.
- **CAP-4**
  - **intent:** A user can save requests in collections for later reuse.
  - **success:** A user can save a request into a collection, find it later, and reuse it; saved requests remain available in a later work session.

## Constraints

- Outbound HTTP requests must include protections against SSRF, enforce timeouts, and limit response size. Exact policy and thresholds remain open questions.
- Keep the primary request-and-response workflow straightforward; additional capabilities must help API work rather than needlessly burden it.

## Non-goals

- The MVP does not promise automatic, definitive identification of every error's true cause. It presents available HTTP and diagnostic evidence; causal claims must not exceed what the request and response support.

## Success signal

A developer can configure and send an HTTP request, inspect its response, find the request in history, and reuse a saved collection request in a later work session. Tests independently verify the outbound-request safety protections. The result is a working MVP with a clear structure for further development, not only a demonstration prototype. The author uses the project to practice the BMAD process from idea through MVP. No time target or subjective ease metric is asserted.

## Open Questions

- Who is the primary target developer beyond the author's own pet-project use, and which concrete scenarios define their needs?
- What evidence or target scenarios would validate a meaningful simplicity advantage over Swagger or Postman?
- Should users be able to compare previous request attempts, or is history and replay sufficient for the MVP?
- Should the client validate URL, parameters, headers, body, or authentication configuration, and which validation behavior is in MVP scope?
- What exact SSRF protections, timeout policy and thresholds, response-size limits, and tests are required?
- Which concrete usability check can demonstrate the intent to avoid lengthy interface learning without imposing an unsupported numeric time target?
