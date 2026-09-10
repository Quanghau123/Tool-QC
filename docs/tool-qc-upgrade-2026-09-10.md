# Báo cáo nâng cấp Tool-QC ngày 10/09/2026

## Phạm vi

- UI vận hành: chọn testcase chính xác theo ID, dừng workload, lịch sử chạy, xem lại báo cáo.
- Cấu hình: phân biệt nguồn biến, che secret, kiểm tra HTTP và TCP cho database/Redis/MQTT.
- Reporting: HTML hiện có và JUnit XML.
- Framework: chạy case song song có giới hạn, Redis step, JSON Schema cơ bản.
- Tự động hóa: generator quét controller .NET và GitHub Actions template.

## Cấu hình mới

- `MAX_PARALLEL_CASES=1`: giới hạn số testcase chạy đồng thời.
- `JUNIT_REPORT_ENABLED=true`: tạo báo cáo JUnit.
- `REDIS_CONNECTION_STRING=`: kết nối cho bước Redis.

## Redis testcase

`request.redis` hỗ trợ `get`, `hget`, `set`, `delete`, `exists`. Assertion dùng `expect.redis.valueEquals`, `exists`, `ttlGreaterThan`.

## JSON Schema

HTTP assertion `expect.jsonSchema` hỗ trợ type, required, properties, items, minItems và enum. Đây là validator không phụ thuộc package ngoài, phù hợp contract assertion phổ biến; chưa thay thế validator JSON Schema Draft đầy đủ.

## Generator

```powershell
.\toolqc.ps1 generate <project> <source-path>
```

Generator chỉ tạo draft. Các mục auth/scope/integrity/validation chưa suy luận chắc chắn được đánh dấu blocked.

## Kiểm thử đã thực hiện

- AutoTest.Redis build Release thành công.
- AutoTest.Generator build Release thành công và chạy smoke thành công.
- AutoTest.Http build Release thành công.
- AutoTest.Web build Release thành công khi dùng output kiểm tra riêng; tiến trình UI đang mở khóa output mặc định.
- JavaScript, PowerShell và JSON syntax hợp lệ; git diff không có whitespace error.

## Giới hạn xác minh

Build graph Runner/FrameworkTests trên máy hiện tại đôi lúc dừng ở MSBuild project-reference evaluation mà không phát sinh compiler diagnostic. Các project thay đổi độc lập đã được build để tách lỗi source khỏi lỗi graph môi trường.
