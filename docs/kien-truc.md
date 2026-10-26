# Hồ sơ kiến trúc Tool-QC

## Tầng
- `AutoTest.Workbooks`: shared core kiểm tra gói Open XML, giới hạn ZIP/tài nguyên, đọc sheet/cell/formula/merge và tạo SHA-256; không chứa nghiệp vụ dự án.
- `AutoTest.ProjectAdapters.BeBooking`: adapter riêng của `be-booking`, nhận diện family `VERIFY`, parse đủ 69 case VERIFY và giữ rule TimeFrame động.
- `AutoTest.Generator`: một pipeline analyze/generate dùng chung cho CLI và Web; output là schema-v2 draft, chưa gọi backend.
- `AutoTest.Web`: upload tạm, hiển thị supported/blocked diagnostics, chuyển draft sang preflight/run/activation hiện có.

## Luật gọi
- Shared workbook core không branch theo project name và không chứa token Campaign/VERIFY; adapter dự án sở hữu cấu trúc workbook và business mapping.
- Workbook gốc chỉ là input tạm, không copy vào repository; metadata/fingerprint được dùng để trace và file upload được dọn sau generation hoặc lỗi.
- Activated testcase từ workbook của `be-booking` nằm riêng tại `projects/be-booking/testcases/workbook-generated/verify/`, không trộn với testcase Campaign viết tay.
- Overwrite chỉ xảy ra sau preview/xác nhận và SHA-256 đích còn khớp; preflight phải qua trước khi ghi file chính thức.

## Hub
- `src/AutoTest.Workbooks` — reader, guard và contract XLSX dùng chung.
- `src/AutoTest.ProjectAdapters.BeBooking/VerifyWorkbookAdapter.cs` — discovery/mapping `VERIFY` của dự án Booking.
- `runner/AutoTest.Generator/WorkbookImport` — registry và orchestration CLI/Web.
- `runner/AutoTest.Web/Program.cs` và `runner/AutoTest.Web/wwwroot/app.js` — upload, catalog, draft review và activation.
- `projects/be-booking/workbook-import/workbook-import.json` — profile giới hạn release và output group.
- `src/AutoTest.UnitX` — evaluator bảng quyết định typed, deterministic và operator whitelist; không tham chiếu HTTP, source compiler hay I/O.
- `runner/AutoTest.Generator/ApiBuilderContractProjection.cs` — projection Builder dùng chung cho OpenAPI/Roslyn, trả fingerprint, provenance, operation và field metadata; Web không tạo parser contract thứ hai.
- `runner/AutoTest.Web/ApiBuilderWorkspace.cs` + `ApiBuilderCompiler.cs` — workspace API Builder tách theo project tại `.artifacts/api-builder/`, giữ `operationId`/fingerprint và typed binding `sourceVariable` → `targetFieldId`, biên dịch path/query/header/body ra draft schema-v2 dùng lại `save`/`${variable}` và luồng preflight/activation hiện hữu; không ghi testcase chính thức trực tiếp.
- `runner/AutoTest.Web/ApiBuilderProjectConfiguration.cs` + `Program.cs` — API Builder nhận source directory local theo request hoặc `projects/<project>/project.local.json` Git-ignored, theo thứ tự request → local mapping → portable `project.json`. Discovery read-only ưu tiên OpenAPI 3.1, fallback Roslyn chỉ cho .NET; Node.js thiếu OpenAPI trả limitation. Boundary từ chối URL/UNC/reparse point, không follow child reparse point, bỏ qua `.env`, source-control, dependencies, build/artifact/log/test-output và không trả/log source path hoặc nội dung source.
- `tests/e2e/api-builder.spec.js` + `playwright.config.js` — Chromium journey development-only chạy host local, tạo workspace → operation → variable/binding → save/reload → compile; trace và screenshot chỉ giữ khi failure.
- `runner/AutoTest.Web/UnitXWorkspace.cs` — persistence draft `.artifacts/unitx` và Web facade gọi evaluator.

## UnitX Phase 2 — boundary source .NET/xUnit

Phase 2 chỉ được xem xét sau khi V1 decision table có review vận hành. Input bắt buộc là metadata bất biến của source đã chọn: repository fingerprint, relative path allowlisted, type/method signature, target framework, package-lock fingerprint và lý do nghiệp vụ. Người dùng xem và duyệt generated xUnit artifact trước khi bất kỳ build nào diễn ra; artifact, review decision, build log và test result được giữ riêng trong `.artifacts/unitx-review/`, không được ghi vào project source.

Build/run chỉ trong môi trường cô lập có filesystem tạm, mạng bị tắt, secret/environment production bị loại, time/memory/process quota và allowlist SDK/package cache. Source scan phải từ chối P/Invoke, process/shell spawn, reflection/dynamic load, file write, network client, credential API và dependency chưa duyệt. Mọi timeout, scan violation hoặc hash thay đổi sau review là `BLOCKED`, không retry bằng cách nới guard.

Không thuộc Phase 2: chạy C# tự do từ textarea, JavaScript/expression evaluator, sửa source của người dùng, tải package/network trong sandbox, test production, hoặc tự động publish generated tests. UnitX V1 tiếp tục chỉ hỗ trợ decimal decision table với operator whitelist.

## Đã chốt
- 2026-09-28: adapter version 2 sinh đủ 69 case trên sheet `VERIFY`, hỗ trợ batch theo thứ tự workbook và blocked riêng từng case thay vì fail toàn workbook.
- 2026-09-28: builder tổng quát dùng TimeFrame tương đối theo mốc sớm nhất của từng case, giữ gap/range Duration và không hard-code `06:00`.
- 2026-09-27: CLI và UI dùng cùng workbook pipeline; analyze/generate không mở request tới backend.
- 2026-09-27: không thêm hoặc chạy unit test theo chỉ đạo request; validation gồm solution build, CLI smoke, schema-v2 preflight và Web upload/generate smoke.
