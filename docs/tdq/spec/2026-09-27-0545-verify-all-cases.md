# SPEC — Sinh toàn bộ testcase VERIFY từ workbook Campaign

Ngày: 2026-09-27 · Bản: 1.0 · Brief: ../brief/2026-09-27-0545-verify-all-cases.md · Lane: full
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md
Trạng thái: CHỜ DUYỆT

## Mục lục

- 1. Mục tiêu & phạm vi
- 1b. Lộ trình
- 2. Đầu ra cụ thể
- 2b. Ranh giới module
- 3. Cách tiếp cận & lý do
- 3b. Năng lực & công cụ
- 4. Yêu cầu bắt buộc
- 5. Ràng buộc & rủi ro
- 6. QC & Definition of Done
- 7. Câu hỏi còn mở

## 1. Mục tiêu & phạm vi

- Mục tiêu: mở rộng pipeline nhập XLSX hiện có để đọc và sinh testcase schema v2 cho toàn bộ 69 case đang được phát hiện trong family `VERIFY` của workbook Campaign. Người dùng có thể chọn nhiều case hoặc chọn tất cả, nhận một JSON riêng cho mỗi case, còn case không thể ánh xạ sẽ bị chặn riêng với diagnostic cụ thể mà không làm hỏng cả batch.
- Trong phạm vi:
  - Workbook upload là nguồn chuẩn cho title, campaign, loại `Spot` hoặc `Duration`, quota theo TimeFrame, duration range/group, thứ tự tạo, chiều đồng bộ, expected validity và ghi chú tester.
  - Port thuật toán đọc `VERIFY` đã được kiểm chứng từ extractor Python sang C# trong adapter `be-booking`; Python chỉ là oracle QC, không được gọi khi Tool-QC chạy analyze/generate.
  - Tạo model trung gian có kiểu dữ liệu rõ ràng cho case, ngày, campaign, frame, range, operation, expectation và diagnostic trước khi sinh JSON.
  - Hỗ trợ đúng 69 stable ID hiện được phát hiện: Case 1–30, 33–57, 61–66 và 68–75; workbook thay đổi làm case mới hoặc layout lạ phải được phát hiện và chặn riêng, không tự suy đoán.
  - Sinh một testcase schema v2 độc lập cho mỗi case, có coverage manifest, tên case và step bằng tiếng Việt, metadata truy vết workbook, stable output name và folder `projects/be-booking/testcases/workbook-generated/verify/`.
  - Fixture runtime ánh xạ TimeFrame theo vị trí tương đối và thời điểm chạy hiện tại; không cố định mốc 06:00 hoặc ngày cụ thể trong workbook.
  - Case expected invalid phải xác minh response từ chối và có evidence database chứng minh không lưu trạng thái một phần.
  - UI tab `Nhập XLSX` hỗ trợ chọn nhiều case, chọn tất cả, hiển thị kết quả generate/blocked theo từng case và preview trạng thái từng output file trước activation.
  - CLI và Web dùng chung một batch application service, cùng trả kết quả theo từng case và không mở request tới backend trong analyze/generate.
  - Regeneration giữ cơ chế create/unchanged/update/conflict, SHA fingerprint và xác nhận overwrite đã có.
  - Bổ sung unit test cho parser, model, builder, batch partial failure và output identity; chạy trọn test suite, build solution, parity 69 case, preflight 69 JSON và Web batch smoke.
- NGOÀI phạm vi:
  - Không hỗ trợ sheet hoặc family ngoài `VERIFY` trong request này.
  - Không sửa repository backend `D:/works/projects/mtc/be-booking`; chỉ đọc extractor, regression oracle và source contract để đối chiếu.
  - Không gọi Python, script backend, API backend, PostgreSQL, Redis hoặc service ngoài trong runtime analyze/generate.
  - Không coi file `verify-v2.1.cases.json` 53 case là nguồn chuẩn vì artifact này thiếu 16 case hiện có trong workbook.
  - Không copy workbook nguồn vào repository và không ghi nội dung cell đầy đủ vào log.
  - Không tự chạy testcase destructive sau khi generate; phạm vi runtime chỉ đến analyze, generate, preview, activation và preflight.

## 1b. Lộ trình

