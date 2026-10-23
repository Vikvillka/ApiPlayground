Read the plan holding the acceptance criteria and intent, and the files listed in its frontmatter context below. Read agent instruction files (AGENTS.md, CLAUDE.md, or equivalents) at repository root and in directories the diff touches. Then read the unified diff below; this is the change under review.

Report unmet acceptance criteria, broken rules, and bugs. Each finding: the location, what goes wrong, and evidence checked against surrounding code. Output a Markdown list of findings only — no severity, priority, or ranking.

Do not invoke any skill, and do not spawn subagents of your own — you are the reviewer. Return findings as text in your final message.

## Plan
---
title: 'Inspect HTTP response details'
type: 'feature'
ticket: '5'
created: '2026-10-22'
status: 'in-review'
baseline_revision: '21033e1b9c150e8213178c2d006bc262bac43205'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md'
  - '_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/DESIGN.md'
  - '_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent вЂ” do not modify unless human renegotiates">

## Intent

**Problem:** Users can send configured requests but cannot inspect complete response details, especially response headers, or switch between headers and body.

**Approach:** Expand the response contract and viewer to present observed HTTP status, elapsed time, response headers, and body with accessible Body and Headers tabs, Body selected by default, and readable JSON formatting. Preserve the distinction between HTTP error responses, policy blocks, timeouts, and other transport failures without unsupported diagnostic claims.

## Boundaries & Constraints

**Always:** Reuse the existing guarded outbound executor and captured `OutboundResult`; do not add a transport. Preserve public-only production policy, exact loopback-only test target, response-size/deadline limits, 4 KiB API request envelope, 2048-character URL limit, and Story 3 persistence smoke behavior. Display the target's HTTP status independently from the API status; HTTP 4xx/5xx remains an observed response, not a transport failure. Preserve multi-valued response headers. Keep request editing behavior and Body/Headers state separate. Default response tab to Body for a new response and retain prior-result labeling while sending. Format JSON for readability without a raw-mode toggle. Textual status and error cues must remain accessible and not rely on color alone.

