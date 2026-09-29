# PLAN — Sinh toàn bộ testcase VERIFY từ workbook Campaign

Ngày: 2026-09-27 · Spec: ../spec/2026-09-27-0545-verify-all-cases.md (bản 1.0, ĐÃ DUYỆT) · Lane: full
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md
Mode thực thi: main — người dùng chuyển sang triển khai trực tiếp sau khi provider sub-agent không có credential hoạt động (ĐÃ CHỐT)
Trạng thái plan: CHỜ DUYỆT

## Mục lục

- Quy tắc thi hành
- P1 — Khóa oracle, model và parser VERIFY
- P2 — Sinh testcase schema v2 theo model
- P3 — Batch CLI, Web API và UI
- P4 — Output 69 case, log và tài liệu
- P5 — QC full và report
- Cụm song song
- Luật file nóng
- Log & test bắt buộc
- Definition of Done
- Ước tính phút
- Dòng Mode thực thi

## Quy tắc thi hành

1. Thứ tự phase là thứ tự phụ thuộc — không đảo.
2. Mỗi task đánh `[~]`, viết check đỏ, triển khai, chạy check xanh rồi đổi `[x]` ngay trong plan.
3. Sau mỗi phase chạy framework test harness; chỉ sang phase sau khi harness PASS.
4. Không sửa backend `D:/works/projects/mtc/be-booking`; extractor Python và regression script chỉ được đọc/chạy làm oracle QC.
5. Analyze/generate không gọi backend API, PostgreSQL, Redis hoặc Python runtime.
6. QC FAIL do Tool-QC thì thêm task fix vào plan và lặp tối đa ba vòng đến khi PASS.
7. Không commit hoặc push cho đến khi người dùng yêu cầu.

## P1 — Khóa oracle, model và parser VERIFY

- [x] **T1.1** (e18m) Tạo test harness workbook riêng và fixture oracle rút gọn để các test parser, builder, batch có entry point ổn định — Test: framework test executable chạy fixture mới và báo lỗi đỏ vì parser tổng quát chưa tồn tại.
  - Chạm: `tests/AutoTest.FrameworkTests/Program.cs`, `tests/AutoTest.FrameworkTests/WorkbookTests/WorkbookTestHarness.cs`, `tests/AutoTest.FrameworkTests/Fixtures/verify-oracle.json` → `Program.cs` là entry point test hiện có; fixture chỉ giữ model expected, không copy workbook.
  - Dùng: `code-testing-agent`
  - Để: tổ chức unit assertions theo component trong custom executable test harness hiện có, không tạo test framework mới.
  - Ra: workbook test harness và fixture oracle có thể chạy độc lập trong full suite.
  - Kiểm: executable test project chạy tới assertion đỏ đã định danh.
  - Không dùng cho: chạy full suite hoặc sửa production parser.
- [x] **T1.2** (e24m) Viết unit test parser cho boundary, merged cells, hai bảng ngày, Spot/Duration, range/group, order, reverse sync, valid/invalid và stable ID — Test: các test parser đỏ trước implementation và mô tả field sai theo case/range.
  - Chạm: `tests/AutoTest.FrameworkTests/WorkbookTests/VerifyWorkbookParserTests.cs` → file test mới cho parser.
  - Cần: T1.1
  - Dùng: `spreadsheets`
  - Để: chọn fixture đại diện từ workbook thật và xác nhận tọa độ/merged range/công thức trước khi viết assertion.
  - Ra: bộ assertion parser cho các topology workbook đang có.
  - Kiểm: test report nêu đúng assertion đỏ do parser chưa hỗ trợ.
  - Không dùng cho: thiết kế request API hoặc builder schema v2.
- [x] **T1.3** (e45m) Thêm typed intermediate model và port parser C# từ thuật toán extractor để parse đủ 69 block `VERIFY` hoặc block riêng với diagnostic — Test: toàn bộ parser tests xanh và analysis trả 69 stable ID duy nhất.
  - Chạm: `src/AutoTest.ProjectAdapters.BeBooking/VerifyCaseModel.cs`, `src/AutoTest.ProjectAdapters.BeBooking/VerifyWorkbookParser.cs`, `src/AutoTest.ProjectAdapters.BeBooking/VerifyWorkbookAdapter.cs` → adapter entry symbol được `WorkbookAdapterRegistry` và `WorkbookImportService` gọi.
  - Cần: T1.2