Người dùng duyệt spec là duyệt luôn lộ trình này.

| Bước/phase | Chạy? | Vì sao |
|---|---|---|
| Phân tích code và workbook | CÓ | Đã xác định 69 block, extractor oracle, adapter hard-code Case 54 và giới hạn batch hiện tại. |
| Research web | BỎ | Open XML SDK và pipeline XLSX đã được chọn, cài và kiểm chứng ở request trước; không có quyết định thư viện mới. |
| Interview | CÓ | Đã chốt batch một JSON mỗi case, block riêng từng case, workbook là nguồn chuẩn và QC full. |
| Spec | CÓ | Cần khóa contract parser, module ownership, batch behavior và DoD trước khi thay adapter sinh testcase destructive. |
| Plan | CÓ | Cần chia parser, builder, batch Web/CLI, tests và QC thành các task có check riêng. |
| Implement | CÓ | Sửa adapter, generator, Web UI, tests và tài liệu trong Tool-QC. |
| QC độc lập | CÓ | QC full gồm review output, parity oracle, unit/full tests, build, preflight và smoke; lỗi thuộc Tool-QC được sửa và chạy lại đến khi xanh. |
| Report | CÓ | Ghi file thay đổi, coverage 69 case, kết quả QC và blocker môi trường nếu có. |

## 2. Đầu ra cụ thể

| # | Đầu ra | Đường dẫn/vị trí | Đo "xong" bằng |
|---|---|---|---|
| 1 | Typed parser và model `VERIFY` của `be-booking` | `src/AutoTest.ProjectAdapters.BeBooking/` | Workbook hiện tại parse đủ 69 stable ID; mỗi case có campaign/range/expectation hoặc diagnostic chặn có tọa độ nguồn. |
| 2 | Schema-v2 testcase builder tổng quát | `src/AutoTest.ProjectAdapters.BeBooking/` | Sinh đúng Spot/Duration, group/range, create/verify flow, coverage và DB evidence theo model; không đọc template Case 54. |
| 3 | Batch generation contract và orchestration dùng chung | `src/AutoTest.Workbooks/`, `runner/AutoTest.Generator/WorkbookImport/`, `runner/AutoTest.Generator/Program.cs` | Nhận nhiều case ID, trả một result cho từng case, giữ case hợp lệ khi case khác bị blocked và đặt output name ổn định. |
| 4 | Web batch upload/generate/preview | `runner/AutoTest.Web/Program.cs`, `runner/AutoTest.Web/wwwroot/` | UI chọn nhiều hoặc tất cả 69 case, hiển thị generated/blocked/conflict theo từng case và activation không ghi đè im lặng. |
| 5 | Bộ testcase generated từ workbook | `projects/be-booking/testcases/workbook-generated/verify/` | Có một JSON cho mỗi case được parse hợp lệ; batch hiện tại tạo đủ 69 file và tất cả qua schema-v2 preflight. |
| 6 | Unit và regression coverage cho tính năng workbook | `tests/AutoTest.FrameworkTests/` | Có test parser parity fixture, range/group, stable ID, valid/invalid mapping, partial failure và output naming; trọn suite không regression. |
| 7 | Tài liệu kiến trúc và hướng dẫn sử dụng | `README.md`, `docs/kien-truc.md` | Tài liệu bỏ giới hạn chỉ Case 54, mô tả source-of-truth, batch UI/CLI, oracle QC, output và behavior blocked. |

## 2b. Ranh giới module

LSP C# đã khởi động nhưng không trả symbol cho project hiện tại; ranh giới được đối chiếu bằng interface `IWorkbookProjectAdapter`, implementation `VerifyWorkbookAdapter`, composition `WorkbookAdapterRegistry` và call flow `WorkbookImportService` đang được CLI/Web sử dụng.

