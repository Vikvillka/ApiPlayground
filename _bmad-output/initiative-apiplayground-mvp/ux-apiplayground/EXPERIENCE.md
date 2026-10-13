---
name: ApiPlayground
status: final
sources:
  - ../initiative-apiplayground-mvp.md
  - ../epic-apiplayground-mvp/epic-apiplayground-mvp.md
  - ../spec-apiplayground/spec-apiplayground.md
  - ../spec-apiplayground/mvp-scope.md
  - ../spec-apiplayground/stack.md
  - ../../brainstorm-apiplayground/brainstorm-apiplayground.md
updated: 2026-10-13
---

# ApiPlayground — Experience Spine

## Foundation

Desktop-first local-browser experience for configuring HTTP requests and inspecting their observed responses. The implementation context is React + TypeScript, ASP.NET Core Web API / C#, and PostgreSQL; no UI component or design-system library is named or adopted here. `DESIGN.md` is the visual identity reference and owns the palette, typography, and component appearance; this spine defines behavior. The primary workspace keeps request and response visible together. Narrow desktop windows stack those areas vertically. Phone and tablet are out of scope. The UI is English and light-only; no language or theme switch is included.

The product is intentionally a focused API-checking tool, not a full Postman replacement. The product's simplicity advantage over Swagger or Postman remains a hypothesis, not a validated claim. The MVP covers request setup and sending, response inspection, history, and explicitly saved collections, with outbound-request safeguards owned by the canonical spec and still subject to external security decisions.

## Information Architecture

| Surface | Reached from | Purpose |
|---|---|---|
| Request workspace | App open; selecting a history or collection item | Configure a request in the left composer and inspect its response on the right. The [cold-open workspace mockup](mockups/workspace-cold-open.html) illustrates the initial state. |
| History & Collections sidebar | Expand the collapsed sidebar | Find automatically recorded requests or explicitly saved collection requests for reuse. |
| Save-to-collection dialog | Explicit save action in the request workspace | Edit the suggested request title, select an existing collection, or create one inline and save. |

The History & Collections sidebar is collapsed by default. Selecting a history or collection item loads its settings into the editor, does not send a request, and clears any stale response so the response area prompts for a new send. History records dispatched attempts, including HTTP error responses and transport failures after dispatch; a request blocked before dispatch by security policy does not enter History. A History persistence failure does not block sending; the UI reports that the History entry was not saved. Collections change only through explicit save; if a save fails, its dialog stays open with a failure message and retry available. Sensitive header values are not persisted in History or Collections; when a saved request is loaded, each such header value is blank in a disabled row with an instruction to enable and re-enter it. Exact sensitive-header classification is unresolved.

## Voice and Tone

Use short, direct English copy. Report observable facts and the next relevant action; do not claim a definitive cause that the request and response cannot establish. Final wording for errors remains open.

| Prefer | Avoid |
|---|---|
| “Enter a URL to send a request.” | Vague disabled controls with no explanation. |
| “HTTP 404” with the received response data. | Treating every non-2xx status as a transport failure. |
| “Request timed out.” / “Could not connect.” | Claiming the API, network, or user configuration is definitely at fault. |
| “Blocked by security policy. Request not sent.” | Suggesting the request may have reached its destination after a policy refusal. |
| “This result is for the previous request.” | Presenting an old result as if it belongs to the request currently being edited. |

## Component Patterns

Behavioral rules only; visual treatment lives in `DESIGN.md.Components`. Component names match the DESIGN spine exactly.