**Xong P1 khi**: parser tests PASS, workbook hiện tại phát hiện đúng 69 ID và không còn hard-code supported chỉ `verify/54`.

## P2 — Sinh testcase schema v2 theo model

- [x] **T2.1** (e28m) Viết coverage matrix và unit test builder cho Spot, Duration, consecutive/gap/multi-range, hai ngày, create order, reverse sync, valid/invalid, Vietnamese names và no-partial-state evidence — Test: builder tests đỏ với output hiện tại đang copy Case 54.
  - Chạm: `tests/AutoTest.FrameworkTests/WorkbookTests/VerifyTestcaseBuilderTests.cs`, `tests/AutoTest.FrameworkTests/Fixtures/verify-coverage-matrix.json` → file test và matrix mới cho generated testcase.
  - Cần: T1.3
  - Dùng: `api-test-orchestrator`
  - Để: khóa schema-v2 coverage, contract/auth/scope/integrity/lifecycle/concurrency/reporting và DB evidence cho case rejected.
  - Ra: coverage matrix cùng assertions theo exact step name của generated suite.
  - Kiểm: builder tests đỏ đúng các contract chưa được sinh.
  - Không dùng cho: gọi backend hoặc thay business expectation theo response thực tế.
- [x] **T2.2** (e55m) Triển khai builder tổng quát chuyển typed model thành một schema-v2 JSON/case với TimeFrame động và deterministic ordering — Test: builder tests xanh, JSON parse được và không chứa giờ 06:00 hoặc ngày cố định 2026-09-27.
  - Chạm: `src/AutoTest.ProjectAdapters.BeBooking/VerifyTestcaseBuilder.cs`, `src/AutoTest.ProjectAdapters.BeBooking/VerifyWorkbookAdapter.cs` → adapter bỏ đọc template Case 54 và gọi builder.
  - Cần: T2.1
- [x] **T2.3** (e18m) Thêm stable output identity, source metadata và file-name mapping cho từng `verify/<number>` — Test: generate lặp lại cùng case tạo byte-identical JSON, cùng ID và cùng file name.
  - Chạm: `src/AutoTest.ProjectAdapters.BeBooking/VerifyOutputIdentity.cs`, `src/AutoTest.ProjectAdapters.BeBooking/VerifyTestcaseBuilder.cs`, `tests/AutoTest.FrameworkTests/WorkbookTests/VerifyTestcaseBuilderTests.cs` → identity helper mới và regression deterministic.
  - Cần: T2.2

**Xong P2 khi**: test builder PASS cho valid/invalid topology, Case 54 vẫn giữ logic đã xác minh và adapter không đọc testcase viết tay.

## P3 — Batch CLI, Web API và UI

- [x] **T3.1** (e22m) Viết unit test batch result cho nhiều case, select-all ordering, blocked riêng, duplicate selection và output naming — Test: batch tests đỏ với service hiện chỉ nhận một `caseId`.
  - Chạm: `tests/AutoTest.FrameworkTests/WorkbookTests/WorkbookBatchTests.cs` → file test mới cho contract và orchestration batch.
  - Cần: T2.3
- [x] **T3.2** (e34m) Mở rộng workbook contract và import service để generate nhiều case, trả result từng case và giữ output hợp lệ khi case khác blocked — Test: batch tests xanh với counts `generated/blocked/conflict` và workbook order ổn định.
  - Chạm: `src/AutoTest.Workbooks/Contracts.cs`, `runner/AutoTest.Generator/WorkbookImport/WorkbookImportService.cs`, `runner/AutoTest.Generator/Program.cs` → contract shared, application service và CLI entry point đang được Web gọi.
  - Cần: T3.1
- [x] **T3.3** (e25m) Đổi Web workbook endpoint sang batch response, giữ upload lifecycle/cancellation và preview metadata từng file — Test: endpoint smoke nhận nhiều case IDs, trả result từng case và dọn temp directory.
  - Chạm: `runner/AutoTest.Web/Program.cs` → workbook generate endpoint và request/response records.
  - Cần: T3.2
- [x] **T3.4** (e30m) Cập nhật tab `Nhập XLSX` để multi-select, chọn tất cả, bỏ chọn, hiển thị generated/blocked/conflict từng case và chuyển từng draft sang flow preflight/activation — Test: browser smoke chọn 69 case và render đủ 69 result không yêu cầu chọn đúng một case.
  - Chạm: `runner/AutoTest.Web/wwwroot/index.html`, `runner/AutoTest.Web/wwwroot/app.js`, `runner/AutoTest.Web/wwwroot/app.css` → workbook UI và asset version/cache liên quan.
  - Cần: T3.3

