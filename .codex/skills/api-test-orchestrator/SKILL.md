---
name: api-test-orchestrator
description: Orchestrate complete API testcase design, review, execution, failure classification, repair, and reruns in Tool-QC. Use whenever creating, migrating, reviewing, or extending testcase JSON under projects/*/testcases; it selects and requires the applicable specialist API testing skills.
---

# API Test Orchestrator

Coordinate testcase work from current backend contract through a verified run. Never stop
at a happy path or hand the user an intermediate testcase to run manually.

## Workflow

1. Read repository instructions, current backend controllers/DTOs/validators/services,
   persistence mappings, and the closest testcase.
2. Create a coverage matrix. For every specialist skill below, mark it `required` or
   `not applicable` with a concrete reason.
3. Read every required specialist `SKILL.md` completely and apply it:
   - `../api-test-contract-validation/SKILL.md` — always required.
   - `../api-test-auth-scope/SKILL.md` — required for authenticated or scoped APIs.
   - `../api-test-data-integrity/SKILL.md` — required for mutations, totals, histories,
     snapshots, reservations, or persistence-dependent reads.
   - `../api-test-concurrency/SKILL.md` — required for stock, points, quota, confirmation,
     idempotency, bulk mutation, or other contested resources.
   - `../api-test-query/SKILL.md` — required for list/search/filter/sort/page endpoints.
   - `../api-test-lifecycle/SKILL.md` — required when status or time changes permission,
     availability, or behavior.
   - `../api-test-reporting/SKILL.md` — always required.
4. Create isolated fixtures with unique IDs and no cross-case execution dependency.
5. Parse affected JSON, build, announce run context, and run the smallest affected tag.
6. Inspect console and HTML report. Let the reporting skill govern classification,
   automatic repairs, reruns, blockers, and final reporting.

Preserve every fixture after execution. Do not run cleanup or delete created test data;
the user must be able to inspect it through APIs and the database.

## Integrity rule

Do not change expectations merely to match actual output. Change them only when current
source or an explicitly approved business rule proves the testcase stale. Do not modify
production backend code unless separately requested.

All results must come from actual execution evidence. Never fabricate or infer a PASS,
response, status, database value, report, duration, or count. Never describe build, JSON
parsing, source review, or theoretical reasoning as a completed test run. Distinguish
`PASS`, `FAIL`, `NOT_RUN`, and `BLOCKED`, and preserve evidence with secrets redacted.

## Deliverable

Report the coverage matrix, files, run commands, totals, failure classifications,
backend evidence, and any item not tested with its reason.
