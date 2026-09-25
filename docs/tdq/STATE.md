# TDQ STATE (generated — do not hand-edit)
Updated: 2026-09-25T16:27:52+07:00 · Project: D:\works\projects\Tool-QC · schema 3

| Field | Value |
|---|---|
| Request | 2026-09-25-1625-campaign-reset-ui |
| Lane | full |
| Phase | analyze |
| Spec | (none) |
| Plan | (none) |
| Quick approval | (not applicable) |
| Doc language | vi |
| Lean level | full |
| QC level | full |
| Run mode | (not settled) |

## Where we are
A request is open, deep mode. Forbidden: Writing the spec while anything is still vague.

## What comes next
Read the code, research, interview the user until nothing is vague.
```
python3 scripts/tdq_state.py set phase=spec
```
Done when: No question is left that would change the outcome

> Write state only through `python3 scripts/tdq_state.py …`. Unsure where you stand → run `tdq_state.py next`.
