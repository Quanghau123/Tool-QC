# Tool-QC architecture

Tool-QC separates project-specific test data from reusable execution modules.

## Dependency direction

```text
AutoTest.Runner
  -> AutoTest.Core (suite orchestration, safety, variables, authentication)
  -> AutoTest.Http
  -> AutoTest.PostgreSql
  -> AutoTest.Mqtt
  -> AutoTest.HttpStub
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

`runner/AutoTest.HttpStub` hiện là compatibility CLI cho Integration Host. Profile nằm tại
`projects/<project>/integrations/<name>/integration.json`; HTTP là transport provider đầu
tiên. Host quản lý start/status/stop, hỗ trợ chạy foreground hoặc background và lưu từng
request/response thật vào `integration-results/`. Contract profile dùng trường `transport`
để provider Redis Streams, Kafka hoặc công nghệ khác có thể được thêm độc lập mà không đổi
testcase orchestration hay business profile hiện có.

Mỗi transport mới phải sở hữu lifecycle, capture model và assertion riêng, nhưng xuất
evidence về một cấu trúc chung gồm session, request/event nhận được, phản hồi/ack đã gửi và
metadata đã che secret. Không thêm nhánh theo tên project trong shared host.

HTTP implementation được chia thành `AutoTest.Integration.Abstractions`,
`AutoTest.Integration.Http` và `AutoTest.Integration.Artifacts`. CLI chỉ parse command,
quản lý ownership và compose provider. HTTP provider sở hữu route matching, response
sequence, timeout/delay, payload limit và capture; artifact module sở hữu JSON/HTML.

## Compatibility

The current JSON contract and CLI remain version 1. Existing projects continue to
live under `projects/<project-name>/testcases/**/*.json`.
