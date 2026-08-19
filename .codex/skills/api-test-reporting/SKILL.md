---
name: api-test-reporting
description: Run Tool-QC testcases autonomously, inspect HTML reports, classify failures, repair testcase or fixture errors, rerun until pass, and stop on backend or environment blockers. Use at the end of every testcase creation, migration, review, or repair task.
---

# API Test Execution and Reporting

## Verification loop

1. Parse affected JSON and confirm project names.
2. Build the runner and applicable framework tests.
3. Announce project, hostname, environment, tags, and destructive status.
4. Run the smallest affected tag and inspect console plus HTML report.
5. Classify every failure:
   - `TEST_SCRIPT_ERROR`: wrong route, payload, saved path, assertion, enum, SQL, cleanup,
     or stale contract. Fix and rerun.
   - `TEST_DATA_ERROR`: incomplete, conflicting, expired, or invalid fixture. Fix and rerun.
   - `ENVIRONMENT_BLOCKER`: unavailable API/broker/database, missing safe configuration,
     or denied execution permission. Diagnose safely and request the exact action.
   - `BACKEND_BUG`: current request/fixture matches source and approved rule, but behavior
     violates the contract or corrupts/leaks data. Stop the affected scenario with evidence.
6. Continue automatically for script/data errors until pass. Run related regression tags
   when shared setup, auth, cleanup, mapping, or framework execution changed.

If destructive execution is approved for an isolated environment but blocked, provide:

```powershell
$env:ALLOW_DESTRUCTIVE_TESTS='true'
dotnet run --project runner/AutoTest.Runner -- --project <project> --tags <tags>
```

Never enable production, expose secrets, or call connection refusal a backend bug. Report
pass/fail/skip totals, files, command, report path, actual failure evidence, classification,
and untested items.