| Component | Use | Behavioral rules |
|---|---|---|
| Workspace layout | Main request and response area | Keep both areas available together on desktop; stack them vertically in a narrow desktop window. The sidebar may collapse independently. |
| Request line | Method, URL, and Send controls | Keep the row fixed above request tabs. Start with GET and a blank URL; Send is unavailable until a URL is entered. |
| Request tabs | Params, Headers, and Body editor sections | Switch among the three clearly named sections; preserve the request values while changing tabs. |
| Key/value row | Query parameter and header entries | Label fields Key and Value; provide an Enabled checkbox checked by default, and add/remove actions. Disabled rows remain present but are not included in the request. Loaded sensitive-header values are blank and disabled, with an instruction to enable and re-enter them before use. |
| JSON body editor | Body tab | Provide a simple JSON field and syntax validation. Do not imply advanced schema, auth, or editor features. |
| Send action | Dispatch the configured request | While sending, show progress, prevent duplicate sends, and retain the previous result with an explicit prior-request label. |
| Response summary | Status and elapsed time | Show the observed HTTP status and elapsed time, including HTTP error statuses; do not translate a status into a root-cause diagnosis. |
| Response content | Response headers and body | Provide Body and Headers tabs, with Body selected by default. Format JSON for reading, with no raw-mode toggle. |
| History & Collections sidebar | Request reuse navigation | Collapse by default. History records only dispatched attempts, including HTTP and post-dispatch transport failures; a pre-dispatch security-policy block is not recorded. A failed History write does not block sending; the UI reports that the History entry was not saved. Collections change only through explicit save. Selecting an item loads the editor and never sends. |
| Saved request item | A history or collection entry | Selecting loads method, URL, parameters, headers, and body into the editor. Sensitive header values are omitted; each appears blank in a disabled row with an instruction to enable and re-enter it before use. |
| Save-to-collection dialog | Explicit collection save | Suggest an editable title derived from method and path; allow an existing collection to be chosen or a new collection to be created in this save flow. On save failure, retain the open form and its values, show a failure message, and allow retry. |
| Status and feedback message | Validation, sending, response, persistence, and failures | Communicate state in text and programmatically, not color alone. Distinguish HTTP error responses, timeouts, other transport failures, security-policy rejections, and persistence failures; explicitly say a policy-rejected request was not sent and a failed History write means the History entry was not saved. |

## State Patterns

| State | Surface | Treatment |
|---|---|---|
| Cold open | Request workspace | Show an empty GET request with a blank URL. Send is unavailable and the response area invites the user to send a request; the Response content Body tab is selected by default. |
| URL missing or invalid | Request line | Keep Send unavailable while the URL is blank. Provide actionable validation for an entered invalid URL; exact validation scope and wording remain open. |
| Invalid JSON body | JSON body editor | Identify syntax validation failure in text and associate it with the body field; do not send invalid JSON. Exact validation details remain open. |
| Sending | Request workspace | Show progress and prevent duplicate sends. Keep the previous response visible, clearly labeled as belonging to the previous request. |
| HTTP response | Response summary and content | Show status and elapsed time, with response body under the selected default Body tab and headers under Headers. An HTTP error is still a response, not a transport failure. |
| Timeout or other transport failure | Status and feedback message | Distinguish timeout from other transport failures where known. State the observation without inferring its cause. Exact diagnostic wording remains open. |
| Security-policy rejection | Status and feedback message | Explicitly state that the request was not sent and do not add it to History. Do not speculate about the blocked destination or claim an external service failure. |
| History or Collections empty | History & Collections sidebar | Show a concise empty explanation. History contains dispatched attempts, including HTTP error responses and post-dispatch transport failures; pre-dispatch blocks are excluded. Collection items appear only after explicit save. Exact empty-state wording remains open. |
| Load a saved request | Request workspace | Load configuration without sending and clear the old response; prompt the user to send the loaded request. Each omitted sensitive header value appears blank in a disabled row with an instruction to enable and re-enter it. Example label only: “Header value not saved — enter it again”; exact diagnostic copy remains open. |
| History persistence failure | Request workspace and History & Collections sidebar | Continue the send regardless of History-write failure and clearly report that the History entry was not saved. The dispatched attempt remains classified as sent; do not imply it was stored. |
| Collection save ready | Save-to-collection dialog | Show the suggested editable title, collection selection, and inline collection creation option. Saving remains an explicit user action. |
| Collection save succeeded | Save-to-collection dialog and History & Collections sidebar | The saved request is available in the chosen collection. The post-save confirmation and dialog dismissal behavior are not specified. |
| Collection save failure | Save-to-collection dialog | Keep the dialog open with entered values, show a textual failure message, and leave retry available. Do not claim the request was saved until saving succeeds. |

