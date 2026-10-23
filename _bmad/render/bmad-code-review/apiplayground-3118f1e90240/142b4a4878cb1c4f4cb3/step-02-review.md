---
failed_layers: '' # set at runtime: comma-separated list of lenses that failed or returned empty
---

# Step 2: Review

## RULES

- All review subagents must run at the same model capability as the current session.
- Run subagents synchronously: launch them together as blocking calls awaited in this turn — never backgrounded or detached, never ending the turn to await results.

## INSTRUCTIONS

1. The review lenses are listed below. For each lens:
   - `Run only when` present and not satisfied by the current context (`{review_mode}`, `{plan_file}`, `{verbatim_intent}`) → skip the lens and tell the user which lens was skipped and why.
   - otherwise → the lens is active.

2. Announce skipped lenses first, then launch every active lens before handling any lens's result. Try running all active lenses simultaneously: substitute the runtime placeholders (`{diff_file}`, `{claims_file}`, `{plan_file}`, `{verbatim_intent}`) into each lens's instruction. `{diff_file}`, `{claims_file}`, and `{plan_file}` are paths: substitute the absolute path and let the lens read the file — a launch prompt never carries diff text, and the child's working directory is not yours. `{verbatim_intent}` is the intent section of `{plan_file}` if it has one, else the user's or conversation's own words on what the change is for, substituted inline as text; a request to review something is not an intent. When an instruction launches a reviewer subagent, launch that child with the prompt text after placeholder substitution; do not load the reviewer instruction file yourself. For any other customized instruction, execute it as written. When running lenses as subagents, spawn every reviewer before reading or reacting to any of their output; begin collection only once all are launched.

#### Quick (`quick`)

Launch a context-free subagent with this prompt:

Read `{plan_file}` — the plan holding the acceptance criteria and intent; it is empty when there is none — and the files its frontmatter `context:` lists. Read the agent instruction files (`AGENTS.md`, `CLAUDE.md`, or equivalents) at the repository root and in the directories the diff touches; those are the rules. Then read the unified diff at `{diff_file}` — it is the change under review.

Report unmet acceptance criteria, broken rules, and bugs. Each finding: the location, what goes wrong, and evidence checked against the surrounding code. Output a Markdown list of findings only — no severity, priority, or ranking.

Do not invoke any skill, and do not spawn subagents of your own — you are the reviewer. Return your findings as text in your final message; do not route them through any findings-reporting tool the host may offer.

3. If a lens's instruction requires subagents and none are available, for each such lens write that lens's child prompt beside `{plan_file}`, named after it with the lens id appended (in `D:/pet_projects/ApiPlayground/_bmad-output/{active_initiative}/` when there is no plan), with every file it points to — the diff, the claims, the reviewer instruction file — replaced inline by that file's contents, and every other line left exactly as written. That session shares no filesystem with this one, so its prompt has to stand alone; this is the only place you read a reviewer instruction file yourself. Then HALT. Ask the user to run each in a separate session (ideally a different LLM) and paste back the findings. When findings are pasted, treat them as those lenses' findings and resume from this point.

4. **Lens failure handling**: If any lens fails, times out, or returns empty results, append the lens's `name` to `failed_layers` (comma-separated) and proceed with findings from the remaining lenses.

5. Collect all findings from the completed lenses, keeping track of each finding's originating lens `id`.

## NEXT

Read fully and follow `D:/pet_projects/ApiPlayground/_bmad/render/bmad-code-review/apiplayground-3118f1e90240/142b4a4878cb1c4f4cb3/step-03-triage.md`
