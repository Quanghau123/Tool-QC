# QC — Hoàn thiện API Builder no-code
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

Ngày: 2026-10-23 · Plan: ../plan/2026-10-23-0136-api-builder-full.md · Vòng: 1 · Mức QC: full

| # | Hạng mục | Lệnh đã chạy | Kết quả | PASS/FAIL |
|---|---|---|---|---|
| Q1 | Projection contract | `dotnet run --project tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj` | Framework harness kiểm projection OpenAPI-first/Roslyn fallback | PASS |
| Q2 | Workspace typed binding | `dotnet run --project tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj` | Harness kiểm source order, type, sensitive target, drift và revision | PASS |
| Q3 | Compiler schema-v2 | `dotnet run --project tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj` | Harness kiểm mapping path/query/header/body và guard preflight | PASS |
| Q4 | UI dynamic journey | `npm run test:e2e` | Chromium 1/1: operation/tree/variable/binding/recovery/compile | PASS |
| Q5 | Reload/recovery | `npm run test:e2e` | Reload khôi phục input binding `${orderId}` | PASS |
| Q6 | Playwright dev-only | `npm run test:e2e` | Chromium pass; cấu hình giữ trace/screenshot khi fail | PASS |
| Q7 | Build/regression tách runtime | `dotnet build runner/AutoTest.Runner/AutoTest.Runner.csproj`; `dotnet build runner/AutoTest.Web/AutoTest.Web.csproj` | Hai build 0 warning / 0 error; runtime API không thuộc scope | PASS |
| Q8 | Tài liệu | `python3 .../doc_lint.py --pair <spec> <plan>` | Lint cặp spec/plan không báo lỗi | PASS |
| QC-F1 | Full suite TDQ + budget | `tdq_test.py tron-bo`; `tdq_test.py so` | `modules=0 failed=0 missed=0`; full-suite `0 of budget 2`, radius miss 0 | PASS (không có module TDQ được đăng ký) |
| QC-F2 | Regression vùng chạm | `node --check ...app.js`; `dotnet build ...Runner`; `dotnet build ...Web`; framework harness; Playwright | JavaScript syntax pass; Runner/Web 0 warning/0 error; framework 66; E2E 1/1 | PASS |
| QC-F3 | Ràng buộc kiến trúc §5 | `Grep` guards ở `Program.cs`, `ApiBuilderWorkspace.cs`, `package.json`, `playwright.config.js` | Local-only/`.env` containment; sensitive binding guard; dev-only Playwright + failure artifacts | PASS |
| QC-F4 | Clean code | Self-check dưới đây | 5/5 Có | PASS |
| QC1.1 | npm UI entrypoint | `npm start`; `npm run test:e2e` | Build 0 warning/0 error, Web host `127.0.0.1:5080`; Chromium 1/1 | PASS |

## Bằng chứng

### Q1–Q3, QC-F2 — framework
```text
Framework tests passed: 66
```

### Q4–Q6, QC-F2 — browser
```text
1 passed (6.1s)
```

### Q7, QC-F2 — builds
```text
dotnet build runner/AutoTest.Runner/AutoTest.Runner.csproj
Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet build runner/AutoTest.Web/AutoTest.Web.csproj
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Q8
```text
[2026-10-23T03:02:29] doc_lint: pairing 2026-10-23-0136-api-builder-full.md ↔ 2026-10-23-0136-api-builder-full.md
```

### QC-F1
```text
tron-bo: running the full suite (0 modules)
Ran 0 tests in 0.000s
NO TESTS RAN
tdq_test: tron-bo modules=0 failed=0 missed=0 ok=True seconds=0.08

tdq_test: so request=2026-10-23-0136-api-builder-full tron_bo=0 bo_sot=0 budget=2
full-suite runs: 0 of budget 2
radius misses: 0
```

### QC-F3
```text
ApiBuilderWorkspace.cs: Không được bind field nhạy cảm hoặc credential.
Program.cs: /api/projects/{project}/api-builder/contract
Program.cs: SafeContractPath(...)
package.json: @playwright/test nằm trong devDependencies
playwright.config.js: trace: 'retain-on-failure'; screenshot: 'only-on-failure'
```

### QC1.1 — npm UI entrypoint
```text
> start
> powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-ui.ps1

Build succeeded.
    0 Warning(s)
    0 Error(s)
Now listening on: http://127.0.0.1:5080

npm run test:e2e
1 passed (7.2s)
```

### QC-F4 — Self-check
- SRP: Có — projection, workspace validation, compiler, façade và DOM rendering giữ trách nhiệm riêng.
- OCP: Có — metadata operation/field từ contract điều khiển UI; không thêm branch theo API hay project.
- LSP: Có — binding/contract validation giữ cùng kiểu lỗi `InvalidDataException` cho input không hợp lệ.
- ISP: Có — binding dùng các record và field metadata cần thiết, không truyền state/runtime executor vào domain.
- DIP: Có — UI gọi façade Web; compiler dùng schema-v2 hiện hữu, không tạo parser hay HTTP executor thứ hai.

## Runtime

NOT_RUN — Không có project/tags runtime được yêu cầu. Không chạy API testcase, không đọc `.env`, không đặt `ALLOW_PRODUCTION` hoặc `ALLOW_DESTRUCTIVE_TESTS`. Browser E2E chỉ dùng fixture OpenAPI local trong `.artifacts` và host Web local.

## Giới hạn hiện tại

- Builder contract khai thác field tree từ schema component/provenance hiện có; fixture E2E dùng `$ref` component để cung cấp leaf fields.
- Browser journey compile được draft; preflight/activation vẫn giữ coverage gate hiện hữu và không được bypass bởi Builder.
- JavaScript LSP đã được người dùng cho phép bỏ qua vì thiếu `typescript-language-server`; phần frontend dùng graphify/grep thay thế.

## Lệch spec chờ duyệt

không có

## Kết luận

PASS toàn bộ 8 DoD và 4 hạng mục QC bắt buộc trong mức `full`.
