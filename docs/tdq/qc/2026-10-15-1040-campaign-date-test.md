# QC — Testcase Campaign dùng chung ngày
Ngày: 2026-10-15 · Plan: ../plan/2026-10-15-1040-campaign-date-test.md · Vòng: 1
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

| # | Hạng mục | Lệnh đã chạy | Kết quả | PASS/FAIL |
|---|---|---|---|---|
| Q1 | Schema v2, manifest và một biến ngày cho tám Campaign | `py -m json.tool` và kiểm tra cấu trúc PowerShell | 21 bước; 8 create dùng `${campaignDate}`; 1 VerifySlots; 5 biến allocation; mọi evidence step tồn tại | PASS |
| Q2 | Preflight và assertion API/DB | `dotnet run --no-build --configuration Release --project runner/AutoTest.Runner/AutoTest.Runner.csproj -- --project be-booking --tags campaign-reject-copy-shared-date --preflight-only` | `Preflight thành công cho 1 testcase.` | PASS |
| Q3 | Build và kiểm tra report tag | Release build; chạy tag đã chọn | Build 0 warning/0 error; HTML và JUnit đã đọc; runtime ghi BLOCKED theo DoD | PASS theo điều kiện DoD |
| QC-F1 | Chạy testcase đã chọn | `dotnet run --no-build --configuration Release --project runner/AutoTest.Runner/AutoTest.Runner.csproj -- --project be-booking --tags campaign-reject-copy-shared-date` | 0/1 pass, 1/1 fail do `localhost:5018` actively refused; phân loại ENVIRONMENT_BLOCKER | BLOCKED |
| QC-F2 | Unit test vùng testcase JSON | Không có unit test riêng cho testcase JSON; đã chạy preflight và tag ở QC-F1 | Tag dừng trước request tạo Campaign; không xác nhận được hành vi API | BLOCKED / thiếu unit test riêng |
| QC-F3 | Ràng buộc kiến trúc trong spec §5 | Không có spec §5 trong lane quick | Không thay đổi code/framework | KHÔNG ÁP DỤNG |
| QC-F4 | Clean code | Không sửa file code | Không áp dụng self-check code | KHÔNG ÁP DỤNG |

## Bằng chứng
### Q1
```text
Schema/project=2/be-booking; steps=21; VerifySlots=1; 8 campaigns share campaignDate=True; proposal params=5; stale verified vars=False; coverage evidence=True
```

### Q2
```text
Preflight thành công cho 1 testcase.
```

### Q3
```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### QC-F1
```text
[THẤT BẠI] campaign.reject-copy-shared-date - Tạo tám Campaign cùng ngày, từ chối Campaign 8 và kiểm tra bản sao (6114ms)
No connection could be made because the target machine actively refused it. (localhost:5018)
Tổng số đã chạy: 1/1, Thành công: 0, Thất bại: 1
HTML: .artifacts/test-runs/be-booking/campaign/2026-10-15_081334_be-booking_campaign-reject-copy-shared-date.html
JUnit: .artifacts/test-runs/be-booking/campaign/2026-10-15_081334_be-booking.junit.xml
```
Năm bước fixture DB/MTC PASS trước lỗi kết nối; request tạo Campaign 1 là bước đầu tiên không chạy được tới API.

## Kết luận
BLOCKED: JSON, manifest, preflight và build đạt; lượt API chưa kiểm chứng được vì dịch vụ local không lắng nghe tại `localhost:5018`. Chưa có unit test riêng cho testcase JSON. Không xóa fixture.
