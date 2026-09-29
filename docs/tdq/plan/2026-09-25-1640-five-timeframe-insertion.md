# PLAN — Testcase chèn Campaign Duration vào chuỗi năm TimeFrame

Ngày: 2026-09-25 · Spec: ../spec/2026-09-25-1640-five-timeframe-insertion.md (bản 1.0, ĐÃ DUYỆT) · Lane: full
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md
Mode thực thi: main — thay đổi tập trung vào một testcase JSON và phụ thuộc tuần tự vào fixture/lifecycle trước khi chạy; triển khai inline ít overhead hơn tách agent (ĐỀ XUẤT, user chốt lúc duyệt)
Trạng thái plan: HOÀN TẤT PHẦN CÓ THỂ THỰC HIỆN; CHỜ MÔI TRƯỜNG ĐỂ RERUN

## Mục lục

- Quy tắc thi hành
- P1 — Chuẩn bị contract và fixture
- P2 — Tạo testcase năm TimeFrame
- P3 — Kiểm tra tĩnh và chạy thực tế
- P4 — Phân loại và báo cáo QC
- Cụm song song
- Log & test bắt buộc
- Definition of Done
- Ước tính phút
- Dòng Mode thực thi

## Quy tắc thi hành

1. Thứ tự phase là thứ tự phụ thuộc — không đảo.
2. Mỗi task được đánh `[~]` khi bắt đầu, viết kiểm tra trước, sửa, kiểm tra xanh rồi đổi `[x]` ngay trong plan.
3. Sau mỗi phase, chạy kiểm tra tương ứng; chỉ sang phase sau khi kiểm tra đạt.
4. Không sửa testcase gốc, backend Booking/MTC hoặc shared framework.
5. Không commit/push cho đến khi người dùng yêu cầu.
6. QC thất bại do testcase/fixture thì sửa và chạy lại; không đổi expectation chỉ để che lỗi backend.

## P1 — Chuẩn bị contract và fixture

- [x] **T1.1** (e8m) Đối chiếu lại testcase gốc, source `CampaignRequest`, `CampaignSlotAvailabilityService`, các planner reallocation và `SlotStateClassifier`; chốt các path save, field movement và điều kiện freeze-window một giờ — Test: bảng mapping trong working log/spec khớp source hiện tại.
  - Dùng: `api-test-orchestrator`, `api-test-contract-validation`, `api-test-data-integrity`, `api-test-lifecycle`
- [x] **T1.2** (e5m) Xác định query fixture chọn đúng năm TimeFrame liên tiếp tám phút, cùng Device/ngày, không trùng dữ liệu QC và giữ được mốc `tf1_start - 1 giờ` — Test: query có các điều kiện `LEAD` cho TF2–TF5 và trả đủ cột `tf1`–`tf5`/thời gian.
  - Dùng: `api-test-query`

**Xong P1 khi**: contract và dữ liệu cần save cho `tf1`–`tf5` được xác định, không còn assumption chưa kiểm chứng.

## P2 — Tạo testcase năm TimeFrame

- [x] **T2.1** (e20m) Tạo file testcase schema v2 mới bằng cách sao chép flow gốc; đổi ID/name/tags/variables; mở rộng bước chọn fixture từ ba lên năm TimeFrame và save `tf4`, `tf5`; đặt quota Campaign A thành `13,13,12,12,12` và B thành 15 spot tại TF2 — Test: JSON parse được, file gốc không thay đổi, body API có đúng năm frame và quota.
  - Chạm: `projects/be-booking/testcases/campaign/frozen-frame-five-timeframes-duration-insertion-flow.json` → file testcase mới, chưa có caller nào phụ thuộc.
  - Cần: T1.1, T1.2
  - Dùng: `api-test-orchestrator`, `api-test-contract-validation`
- [x] **T2.2** (e18m) Mở rộng các bước snapshot/lifecycle/integrity sau chèn để hiển thị và đối chiếu số lượng slot B chèn, số lượng slot A dời, quota, thứ tự, TF1 bất biến, movement chỉ ở TF2–TF5 và trạng thái A/B — Test: các bước PostgreSQL trả result-set diagnostic có row evidence; coverage evidence trỏ đúng tên bước.
  - Chạm: `projects/be-booking/testcases/campaign/frozen-frame-five-timeframes-duration-insertion-flow.json` → cùng file T2.1; runner chỉ đọc khai báo testcase.
  - Cần: T2.1
  - Dùng: `api-test-data-integrity`, `api-test-lifecycle`
- [x] **T2.3** (e8m) Hoàn thiện coverage schema v2 và source verification cho testcase mới — Test: mọi mục `contract`, `validation`, `authentication`, `scope`, `integrity`, `lifecycle`, `concurrency`, `reporting` có trạng thái hợp lệ và evidence step tồn tại.
  - Chạm: `projects/be-booking/testcases/campaign/frozen-frame-five-timeframes-duration-insertion-flow.json` → coverage của file mới.
  - Cần: T2.2
  - Dùng: `api-test-orchestrator`

**Xong P2 khi**: testcase mới độc lập, đủ coverage và không có diff ở testcase gốc.

