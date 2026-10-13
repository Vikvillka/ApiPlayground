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

- **Workspace layout** — Canvas uses `{colors.background}`; work areas use `{colors.surface}`. Maintain the desktop side-by-side request/response composition, with compact gutters and clear headings. On narrow desktop, stack the two areas vertically without hiding either. The [cold-open workspace mockup](mockups/workspace-cold-open.html) illustrates the initial empty-GET state.
- **Request line** — Keep method, URL, and Send in one persistent row above the tabs. Use `{colors.text}` for values. Where a field or action needs a meaningful boundary or receives focus, use a strong `{colors.action}` outline; the subtle `{colors.border}` alone is insufficient. Pair focus/boundary color with shape or placement.
- **Request tabs** — Params, Headers, and Body are plainly labeled. Mark the selected tab with `{colors.action}` plus a non-color cue such as weight or underline; show keyboard focus with a strong `{colors.action}` outline.
- **Key/value row** — Used for Params and Headers. Use the field labels Key and Value; keep inputs aligned and compact, with an Enabled checkbox checked by default and explicit add/remove controls. Use a strong `{colors.action}` outline for meaningful field boundaries/focus where needed, paired with shape or placement. Avoid truncating entered values without a way to inspect them. Sensitive saved-header values appear blank in disabled rows with an instruction to enable and re-enter the value.
- **JSON body editor** — A simple monospace JSON field with syntax validation feedback. Do not add IDE-like completion, request scripting, or formatting toolbars.
- **Send action** — Use `{colors.action}` with `{colors.surface}` foreground and a clear Send label. Disabled, sending, and focus states remain distinguishable without relying on color alone.
- **Response summary** — Show HTTP status and elapsed time in readable labels; status is data, not an inferred diagnosis. Use text and structure in addition to color.
- **Response content** — Provide Body and Headers tabs, with Body selected by default. Keep both response parts easy to find; format JSON for reading and do not offer a raw-mode toggle. Selected state uses `{colors.action}` plus a non-color cue; focus uses a strong `{colors.action}` outline.
- **History & Collections sidebar** — Collapsible and closed by default. Use a quiet white surface and clear section labels; distinguish an empty collection/history list from a persistence error with text, not color alone. Do not introduce unrelated navigation.
- **Saved request item** — Identify the saved request with method and editable title/path context, using monospace only for HTTP data. Indicate selection with both a visible structural cue and accessible state.
- **Save-to-collection dialog** — Present the suggested editable title based on method and path, collection selection, and inline collection creation in one focused save action. Use a strong `{colors.action}` outline for meaningful field boundaries/focus where needed, paired with a non-color cue. Do not imply extra collection-management features.
- **Status and feedback message** — Pair concise text with any color or icon. Use readable text and structure for status; never communicate state through color alone.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Keep request and observed response as the main work surface. | Expand the MVP into a full Postman-style feature set. |
| Use system sans for interface text and monospace for HTTP/JSON data. | Assume or require an external font or an unselected UI library. |
| Use text, structure, and adequate contrast together for state and boundaries. | Rely on the pale border or color alone to communicate important meaning. |
| Keep JSON editing simple and response JSON readable. | Add raw response mode, scripting, advanced validation, or IDE-style editing. |
| Use the selected light palette consistently. | Add dark mode, theme switching, or language switching to the MVP. |
