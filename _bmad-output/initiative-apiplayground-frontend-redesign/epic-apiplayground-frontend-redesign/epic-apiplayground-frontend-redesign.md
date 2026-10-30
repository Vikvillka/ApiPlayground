---
tracker_id: ""
key: ""
type: epic
title: "Frontend component architecture, state, and redesign"
parent: initiative-apiplayground-frontend-redesign
covers: [R1, R2, R3, R4, R5, R6]
after: []
assignee: ""
risk: medium
---

# Frontend component architecture, state, and redesign

## Description

The request playground runs as one file. This epic rebuilds the frontend in the order the stories below: a foundation with one request layer and the theme, pure helpers and MobX stores, the request editor carrying one request end to end, the response panel, the history, collections, and save dialog, then a refactor sweep and an integrated verification. Each step keeps the existing tests passing.

## Outcome

A developer changes one feature inside one component folder, and the playground users see the redesigned layout with the same send, history, collection, and save behavior as today.

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

The `frontend/` workspace only. Not the backend: the tracer API contract is consumed unchanged. Not new features; navigation shows only what exists today: the request editor, the response panel, history, and collections.

## References

- constraint — frontend/vite.config.ts, `/api` proxy; request headers `Content-Type: application/json` and `X-ApiPlayground-Request: 1`
- contract — frontend/src/tracer.ts, current endpoints and types, to be preserved in the request layer
- behavior — frontend/src/App.test.tsx and frontend/e2e/story9-mvp.spec.ts
- design — reference screenshot `image.png` at the repository root, confirmed by the owner on 2026-10-30; see Notes

## Notes

- Assumption: the package list (`@mui/material`, `@mui/icons-material`, `@emotion/react`, `@emotion/styled`, `mobx`, `mobx-react-lite`) is confirmed by the owner before install.
- Decision: the reference screenshot is `image.png` at the repository root, confirmed by the owner on 2026-10-30. It is not committed to git.
- Open question: the current layout is single-column at a narrow viewport and lacks the reference's two-column request/response layout, Response/Headers/Code tabs, and Export/Copy actions. The owner has not yet decided whether to match it fully; decide before story 4 (response panel) is closed.
- Assumption: `App.test.tsx` expectations are the behavioral contract; a change requires owner approval. Known Material UI constraints from the current tests: a dialog must unmount on close, use `disableRestoreFocus` with a custom Tab wrap, and keep the history drawer persistent rather than a modal (modal sets `aria-hidden` and breaks `getByRole`); the method select stays native for `selectOption`; response tabs need `aria-controls` and `selectionFollowsFocus`; the status summary text is "HTTP 200" and "12 ms" with no separator text node.
- Unknown: the E2E first run can time out waiting for API readiness (a flake recorded in the backend closeout); the cause is not isolated. Story 7 records it if it recurs.
- Decision: the code snippet tab is not part of this epic; it moved to epic-apiplayground-request-export; owner, 2026-10-29.
