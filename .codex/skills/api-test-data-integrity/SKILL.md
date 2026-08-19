---
name: api-test-data-integrity
description: Design database integrity, transaction, rollback, history, snapshot, aggregate, reservation, and side-effect tests for Tool-QC APIs. Use for mutations and reads whose correctness depends on persisted state or totals.
---

# API Data Integrity Testing

## Required coverage

- Assert all material response fields and database state before/after mutations.
- Rejected requests create, update, and delete nothing.
- Partial failure rolls back atomically; mixed valid/invalid bulk input cannot partially
  mutate unless the current contract explicitly permits it.
- Duplicate/repeated requests follow the documented idempotency behavior.
- Unrelated rows, parents, children, balances, stock, and histories remain unchanged.
- Audit/history rows have correct count, action, actor, scope IDs, transaction IDs,
  timestamps, point/quantity deltas, and before/after values.
- Snapshot fields remain unchanged after editing current master data.
- Aggregates use the complete dataset, not only the current page.
- Active reservations affect availability exactly once; expired, cancelled, completed,
  or not-yet-reserving carts do not.

Use parameterized PostgreSQL steps that match the current EF mapping. Prefer one statement
or CTE for atomic setup/checks; do not place parameters inside dollar-quoted blocks.