**Xong P3 khi**: CLI và Web dùng cùng batch service, select-all trả cùng ordering và một case blocked không làm mất JSON case khác.

## P4 — Output 69 case, log và tài liệu

- [x] **T4.1** (e32m) Sinh và activate bộ 69 JSON từ workbook hiện tại vào output group riêng, giữ một file mỗi case và profile adapter version mới — Test: folder có đúng 69 JSON, ID duy nhất và không sửa testcase dưới `testcases/campaign/`.
  - Chạm: `projects/be-booking/testcases/workbook-generated/verify/`, `projects/be-booking/workbook-import/workbook-import.json` → output generated và profile project-specific.
  - Cần: T3.4
- [x] **T4.2** (e16m) Cập nhật README và hồ sơ kiến trúc với 69-case support, workbook source-of-truth, Python oracle, batch UI/CLI, output và blocked semantics — Test: doc lint PASS và tài liệu không còn tuyên bố chỉ Case 54 được sinh.
  - Cần: T4.1
- [x] **T4.3** (e14m) Hoàn thiện asset/version metadata để browser không giữ UI single-select cũ — Test: HTML tham chiếu asset version mới và browser tải JavaScript có select-all batch flow.
  - Chạm: `runner/AutoTest.Web/wwwroot/index.html` → một chủ ghi asset version sau khi UI hoàn tất.
  - Cần: T3.4

**Xong P4 khi**: output có 69 file riêng, profile/tài liệu đúng release mới và UI tải asset batch mới.

## P5 — QC full và report

- [x] **T5.1** (e24m) Chạy parser parity giữa C# và extractor oracle cho đủ 69 case, xuất field-level diff khi lệch và sửa Tool-QC trong phạm vi feature — Test: parity report có 69 PASS, 0 mismatch.
  - Cần: T4.1
- [x] **T5.2** (e18m) Chạy schema-v2 preflight toàn bộ 69 generated JSON và sửa generator/testcase khi validator phát hiện lỗi — Test: preflight báo 69 testcase thành công, 0 issue và không mở request backend.
  - Cần: T5.1
- [x] **T5.3** (e16m) Chạy Web batch smoke cho upload, analyze, select-all, generate, preview, conflict guard và temp cleanup — Test: smoke PASS cho toàn flow và process log không chứa workbook cells hoặc secret.
  - Cần: T5.2
- [x] **T5.4** (e22m) Chạy custom framework test harness, full solution build và kiểm tra regression single-case/batch — Test: test executable exit 0 và solution build 0 error.
  - Cần: T5.3
  - Dùng: `run-tests`
  - Để: nhận diện test project là executable harness trên SDK 8, chạy đúng command và không dùng sai VSTest/MTP flags.
  - Ra: log test harness và build cuối cùng cho QC report.
  - Kiểm: cả test command và build command exit 0.
  - Không dùng cho: sinh unit test hoặc thay assertion đang fail.
- [x] **T5.5** (e12m) Ghi QC report, working log và đối chiếu git diff không chạm backend hoặc testcase Campaign viết tay — Test: report có PASS/FAIL/BLOCKED cho Q1–Q16 và liệt kê evidence command/result.
  - Cần: T5.4

**Xong P5 khi**: parity, 69-file preflight, Web smoke, framework tests và solution build đều PASS; report đủ Q1–Q16.

## Cụm song song

- T1.1 phải xong trước T1.2; T1.3 là critical path vì mọi task sau đọc typed model.
- Sau T1.3, T2.1/T2.2/T2.3 chạy tuần tự vì cùng khóa builder contract và file test.
- Sau T3.2, T3.3 là API contract; T3.4 chỉ bắt đầu khi response shape ổn định.
- Sau T3.4, T4.2 có thể viết song song với T4.1 vì chỉ đọc contract đã chốt; T4.3 chờ UI code cuối để bump asset một lần.
- T5.1–T5.5 chạy tuần tự vì mỗi bước dùng artifact/evidence của bước trước.

## Luật file nóng

