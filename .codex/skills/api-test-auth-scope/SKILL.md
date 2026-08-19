---
name: api-test-auth-scope
description: Design authentication, authorization, role, permission, assignment, parent-resource, event, tenant, and data-leakage API tests. Use for protected Tool-QC endpoints or any route/data scoped by an actor, event, tenant, owner, booth, customer, or other parent.
---

# API Authentication and Scope Testing

## Required coverage

- Valid actor with required permission and assignment.
- Missing token, malformed token, expired token when practical, and wrong actor type.
- Authenticated actor missing the precise permission.
- Actor has permission but is not assigned to the target event/tenant/scope.
- Resource belongs to the correct parent versus an existing different parent.
- Nonexistent resource and cross-scope existing resource return the current safe error
  without leaking existence or data.
- List, detail, mutation, bulk, history, and nested endpoints enforce the same scope.
- Obsolete routes are not accidentally exposed after route migration.
- CMS/device/public middleware does not misclassify another actor type.

Verify denied mutations leave persistence unchanged. Never hard-code reusable credentials;
use environment configuration and existing authentication fixtures.
