---
name: api-test-concurrency
description: Design concurrency, race-condition, idempotency, duplicate-request, last-stock, point-balance, quota, and conflicting-payload API tests. Use when multiple users or requests can contend for mutable resources in Tool-QC testcases.
---

# API Concurrency Testing

Start with one-user happy path. Increase to two deterministic contenders, then 10, then
the requested load only after earlier levels behave correctly.

## Required coverage

- Same request concurrently from at least two actors.
- Same actor repeats confirm/update/delete concurrently.
- Two distinct payloads target the same record simultaneously.
- Last stock/seat/quota/unit and exact remaining balance contention.
- Duplicate retry after a successful response and after an uncertain/failed response.
- Verify no negative stock/balance, oversubscription, duplicate transaction/history,
  lost update, leaked reservation, or inconsistent response/database totals.
- Verify failure of one contender does not roll back or corrupt a legitimate winner.

Use Tool-QC `concurrentRequests` when payloads differ and `parallelRequests` when they are
identical. Give each request an explicit expected outcome; do not accept broad statuses
that conceal a race defect.