## Interaction Primitives

- Keep method, URL, and Send together in the persistent request line; Params, Headers, and Body remain separate, plainly labeled tabs.
- Params and Headers use compact Key and Value rows. Each row starts with its Enabled checkbox checked; unchecking excludes it temporarily without deleting it. Add and remove controls act on the relevant row. A loaded sensitive header is blank and disabled with an instruction to enable and re-enter it before use.
- Body accepts simple JSON and reports syntax validation. Do not add schema validation, scripting, or IDE behavior by implication.
- Sending exposes progress, blocks duplicate sends, and retains but labels an earlier result. Loading History or a collection entry is an edit/load action only, never an implicit send.
- The Response content has Body and Headers tabs, with Body selected by default. Selecting either tab changes only the displayed response section.
- History records dispatched attempts only, including HTTP failures and transport failures after dispatch. A pre-dispatch security-policy block is not sent and is not recorded. If automatic History persistence fails, sending proceeds and the interface reports that the History entry was not saved.
- Saving to a collection is explicit. Offer the method-and-path-derived title as an editable suggestion, then choose an existing collection or create one inline. If the save fails, retain the open form and values, show a failure message, and allow retry.
- Do not automatically persist sensitive header values. A reused request that omits them requires the user to enter them again; classification rules are not yet defined.
- Do not add undocumented keyboard shortcuts, bulk operations, request comparison, or collection management controls.

## Accessibility Floor

Target WCAG 2.2 AA for text and meaningful non-text UI boundaries/actions. Contrast calculations and token values are recorded in `DESIGN.md`. Use primary text rather than muted text for essential copy on the background canvas. Do not use the subtle border alone for meaningful control boundaries; use a strong action-colored outline where needed and pair color with a non-color cue. Verify actual rendered foreground/background and boundary pairs. Visual appearance and tokens remain governed by `DESIGN.md`.

- All controls, including tabs, row checkboxes, add/remove controls, save flow, and sidebar controls, must be operable by keyboard with a logical focus order and visible focus.
- Give fields and actions clear programmatic labels; associate validation feedback with the affected field.
- Expose sending, completed response, validation, and error changes to assistive technology using appropriate status/live-region semantics without repeatedly announcing stale content.
- Never rely on color alone to identify selected tabs, HTTP error status, validation failures, transport errors, or security rejection.
- Ensure disabled Send communicates why it is unavailable, and distinguish “not sent” from a completed HTTP response.

## Responsive & Platform

The target is a desktop-first browser used locally. At desktop widths, request composer and response are side by side; in a narrow desktop window, they stack vertically while both remain reachable. The builder will choose the breakpoint and behavior around it during implementation, preserving the vertical stack when the side-by-side layout no longer fits. History & Collections is a collapsible sidebar, closed initially. Phone and tablet are explicitly out of scope; do not add mobile navigation or claim mobile support. No browser matrix is defined.

## Inspiration & Anti-patterns

The brainstorm mentions Postman and Swagger as context for the unvalidated hypothesis that a simpler workflow could help. Borrow only the confirmed basic API-checking ideas: configure a request, send it, inspect the result, and reuse it through History or Collections. Neither product is a feature blueprint, and no superiority claim is established.

Explicit non-goals for this UX: definitive root-cause diagnosis; comparing request attempts; advanced validation; an authentication editor unless the product owner resolves the open auth scope; raw-response mode; IDE-style body tooling; phone/tablet layouts; and unrelated Postman-scale capabilities. Exact SSRF/allowed-target rules, timeout and response-size policies are external product/security decisions and remain open rather than being invented here.

## Open Questions

### External security and implementation decisions

