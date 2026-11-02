---
epic: epic-backend-refactor
date: 2026-11-02
verdict: accepted-with-open-items
criteria: declared
headless: false
---

# Retrospective: epic-backend-refactor

Status: final. Phases 1, 2, 4 and 5 are complete. Phase 3 (`bmad-review`) was not run at the user's request.

## Epic summary

Epic: Backend Refactoring (`epic-backend-refactor`), initiative `initiative-apiplayground-backend-refactor`. Criteria declared in the epic file's Done when (5 items).

Tickets: all 10 are `done` in `tickets.py status`. No ticket is `built`, and `pending_tickets` is empty. Numbering below follows the ref order 1.1 to 1.10.

| No. | Ref | Title | Baseline | Commit range (evidence) |
|---|---|---|---|---|
| 1 | 1.1 | Share request-header redaction | aa53e5d | aa53e5d..b9f3e73 (b9f3e73) |
| 2 | 1.2 | Characterize the contract and migrate tracer dispatch to a controller | bc708d2 | bc708d2..5b561d7 (e380dce, 8c0f193, 5b561d7) |
| 3 | 1.3 | Share only common request validation | b9f3e73 | b9f3e73..37842910 (3784291) |
| 4 | 1.4 | Move persisted models into Domain | 9bd1fca | 9bd1fca..d3fa226 (d3fa226) |
| 5 | 1.5 | Refactor sweep | none in plan | no own range; see Missing evidence |
| 6 | 1.6 | Refactor the guarded outbound path | 5b561d7 | 5b561d7..aa53e5d (aa53e5d) |
| 7 | 1.7 | Migrate remaining routes to controllers | 37842910 | 37842910..9bd1fca (9bd1fca) |
| 8 | 1.8 | Standardize errors on Problem Details | d3fa226 | d3fa226..2188e67 (2188e67) |
| 9 | 1.9 | Organize DI and configuration, add safe NLog | 2188e67 | 2188e67..b69da11 (7530c6f, 6fa47a0, 834576c, b69da11) |
| 10 | 1.10 | Integrated closeout | b69da11 | b69da11..6937258 (3618263, 6937258), cut here |

Ranges were ordered by their position in `git rev-list --reverse HEAD`. Ticket 10's range is inferred to run to HEAD and is cut at 6937258. The commits after it (2fa4a6a onward) are frontend-redesign and request-export work, not this epic. The commit 87bdd7f is a docs close-out of this epic and is recorded here, not in a code range.

Evidence inventory:
- Available: epic file, initiative file, `tickets.toml`, 10 plan files with frontmatter, git ranges for 9 plans (`git_evidence.py` output for each), commit subjects.
- Not yet read: `tickets.py find` entries, story files, plan bodies (Review Triage Log, Verification, Code Review), full diffs.
- Missing: no session logs captured. No previous retrospective exists for this epic's predecessor in the ordering.

## Missing evidence

- Ticket 5 (Refactor sweep) plan has no `baseline_revision`. Its commit 834576c lies in ticket 9's range, so its evidence is attributed by position, not by its own range.
- Commit `stories` lists are empty for every commit. Attribution is by range only.
- `git_evidence.py` output for range 8 (ticket 9) was truncated in the terminal. Its file list is incomplete here and must be re-read from the JSON before Phase 2.
- Session logs: not captured. Process-lesson analysis is skipped until they are found.

## Findings

Scope: inline checks on the epic's backend and frontend diff (range bc708d2..6937258). `bmad-review` was not run; the lens checks below are narrowed to the targeted checks listed.

- F1 (god-file, test side). `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` is 1151 lines, the largest file in the backend. The epic added 563 lines to it. Source: current file size; diff stat of the range. The largest source file is `backend/src/ApiPlayground.Infrastructure/Services/GuardedTargetHttpClient.cs` at 296 lines, so no source-side god-class.
- F2 (spec, accepted deviation). Only `GET /health` remains a Minimal API mapping (`backend/src/ApiPlayground.Api/Endpoints/TracerEndpoints.cs:59`). The epic's Done when item 3 keeps it as an infra probe. Recorded as an accepted deviation.
- F3 (spec, outbound). A single `new HttpClient` exists, in `backend/src/ApiPlayground.Infrastructure/Services/GuardedTargetHttpClient.cs:62`. This matches the single guarded outbound path in AGENTS.md.
- F4 (defect, test budget). `frontend/src/App.test.tsx:157` ("saves explicitly to a new collection...") fails at the default 5000 ms timeout on this machine. It passes with a 60000 ms timeout. Repeated runs fail the same way, so this is not flakiness. The file was changed in the ticket 8 range (commit 2188e67). The test needs a larger timeout or fewer waits.
- F5 (attribution). Commit `834576c` (refactor sweep, ticket 5) sits in ticket 9's range. Ticket 5 has no baseline, so its work has no per-ticket evidence. This is the gap from Phase 1.

## Behavior verification

- Build: `dotnet build backend/ApiPlayground.sln --configuration Release` passed with 0 errors and 0 warnings.
- Frontend unit tests: `npm test` gave 74 passed, 1 failed of 75 (F4).
- Backend tests: `dotnet test backend/ApiPlayground.sln --configuration Release` gave 192 passed, 0 failed, 0 skipped. This covers migration continuity, outbound safety, and architecture boundary tests. The run used the connection strings the user set in the shell.
- Behavior of the running API: not exercised; depends on the backend tests and a database.

## Previous-retro follow-through

None found. No earlier retrospective exists in the tree for this initiative.

## Action items

Proposed, not applied. Owners are proposed; the user confirms them.

1. Fix the timeout in `frontend/src/App.test.tsx` near line 157 (F4). Decision: fixed (user: "исправить"). The test now has a 60000 ms timeout argument. `App.test.tsx` gives 24 passed, 0 failed. Owner: user. Still to verify: full `npm test` run.
2. Split `backend/tests/ApiPlayground.Tests/TracerEndpointTests.cs` (1151 lines) (F1). Decision: not done (user: "не делать"). The file size is accepted as-is. Owner: user.
3. Record the attribution gap for ticket 5 (F5). Decision: recorded (user: "да"). Commit `834576c` should be treated as ticket 5 work, not ticket 9 work, in future retros. Owner: user. Process lesson: a plan without `baseline_revision` cannot be attributed by range.
4. Record `GET /health` as an accepted deviation from the Minimal API convention (F2), so later retros stop re-flagging it. Decision: recorded as accepted deviation (user: "да"). Owner: user.

No spec reconciliation is proposed. F3 matches the spec.

## Acceptance verdict

Verdict: accepted-with-open-items (machine verdict, criteria: declared).

Evidence:
- All 10 tickets are `done`. `pending_tickets` is empty, so the unfinished-tickets rule does not force a rejection.
- Build: 0 errors, 0 warnings.
- Backend tests: 192 passed, 0 failed, 0 skipped.
- Frontend tests: 74 of 75 passed before the F4 fix. The one failure (F4) was a timeout budget issue, not a functional failure. After the fix `App.test.tsx` passes 24 of 24.
- Open items after user decisions: F1 (test file size accepted as-is), F2 (`GET /health` accepted deviation), F5 (ticket 5 attribution gap, recorded as a process lesson). None of them blocks acceptance.

The human decision overrides this machine verdict. Decision: user confirmed the verdict `accepted-with-open-items` and all action-item decisions above.

## Open questions

- Should ticket 5's sweep commit count toward ticket 9's evidence or be reported as its own unattributed work?
- Were session logs for these tickets kept anywhere?

## Assumptions

None yet. Interactive run.
