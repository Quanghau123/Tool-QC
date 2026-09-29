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

## Đã chốt
- 2026-09-28: adapter version 2 sinh đủ 69 case trên sheet `VERIFY`, hỗ trợ batch theo thứ tự workbook và blocked riêng từng case thay vì fail toàn workbook.
- 2026-09-28: builder tổng quát dùng TimeFrame tương đối theo mốc sớm nhất của từng case, giữ gap/range Duration và không hard-code `06:00`.
- 2026-09-27: CLI và UI dùng cùng workbook pipeline; analyze/generate không mở request tới backend.
- 2026-09-27: không thêm hoặc chạy unit test theo chỉ đạo request; validation gồm solution build, CLI smoke, schema-v2 preflight và Web upload/generate smoke.
