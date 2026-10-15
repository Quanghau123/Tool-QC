# TDQ STATE (generated — do not hand-edit)
Updated: 2026-10-15T09:10:09+07:00 · Project: D:\works\projects\Tool-QC · schema 3

| Field | Value |
|---|---|
| Request | 2026-10-15-1040-campaign-date-test |
| Lane | quick |
| Phase | idle |
| Spec | (none) |
| Plan | docs/tdq/plan/2026-10-15-1040-campaign-date-test.md — ⏳ awaiting approval |
| Quick approval | ✔ approved |
| Doc language | vi |
| Lean level | full |
| QC level | full |
| Run mode | main |

## Where we are
Finished, or no request opened yet. Forbidden: Overwriting an unfinished request without asking the user.

## What comes next
Wait for a new request from the user.
```
python3 scripts/tdq_state.py init <YYYY-MM-DD-HHMM-slug> <nhanh|chuyen-sau> [--lang <code>]
```
Done when: A new request is open

> Write state only through `python3 scripts/tdq_state.py …`. Unsure where you stand → run `tdq_state.py next`.
