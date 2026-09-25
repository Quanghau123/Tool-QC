# Hồ sơ kiến trúc Tool-QC

## Tầng
- Testcase project: khai báo request, fixture, assertion và coverage cho từng dịch vụ.
- Shared runner: đọc schema, nội suy biến, thực thi transport và sinh report.
- Backend services: xử lý nghiệp vụ Booking/MTC, Redis callback, PostgreSQL và Elasticsearch.
- Báo cáo/QC: lưu bằng chứng runtime, JUnit và HTML; không thay đổi contract backend.

## Luật gọi
- Testcase project được gọi shared runner qua schema khai báo; không gọi trực tiếp production code.
- Shared runner không được chứa business rule riêng của `be-booking` hoặc MTC.
- QC chỉ đọc backend source và dữ liệu runtime; không sửa backend PROD trong request này.

## Hub
- `projects/be-booking/testcases/campaign/frozen-frame-duration-insertion-flow.json` — 15 bước, hub của request này.
- `runner/AutoTest.Runner` — entrypoint thực thi testcase.
- `src/AutoTest.Core` — điều phối suite và assertion dùng chung.
- Elasticsearch dispatch log — nguồn bằng chứng callback MTC trong testcase.
- PostgreSQL campaign state — nguồn bằng chứng lifecycle và quota.

## Đã chốt
- 2026-09-25: request mở rộng được phép sửa shared Redis contract/executor/validator, framework tests, cấu hình môi trường và đúng testcase frozen-frame-duration-insertion-flow; không sửa backend Booking/MTC hoặc testcase nghiệp vụ khác.
- 2026-09-25: `errorMessage` là trường nullable, chỉ có ở log thất bại theo backend source hiện tại.

- 2026-09-25: Tool-QC đọc callback Redis Stream bằng consumer group riêng; không dùng chung group nghiệp vụ và không xóa message khỏi stream.
