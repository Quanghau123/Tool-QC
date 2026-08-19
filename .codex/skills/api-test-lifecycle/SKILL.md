---
name: api-test-lifecycle
description: Design status, lifecycle, temporal-boundary, expiry, cancellation, completion, and blocked-mutation API tests. Use when endpoint behavior depends on entity status, event phase, reservation expiry, or current time.
---

# API Lifecycle and Time Testing

## Required coverage

- Exercise every valid lifecycle/status enum and invalid enum values via the contract
  validation skill.
- Test before start, exact start boundary, active interval, exact end boundary, ended,
  cancelled, expired, completed, paused, maintenance, and decommissioned as applicable.
- Separate read, login/token, operational mutation, administrative mutation, history,
  cleanup, and exception operations; rules may differ.
- Verify blocked requests return the exact lifecycle message and make no database change.
- Verify expired/cancelled/completed reservations do not reduce availability or apply
  effects twice.
- Use deterministic time variables or direct fixtures; avoid fixed sleeps.

Use the approved business rule as authority, then confirm current backend implementation.
Record any contract ambiguity instead of inventing a rule.
