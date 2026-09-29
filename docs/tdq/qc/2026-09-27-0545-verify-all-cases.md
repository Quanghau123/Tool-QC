# QC — Sinh toàn bộ testcase VERIFY từ workbook Campaign

Ngày kiểm tra: 2026-09-28

## Kết quả

| Mục | Trạng thái | Bằng chứng |
|---|---|---|
| Q1–Q3 Parser/model | PASS | Phát hiện 69 ID duy nhất; framework assertions và oracle parity khớp campaign count, validity, sync direction. |
| Q4–Q6 Builder/identity | PASS | Schema v2, TimeFrame tương đối, Duration gap/group, reverse sync, output deterministic, valid/invalid DB evidence. |
| Q7 Batch partial failure | PASS | Batch test giữ workbook order, loại ID trùng, block missing riêng. |
| Q8 UI/CLI parity | PASS | CLI và Web batch cùng sinh 69/69, blocked 0. |
| Q9 Activation safety | PASS | Dùng flow preview/preflight/activation hiện có; output group tách riêng. |
| Q10 Full generation | PASS | Có 69 JSON tại `projects/be-booking/testcases/workbook-generated/verify/`. |
| Q11 Preflight | PASS | `Preflight thành công cho 69 testcase.` |
| Q12 Regression tests | PASS | `Framework tests passed: 57`. |
| Q13 Web smoke | PASS | Upload/analyze 69; batch generate 69; blocked 0; upload tạm được endpoint dọn trong `finally`. |
| Q14 Build | PASS | `dotnet build ApiAutoTest.sln`: 0 warning, 0 error. |
| Q15 Isolation | PASS | Open XML reader dùng chung; parser/builder VERIFY nằm trong adapter be-booking; backend không bị sửa. |
| Q16 Documentation | PASS | README, architecture và profile adapter mô tả version 2, 69 case và batch flow. |

## Lệnh chính

- `dotnet run --project tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj`
- `dotnet run --no-build --project runner/AutoTest.Runner/AutoTest.Runner.csproj -- --project be-booking --testcase-directory projects/be-booking/testcases/workbook-generated/verify --preflight-only`
- `dotnet build ApiAutoTest.sln`
- Web smoke trên `http://127.0.0.1:5081` với workbook thật.

## Ghi chú

- Analyze/generate không gửi request tới backend be-booking.
- Không sửa repository `D:/works/projects/mtc/be-booking` và không sửa testcase viết tay dưới `projects/be-booking/testcases/campaign/`.
- Log import chỉ ghi timestamp, tên file, fingerprint rút gọn, số sheet và thời lượng; không ghi nội dung cell hoặc secret.
