# Báo cáo kiểm thử Device Login với EventCode

- Thời gian chạy: 2026-08-21 16:52:11 (Asia/Saigon)
- Project: `ops-service`
- Tag: `device-login-event-code`
- Endpoint chính: `POST /api/OnsiteOperations/Auth`
- Dữ liệu sau test: được giữ lại theo yêu cầu, không chạy cleanup.
- Báo cáo HTML gốc: `test-results/ops-service/devices/2026-08-21_165211_ops-service_device-login-event-code.html`

## Kết quả tổng hợp

| Nhóm | Kết quả | Phân loại | Bằng chứng |
|---|---|---|---|
| Scope hai Event dùng cùng DeviceCode | HỎNG | BACKEND_BUG / DATABASE_SCHEMA_MISMATCH | Device thứ hai trả HTTP 500, PostgreSQL 23505 tại unique constraint `IX_device_Code`. |
| Validation và lỗi xác thực | ĐẠT | PASS | Toàn bộ bước validation, malformed JSON, sai kiểu, sai mật khẩu/code, case/whitespace và kiểm tra DB đã chạy thành công. |
| Lifecycle Device/Event | ĐẠT | PASS | Active, Paused, Maintenance, Decommissioned, soft-delete, Ended và Cancelled đúng contract source hiện tại; DB token đúng. |

Tổng số nhóm runner: **3**; đạt **2**; hỏng **1**; skipped **0**.

## Lỗi backend được xác nhận

### Hai thiết bị cùng Code ở hai Event khác nhau không tạo được

Request thứ nhất tạo Device trong Event A thành công. Request thứ hai dùng cùng `code`, nhưng `eventId` khác, trả:

```text
HTTP 500
23505: duplicate key value violates unique constraint "IX_device_Code"
Key ("Code")=(DEVICE_DUPLICATE-...) already exists.
```

Expected theo thay đổi EventCode:

```text
HTTP 200
Cho phép cặp (EventId A, DeviceCode X) và (EventId B, DeviceCode X)
```

Actual database vẫn đang áp dụng unique index toàn cục `IX_device_Code`. Vì vậy API đăng nhập đã nhận `EventCode`, nhưng database hiện chạy chưa cho phép tình huống cốt lõi mà EventCode cần phân biệt. Response 500 còn làm lộ stack trace và chi tiết constraint; đây cũng là vấn đề error handling.

Các bước đăng nhập chéo A/B chưa thể chạy vì fixture Device B không được tạo. Trạng thái: **BLOCKED_BY_BACKEND**.

## Validation đã xác nhận

Suite đã chạy thật và đạt các nhóm sau:

- Payload hợp lệ trả `accessToken` và `refreshToken`.
- Thiếu, null và rỗng cho `eventCode`, `code`, `password` bị từ chối.
- Sai mật khẩu và mã thiết bị không tồn tại bị từ chối.
- Sai hoa/thường và khoảng trắng không đăng nhập nhầm thiết bị.
- Field ngoài contract không gây HTTP 500.
- Malformed JSON và field sai kiểu trả lỗi client, không gây HTTP 500.
- Các request thất bại không tạo thêm refresh token; đã đối chiếu database.

## Lifecycle đã xác nhận

- Active: đăng nhập được.
- Paused: đăng nhập được theo rule hiện tại.
- Maintenance: đăng nhập được theo rule hiện tại.
- Decommissioned: bị chặn.
- Device xóa mềm: không tìm thấy.
- Event Ended: đăng nhập được.
- Event Cancelled: bị chặn bằng `Mes.Event.NotAllowed.EventStatus`.
- Database chỉ có refresh token cho các trạng thái được phép.

## Kết luận

Thay đổi EventCode **chưa hoàn chỉnh trên môi trường đang chạy**: tầng request/service đã dùng EventCode, nhưng schema database vẫn unique toàn cục theo Device.Code. Cần áp dụng/kiểm tra migration chuyển unique index sang `(EventId, Code)`, rồi chạy lại suite. Tool-QC không sửa backend và không giả lập kết quả pass.

Lệnh chạy lại:

```powershell
dotnet run --project runner/AutoTest.Runner --no-build -- --project ops-service --tags device-login-event-code
```