- Which destinations are allowed, and what exact SSRF policy and enforcement rules apply? The security owner must decide; this UX contract does not imply unrestricted destinations.
- What timeout and response-size values apply? The security owner must set the policies and thresholds outside UX.
- Which header names or values are classified as sensitive and therefore omitted from History and Collections? This classification requires an explicit security/implementation decision; the blank, disabled-row treatment after load is settled.
- What history retention period/limit and local-data lifecycle (including any reset or deletion behavior) will be implemented? Do not imply export, backup, or recovery behavior.
- What request validation and authentication configuration, if any, are in MVP scope? No authentication editor or additional validation capability is implied by these spines.

### UX and product discovery questions

- What detailed diagnostic and validation copy should be used? The message must describe observed evidence without overclaiming cause. “Header value not saved — enter it again” is an example label only, not finalized product copy.
- Which developer persona and concrete use scenarios beyond the project author's own use should shape the product, and what usability evidence would validate the low-friction intent? The persona and simplicity advantage remain unvalidated hypotheses.
- What confirmation and dialog-dismissal behavior should follow a successful Collection save? The saved request must be available in the chosen collection, but no success message or dismissal pattern is committed here.

## Key Flows

### Flow 1 — Diagnose a request error (Alex, an illustrative name for the project author/developer working locally; CAP-1, CAP-2, CAP-3)

1. Alex opens the local browser workspace and sees an empty GET request, a blank URL, and the response prompt.
2. Alex enters an endpoint URL, chooses the intended method, and configures any needed Params or Headers; each ordinary row starts enabled. For a body-bearing request, Alex selects the request Body tab and enters valid JSON.
3. Alex chooses Send. While sending, progress is visible, duplicate sends are prevented, and any previous result remains clearly labeled as belonging to the previous request.
4. The request is dispatched. If an HTTP response arrives, the Response summary shows its status and elapsed time. In Response content, the Body tab is selected by default, and the Headers tab is available; JSON is formatted for reading. The dispatched attempt is eligible for History whether the HTTP status is success or failure.
5. Alex sees an HTTP error with its received status/data, or a post-dispatch timeout or other transport failure described as an observed failure rather than an unsupported diagnosis. A dispatched attempt appears in History when persistence succeeds.
6. **Climax:** Alex can inspect the observed error and its available response details, distinguish an HTTP response from a transport failure, and find the dispatched attempt in History when saved.

Failure path: invalid URL or JSON blocks dispatch and the relevant field identifies the validation issue; no request attempt is recorded. A security-policy block explicitly says the request was not sent and is not recorded in History. If a timeout or other transport failure occurs after dispatch, it is a History-eligible attempt. If automatic History persistence fails, sending remains complete and the interface clearly reports that the History entry was not saved. Exact diagnostic wording and security criteria remain open.

### Flow 2 — Reuse and save a request across sessions (Alex, an illustrative name for the project author/developer returning later; CAP-3, CAP-4)

1. In a later work session, Alex expands the History & Collections sidebar and locates a dispatched attempt in History or an explicitly saved request in a collection.
2. Alex selects an item. Its method, URL, query parameters, headers, and body load into the editor; the selection does not send a request.
3. The previous response is cleared. The Response content returns to its Body tab and prompts Alex to send the loaded request, so old data cannot appear to match the new configuration.
4. Each sensitive header whose value was omitted is shown as a blank, disabled row with an instruction to enable and re-enter its value before use. Example label only: “Header value not saved — enter it again”; exact copy remains open.
5. Alex reviews the configuration, supplies any required sensitive values, and explicitly chooses Send to repeat the request. The result follows Flow 1.
6. To keep a request for a later session, Alex explicitly saves it, edits the suggested method-and-path title if needed, and selects an existing collection or creates one inline.
7. **Climax:** Alex has intentionally repeated or saved the request for later without an accidental send or the false impression that omitted sensitive values were retained.

Failure path: if collection saving fails, the save dialog remains open with its entered values, shows a failure message, and leaves retry available; it does not claim success. If Alex has not re-entered an omitted sensitive header, its row remains disabled and is not included in the sent request. Exact sensitive-header classification and diagnostic wording remain open.
