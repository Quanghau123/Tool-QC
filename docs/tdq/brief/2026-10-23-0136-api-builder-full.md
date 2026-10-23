# BRIEF — Mở rộng đầy đủ API Builder
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Nguyên văn

> tiến hàn triển khai phần mở rộng đầy đủ

Mục tiêu đọc ban đầu: mở rộng API Builder sau MVP thành trải nghiệm no-code hoàn chỉnh: chọn operation từ OpenAPI/Roslyn, dựng cây request/response động, gán biến từ response vào request có kiểm tra kiểu/path, và có browser E2E evidence. Có thể đánh giá/cài dependency ngoài khi thực sự cần; ưu tiên thư viện nền tảng và nêu lý do trước khi thêm dependency.

Phạm vi dự kiến: `runner/AutoTest.Generator/`, `runner/AutoTest.Web/`, frontend Web, framework tests, browser E2E tooling và tài liệu/cấu hình công khai nếu contract thay đổi. Chưa rõ: mức ưu tiên giữa OpenAPI và Roslyn khi cùng hiện diện, framework browser E2E phù hợp với môi trường hiện tại, và dependency nào có thể cần.

## Hiểu & kiến thức

### Capability inventory

| Năng lực | Hiện có | Khoảng trống cần giải quyết |
|---|---|---|
| Contract nguồn | `OpenApiContractAdapter` chuẩn hóa OpenAPI 3.1; `SourceContractAnalyzer` phân tích Roslyn | Chưa có API Builder endpoint lấy/chọn contract và lưu provenance theo operation. |
| Workspace/compiler | Workspace có revision, biến A–Z, containment/redaction; compiler sinh schema-v2 `save`/`${variable}` | Step chỉ là field phẳng; chưa có tree, source operation, request location/body lồng nhau, hay tương thích type khi bind. |
| Web UI | Có tạo/lưu/compile workspace và chuyển vào lifecycle draft/preflight/activate | `defaultApiBuilderSteps()` đang cung cấp hai step mẫu cố định, không có operation chooser/tree. |
| Evidence | Framework test 66 PASS trước đó; runtime API endpoint đã được probe | Chưa có browser E2E harness/click-through evidence. |
| Browser tooling | `node`, `npm`, `npx` có trên PATH; repo chưa dùng package JS ngoài | Có thể dùng Playwright để chạy Chromium cô lập, nhưng phải nêu lý do và thêm dependency có chủ đích. |

### Lộ trình

1. Chuẩn hóa endpoint/API lấy contract từ OpenAPI hoặc Roslyn, có provenance và diagnostics.
2. Mở rộng workspace/compiler để biểu diễn operation và request/response field tree an toàn.
3. Thay UI step mẫu bằng chooser/tree/binding có kiểm tra tương thích type và thứ tự biến.
4. Thêm browser E2E chạy ứng dụng thật, kèm kiểm chứng save/reload/compile.
5. Chạy build, framework regressions và browser evidence; cập nhật tài liệu công khai nếu contract thay đổi.

## Hỏi đáp

### Cần chốt

Đã chốt theo phản hồi `1a 2a 3a`:

- OpenAPI 3.1 là nguồn contract ưu tiên; Roslyn chỉ bổ sung provenance/risk, và là fallback khi không có OpenAPI hợp lệ.
- Được thêm `@playwright/test` ở `devDependencies` để chạy Chromium cô lập qua `npx playwright`; không thêm dependency vào production Web.
- QC Full: build, framework regression, browser E2E thật, cùng kiểm drift/binding/reload/preflight.

### Quyết định dependency

Playwright phù hợp vì repo đã dùng Node/npm cho lifecycle UI, còn browser E2E hiện chưa có harness. Dependency chỉ phục vụ kiểm thử, nằm ngoài `runner/AutoTest.Web` và cần kèm script xác định cùng evidence chạy Chromium. Không thêm thư viện UI hay parser bên thứ ba: .NET/Roslyn và adapter OpenAPI hiện có đủ khả năng chuẩn hóa contract.