| Module | Vùng file | Phụ thuộc module | Đầu ra §2 nào |
|---|---|---|---|
| Workbook contract | `src/AutoTest.Workbooks/` | Không phụ thuộc business `be-booking` | 3 |
| Be-booking VERIFY adapter | `src/AutoTest.ProjectAdapters.BeBooking/` | Workbook contract và schema-v2 model hiện có | 1, 2 |
| Import application và CLI | `runner/AutoTest.Generator/WorkbookImport/`, `runner/AutoTest.Generator/Program.cs` | Workbook contract, adapter registry và generator services | 3 |
| Web batch surface | `runner/AutoTest.Web/Program.cs`, `runner/AutoTest.Web/wwwroot/` | Import application và activation flow hiện có | 4 |
| Generated project suites | `projects/be-booking/testcases/workbook-generated/verify/` | Output của adapter; runner schema v2 | 5 |
| Framework verification | `tests/AutoTest.FrameworkTests/` | Workbook contract, adapter và import application | 6 |
| Public documentation | `README.md`, `docs/kien-truc.md` | Contract đã triển khai ở các module trên | 7 |

## 3. Cách tiếp cận & lý do

- Chọn port thuật toán `extract_verify_v21.py` sang C# theo hai bước: parse worksheet block thành typed intermediate model, sau đó builder chuyển model thành schema-v2 testcase. Cách này tách lỗi đọc workbook khỏi lỗi sinh testcase và tránh 69 template thủ công.
- Parser dùng title row làm boundary, hai bảng ngày nằm cạnh nhau, merged-anchor propagation và cell coordinates để đọc campaign, `Spot`/`Duration`, Requested/Maximum/Allocation, range/group, creation order, reverse sync, expected validity cùng notes. Giá trị thiếu hoặc mâu thuẫn tạo diagnostic riêng cho case thay vì suy đoán.
- Workbook upload là nguồn chuẩn. Extractor Python chạy ngoài runtime trên cùng workbook để tạo oracle parity; file JSON 53 case chỉ được dùng tham khảo lịch sử vì đã cũ.
- Stable identity tiếp tục là `verify/<case-number>`; output file dùng case number và slug xác định từ title, không phụ thuộc thứ tự người dùng chọn. Batch result giữ thứ tự workbook để UI và CLI đối chiếu dễ dàng.
- Builder dùng topology campaign trong model để tạo fixture và steps theo dữ liệu thay vì copy Case 54. Absolute workbook times được chuẩn hóa thành frame index, độ dài và khoảng cách; runtime chọn TimeFrame tương lai dựa trên giờ hiện tại nhưng giữ nguyên quan hệ liên tiếp/rời nhau của case.
- Với case valid, testcase kiểm tra Verify/Create và allocation theo expected. Với case invalid, testcase kiểm tra lý do từ chối và snapshot database trước/sau để chứng minh không có partial persistence.
- Batch orchestration xử lý từng case độc lập và gom `generated`, `blocked`, `unchanged`, `update`, `conflict`; lỗi một case không throw làm mất kết quả các case còn lại. Lỗi đọc workbook hoặc adapter không tồn tại vẫn fail toàn request vì không thể xác định case boundary.
- Đã loại gọi Python trong runtime vì tạo dependency môi trường ngoài .NET, khó deploy và phá boundary Tool-QC. Đã loại sinh 69 template tĩnh vì dễ lệch workbook và không mở rộng khi QC chỉnh dữ liệu. Đã loại dùng canonical JSON 53 case làm nguồn chuẩn vì thiếu Case 56, 57, 61–66 và 68–75.

## 3b. Năng lực & công cụ

| Skill | Nguồn | Phán quyết | Dùng ở đâu / Lý do loại |
|---|---|---|---|
| `tdq-intake`, `tdq-spec`, `tdq-plan`, `tdq-build` | plugin:tdq-workflow | NỀN | Điều phối analysis, approval, implementation, QC và report. |
| `spreadsheets` | built-in | DÙNG | Kiểm tra workbook, merged ranges, formulas và parity 69 case. |
| `api-test-orchestrator` | project | DÙNG | Bảo đảm testcase schema v2, coverage manifest, DB evidence và quy tắc giữ fixture. |
| `code-testing-agent` | user | DÙNG | Thiết kế unit coverage cho parser, builder và batch application behavior. |
| `run-tests` | user | DÙNG | Chọn đúng lệnh .NET test và chạy trọn suite trong QC. |
| Đã xét 75 skill khác | user/plugin/built-in | KHÔNG | khác lĩnh vực |

## 4. Yêu cầu bắt buộc

