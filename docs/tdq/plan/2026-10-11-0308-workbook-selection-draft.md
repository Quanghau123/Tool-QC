# QUICK — Làm rõ trạng thái sinh draft XLSX
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Phạm vi
- Trong: xóa lựa chọn stale sau khi upload đã dùng; khóa checkbox và giải thích cần phân tích lại để tạo batch mới.
- Ngoài: đổi API/upload lifecycle, chạy testcase backend destructive.
- Bỏ B0: đã có report XLSX gần đây, capability không mới.
- Bỏ B2: nguyên nhân nằm trong UI/API nội bộ; không cần nghiên cứu bên ngoài.

## Task
- [x] Sửa trạng thái chọn testcase sau generate, thêm hướng dẫn phân tích lại; kiểm bằng self-check Node. Chạm: `runner/AutoTest.Web/wwwroot/app.js`, `tests/workbook-selection-ui.test.js`.
- [x] Ghi nhận không có tài liệu BA đã xác nhận. Chạm: `AGENTS.md`.
- [x] Build Web, chạy self-check; lưu QC và working log. Chạm: `docs/tdq/brief/2026-10-11-0308-workbook-selection-draft.md`, `docs/tdq/plan/2026-10-11-0308-workbook-selection-draft.md`, `docs/workinglog/2026-10-11.md`.

## Definition of Done
- `node tests/workbook-selection-ui.test.js` pass; controls phản ánh upload đã dùng và chỉ dẫn phân tích lại.
- `dotnet build runner/AutoTest.Web/AutoTest.Web.csproj -c Release --no-restore` pass.
- Draft Case 1 được giải thích, không chạy API destructive; no BA status có ngày trong `AGENTS.md`.

## QC
- PASS: `node tests/workbook-selection-ui.test.js` — reset selection, guidance, aria-disabled.
- PASS: `dotnet build runner/AutoTest.Web/AutoTest.Web.csproj -c Release --no-restore` — 0 warning, 0 error.
- PASS: UI smoke `GET /` và `GET /api/projects/be-booking/tests` — HTTP 200; static JS chứa reset/guidance. Một host cũ chiếm cổng khiến tiến trình smoke mới không bind; endpoint đang phục vụ vẫn trả 200.
- PASS: workbook SHA-256 vẫn khớp `608d78161d0199bb1c187a805c0795368821e83399446c71195eedaa0561ed07`; không chạy API Campaign destructive.
- PASS: review độc lập không chạy được do không có slot model; đã thay bằng diff/self-check/build review tại chỗ.




