# QUICK — Testcase Campaign từ chối và copy dùng chung ngày
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

**Ngày:** 2026-10-15 · Brief: ../brief/2026-10-15-1040-campaign-date-test.md · Lane: quick
**Trạng thái:** HOÀN THÀNH
**Ước tính sẽ dùng skill:** api-test-orchestrator, api-test-contract-validation, api-test-auth-scope, api-test-data-integrity, api-test-lifecycle, api-test-reporting

## Phạm vi
- Theo hướng dẫn mới của người dùng, bám cấu trúc Case 54; giữ nguyên dữ liệu tái hiện 8 Campaign nhưng bỏ các nhánh phụ và chỉ VerifySlots một lần trước khi tạo bản sao.
- Trong: thêm một JSON schema v2 dưới `projects/be-booking/testcases/campaign`; tạo tuần tự 8 Campaign cùng ngày theo ảnh, từ chối Campaign 8 rồi tạo bản copy bằng POST mới; kiểm tra ngày và allocation lưu trong DB.
- Ngày kiểm thử lấy từ một biến `campaignDate` trong fixture DB; mọi request dùng lại biến này. `runEnd` được tính từ cùng biến để đáp ứng validator.
- Copy mô phỏng bằng `POST /api/Campaigns` vì controller hiện không có endpoint copy. Assert bản copy có tổng lượng và allocation mong đợi, gồm khác allocation Campaign 8 đã từ chối.
- Ngoài: sửa backend, kiểm thử UI, thay đổi testcase khác, xóa fixture sau chạy. Bỏ B0: report trước đã chạm đúng thư mục; bỏ B2: không có unknown bên thứ ba; bỏ scope round: chỉ thêm một case vào suite hiện hữu.

## Task
- [x] **T1** Tạo một case schema v2 cho chuỗi tạo, từ chối và copy; thêm manifest với source paths và evidence step names chính xác. — Test: `py -3 -m json.tool projects/be-booking/testcases/campaign/campaign-reject-copy-shared-date.json` và `dotnet run --project runner/AutoTest.Runner -- --project be-booking --preflight-only`.
  Chạm: `projects/be-booking/testcases/campaign/campaign-reject-copy-shared-date.json`
- [x] **T2** Rút gọn testcase theo Case 54; build, preflight, chạy riêng tag và kiểm tra HTML/JUnit report; giữ nguyên dữ liệu đã tạo. — Test: Release build và tag `campaign-reject-copy-shared-date`; build/preflight PASS, tag BLOCKED vì `localhost:5018` từ chối kết nối sau khi năm bước fixture PASS.
  Chạm: `projects/be-booking/testcases/campaign/campaign-reject-copy-shared-date.json`

## Definition of Done
- [x] `schemaVersion=2`, project là `be-booking`, manifest hợp lệ và cả 8 create requests dùng cùng `campaignDate`.
- [x] Preflight chấp nhận case; testcase có assertion API và DB cho trạng thái từ chối, tổng lượng và allocation của bản copy.
- [x] Runner build thành công và report của tag đã được kiểm tra; runtime được ghi `BLOCKED` vì API local từ chối kết nối.

