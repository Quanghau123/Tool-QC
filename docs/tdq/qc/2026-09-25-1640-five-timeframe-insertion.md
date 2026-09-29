# QC — Testcase chèn Campaign Duration vào chuỗi năm TimeFrame
Ngày: 2026-09-26 · Plan: ../plan/2026-09-25-1640-five-timeframe-insertion.md · Vòng: 1
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

| # | Hạng mục | Lệnh đã chạy | Kết quả | PASS/FAIL |
|---|---|---|---|---|
| Q1 | JSON và định danh | `ConvertFrom-Json` + kiểm tra schema/project/id/destructive | Schema `2`, project `be-booking`, ID mới, destructive `true`; 15 bước | PASS |
| Q2 | Coverage preflight | `dotnet run --project runner/AutoTest.Runner -- --project be-booking --case-ids campaign.frozen-frame-five-timeframes-duration-insertion-flow --preflight-only` | `Preflight thành công cho 1 testcase.` | PASS |
| Q3 | Fixture năm TimeFrame | Runtime runner với tag `campaign-five-timeframes` | Bước fixture chạy PostgreSQL thành công nhưng API dừng trước khi hoàn tất flow; không có evidence runtime đầy đủ cho năm TF | BLOCKED |
| Q4 | Campaign A | Runtime runner với case ID mới | Không tạo được A do `localhost:5018` từ chối kết nối; chưa có snapshot quota `13/13/12/12/12` | BLOCKED |
| Q5 | Freeze/lifecycle | Runtime runner + JUnit/HTML | Dừng tại bước tạo A, chưa tới freeze/dispatch/Redis/callback | BLOCKED |
| Q6 | Campaign B | Runtime runner + JUnit/HTML | Chưa tới bước tạo B; không có response thực tế | BLOCKED |
| Q7 | Integrity sau chèn | Runtime runner + JUnit/HTML | Chưa có result-set sau chèn; không suy diễn PASS | BLOCKED |
| Q8 | Build | `dotnet build runner/AutoTest.Runner/AutoTest.Runner.csproj --configuration Release` | `Build succeeded. 0 Warning(s), 0 Error(s)` | PASS |
| Q9 | Runtime report | Runner case ID mới | JUnit và HTML được tạo tại artifact lần chạy; tổng `1`, thành công `0`, thất bại `1` | PASS |
| Q10 | Không hồi quy phạm vi | Hash testcase gốc + `git diff --check` | Testcase gốc không đổi; diff check không báo lỗi; không sửa shared framework | PASS |
| QC-F1 | Framework suite | `dotnet test tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj --configuration Release --no-restore` | Exit code `0`; không có output lỗi | PASS |
| QC-F2 | Touched-area regression | Không có mã runtime bị chạm; testcase JSON không có unit test riêng | KHÔNG ÁP DỤNG — chỉ thay đổi khai báo testcase | PASS |
| QC-F3 | Ràng buộc kiến trúc | Kiểm tra file thay đổi và nội dung testcase | Chỉ dùng shared runner/schema; không sửa backend hoặc shared framework | PASS |
| QC-F4 | Clean code | Kiểm tra thay đổi | KHÔNG ÁP DỤNG — không sửa file code | PASS |

## Bằng chứng
### Q1–Q2
```text
schema=2 project=be-booking cases=1
id=campaign.frozen-frame-five-timeframes-duration-insertion-flow destructive=True steps=15
Preflight thành công cho 1 testcase.
```

### Q8
```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Q9 / Q3–Q7
```text
[THẤT BẠI] campaign.frozen-frame-five-timeframes-duration-insertion-flow - Tạo Spot A trên năm TimeFrame và chèn Duration B sau khung đã khóa (33825ms)
No connection could be made because the target machine actively refused it. (localhost:5018)
Tổng số đã chạy: 1/1, Thành công: 0, Thất bại: 1
Báo cáo JUnit: .artifacts/test-runs/be-booking/campaign/2026-09-26_002930_be-booking.junit.xml
Báo cáo HTML: .artifacts/test-runs/be-booking/campaign/2026-09-26_002930_be-booking_campaign-five-timeframes.html
```

**Phân loại:** `ENVIRONMENT_BLOCKER` — endpoint Booking tại `localhost:5018` không lắng nghe. Đây không phải `TEST_SCRIPT_ERROR` hoặc `TEST_DATA_ERROR`; không sửa expectation và không chạy cleanup.

## Kết luận
Q1, Q2, Q8, Q9, Q10 và QC-F1–QC-F4 PASS. Q3–Q7 BLOCKED vì backend Booking không khả dụng tại `localhost:5018`; cần khởi động đúng môi trường test rồi chạy lại case ID mới.
