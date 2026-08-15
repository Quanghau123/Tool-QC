# API Auto Test — agent instructions

## Purpose

This repository is a reusable, data-driven API automation framework for multiple
services. Project-specific behavior belongs under `projects/<project-name>/`.
The shared engine must remain independent of any business project.

The expected user workflow is:

1. Configure `.env`.
2. Select or create a project under `projects/`.
3. Change only that project's `project.json` and `testcases/*.json`.
4. Run the shared runner.
5. Review the complete pass/fail summary.

## Read before acting

Before making a change, read these files:

- `README.md`
- `.env.example` (key names only; never print values from `.env`)
- The selected project's `project.json`
- Every test case in the selected project's `testcases/` when changing or
  evaluating that project
- Relevant code under `src/AutoTest.Core/` only when changing framework behavior

Never read, print, copy, summarize, or commit secret values from `.env`.

## Repository ownership boundaries

### Shared framework — do not change for a single project's needs

- `src/AutoTest.Core/`
- `runner/AutoTest.Runner/`

Change shared code only when the requested capability is genuinely reusable by
multiple projects. Never add checks such as `if (project == "ops-service")` or
hard-code a project's endpoints, response envelope, credentials, database
schema, roles, or business rules in shared code.

### Project-specific configuration — normal extension point

- `projects/<project-name>/project.json`
- `projects/<project-name>/testcases/*.json`

When onboarding a service, copy `projects/project-template/` and modify only the
copy. Do not modify `project-template` with business-specific examples.

### Environment configuration

- `.env.example` is safe documentation and must contain placeholders only.
- `.env` holds local values and must remain ignored by Git.
- Operating-system and CI environment variables override `.env`.
- Never place tokens, passwords, API keys, or connection strings in project or
  test-case JSON files. Reference them with `${env:VARIABLE_NAME}`.

## Required workflow for adding a project

1. Copy `projects/project-template` to `projects/<project-name>`.
2. Set the same project name in its `project.json` and every test suite.
3. Configure `baseUrlVariable`; add that variable to `.env.example` with an empty
   or safe local placeholder.
4. Configure authentication as data (`static-token` or `login`), never in C#.
5. Add real production hostnames to `safety.productionHosts`.
6. Add a non-destructive health/readiness smoke test first.
7. Add feature cases in separate, clearly named JSON files.
8. Mark every case that creates, updates, deletes, publishes, sends, or otherwise
   changes state as `"destructive": true`.
9. Give every case a stable, unique `id` such as `events.create-success`.
10. Run the validation/build and the selected project's smoke suite.

## Test-case design rules

- Keep test cases declarative; do not create C# test classes for a service.
- The `name` of every test case and test step, including cleanup steps, must be written in clear, natural Vietnamese so Vietnamese users can immediately understand what is being tested. This rule applies to descriptive `name` fields in the test specification, not to business-data fields named `name` inside request bodies or expected API data.
- Whenever a test case creates a local test account, device, or other test fixture that requires a password, the password must be exactly `Admin@123`, and `confirmPassword` must use the same value. This fixed value is test data only; never use it for production credentials, real users, environment secrets, or authentication configuration.
- A case owns its data. Use `${unique}` to avoid collisions.
- Use `${nowIso}`, `${futureStartIso}`, and `${futureEndIso}` when a test fixture needs ISO 8601 event times.
- Use the `${futureDay*Iso}` variables exposed by the runner for deterministic relative-day schedule tests.
- Save response values with JSON paths and reuse them in later steps.
- Add cleanup for created data whenever the API supports cleanup.
- Cleanup must be safe to run after a partially failed case.
- Assert response status and all fields material to the behavior; status-only
  checks are acceptable only for health endpoints.
- Use `maxResponseTimeMs` only when the requirement has a meaningful threshold.
- Do not rely on execution order between separate cases.
- Do not use production data identifiers or real customer information.
- Tags should use stable categories such as `smoke`, `regression`, `integration`,
  `auth`, and the feature name.

Example:

```json
{
  "project": "sample-service",
  "cases": [
    {
      "id": "items.create-and-read",
      "name": "Create and read an item",
      "tags": ["smoke", "items"],
      "destructive": true,
      "variables": {
        "itemName": "Auto-${unique}"
      },
      "steps": [
        {
          "name": "Create item",
          "auth": "admin",
          "request": {
            "method": "POST",
            "path": "/api/items",
            "body": { "name": "${itemName}" }
          },
          "expect": {
            "status": 201,
            "json": {
              "$.data.name": { "equals": "${itemName}" },
              "$.data.id": { "exists": true }
            }
          },
          "save": { "itemId": "$.data.id" }
        }
      ],
      "cleanup": [
        {
          "name": "Delete item",
          "auth": "admin",
          "request": {
            "method": "DELETE",
            "path": "/api/items/${itemId}"
          }
        }
      ]
    }
  ]
}
```