## P3 — Kiểm tra tĩnh và chạy thực tế

- [x] **T3.1** (e5m) Parse testcase mới và chạy preflight đúng case ID — Test: `dotnet run --project runner/AutoTest.Runner -- --project be-booking --case-ids campaign.frozen-frame-five-timeframes-duration-insertion-flow --preflight-only` trả exit code 0.
  - Cần: T2.3
- [x] **T3.2** (e8m) Build runner Release và chạy unit/framework tests liên quan — Test: `dotnet build runner/AutoTest.Runner/AutoTest.Runner.csproj --configuration Release` không có error; test suite liên quan PASS hoặc được ghi rõ NOT_RUN.
  - Cần: T3.1
- [x] **T3.3** (e12m) Chạy đúng testcase destructive trên môi trường test được cấu hình, giữ request/response/DB/Redis evidence — Test: runner tạo JUnit/HTML mới; không bật production; output nêu đúng case ID và tag.
  - Cần: T3.2
  - Dùng: `api-test-reporting`

**Xong P3 khi**: đã có kết quả runtime thật hoặc blocker môi trường được chứng minh bằng report/evidence.

## P4 — Phân loại và báo cáo QC

- [x] **T4.1** (e12m) Đọc console, JUnit và HTML; nếu lỗi là testcase/fixture thì sửa đúng file mới và chạy lại; nếu là backend/environment thì dừng bước bị ảnh hưởng và giữ evidence — Test: mỗi failure có phân loại `TEST_SCRIPT_ERROR`, `TEST_DATA_ERROR`, `BACKEND_BUG` hoặc `ENVIRONMENT_BLOCKER` và nguyên nhân cụ thể.
  - Cần: T3.3
  - Dùng: `api-test-reporting`
- [x] **T4.2** (e8m) Ghi QC/report và cập nhật working log, đánh dấu DoD theo evidence thực tế — Test: report có totals PASS/FAIL/BLOCKED, path artifacts, file thay đổi và item chưa chạy.
  - Chạm: `docs/tdq/qc/2026-09-25-1640-five-timeframe-insertion.md`, `docs/tdq/report/2026-09-25-1640-five-timeframe-insertion.md`, `docs/workinglog/2026-09-25.md` → hồ sơ request và log; không có runtime caller.
  - Cần: T4.1

**Xong P4 khi**: QC/report phản ánh đúng bằng chứng và không còn task chưa xử lý.

## Cụm song song

- Cụm 1: T1.1 và T1.2 có thể khảo sát song song; T1.2 vẫn phải dùng kết quả source để chốt query trước khi ghi testcase.
- Cụm 2: T2.1 → T2.2 → T2.3 là chuỗi tuần tự vì cùng sửa một file mới.
- Cụm 3: T3.1 → T3.2 → T3.3 → T4.1 → T4.2 là chuỗi tuần tự vì mỗi bước đọc output bước trước.
- Chỉ dùng một chủ ghi cho file testcase mới; không tách hai task cùng sửa file.

## Log & test bắt buộc

- Log: BỎ — request chỉ tạo/sửa testcase JSON và tài liệu TDQ, không tạo hoặc sửa mã runtime.
- [x] **TX.1** (e4m) Kiểm tra JSON/testcase bằng parser và preflight — Test: lệnh T3.1 trả exit code 0.

## Definition of Done

- [ ] Q1 File mới parse được, ID/project/schema/destructive đúng — `ConvertFrom-Json` và lệnh T3.1.
- [ ] Q2 Coverage preflight đạt, evidence step hợp lệ — lệnh T3.1 trả exit code 0.
- [ ] Q3 Fixture chọn đủ 5 TF liên tiếp tám phút và mốc TF1 một giờ — report bước fixture có row evidence.
- [ ] Q4 Campaign A có quota `13/13/12/12/12`, tổng 62 — report bước snapshot A PASS.
- [ ] Q5 Freeze/lifecycle, dispatch history, Redis callback và Campaign Scheduled khớp — report các bước lifecycle PASS hoặc blocker có evidence.
- [ ] Q6 Campaign B Duration 15 spot tại TF2 — response và PostgreSQL result-set PASS.
- [ ] Q7 Có số lượng chèn/dời, thứ tự, TF1 bất biến, quota A=62, B=15, movement chỉ TF2–TF5 — report integrity PASS hoặc failure được phân loại.
- [ ] Q8 Runner build Release không có error — lệnh T3.2 PASS.
- [ ] Q9 HTML/JUnit runtime mới và report totals đầy đủ — artifact từ T3.3/T4.1.
- [ ] Q10 Testcase gốc/shared framework không bị sửa — `git diff --` chỉ chứa file mới và hồ sơ TDQ hợp lệ.

## Ước tính phút

Tổng eNm: 88 phút, không tính thời gian chờ người dùng duyệt plan hoặc thời gian backend retry đến khi TF1 freeze/callback.

## Dòng Mode thực thi

Mode đề xuất: `main` — một file testcase mới là vùng ghi duy nhất; các bước phụ thuộc chặt nên chạy tuần tự trong cùng context.




