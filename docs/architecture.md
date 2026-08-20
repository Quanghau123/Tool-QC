# Tool-QC architecture

Tool-QC separates project-specific test data from reusable execution modules.

## Dependency direction

```text
AutoTest.Runner
  -> AutoTest.Core (suite orchestration, safety, variables, authentication)
  -> AutoTest.Http
  -> AutoTest.PostgreSql
  -> AutoTest.Mqtt
  -> AutoTest.Reporting.Html
  -> AutoTest.TestValidation

All modules depend on AutoTest.Abstractions (public contracts and execution results).

`AutoTest.TestValidation` performs a no-network preflight before execution so malformed
or ambiguous testcase contracts fail before any fixture or backend state is changed.

`AutoTest.MessageScanner` is an independent static-analysis/reporting module. Its CLI
accepts any source directory and emits an XLSX message catalog without coupling the
test runner or shared step-executor contract to a specific backend.
```

`projects/<project-name>/` contains only configuration and declarative test cases.
No reusable module may branch on a project name or contain an ops-service route,
database table, credential, or business rule.

## Extension model

Each tool implements `ITestStepExecutor`. The registry selects exactly one executor
for a step. Adding Redis, Kafka, gRPC, telemetry, or another provider therefore adds
a module and one registration at the composition root without changing testcase
orchestration.

Assertions owned by a transport stay in that transport module. Reports consume the
common `RunResult` model and have no transport dependency.

## Compatibility

The current JSON contract and CLI remain version 1. Existing projects continue to
live under `projects/<project-name>/testcases/**/*.json`.
