---
name: api-test-contract-validation
description: Design API contract, boundary, malformed-input, enum, identifier, numeric, and message-convention tests. Use for every Tool-QC endpoint testcase, especially request validators, response envelopes, enum inputs, IDs, quantities, and standardized error messages.
---

# API Contract and Validation Testing

Confirm method, current route, content type, request DTO, response envelope, status, and
exact message key from current source.

## Required coverage

- Required fields: valid, omitted, `null`, empty, and whitespace where distinct.
- Strings: minimum, maximum, maximum + 1, Unicode, and relevant special characters.
- Every enum: all valid members plus `-1`, `99`, and `999999` when numeric transport is
  accepted. Also cover zero, `null`, omitted, string, and fractional values when distinct.
- IDs: valid, nonexistent, `Guid.Empty`, malformed, deleted/inactive, and wrong parent
  scope when applicable.
- Numbers: negative, zero, one, exact boundary, boundary + 1, very large value, and
  overflow/precision risk.
- Collections: omitted, `null`, empty, one item, duplicates, invalid member, and mixed
  valid/invalid members.
- Reject unknown or case-variant properties when the backend contract or security model
  requires strict binding.

For every rejection, assert HTTP status and exact current message convention. Compare
similar validators for inconsistency, but do not assume all validation failures use the
same message.