## Safety rules

- Production execution is denied by default. Never set `ALLOW_PRODUCTION=true`
  on the user's behalf.
- Destructive execution is denied by default. Enable
  `ALLOW_DESTRUCTIVE_TESTS=true` only when the user has identified an isolated
  test environment and explicitly wants state-changing cases run.
- Before running, state the selected project, base URL hostname (never credentials),
  environment, tags, and whether destructive cases are enabled.
- Never weaken production guards, secret redaction, or destructive-test guards to
  make a run pass.
- Never log authorization headers, login bodies, tokens, passwords, database
  connection strings, or Redis connection strings.
- A connection failure is an environment failure, not a reason to change expected
  results or skip assertions.

## Commands

Build the reusable runner:

```powershell
dotnet build runner/AutoTest.Runner/AutoTest.Runner.csproj
```

Run one project and tag set:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags smoke
```

If `--project` or `--tags` is omitted, the runner uses `ACTIVE_PROJECT` and
`TEST_TAGS` from the environment configuration.

## Verification requirements

### Tests against a changing service

When an existing test case was written for an older version of the selected
service, never repair it from names or behavior remembered from an earlier run.
Before editing the test:

1. Read the latest controller routes, request/response DTOs, entities, EF model
   snapshot, and the service methods that implement the behavior in the current
   service source tree.
2. Treat API response fields and database tables/columns as versioned contracts.
   Confirm their current names from source; do not infer them from an old HTML
   report or an old test case.
3. Run the smallest applicable tag against the available local service and use
   the actual response body/report to correct the next mismatch. A successful
   HTTP status does not prove a saved JSON path still exists.
4. Continue the inspect-edit-run loop until the requested tag passes, or report
   the concrete environment blocker. Do not tell the user a migrated test is
   ready based on JSON parsing or a successful runner build alone.
5. After changing shared database-command handling, verify it with a real
   parameterized PostgreSQL step. In particular, do not place Npgsql parameters
   inside PostgreSQL dollar-quoted blocks (`DO $$ ... $$`) and do not send
   multiple parameterized SQL commands as one prepared statement. Prefer one
   parameterized statement, using CTEs when several mutations must be atomic.

For the current ops-service redemption design, always re-check the source before
use. As of 2026-08-15 it uses `cartId` in the Scan response and persists data in
`redemption_cart`, `redemption_cart_gift`, and `redemption_history`; the older
`redemption_session`, `redemption_session_line`, `redemption`, `redemption_line`,
`Stage`, and `ExchangedQuantity` contracts are obsolete. This note is a warning
against stale assumptions, not a substitute for checking the latest source.

After framework changes:

1. Build `runner/AutoTest.Runner/AutoTest.Runner.csproj` with zero errors.
2. Run applicable framework tests when they exist.
3. Run at least the selected project's non-destructive smoke cases when its API
   is available.
4. Report build and test results separately. Never describe a connection-refused
   run as a framework build failure.

After project/test-case-only changes:

1. Confirm all JSON parses and project names match.
2. Build the runner.
3. Run only the requested environment/tags; do not silently expand scope.
4. Report total passed, failed, and any cases not run due to safety controls.

## Change discipline

- Search for an existing shared capability before adding another implementation.
- Keep changes within the requested scope.
- Do not add a dependency unless the platform libraries cannot provide the
  required reusable capability; explain the reason before adding it.
- Do not commit or push unless the user explicitly asks and approves it.
- Update `README.md`, `.env.example`, `project-template`, and this instruction file
  whenever a shared behavior or public test-case contract changes.

## Current capability boundary

The shared runner currently supports HTTP JSON, multipart form, concurrent HTTP requests per step, PostgreSQL fixture commands, and MQTT cases including real broker Last Will verification, variable interpolation,
simple object-property JSON paths, chained values, static-token/login and saved-token
authentication, per-step dynamic MQTT credentials, bounded step retries, cleanup,
tag filtering, response assertions, secret redaction, and safety guards.

PostgreSQL assertions/Redis assertions, full JSON Schema validation, HTML/JUnit reporting,
parallel case scheduling, and CI templates are not yet implemented. Do not claim they
exist. When implementing them, keep provider behavior configurable and reusable,
and add verification for the shared engine before advertising the capability.