**Never:** Add History, Collections, authentication, request scripting/schema validation, raw-response mode, alternate transports, response-header editing, or unsupported root-cause diagnosis. Do not expose secrets or change outbound security policy.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| HTTP success | Controlled response with status, elapsed time, repeated headers, JSON body | Show observed status/time; Body tab selected and JSON formatted; Headers tab exposes all values | No error expected |
| HTTP error | Controlled 4xx/5xx response with headers/body | Display it as a completed HTTP response with target status, elapsed time, headers, and body | Do not classify as transport failure or infer cause |
| Transport failure | Timeout or other executor failure | Show accessible distinct failure state, not response tabs with fabricated HTTP response details | State only observed failure; do not claim cause |
| Policy rejection | Executor rejects before dispatch | State request was not sent; do not show it as an HTTP response | Preserve policy distinction |
| Response tab switch | A response with body and headers | Switch displayed content; Body is selected by default for each new response | Keep request tabs unaffected |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Application/Outbound/OutboundResult.cs` вЂ” source contract already carries nullable target status, elapsed time, case-insensitive multi-value headers, body, and failure. Reuse it.
- `backend/src/ApiPlayground.Infrastructure/Outbound/GuardedTargetHttpClient.cs` вЂ” captures response and content headers, status, elapsed time, and bounded body. Preserve redirects, timeout, response cap, and guarded transport behavior.
- `backend/src/ApiPlayground.Application/Outbound/GetTracerUseCase.cs` вЂ” treats `Failure.None` HTTP responses, including 4xx/5xx, as completed responses and retains persistence smoke check; preserve this behavior.
- `backend/src/ApiPlayground.Api/Program.cs` вЂ” endpoint currently emits status, elapsed, body, failure and request-sent but omits response headers. Extend payload with captured headers while keeping API status mapping and request guards unchanged.
- `frontend/src/tracer.ts` вЂ” `TracerResponse` contract currently lacks response headers; restore a multi-value header shape consistent with API output and preserve failure handling.
- `frontend/src/App.tsx` and `frontend/src/App.css` вЂ” response area currently shows summary/body only. Add independent response-tab state, accessible tab semantics and header rendering; retain existing request tabs and format JSON via existing formatter.
- `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` вЂ” controlled loopback success tests verify selected request values but need response header and HTTP error assertions; extend using the owned exact loopback policy.
- `backend/tests/ApiPlayground.Tests/Outbound/ControlledLoopbackServer.cs` вЂ” existing Kestrel harness supports deterministic status, headers and body; no external network tests.
- `frontend/src/App.test.tsx` вЂ” currently checks basic body visibility; add status/time, header values, tab default/switch, formatted JSON, HTTP-error response, and transport/policy state tests.
- `_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md`, `ux-apiplayground/DESIGN.md`, and `ux-apiplayground/EXPERIENCE.md` вЂ” authoritative CAP-2 and response/accessibility behavior.

## Tasks & Acceptance

**Execution:**
- [x] `backend/src/ApiPlayground.Api/Program.cs` вЂ” serialize captured multi-value response headers with status, elapsed time, body and existing failure fields; do not change response/API status semantics.
- [x] `frontend/src/tracer.ts`, `frontend/src/App.tsx`, and `frontend/src/App.css` вЂ” represent headers and implement accessible Body/Headers response tabs, default Body selection, readable JSON, and distinct non-response failure states.
- [x] `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` and `frontend/src/App.test.tsx` вЂ” cover the matrix with controlled loopback and UI tests, including 4xx response vs timeout/policy failure.
- [x] Run frontend tests/build, complete backend suite with PostgreSQL integration configuration, `git diff --check`, and independent review; record evidence here.

**Acceptance Criteria:**
- Given a controlled HTTP response, when it arrives, then the response pane shows the target's status and elapsed time and provides all captured header values and body.
- Given the target returns HTTP 4xx or 5xx, when the response is displayed, then it remains an HTTP response with its observed details rather than a transport failure.
- Given response JSON, when Body is selected, then it is formatted for reading without a raw-mode toggle; Body is selected by default for a new response and Headers tab switches to captured headers.
- Given timeout, other transport failure, or policy rejection, when the request ends, then the UI communicates the distinct outcome accessibly and does not invent response details or unsupported causes; policy rejection explicitly says not sent.
- Given request input tabs and response tabs, when either is switched, then the two independent tab states do not affect one another.

## Implementation Notes

- Added the captured outbound multi-value headers to the tracer API response without changing API HTTP status mapping.
- Added independent accessible response tabs. Successful HTTP results show the observed status/time and formatted body or each response header value; policy and transport failures remain separate from HTTP response details.
- Added a controlled loopback 429 endpoint assertion and UI tests for success/error headers, formatted JSON, tabs, policy rejection, and transport failure.

## Plan Change Log

## Review Triage Log

| Verdict | Evidence | Route |
|---|---|---|
| _Pending independent quick review._ | _No findings received yet._ | _Pending_ |

## Design Notes

Represent header values as arrays so repeated response header values survive serialization. Render header names and each value as observed; do not combine arrays into a fabricated single value.

## Verification

**Commands:**
- `npm test` from `frontend` вЂ” passed, 16 tests across 2 files.
- `npm run build` from `frontend` вЂ” passed TypeScript checking and production Vite build.
- `dotnet test backend/ApiPlayground.sln --configuration Release` вЂ” passed, 86 tests, 0 failed, 0 skipped. Integration suite completed; outbound tests use controlled test-owned targets.
- `git diff --check` вЂ” passed with no whitespace errors.

## Context: D:/pet_projects/ApiPlayground/_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md
---
id: SPEC-apiplayground
companions:
  - mvp-scope.md
  - stack.md
sources:
  - ../../brainstorm-apiplayground/brainstorm-apiplayground.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability вЂ” consult them only if you need narrative rationale or prose color this contract intentionally omits.

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


## Context: D:/pet_projects/ApiPlayground/_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/DESIGN.md
---
name: ApiPlayground
description: Compact, calm technical workspace for configuring HTTP requests and inspecting observed responses.
status: final
sources:
  - ../initiative-apiplayground-mvp.md
  - ../epic-apiplayground-mvp/epic-apiplayground-mvp.md
  - ../spec-apiplayground/spec-apiplayground.md
  - ../spec-apiplayground/mvp-scope.md
  - ../spec-apiplayground/stack.md
  - ../../brainstorm-apiplayground/brainstorm-apiplayground.md
updated: 2026-10-13
colors:
  background: '#F3F5F7'
  surface: '#FFFFFF'
  text: '#26313D'
  muted: '#667483'
  border: '#D9E0E6'
  action: '#315F83'
  accent: '#347B78'
  error: '#B34242'
typography:
  interface:
    fontFamily: 'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif'
    fontSize: '1rem'
    fontWeight: '400'
    lineHeight: '1.5'
  label:
    fontFamily: 'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif'
    fontSize: '0.875rem'
    fontWeight: '500'
    lineHeight: '1.4'
  heading:
    fontFamily: 'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif'
    fontSize: '1.125rem'
    fontWeight: '600'
    lineHeight: '1.35'
  data:
    fontFamily: 'ui-monospace, SFMono-Regular, Consolas, "Liberation Mono", monospace'
    fontSize: '0.875rem'
    fontWeight: '400'
    lineHeight: '1.5'
rounded:
  sm: '0.25rem'
  md: '0.375rem'
  lg: '0.5rem'
  full: '9999px'
spacing:
  '1': '0.25rem'
  '2': '0.5rem'
  '3': '0.75rem'
  '4': '1rem'
  '5': '1.5rem'
  '6': '2rem'
components:
  'Workspace layout':
    canvas: '{colors.background}'
    panel: '{colors.surface}'
    text: '{colors.text}'
    gutter: '{spacing.4}'
    section-gap: '{spacing.5}'
    radius: '{rounded.md}'
  'Request line':
    surface: '{colors.surface}'
    text: '{colors.text}'
    muted: '{colors.muted}'
    border: '{colors.border}'
    font: '{typography.interface.fontFamily}'
    gap: '{spacing.2}'
    radius: '{rounded.sm}'
  'Request tabs':
    surface: '{colors.surface}'
    text: '{colors.text}'
    muted: '{colors.muted}'
    selected: '{colors.action}'
    border: '{colors.border}'
    label-font: '{typography.label.fontFamily}'
    padding: '{spacing.3}'
  'Key/value row':
    surface: '{colors.surface}'
    text: '{colors.text}'
    muted: '{colors.muted}'
    border: '{colors.border}'
    font: '{typography.interface.fontFamily}'
    gap: '{spacing.2}'
    radius: '{rounded.sm}'
  'JSON body editor':
    surface: '{colors.surface}'
    text: '{colors.text}'
    border: '{colors.border}'
    data-font: '{typography.data.fontFamily}'
    padding: '{spacing.3}'
    radius: '{rounded.sm}'
  'Send action':
    background: '{colors.action}'
    foreground: '{colors.surface}'
    font: '{typography.label.fontFamily}'
    padding: '{spacing.3}'
    radius: '{rounded.sm}'
  'Response summary':
    surface: '{colors.surface}'
    text: '{colors.text}'
    muted: '{colors.muted}'
    border: '{colors.border}'
    heading-font: '{typography.heading.fontFamily}'
    data-font: '{typography.data.fontFamily}'
    gap: '{spacing.2}'
  'Response content':
    surface: '{colors.surface}'
    text: '{colors.text}'
    muted: '{colors.muted}'
    border: '{colors.border}'
    data-font: '{typography.data.fontFamily}'
    padding: '{spacing.3}'
    radius: '{rounded.sm}'
  'History & Collections sidebar':
    surface: '{colors.surface}'
    text: '{colors.text}'
    muted: '{colors.muted}'
    border: '{colors.border}'
    heading-font: '{typography.heading.fontFamily}'
    padding: '{spacing.3}'
    radius: '{rounded.md}'
  'Saved request item':
    surface: '{colors.surface}'
    text: '{colors.text}'
    muted: '{colors.muted}'
    border: '{colors.border}'
    data-font: '{typography.data.fontFamily}'
    gap: '{spacing.2}'
    radius: '{rounded.sm}'
  'Save-to-collection dialog':
    surface: '{colors.surface}'
    text: '{colors.text}'
    muted: '{colors.muted}'
    border: '{colors.border}'
    heading-font: '{typography.heading.fontFamily}'
    padding: '{spacing.5}'
    radius: '{rounded.md}'
  'Status and feedback message':
    surface: '{colors.surface}'
    text: '{colors.text}'
    muted: '{colors.muted}'
    accent: '{colors.accent}'
    error: '{colors.error}'
    font: '{typography.interface.fontFamily}'
    gap: '{spacing.2}'
    radius: '{rounded.sm}'
---

## Brand & Style

ApiPlayground is a compact, calm technical workspace: the request and its observed response stay central, and supporting controls remain quiet. The Neutral Technical light palette is the selected visual direction. Use semantic system sans-serif typography for the interface and monospace for HTTP values, JSON, and response data; do not depend on externally hosted fonts. Keep the interface direct and readable rather than decorative. This is a desktop-first local-browser product, not a mobile client. These two spines are the implementation contract and take precedence over any conflicting preview or visual reference.

## Colors

The Neutral Technical palette was selected during discovery; the semantic tokens below are authoritative.

- **Background (`{colors.background}`)** is the cool, pale canvas behind the work areas.
- **Surface (`{colors.surface}`)** separates the request, response, sidebar, and dialog surfaces from the canvas.
- **Text (`{colors.text}`)** is the default high-contrast interface text and should carry essential labels and information.
- **Muted (`{colors.muted}`)** is for secondary metadata on white surfaces, not essential small text on the background canvas.
- **Border (`{colors.border}`)** is a quiet decorative divider only; it is not sufficient by itself to communicate an important control boundary.
- **Action (`{colors.action}`)** identifies the primary Send action, selected navigation/tab indicators, and visible focus treatment where needed.
- **Accent (`{colors.accent}`)** is a restrained secondary emphasis, never a substitute for a status label.
- **Error (`{colors.error}`)** may accompany a failure message and its identifying text; color is never the sole signal.

Contrast check for the selected palette (WCAG relative-luminance ratios, rounded to two decimals): text on background 12.09:1 and on surface 13.21:1; muted on surface 4.78:1 but muted on background 4.38:1, below the 4.5:1 normal-text target; action on background 6.21:1 and on surface 6.78:1; accent on background 4.52:1 and on surface 4.94:1; error on background 5.09:1 and on surface 5.56:1. Border against background is 1.22:1 and against surface 1.33:1, below the 3:1 non-text target. Keep essential text in `{colors.text}` on the canvas. Use `{colors.action}` for a strong outline around meaningful controls and for visible focus; its contrast exceeds 3:1 against both `{colors.background}` and `{colors.surface}`. The outline must be paired with shape, position, underline, text, or another non-color cue where state or meaning is conveyed. Verify rendered foreground/background and boundary pairs against WCAG 2.2 AA; do not change the selected palette values silently.

## Typography

Use `{typography.interface}` for ordinary interface copy, `{typography.label}` for controls and compact labels, and `{typography.heading}` for short surface headings. Use `{typography.data}` for methods, URLs, header names and values, JSON, status codes, elapsed time, and response data. The specified stacks use system fonts only. Keep text casing natural and avoid dense all-caps labels; preserve legibility before reducing type to fit.

## Layout & Spacing

Use the compact `{spacing.1}` through `{spacing.6}` scale to group related controls closely and separate the major work areas. At desktop widths, request composer and response are visible side by side, with the collapsible History & Collections sidebar available alongside the workspace. The request method, URL, and Send action form a fixed row above the Params, Headers, and Body tabs. On narrow desktop windows, stack request and response vertically. Phone and tablet layouts are out of scope; the exact narrow-window threshold and resulting window behavior remain open in `EXPERIENCE.md`.

## Elevation & Depth

Use tonal separation between `{colors.background}` and `{colors.surface}` plus clear layout grouping. Avoid decorative shadows, gradients, and layered-card effects. A dialog may be visually raised only enough to distinguish it from its scrim; hierarchy otherwise comes from placement, type, and boundaries.

## Shapes

Use `{rounded.sm}` for fields, rows, tabs, and compact controls; `{rounded.md}` for work areas and dialogs; use `{rounded.lg}` sparingly for larger enclosing surfaces. Keep geometry restrained and technical. `{rounded.full}` is reserved for small, non-essential indicators when a true circular shape is useful, not for buttons or large pill containers.

## Components

- **Workspace layout** вЂ” Canvas uses `{colors.background}`; work areas use `{colors.surface}`. Maintain the desktop side-by-side request/response composition, with compact gutters and clear headings. On narrow desktop, stack the two areas vertically without hiding either. The [cold-open workspace mockup](mockups/workspace-cold-open.html) illustrates the initial empty-GET state.
- **Request line** вЂ” Keep method, URL, and Send in one persistent row above the tabs. Use `{colors.text}` for values. Where a field or action needs a meaningful boundary or receives focus, use a strong `{colors.action}` outline; the subtle `{colors.border}` alone is insufficient. Pair focus/boundary color with shape or placement.
- **Request tabs** вЂ” Params, Headers, and Body are plainly labeled. Mark the selected tab with `{colors.action}` plus a non-color cue such as weight or underline; show keyboard focus with a strong `{colors.action}` outline.
- **Key/value row** вЂ” Used for Params and Headers. Use the field labels Key and Value; keep inputs aligned and compact, with an Enabled checkbox checked by default and explicit add/remove controls. Use a strong `{colors.action}` outline for meaningful field boundaries/focus where needed, paired with shape or placement. Avoid truncating entered values without a way to inspect them. Sensitive saved-header values appear blank in disabled rows with an instruction to enable and re-enter the value.
- **JSON body editor** вЂ” A simple monospace JSON field with syntax validation feedback. Do not add IDE-like completion, request scripting, or formatting toolbars.
- **Send action** вЂ” Use `{colors.action}` with `{colors.surface}` foreground and a clear Send label. Disabled, sending, and focus states remain distinguishable without relying on color alone.
- **Response summary** вЂ” Show HTTP status and elapsed time in readable labels; status is data, not an inferred diagnosis. Use text and structure in addition to color.
- **Response content** вЂ” Provide Body and Headers tabs, with Body selected by default. Keep both response parts easy to find; format JSON for reading and do not offer a raw-mode toggle. Selected state uses `{colors.action}` plus a non-color cue; focus uses a strong `{colors.action}` outline.
- **History & Collections sidebar** вЂ” Collapsible and closed by default. Use a quiet white surface and clear section labels; distinguish an empty collection/history list from a persistence error with text, not color alone. Do not introduce unrelated navigation.
- **Saved request item** вЂ” Identify the saved request with method and editable title/path context, using monospace only for HTTP data. Indicate selection with both a visible structural cue and accessible state.
- **Save-to-collection dialog** вЂ” Present the suggested editable title based on method and path, collection selection, and inline collection creation in one focused save action. Use a strong `{colors.action}` outline for meaningful field boundaries/focus where needed, paired with a non-color cue. Do not imply extra collection-management features.
- **Status and feedback message** вЂ” Pair concise text with any color or icon. Use readable text and structure for status; never communicate state through color alone.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Keep request and observed response as the main work surface. | Expand the MVP into a full Postman-style feature set. |
| Use system sans for interface text and monospace for HTTP/JSON data. | Assume or require an external font or an unselected UI library. |
| Use text, structure, and adequate contrast together for state and boundaries. | Rely on the pale border or color alone to communicate important meaning. |
| Keep JSON editing simple and response JSON readable. | Add raw response mode, scripting, advanced validation, or IDE-style editing. |
| Use the selected light palette consistently. | Add dark mode, theme switching, or language switching to the MVP. |


## Context: D:/pet_projects/ApiPlayground/_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/EXPERIENCE.md
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

# ApiPlayground вЂ” Experience Spine

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
| вЂњEnter a URL to send a request.вЂќ | Vague disabled controls with no explanation. |
| вЂњHTTP 404вЂќ with the received response data. | Treating every non-2xx status as a transport failure. |
| вЂњRequest timed out.вЂќ / вЂњCould not connect.вЂќ | Claiming the API, network, or user configuration is definitely at fault. |
| вЂњBlocked by security policy. Request not sent.вЂќ | Suggesting the request may have reached its destination after a policy refusal. |
| вЂњThis result is for the previous request.вЂќ | Presenting an old result as if it belongs to the request currently being edited. |

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
| Load a saved request | Request workspace | Load configuration without sending and clear the old response; prompt the user to send the loaded request. Each omitted sensitive header value appears blank in a disabled row with an instruction to enable and re-enter it. Example label only: вЂњHeader value not saved вЂ” enter it againвЂќ; exact diagnostic copy remains open. |
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
- Ensure disabled Send communicates why it is unavailable, and distinguish вЂњnot sentвЂќ from a completed HTTP response.

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

- What detailed diagnostic and validation copy should be used? The message must describe observed evidence without overclaiming cause. вЂњHeader value not saved вЂ” enter it againвЂќ is an example label only, not finalized product copy.
- Which developer persona and concrete use scenarios beyond the project author's own use should shape the product, and what usability evidence would validate the low-friction intent? The persona and simplicity advantage remain unvalidated hypotheses.
- What confirmation and dialog-dismissal behavior should follow a successful Collection save? The saved request must be available in the chosen collection, but no success message or dismissal pattern is committed here.

## Key Flows

### Flow 1 вЂ” Diagnose a request error (Alex, an illustrative name for the project author/developer working locally; CAP-1, CAP-2, CAP-3)

1. Alex opens the local browser workspace and sees an empty GET request, a blank URL, and the response prompt.
2. Alex enters an endpoint URL, chooses the intended method, and configures any needed Params or Headers; each ordinary row starts enabled. For a body-bearing request, Alex selects the request Body tab and enters valid JSON.
3. Alex chooses Send. While sending, progress is visible, duplicate sends are prevented, and any previous result remains clearly labeled as belonging to the previous request.
4. The request is dispatched. If an HTTP response arrives, the Response summary shows its status and elapsed time. In Response content, the Body tab is selected by default, and the Headers tab is available; JSON is formatted for reading. The dispatched attempt is eligible for History whether the HTTP status is success or failure.
5. Alex sees an HTTP error with its received status/data, or a post-dispatch timeout or other transport failure described as an observed failure rather than an unsupported diagnosis. A dispatched attempt appears in History when persistence succeeds.
6. **Climax:** Alex can inspect the observed error and its available response details, distinguish an HTTP response from a transport failure, and find the dispatched attempt in History when saved.

Failure path: invalid URL or JSON blocks dispatch and the relevant field identifies the validation issue; no request attempt is recorded. A security-policy block explicitly says the request was not sent and is not recorded in History. If a timeout or other transport failure occurs after dispatch, it is a History-eligible attempt. If automatic History persistence fails, sending remains complete and the interface clearly reports that the History entry was not saved. Exact diagnostic wording and security criteria remain open.

### Flow 2 вЂ” Reuse and save a request across sessions (Alex, an illustrative name for the project author/developer returning later; CAP-3, CAP-4)

1. In a later work session, Alex expands the History & Collections sidebar and locates a dispatched attempt in History or an explicitly saved request in a collection.
2. Alex selects an item. Its method, URL, query parameters, headers, and body load into the editor; the selection does not send a request.
3. The previous response is cleared. The Response content returns to its Body tab and prompts Alex to send the loaded request, so old data cannot appear to match the new configuration.
4. Each sensitive header whose value was omitted is shown as a blank, disabled row with an instruction to enable and re-enter its value before use. Example label only: вЂњHeader value not saved вЂ” enter it againвЂќ; exact copy remains open.
5. Alex reviews the configuration, supplies any required sensitive values, and explicitly chooses Send to repeat the request. The result follows Flow 1.
6. To keep a request for a later session, Alex explicitly saves it, edits the suggested method-and-path title if needed, and selects an existing collection or creates one inline.
7. **Climax:** Alex has intentionally repeated or saved the request for later without an accidental send or the false impression that omitted sensitive values were retained.

Failure path: if collection saving fails, the save dialog remains open with its entered values, shows a failure message, and leaves retry available; it does not claim success. If Alex has not re-entered an omitted sensitive header, its row remains disabled and is not included in the sent request. Exact sensitive-header classification and diagnostic wording remain open.


## Unified diff
diff --git a/backend/src/ApiPlayground.Api/Program.cs b/backend/src/ApiPlayground.Api/Program.cs
index 85beb2b..ca9982e 100644
--- a/backend/src/ApiPlayground.Api/Program.cs
+++ b/backend/src/ApiPlayground.Api/Program.cs
@@ -93,6 +93,7 @@ app.MapPost("/api/tracer/get", async (TracerRequest request, GetTracerUseCase us
 		failure = result.Outbound.Failure.ToString(),
 		statusCode = result.Outbound.StatusCode,
 		elapsedMilliseconds = result.Outbound.Elapsed.TotalMilliseconds,
+		headers = result.Outbound.Headers,
 		body = System.Text.Encoding.UTF8.GetString(result.Outbound.Body.Span),
 		persistenceVerified = result.PersistenceVerified,
 		error = result.Error,
diff --git a/backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs b/backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs
index a79836f..52f27e5 100644
--- a/backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs
+++ b/backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs
@@ -209,6 +209,35 @@ public sealed class TracerEndpointTests
         }
     }
 
+    [Fact]
+    public async Task Http_error_response_preserves_target_status_elapsed_and_repeated_headers()
+    {
+        await using var target = await Outbound.ControlledLoopbackServer.StartAsync(async context =>
+        {
+            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
+            context.Response.Headers.Append("X-Observed", "first");
+            context.Response.Headers.Append("X-Observed", "second");
+            context.Response.Headers.Append("X-Only", "value");
+            await context.Response.WriteAsync("{\"error\":\"slow down\"}");
+        });
+        var factory = CreateGuardedFactory(target.BaseAddress);
+        await using (factory)
+        using (var request = CreateAllowedRequest(target.BaseAddress.ToString()))
+        using (var response = await factory.CreateClient().SendAsync(request))
+        {
+            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
+            var payload = await response.Content.ReadFromJsonAsync<TracerResponse>();
+            Assert.NotNull(payload);
+            Assert.Equal("None", payload.Failure);
+            Assert.Equal(429, payload.StatusCode);
+            Assert.True(payload.ElapsedMilliseconds >= 0);
+            Assert.Equal("{\"error\":\"slow down\"}", payload.Body);
+            Assert.Equal(new[] { "first", "second" }, payload.Headers["X-Observed"]);
+            Assert.Equal(new[] { "value" }, payload.Headers["X-Only"]);
+            Assert.True(payload.RequestSent);
+        }
+    }
+
     [Theory]
     [InlineData("GET", "")]
     [InlineData("POST", "{\"hello\":\"world\"}")]
@@ -367,5 +396,6 @@ public sealed class TracerEndpointTests
         public Task<bool> VerifyRoundTripAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
     }
 
-    private sealed record TracerResponse(string Failure, int? StatusCode, string Body, bool PersistenceVerified, string? Error, bool RequestSent);
+    private sealed record TracerResponse(string Failure, int? StatusCode, double ElapsedMilliseconds,
+        Dictionary<string, string[]> Headers, string Body, bool PersistenceVerified, string? Error, bool RequestSent);
 }
diff --git a/frontend/src/App.css b/frontend/src/App.css
index b43e488..f2218a9 100644
--- a/frontend/src/App.css
+++ b/frontend/src/App.css
@@ -23,6 +23,13 @@ h2 { margin: 0 0 18px; font-size: 1.125rem; }
 .request-line button { height: 38px; border: 0; border-radius: 4px; background: #315F83; color: #fff; cursor: pointer; }
 .request-line button:disabled { border: 1px dashed #D9E0E6; background: #fff; color: #667483; cursor: not-allowed; }
 pre { overflow: auto; max-height: 520px; padding: 14px; border: 1px solid #D9E0E6; border-radius: 4px; background: #F3F5F7; white-space: pre-wrap; overflow-wrap: anywhere; font: .875rem/1.5 ui-monospace, Consolas, monospace; }
+.response-summary { display: flex; align-items: baseline; gap: 12px; }
+.response-summary strong, .response-summary span, .response-header dt, .response-header dd { font-family: ui-monospace, Consolas, monospace; }
+.response-headers dl { display: grid; gap: 10px; margin: 0; }
+.response-header { display: grid; grid-template-columns: minmax(120px, 1fr) minmax(0, 2fr); gap: 12px; padding: 10px; border: 1px solid #9BA9B5; border-radius: 4px; }
+.response-header dt { font-weight: 600; overflow-wrap: anywhere; }
+.response-header dd { display: grid; gap: 4px; min-width: 0; margin: 0; overflow-wrap: anywhere; }
+.response-header dd > div + div { padding-top: 4px; border-top: 1px solid #D9E0E6; }
 .tabs { display: flex; gap: 4px; margin: 18px 0 12px; border-bottom: 1px solid #D9E0E6; }
 .tabs button { padding: 8px 12px; border: 0; border-bottom: 3px solid transparent; background: transparent; color: #26313D; cursor: pointer; }
 .tabs button[aria-selected="true"] { border-bottom-color: #315F83; font-weight: 600; }
diff --git a/frontend/src/App.test.tsx b/frontend/src/App.test.tsx
index 77cbc54..314fa60 100644
--- a/frontend/src/App.test.tsx
+++ b/frontend/src/App.test.tsx
@@ -1,4 +1,4 @@
-import { fireEvent, render, screen, waitFor } from "@testing-library/react";
+import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
 import { afterEach, describe, expect, it, vi } from "vitest";
 import { App } from "./App";
 
@@ -18,7 +18,8 @@ describe("request workspace", () => {
       failure: "None",
       statusCode: 200,
       elapsedMilliseconds: 12,
-      body: "hello",
+      headers: { "Content-Type": ["application/json"], "X-Observed": ["first", "second"] },
+      body: "{\"hello\":\"world\",\"count\":2}",
       persistenceVerified: true,
       error: null,
       requestSent: true,
@@ -29,10 +30,67 @@ describe("request workspace", () => {
     fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
     fireEvent.click(screen.getByRole("button", { name: "Send" }));
 
-    await waitFor(() => expect(screen.getByText("hello")).toBeInTheDocument());
+    await waitFor(() => expect(screen.getByText(/HTTP 200/)).toBeInTheDocument());
+    expect(screen.getByText("12 ms")).toBeInTheDocument();
+    expect(within(screen.getByRole("tablist", { name: "Response details" })).getByRole("tab", { name: "Body" })).toHaveAttribute("aria-selected", "true");
+    expect(screen.getByText(/"hello": "world"/)).toBeInTheDocument();
+    expect(screen.queryByText("first")).not.toBeInTheDocument();
+    const responseTabs = within(screen.getByRole("tablist", { name: "Response details" }));
+    fireEvent.click(responseTabs.getByRole("tab", { name: "Headers" }));
+    expect(responseTabs.getByRole("tab", { name: "Headers" })).toHaveAttribute("aria-selected", "true");
+    expect(screen.getAllByText("first")).toHaveLength(1);
+    expect(screen.getByText("second")).toBeInTheDocument();
+    expect(screen.getByRole("tab", { name: "Params" })).toHaveAttribute("aria-selected", "true");
+    fireEvent.click(responseTabs.getByRole("tab", { name: "Body" }));
+    expect(responseTabs.getByRole("tab", { name: "Body" })).toHaveAttribute("aria-selected", "true");
     expect(fetchMock).toHaveBeenCalledWith("/api/tracer/get", expect.objectContaining({ method: "POST" }));
   });
 
+  it("shows HTTP error statuses as completed responses with headers and body", async () => {
+    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
+      failure: "None", statusCode: 404, elapsedMilliseconds: 8,
+      headers: { "X-Reason": ["not found"] }, body: "{\"message\":\"missing\"}",
+      persistenceVerified: true, error: null, requestSent: true,
+    }), { status: 200, headers: { "Content-Type": "application/json" } }));
+    vi.stubGlobal("fetch", fetchMock);
+    render(<App />);
+    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test/missing" } });
+    fireEvent.click(screen.getByRole("button", { name: "Send" }));
+
+    await waitFor(() => expect(screen.getByText(/HTTP 404/)).toBeInTheDocument());
+    expect(screen.getByText(/"message": "missing"/)).toBeInTheDocument();
+    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
+    fireEvent.click(within(screen.getByRole("tablist", { name: "Response details" })).getByRole("tab", { name: "Headers" }));
+    expect(screen.getByText("not found")).toBeInTheDocument();
+  });
+
+  it("keeps policy rejection distinct and does not fabricate HTTP response details", async () => {
+    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
+      failure: "PolicyRejected", statusCode: null, elapsedMilliseconds: 0, headers: {}, body: "",
+      persistenceVerified: false, error: "Blocked by security policy. Request not sent.", requestSent: false,
+    }), { status: 403, headers: { "Content-Type": "application/json" } }));
+    vi.stubGlobal("fetch", fetchMock);
+    render(<App />);
+    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
+    fireEvent.click(screen.getByRole("button", { name: "Send" }));
+
+    expect(await screen.findByRole("alert")).toHaveTextContent("Request not sent");
+    expect(screen.queryByRole("tablist", { name: "Response details" })).not.toBeInTheDocument();
+    expect(screen.queryByText(/HTTP \d/)).not.toBeInTheDocument();
+  });
+
+  it("shows transport failures accessibly without inventing response details", async () => {
+    const fetchMock = vi.fn().mockRejectedValue(new Error("The total request deadline expired."));
+    vi.stubGlobal("fetch", fetchMock);
+    render(<App />);
+    fireEvent.change(screen.getByLabelText("Request URL"), { target: { value: "https://example.test" } });
+    fireEvent.click(screen.getByRole("button", { name: "Send" }));
+
+    expect(await screen.findByRole("alert")).toHaveTextContent("The total request deadline expired.");
+    expect(screen.queryByRole("tablist", { name: "Response details" })).not.toBeInTheDocument();
+    expect(screen.queryByText(/HTTP \d/)).not.toBeInTheDocument();
+  });
+
   it("retains and labels an earlier result during a later send and failure", async () => {
     const fetchMock = vi.fn()
       .mockResolvedValueOnce(new Response(JSON.stringify({
diff --git a/frontend/src/App.tsx b/frontend/src/App.tsx
index 4875cb6..894081b 100644
--- a/frontend/src/App.tsx
+++ b/frontend/src/App.tsx
@@ -5,6 +5,7 @@ import "./App.css";
 
 type RequestRow = { id: number; key: string; value: string; enabled: boolean };
 type RequestTab = "Params" | "Headers" | "Body";
+type ResponseTab = "Body" | "Headers";
 const methods = ["GET", "POST", "PUT", "PATCH", "DELETE"] as const;
 const makeRow = (id: number): RequestRow => ({ id, key: "", value: "", enabled: true });
 
@@ -12,6 +13,7 @@ export function App() {
   const [url, setUrl] = useState("");
   const [method, setMethod] = useState<(typeof methods)[number]>("GET");
   const [tab, setTab] = useState<RequestTab>("Params");
+  const [responseTab, setResponseTab] = useState<ResponseTab>("Body");
   const [params, setParams] = useState<RequestRow[]>([makeRow(1)]);
   const [headers, setHeaders] = useState<RequestRow[]>([makeRow(2)]);
   const [body, setBody] = useState("");
@@ -59,6 +61,7 @@ export function App() {
         ...(body.trim() && (method === "POST" || method === "PUT" || method === "PATCH") ? { body } : {}),
       });
       setResult(response);
+      setResponseTab("Body");
       setPreviousResult(false);
       if (response.error && response.failure !== "PolicyRejected")
         setError(response.error);
@@ -124,9 +127,24 @@ export function App() {
           {!result && !error && !loading && <p>Enter a URL to send a request.</p>}
           {result && <div>
             {previousResult && <p className="previous-result" role="status">This result is for the previous request.</p>}
-            {result.requestSent && result.statusCode !== null && <p><strong>HTTP {result.statusCode}</strong> ┬╖ {result.elapsedMilliseconds.toFixed(0)} ms</p>}
-            {result.error && <p role="alert">{result.error}</p>}
-            <pre>{formatResponseBody(result.body) || (result.requestSent ? "The response body is empty." : "Request not sent.")}</pre>
+            {result.requestSent && result.statusCode !== null && <p className="response-summary"><strong>HTTP {result.statusCode}</strong><span>{result.elapsedMilliseconds.toFixed(0)} ms</span></p>}
+            {result.error && <p role={result.requestSent ? "status" : "alert"}>{result.error}</p>}
+            {result.requestSent && result.statusCode !== null && <>
+              <div className="tabs response-tabs" role="tablist" aria-label="Response details">
+                {(["Body", "Headers"] as const).map(value => <button key={value} type="button" role="tab" aria-selected={responseTab === value} onClick={() => setResponseTab(value)}>{value}</button>)}
+              </div>
+              {responseTab === "Body" && <div role="tabpanel" aria-label="Response body">
+                <pre>{formatResponseBody(result.body) || "The response body is empty."}</pre>
+              </div>}
+              {responseTab === "Headers" && <div role="tabpanel" aria-label="Response headers" className="response-headers">
+                {Object.entries(result.headers ?? {}).length === 0
+                  ? <p>No response headers.</p>
+                  : <dl>{Object.entries(result.headers ?? {}).map(([name, values]) => <div className="response-header" key={name}>
+                    <dt>{name}</dt>
+                    <dd>{values.map((value, index) => <div key={`${name}-${index}`}>{value}</div>)}</dd>
+                  </div>)}</dl>}
+              </div>}
+            </>}
           </div>}
         </section>
       </div>
diff --git a/frontend/src/tracer.ts b/frontend/src/tracer.ts
index 53688c5..48e65ce 100644
--- a/frontend/src/tracer.ts
+++ b/frontend/src/tracer.ts
@@ -2,6 +2,7 @@ export type TracerResponse = {
   failure: string;
   statusCode: number | null;
   elapsedMilliseconds: number;
+  headers: Record<string, string[]>;
   body: string;
   persistenceVerified: boolean;
   error: string | null;

--- UNTRACKED PLAN ---

---
title: 'Inspect HTTP response details'
type: 'feature'
ticket: '5'
created: '2026-10-22'
status: 'in-review'
baseline_revision: '21033e1b9c150e8213178c2d006bc262bac43205'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md'
  - '_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/DESIGN.md'
  - '_bmad-output/initiative-apiplayground-mvp/ux-apiplayground/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent вЂ” do not modify unless human renegotiates">

## Intent

**Problem:** Users can send configured requests but cannot inspect complete response details, especially response headers, or switch between headers and body.

**Approach:** Expand the response contract and viewer to present observed HTTP status, elapsed time, response headers, and body with accessible Body and Headers tabs, Body selected by default, and readable JSON formatting. Preserve the distinction between HTTP error responses, policy blocks, timeouts, and other transport failures without unsupported diagnostic claims.

## Boundaries & Constraints

**Always:** Reuse the existing guarded outbound executor and captured `OutboundResult`; do not add a transport. Preserve public-only production policy, exact loopback-only test target, response-size/deadline limits, 4 KiB API request envelope, 2048-character URL limit, and Story 3 persistence smoke behavior. Display the target's HTTP status independently from the API status; HTTP 4xx/5xx remains an observed response, not a transport failure. Preserve multi-valued response headers. Keep request editing behavior and Body/Headers state separate. Default response tab to Body for a new response and retain prior-result labeling while sending. Format JSON for readability without a raw-mode toggle. Textual status and error cues must remain accessible and not rely on color alone.

**Never:** Add History, Collections, authentication, request scripting/schema validation, raw-response mode, alternate transports, response-header editing, or unsupported root-cause diagnosis. Do not expose secrets or change outbound security policy.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| HTTP success | Controlled response with status, elapsed time, repeated headers, JSON body | Show observed status/time; Body tab selected and JSON formatted; Headers tab exposes all values | No error expected |
| HTTP error | Controlled 4xx/5xx response with headers/body | Display it as a completed HTTP response with target status, elapsed time, headers, and body | Do not classify as transport failure or infer cause |
| Transport failure | Timeout or other executor failure | Show accessible distinct failure state, not response tabs with fabricated HTTP response details | State only observed failure; do not claim cause |
| Policy rejection | Executor rejects before dispatch | State request was not sent; do not show it as an HTTP response | Preserve policy distinction |
| Response tab switch | A response with body and headers | Switch displayed content; Body is selected by default for each new response | Keep request tabs unaffected |

</frozen-after-approval>

## Code Map

- `backend/src/ApiPlayground.Application/Outbound/OutboundResult.cs` вЂ” source contract already carries nullable target status, elapsed time, case-insensitive multi-value headers, body, and failure. Reuse it.
- `backend/src/ApiPlayground.Infrastructure/Outbound/GuardedTargetHttpClient.cs` вЂ” captures response and content headers, status, elapsed time, and bounded body. Preserve redirects, timeout, response cap, and guarded transport behavior.
- `backend/src/ApiPlayground.Application/Outbound/GetTracerUseCase.cs` вЂ” treats `Failure.None` HTTP responses, including 4xx/5xx, as completed responses and retains persistence smoke check; preserve this behavior.
- `backend/src/ApiPlayground.Api/Program.cs` вЂ” endpoint currently emits status, elapsed, body, failure and request-sent but omits response headers. Extend payload with captured headers while keeping API status mapping and request guards unchanged.
- `frontend/src/tracer.ts` вЂ” `TracerResponse` contract currently lacks response headers; restore a multi-value header shape consistent with API output and preserve failure handling.
- `frontend/src/App.tsx` and `frontend/src/App.css` вЂ” response area currently shows summary/body only. Add independent response-tab state, accessible tab semantics and header rendering; retain existing request tabs and format JSON via existing formatter.
- `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` вЂ” controlled loopback success tests verify selected request values but need response header and HTTP error assertions; extend using the owned exact loopback policy.
- `backend/tests/ApiPlayground.Tests/Outbound/ControlledLoopbackServer.cs` вЂ” existing Kestrel harness supports deterministic status, headers and body; no external network tests.
- `frontend/src/App.test.tsx` вЂ” currently checks basic body visibility; add status/time, header values, tab default/switch, formatted JSON, HTTP-error response, and transport/policy state tests.
- `_bmad-output/initiative-apiplayground-mvp/spec-apiplayground/spec-apiplayground.md`, `ux-apiplayground/DESIGN.md`, and `ux-apiplayground/EXPERIENCE.md` вЂ” authoritative CAP-2 and response/accessibility behavior.

## Tasks & Acceptance

**Execution:**
- [x] `backend/src/ApiPlayground.Api/Program.cs` вЂ” serialize captured multi-value response headers with status, elapsed time, body and existing failure fields; do not change response/API status semantics.
- [x] `frontend/src/tracer.ts`, `frontend/src/App.tsx`, and `frontend/src/App.css` вЂ” represent headers and implement accessible Body/Headers response tabs, default Body selection, readable JSON, and distinct non-response failure states.
- [x] `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` and `frontend/src/App.test.tsx` вЂ” cover the matrix with controlled loopback and UI tests, including 4xx response vs timeout/policy failure.
- [x] Run frontend tests/build, complete backend suite with PostgreSQL integration configuration, `git diff --check`, and independent review; record evidence here.

**Acceptance Criteria:**
- Given a controlled HTTP response, when it arrives, then the response pane shows the target's status and elapsed time and provides all captured header values and body.
- Given the target returns HTTP 4xx or 5xx, when the response is displayed, then it remains an HTTP response with its observed details rather than a transport failure.
- Given response JSON, when Body is selected, then it is formatted for reading without a raw-mode toggle; Body is selected by default for a new response and Headers tab switches to captured headers.
- Given timeout, other transport failure, or policy rejection, when the request ends, then the UI communicates the distinct outcome accessibly and does not invent response details or unsupported causes; policy rejection explicitly says not sent.
- Given request input tabs and response tabs, when either is switched, then the two independent tab states do not affect one another.

## Implementation Notes

- Added the captured outbound multi-value headers to the tracer API response without changing API HTTP status mapping.
- Added independent accessible response tabs. Successful HTTP results show the observed status/time and formatted body or each response header value; policy and transport failures remain separate from HTTP response details.
- Added a controlled loopback 429 endpoint assertion and UI tests for success/error headers, formatted JSON, tabs, policy rejection, and transport failure.

## Plan Change Log

## Review Triage Log

| Verdict | Evidence | Route |
|---|---|---|
| _Pending independent quick review._ | _No findings received yet._ | _Pending_ |

## Design Notes

Represent header values as arrays so repeated response header values survive serialization. Render header names and each value as observed; do not combine arrays into a fabricated single value.

## Verification

**Commands:**
- `npm test` from `frontend` вЂ” passed, 16 tests across 2 files.
- `npm run build` from `frontend` вЂ” passed TypeScript checking and production Vite build.
- `dotnet test backend/ApiPlayground.sln --configuration Release` вЂ” passed, 86 tests, 0 failed, 0 skipped. Integration suite completed; outbound tests use controlled test-owned targets.
- `git diff --check` вЂ” passed with no whitespace errors.

