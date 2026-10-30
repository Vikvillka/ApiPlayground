---
tracker_id: ""
key: ""
type: initiative
title: "Frontend refactor and redesign of the request playground"
parent: none
covers: [R1, R2, R3, R4, R5, R6]
after: []
assignee: ""
risk: medium
---

# Frontend refactor and redesign of the request playground

## Description

The frontend is a single monolithic `App.tsx` with one global stylesheet and one HTTP module. This initiative splits it into small components with their own styles, moves state into MobX stores, puts every server call behind one request abstraction, and redesigns the UI in the approved reference style using Material UI and a free icon set. The backend API contract is not changed.

## Outcome

Developers add or change a frontend feature inside one component folder without touching a monolithic file, and users get the redesigned playground with every current behavior intact.

## Requirements

- R1: Every server call goes through one request abstraction; endpoints, methods, and headers stay the same as today.
- R2: Application state lives in MobX stores; components read and change state only through the stores.
- R3: Each component lives in its own folder with its component file and its style file; `App.tsx` only composes; `App.css` is removed.
- R4: The UI is redesigned to match the approved reference screenshot, built with Material UI and a free icon library.
- R5: Current behavior is preserved: unit tests and the Story 9 end-to-end contract pass, or a changed expectation is approved by the owner.
- R6: Helpers that do not touch the DOM are pure and tested; no navigation item exists for a feature the product does not have.

## Done when

1. The frontend is made of small components, each in its own folder with its style file, and `App.css` and the old `tracer.ts` are gone.
2. Every server call goes through the single request layer, and all application state is held in MobX stores.
3. The redesigned UI matches the reference screenshot in layout and uses Material UI with a free icon set.
4. Type check, unit tests, production build, and the Story 9 end-to-end suite pass, with any changed expectation approved by the owner.

## Boundaries

The `frontend/` workspace only. Not the backend: the tracer API contract is consumed unchanged. Not new features; the navigation shows only what exists today. Tracer path: one request sent from the new request editor through the request layer and shown in the new response panel.

- Touch point: backend tracer API — consumed as is, no change; owner: epic-backend-refactor (closed)

## References

- constraint — frontend/vite.config.ts, `/api` proxy and the request headers `Content-Type: application/json` and `X-ApiPlayground-Request: 1`
- contract — frontend/src/tracer.ts, current endpoints and types, to be preserved
- behavior — frontend/src/App.test.tsx and frontend/e2e/story9-mvp.spec.ts, the current behavioral contract
- design — reference screenshot from the owner's request of 2026-10-29; path to be recorded in the epic's Notes

## Notes

- Decision: the frontend is restructured in one initiative with one epic; split by the owner's request of 2026-10-29.
- Assumption: packages to add are `@mui/material`, `@mui/icons-material`, `@emotion/react`, `@emotion/styled`, `mobx`, `mobx-react-lite`; the owner confirms before install.
- Open question: the reference screenshot file path is not recorded; the owner provides it before story 4 starts.
- Decision: new features (Code snippet, Copy, JSON export) are epic 2 in this initiative, planned after epic 1; backend work is limited to the JSON export endpoint; Save Test is removed from scope; owner, 2026-10-29.
