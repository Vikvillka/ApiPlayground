---
tracker_id: ""
key: ""
type: epic
title: "Code snippet, copy, and JSON export for requests"
parent: initiative-apiplayground-frontend-redesign
covers: [N1, N2, N3]
after: [epic-apiplayground-frontend-redesign]
assignee: ""
risk: medium
---

# Code snippet, copy, and JSON export for requests

## Description

The owner asked for three features that the product does not have yet: a code snippet for the current request, copying it to the clipboard, and exporting a request and its response as JSON. They are a new epic after the frontend redesign, so they are built on the new component structure. The backend gets only one endpoint now, for the JSON export. Save Test is out of scope.

## Outcome

A user can view the current request as a code snippet, copy it, and export a request with its response as a JSON file. Each feature is a separate frontend component, and the backend exposes an export endpoint that the frontend consumes through the single request layer.

## Requirements

Proposed; the owner confirms before inception.

- N1: The current request can be shown as a code snippet in a Code tab of the response area, built on the client from the request editor state.
- N2: The user can copy the snippet to the clipboard.
- N3: The user can export a request and its response as a JSON file; the backend provides the export endpoint for it.

## Done when

1. The Code tab shows a snippet for the current request, and Copy puts it on the clipboard.
2. JSON export produces a file for the current request and response through the backend export endpoint.
3. The backend export endpoint is covered by backend tests, and the frontend features are covered by unit tests and the end-to-end suite.

## Boundaries

Backend: only the JSON export endpoint. No server-side code snippet generation, no save or test endpoints, no other backend changes. Frontend: the new features only, each in its own component folder. The existing tracer API contract is unchanged. Save Test is not part of this epic.

- Touch point: backend tracer API — extended with one export endpoint; owner: this epic; existing endpoints unchanged.

## References

- contract — backend tracer API endpoints in the backend API project, the basis for the new endpoint
- design — reference screenshot, which shows the Code and Copy controls; path recorded in the initiative Notes

## Notes

- Decision: new features are a separate epic inside the initiative, not part of epic-apiplayground-frontend-redesign, which keeps "no new features" for its scope; owner, 2026-10-29.
- Decision: backend preparation covers only the JSON export endpoint; owner, 2026-10-29.
- Decision: Save Test is removed from scope, with its save endpoint; owner, 2026-10-29.
- Decision: export format is JSON; owner, 2026-10-29.
- Decision: code snippet languages are `curl` and JavaScript `fetch` (no extra dependencies); owner, 2026-10-29. Python `requests` and C# `HttpClient` are not in scope.
- Decision: breakdown approved as six story entries in tickets.toml (snippet helper, Code tab, Copy, backend export endpoint, frontend export, integrated check); owner, 2026-10-29. The export endpoint path and body shape are decided during inception before story 4 is coded.