- Log service bật mặc định: timestamp, workbook fingerprint rút gọn, adapter, sheet/case count, generated/blocked/conflict count, parse/generate duration và mã diagnostic; không log nội dung workbook, request body testcase hoặc secret; giảm được bằng log level.
- Không placeholder, không TODO stub, không mock trình bày như dữ liệu thật.
- Mỗi thành phần parser, model mapping, builder và batch orchestration có unit test riêng; chạy được trong test project hiện có.
- Analyze/generate không gọi API `be-booking`, database, Redis, Python hoặc backend script.
- Mỗi generated suite dùng `schemaVersion: 2`; mỗi case có coverage manifest với source files đã kiểm tra và evidence step có thật.
- Tên testcase và step bằng tiếng Việt; identifiers, code, commands và paths bằng tiếng Anh.
- Case mutation bị từ chối phải có database evidence xác nhận không tạo Campaign, schedule, allocation hoặc record liên quan một phần.
- Fixture được giữ sau khi chạy testcase; generated suite không thêm cleanup xóa dữ liệu.
- TimeFrame và schedule date được chọn động từ runtime; không hard-code 06:00 hoặc ngày 27–28/09/2026.
- Stable ID, output name và activation path phải deterministic giữa các lần generate cùng workbook.
- Code bám SOLID; shared modules không chứa token Campaign/VERIFY hoặc branch theo project name; business workbook mapping chỉ nằm trong adapter `be-booking`.

## 5. Ràng buộc & rủi ro

Ràng buộc kiến trúc phải giữ từ `docs/kien-truc.md`:
- `AutoTest.Workbooks` chỉ kiểm tra/đọc Open XML và model trung lập; việc này chỉ mở rộng contract batch, không đưa business Campaign vào shared core.
- `AutoTest.ProjectAdapters.BeBooking` sở hữu cấu trúc workbook và business mapping; parser/model/builder `VERIFY` nằm tại đây.
- `AutoTest.Generator` là pipeline analyze/generate dùng chung cho CLI và Web; output là schema-v2 draft và chưa gọi backend.
- Workbook gốc chỉ là input tạm, không copy vào repository; metadata/fingerprint được dùng để trace và temp upload được dọn ở mọi nhánh.
- Activated testcase nằm tại `projects/be-booking/testcases/workbook-generated/verify/`; không trộn với testcase Campaign viết tay.
- Overwrite chỉ xảy ra sau preview/xác nhận và SHA-256 đích còn khớp; preflight phải qua trước activation.
- Các dòng `Đã chốt` giới hạn Case 54 và bỏ unit test là lịch sử release 1, phải được bổ sung quyết định mới thay vì xóa lịch sử.

| Rủi ro | Ảnh hưởng | Cách giảm |
|---|---|---|
| Layout 69 block có merge hoặc ô trống khác nhau | Parser gán sai campaign/range | Typed parser theo boundary và coordinates; fixture parity đại diện; block case khi thiếu dữ liệu bắt buộc. |
| Oracle Python và C# hiểu khác formula/cached value | Sinh expectation sai | Đối chiếu toàn bộ 69 case theo model canonical trong QC và ghi diff theo case/field. |
| Builder tổng quát làm mất rule riêng Spot/Duration | Testcase pass sai hoặc fail giả | Model tách loại campaign, duration groups và allocation; golden regression Case 54 cùng các topology valid/invalid. |
| TimeFrame động không giữ topology workbook | Allocation khác expected | Chuẩn hóa index, độ dài và khoảng cách; unit test consecutive, gap và multi-range; preflight toàn output. |
| Batch lớn che lỗi từng case | Người dùng tưởng đủ 69 dù có case lỗi | Result và UI có tổng số cùng trạng thái từng case; activation chỉ nhận file preflight PASS; blocked luôn có code/range/reason. |
| Invalid testcase tạo partial data | Dữ liệu test sai và khó điều tra | Snapshot DB trước/sau bằng evidence steps; không cleanup fixture; expectation không đổi để khớp backend. |
| Thay contract ảnh hưởng Case 54 hoặc flow single-select | Regression tính năng cũ | Giữ single selection như batch một phần tử; full test suite và Case 54 golden comparison. |
| Generated output quá lớn hoặc thay đổi không ổn định | Diff khó review, conflict liên tục | Serializer deterministic, stable ordering và snapshot kiểm tra generate lặp lại cho cùng fingerprint. |

