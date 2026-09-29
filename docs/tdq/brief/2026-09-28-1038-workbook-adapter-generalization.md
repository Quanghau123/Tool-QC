# BRIEF — Tổng quát hóa workbook adapter đa dự án

Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Nguyên văn

> tiến hành mở request và sửa

Mục tiêu ban đầu: mở request mới để sửa kiến trúc nhập XLSX theo hướng dùng chung thực sự cho nhiều dự án và nhiều cấu trúc workbook. Phạm vi dự kiến gồm bỏ phụ thuộc trực tiếp `be-booking` khỏi orchestration chung, hỗ trợ nhiều adapter trong một project, nhận diện adapter theo workbook, trả output group/version/status/diagnostic từ adapter, bỏ hard-code `workbook-generated/verify` khỏi Web, đồng bộ version code/profile, và giữ nguyên flow upload, batch, preflight hiện có.

## Hiểu & kiến thức

- Shared core `AutoTest.Workbooks` đã trung lập và chỉ đọc Open XML.
- `WorkbookImportService` hiện ép kiểu `VerifyWorkbookAdapter`, khiến core orchestration biết nghiệp vụ `be-booking`.
- `WorkbookAdapterRegistry` hiện resolve một adapter theo project, chưa hỗ trợ nhiều workbook family cho cùng project.
- Web endpoint đang hard-code output group `workbook-generated/verify`.
- Adapter code trả tên version 1 trong khi profile ghi version 2.
- Đây là thay đổi kiến trúc nhiều contract và nhiều file, cần lane chuyên sâu, spec, plan, implementation và QC đầy đủ.

## Hỏi đáp

### Vòng phạm vi — 2026-09-28 14:03 +07:00

1. Request bao quanh những mặt nào?
- Người dùng chọn nguyên văn: `1abcd`.
- Chốt: khả năng mở rộng nhiều dự án/nhiều adapter; tương thích ngược `be-booking`; độ tin cậy và an toàn dữ liệu; UI/API trung lập.

2. Dự án/workbook mục tiêu trong đợt này?
- Người dùng chọn nguyên văn: `2a`.
- Chốt: chưa thêm adapter dự án thứ hai; thiết kế contract và kiểm thử để sẵn sàng mở rộng.

3. Mức tương thích cần giữ?
- Người dùng chọn nguyên văn: `3a`.
- Chốt: giữ contract schema-v2, CLI, Web endpoint và output hiện tại; thay đổi chủ yếu ở orchestration/adapter bên trong, response chỉ được bổ sung metadata tương thích.

4. Mức kiểm thử?
- Người dùng chọn nguyên văn: `4a`.
- Chốt: full QC gồm custom harness, regression 69 case, preflight output, Web smoke và solution build.