- `src/AutoTest.ProjectAdapters.BeBooking/VerifyWorkbookAdapter.cs` chỉ T1.3 và T2.2 sửa, theo thứ tự phụ thuộc.
- `tests/AutoTest.FrameworkTests/Program.cs` chỉ T1.1 sửa; test mới nằm trong file riêng theo component.
- `runner/AutoTest.Generator/Program.cs` chỉ T3.2 sửa.
- `runner/AutoTest.Web/Program.cs` chỉ T3.3 sửa.
- `runner/AutoTest.Web/wwwroot/app.js` chỉ T3.4 sửa; `index.html` do T3.4 sửa UI và T4.3 chỉ bump asset version sau cùng.
- `README.md` và `docs/kien-truc.md` chỉ T4.2 sửa.

## Log & test bắt buộc

- [x] **TX.1** (e14m) Bổ sung structured import log bật mặc định với timestamp, fingerprint rút gọn, adapter, case counts, generated/blocked/conflict counts, duration và diagnostic code; giảm được theo log level — Test: CLI/Web smoke có metadata bắt buộc và không có workbook cell content.
  - Chạm: `src/AutoTest.Workbooks/WorkbookImportLogger.cs`, `runner/AutoTest.Generator/WorkbookImport/WorkbookImportService.cs` → logger shared-neutral và application call sites.
  - Cần: T3.2
- [x] **TX.2** (e16m) Hoàn thiện unit coverage cho parser, builder, identity và batch partial failure trong framework harness — Test: test executable PASS toàn bộ assertion mới bằng một command.
  - Chạm: `tests/AutoTest.FrameworkTests/WorkbookTests/VerifyWorkbookParserTests.cs`, `tests/AutoTest.FrameworkTests/WorkbookTests/VerifyTestcaseBuilderTests.cs`, `tests/AutoTest.FrameworkTests/WorkbookTests/WorkbookBatchTests.cs` → đóng gap assertion sau implementation.
  - Cần: T3.2

## Definition of Done

- [ ] Q1 Parser discovery đúng 69 stable ID và source range, không trùng — chạy workbook analysis assertion.
- [ ] Q2 Parser parity đủ 69 case, 0 field mismatch với oracle — chạy parity verifier.
- [ ] Q3 Typed model phủ consecutive, gap, multi-range, hai ngày, reverse sync, valid và invalid — chạy parser unit group.
- [ ] Q4 Builder giữ Spot/Duration semantics, TimeFrame động, tên tiếng Việt và coverage — chạy builder unit group.
- [ ] Q5 Mọi invalid mutation case có database evidence không lưu partial state — chạy generated JSON integrity scan.
- [ ] Q6 Generate lặp lại giữ ID, file name, JSON ordering và fingerprint — chạy deterministic generation comparison.
- [ ] Q7 Batch partial failure giữ case hợp lệ và trả counts/diagnostics đúng — chạy batch unit group.
- [ ] Q8 Web/CLI trả cùng ordering, status và draft identity — chạy batch parity smoke.
- [ ] Q9 Preview create/unchanged/update/conflict và overwrite guard đúng — chạy activation safety smoke.
- [ ] Q10 Workbook hiện tại sinh đúng 69 JSON không gọi backend/Python runtime — chạy full workbook generation smoke.
- [ ] Q11 Cả 69 JSON qua schema-v2 preflight và ID không trùng — chạy runner preflight-only trên output group.
- [ ] Q12 Unit assertions mới và full framework harness PASS — chạy test executable.
- [ ] Q13 Web upload/analyze/select-all/generate/preview dọn temp và render per-case — chạy Web batch smoke.
- [ ] Q14 Solution build 0 error — chạy build toàn solution.
- [ ] Q15 Shared source không chứa Campaign/VERIFY business mapping hoặc branch project — chạy architecture source scan.
- [ ] Q16 README và hồ sơ kiến trúc mô tả đúng release 69 case — chạy doc lint và content assertions.

## Ước tính phút

Tổng eNm: 455 phút, không tính thời gian người dùng duyệt hoặc thời gian backend bên ngoài; runtime analyze/generate không mở request backend.

## Dòng Mode thực thi

Mode đề xuất: `subagent` — `tdq_bench simulate` đo 20 task, 11 wave; đội 33,7 phút so với main 40,7 phút, nhanh hơn 7,0 phút ở hệ số agent 1,5. Có 14 task tách được và 6 task leader giữ; không có task `(mcp)`.