Không cần model AI, dịch vụ có phí hoặc NuGet mới. Tiếp tục dùng `DocumentFormat.OpenXml` 3.5.1 đã có trong solution.

## 6. QC & Definition of Done

Mức QC: `full` theo lựa chọn người dùng.

| # | Hạng mục kiểm | Điều kiện PASS |
|---|---|---|
| Q1 | Parser discovery | Workbook hiện tại tạo đúng 69 stable ID và đúng source range; không có ID trùng. |
| Q2 | Parser parity | Model C# và oracle extractor khớp case number, campaign count/type, requested allocation, range/group, creation order, sync direction và expected validity cho cả 69 case. |
| Q3 | Typed model unit coverage | Các topology consecutive, gap, multi-range, hai ngày, reverse sync, valid và invalid đều có assertion theo field. |
| Q4 | Builder coverage | Output bảo toàn Spot/Duration semantics, expectation, dynamic TimeFrame mapping, Vietnamese names và schema-v2 coverage. |
| Q5 | Invalid integrity | Mọi generated invalid mutation case có evidence step xác minh không lưu partial database state. |
| Q6 | Stable identity | Generate lặp lại cùng workbook và selection cho cùng ID, tên file, thứ tự JSON và fingerprint metadata. |
| Q7 | Batch partial failure | Một case blocked không ngăn case hợp lệ khác được generate; result counts và diagnostics đúng theo từng case. |
| Q8 | UI/CLI parity | Multi-select và select-all qua Web/CLI trả cùng case ordering, trạng thái và draft content identity. |
| Q9 | Activation safety | Preview phân biệt create/unchanged/update/conflict; conflict không overwrite khi chưa xác nhận; chỉ file preflight PASS được activate. |
| Q10 | Generate full workbook | Batch workbook hiện tại sinh đủ 69 JSON riêng dưới output group, không gọi backend và không cần Python runtime. |
| Q11 | Schema-v2 preflight | Toàn bộ 69 JSON qua preflight, coverage manifest tham chiếu step tồn tại và project/ID không trùng. |
| Q12 | Unit và full regression | Unit tests mới cùng toàn bộ test suite hiện có đều PASS, không bỏ qua test lỗi. |
| Q13 | Web smoke | Upload, analyze, select-all, generate và preview batch hoàn tất; blocked diagnostic hiển thị riêng; temp file được dọn. |
| Q14 | Build | Toàn solution build thành công với 0 error; lỗi compile/analyzer thuộc phạm vi thay đổi được sửa và build lại. |
| Q15 | Architecture isolation | Shared workbook/generator không chứa business token hoặc branch `be-booking`; adapter không phụ thuộc Web. |
| Q16 | Documentation | README và hồ sơ kiến trúc mô tả đúng 69-case support, source-of-truth, batch flow, output, oracle và giới hạn family. |

DoD:
- Bảy đầu ra ở §2 tồn tại và mỗi đầu ra có ít nhất một mục QC PASS.
- Workbook `Campaign (1).xlsx` hiện tại được analyze thành 69 case và sinh một JSON schema v2 cho từng case.
- Không case nào còn blocked bằng thông báo adapter version 1; case thực sự mơ hồ chỉ block riêng với diagnostic có sheet, range, code và reason.
- Parser C# đạt parity 69 case với extractor oracle; runtime Tool-QC không gọi Python hoặc backend.
- Toàn bộ 69 output qua preflight; valid/invalid expectations, dynamic TimeFrame và DB integrity evidence được bảo toàn.
- Multi-select/select-all hoạt động trên UI và CLI; một case lỗi không làm mất output case hợp lệ.
- Unit tests, full suite, solution build và Web batch smoke PASS; agent tự sửa lỗi Tool-QC và chạy lại đến khi xanh.
- Working log, QC report, README và `docs/kien-truc.md` phản ánh đúng implementation và kết quả kiểm chứng.

## 7. Câu hỏi còn mở

(Không còn câu hỏi.)
