# SPEC — Nhập XLSX và sinh testcase theo adapter dự án

Ngày: 2026-09-27 · Bản: 1.1 · Brief: ../brief/2026-09-27-0705-xlsx-testcase-import.md · Lane: full
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md
Trạng thái: ĐÃ DUYỆT

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

- Mục tiêu: thêm vào Tool-QC một pipeline dùng chung để nạp `.xlsx`, phân tích workbook bằng adapter của project, chọn case và sinh testcase schema v2. Release đầu hỗ trợ family `VERIFY` của workbook Campaign cho `be-booking`, bao gồm Case 54 với TimeFrame động thay vì cố định 06:00.
- Trong phạm vi:
  - Thêm UI upload/chọn workbook và CLI import dùng chung một application service.
  - Kiểm tra file `.xlsx` an toàn, đọc sheet/cell/formula/merged range thành model trung lập với project.
  - Thêm contract adapter theo project; shared code không biết business rule Campaign.
  - Thêm adapter/profile `be-booking` nhận diện các block `VERIFY - Case N`, tạo stable ID gồm sheet/family/case/variant, trả diagnostics và sinh một JSON schema v2 cho mỗi case được hỗ trợ.
  - Chuẩn hoá thời gian workbook thành vị trí tương đối TF1…TFn; fixture runtime tự chọn chuỗi TimeFrame tương lai thỏa điều kiện, không hard-code 06:00.
  - Lưu testcase đã activate dưới `projects/be-booking/testcases/workbook-generated/verify/`, tách khỏi `testcases/campaign/`.
  - Regenerate phải hiển thị preview/diff và chỉ ghi đè sau xác nhận; file chỉnh tay không bị ghi đè im lặng.
  - Case chưa hỗ trợ không chặn các case khác; hiển thị trạng thái blocked và diagnostics cụ thể.
  - Không copy workbook nguồn vào repository; lưu fingerprint SHA-256, tên file, sheet, vùng nguồn và metadata importer trong artifact/testcase generated.
  - Xác minh bằng build, CLI analyze/generate, schema-v2 preflight và Web smoke không mở request backend; không viết hoặc chạy unit test theo yêu cầu người dùng.
- NGOÀI phạm vi:
  - Chưa hỗ trợ đầy đủ `1Day_v1`, `1Day_v2`, `ManyDays`, `PERF`, sheet ẩn hoặc các family ngoài `VERIFY`.
  - Không sửa backend `be-booking`, không tạo request tới backend trong lúc analyze/generate và không thay đổi business expectation để khớp kết quả bất thường.
  - Không tự động activate ngay sau upload; người dùng vẫn phải review, preflight, chạy draft và xác nhận lưu.
  - Không lưu hoặc commit workbook nguồn và không thêm dữ liệu bí mật vào testcase.

## 1b. Lộ trình

User duyệt spec là duyệt luôn lộ trình này.

| Bước/phase | Chạy? | Vì sao |
|---|---|---|
| Phân tích code/workbook | CÓ | Cần xác định generator hiện tại, workbook layout, stable identity và ranh giới shared/project. |
| Research web | CÓ | Cần chọn cách đọc XLSX và guard upload/ZIP an toàn. |
| Interview | CÓ | Đã chốt family đầu, output granularity, regeneration, unsupported cases, retention và QC. |
| Spec | CÓ | Chốt contract và giới hạn release đầu trước khi thêm shared capability. |
| Plan | CÓ | Tách spike thư viện, shared reader, adapter, Web/CLI, test và QC. |
| Implement | CÓ | Thêm code runtime, UI và config project. |
| QC độc lập (agent) | BỎ | Người dùng yêu cầu chỉ build và tự sửa lỗi; không chạy unit test hoặc vòng agent QC độc lập. |
| Report | CÓ | Ghi package đã chọn, file thay đổi, build/preflight/smoke và hạn chế còn lại. |

## 2. Đầu ra cụ thể

| # | Đầu ra | Đường dẫn/vị trí | Đo "xong" bằng |
|---|---|---|---|
| 1 | Shared XLSX reader, security guards, workbook model và adapter contract | `src/AutoTest.Workbooks/` và solution/project references liên quan | Đọc đúng workbook fixture có merge/formula; từ chối file/ZIP vượt giới hạn; không chứa tên project hoặc route Campaign. |
| 2 | Host import/analyze/generate dùng cho CLI và Web | `runner/AutoTest.Generator/` | CLI phân tích workbook, liệt kê case/diagnostics và sinh draft schema v2 từ danh sách stable ID. |
| 3 | Adapter/profile Campaign của `be-booking` | `projects/be-booking/workbook-import/` và adapter assembly/config được project khai báo | Nhận diện family `VERIFY`, phân biệt case trùng số ở sheet khác và sinh Case 54 tương đương testcase chuẩn với TimeFrame động. |
| 4 | UI upload, chọn case, diagnostics, preview/diff và xác nhận activate | `runner/AutoTest.Web/Program.cs`, `runner/AutoTest.Web/wwwroot/` | Người dùng upload file, chọn case supported, xem blocked diagnostics, preflight/run draft và lưu vào group riêng. |
| 5 | Testcase generated tách khỏi testcase viết tay | `projects/be-booking/testcases/workbook-generated/verify/` | Một JSON cho mỗi case activate; `SourceGroup` là `workbook-generated`; không có diff ngoài ý muốn trong `testcases/campaign/`. |
| 6 | Build verification và tài liệu sử dụng | `ApiAutoTest.sln`, `README.md`, `docs/architecture.md`, `docs/kien-truc.md` | Build xanh sau khi agent tự sửa mọi compile error; tài liệu mô tả UI/CLI, project profile, output folder và giới hạn family `VERIFY`. |

## 2b. Ranh giới module

LSP effect check chưa trả symbol dù server đã khởi động; ranh giới dưới đây được đối chiếu từ composition root, project references và call flow hiện có, không chỉ từ tên thư mục.

| Module | Vùng file | Phụ thuộc module | Đầu ra §2 nào |
|---|---|---|---|
| Workbook core | `src/AutoTest.Workbooks/` | Open XML package đã chốt ở §3; không phụ thuộc project | 1 |
| Import application | `runner/AutoTest.Generator/WorkbookImport/` | Workbook core, adapter contract, generator schema models | 2 |
| Be-booking workbook adapter | `projects/be-booking/workbook-import/`, assembly adapter riêng được solution nạp | Workbook core và contract import; không phụ thuộc Web | 3, 5 |
| Web composition/UI | `runner/AutoTest.Web/Program.cs`, `runner/AutoTest.Web/wwwroot/` | Import application thông qua process/service contract; runner preflight/run/activate hiện có | 4 |
| Build verification | `ApiAutoTest.sln` và các project files được sửa | Workbook core, import application, adapter và Web composition | 6 |
| Public documentation | `README.md`, `docs/architecture.md`, `docs/kien-truc.md` | Contract đã triển khai ở các module trên | 6 |

## 3. Cách tiếp cận & lý do

- Chọn Open XML SDK `DocumentFormat.OpenXml` 3.5.1 cho shared reader. Package này cung cấp SpreadsheetML chính thức và cho phép đọc từng worksheet/part mà không buộc materialize toàn workbook lớn; project hiện chưa có package đọc XLSX nên platform libraries không đủ để xử lý shared strings, date styles, merged ranges và formulas một cách tin cậy.
- Shared reader xác thực extension/signature ZIP, file size, entry count, tổng uncompressed bytes và compression ratio trước khi tạo workbook model. Model chỉ chứa dữ liệu cần cho adapter, có giới hạn vùng/hàng để tránh giữ toàn bộ sheet lớn trong memory.
- Adapter được project khai báo bằng descriptor trong `projects/<project>/workbook-import/`; generator resolve adapter qua contract/registry, không branch theo tên project. Business parser/normalizer/template `VERIFY` nằm trong adapter `be-booking`.
- `VERIFY` parser dùng title rows làm boundary, merged-anchor propagation và workbook metadata để tạo case model. Stable ID dùng sheet slug + family + case number + variant; unsupported layout trả diagnostic có sheet/range/reason.
- Generator sinh draft trước, không ghi trực tiếp vào active testcase. Metadata nguồn gồm SHA-256 và coordinates giúp diff/regenerate. Activate dùng output path deterministic và kiểm tra fingerprint của file generated hiện tại trước khi overwrite.
- Case 54 là golden fixture: A `[8,8,0,0,8,0]`, B `[4,4,4,4,4,4]`, C ranges `[6,5]` và `[7]`; absolute time được đổi thành offset/index, rồi template sinh fixture chọn sáu TimeFrame 8 phút liên tiếp tại runtime.
- Đã loại ClosedXML làm implementation mặc định vì workbook có sheet rất lớn và release đầu chỉ cần đọc có chọn lọc; object model in-memory làm tăng rủi ro memory. Đã loại MiniExcel vì trọng tâm của nó là bảng phẳng/streaming và chưa chứng minh đủ metadata merged/style/formula cho layout Campaign. Không parse ZIP/XML thủ công vì Open XML SDK đã cung cấp contract chuẩn.

## 3b. Năng lực & công cụ

| Skill | Nguồn | Phán quyết | Dùng ở đâu / Lý do loại |
|---|---|---|---|
| `tdq-intake`, `tdq-spec`, `tdq-plan`, `tdq-build` | plugin:tdq-workflow | NỀN | Điều phối phân tích, approval, implementation và QC. |
| `spreadsheets` | built-in | DÙNG | Phân tích workbook, sheet, merged range và công thức. |
| `excel-miniexcel` | plugin | DÙNG | Đối chiếu thư viện đọc XLSX và guard import. |
| `brainstorming` | plugin | DÙNG | Chốt ranh giới shared core với adapter nghiệp vụ. |
| `dotnet-feature-flow` | plugin | DÙNG | Triển khai xuyên shared library, generator, Web API và UI. |
| `dotnet-testing` | plugin | DÙNG | Test reader, adapter và pipeline sinh draft. |
| Đã xét 36 skill khác | user/plugin/built-in | KHÔNG | khác lĩnh vực |

## 4. Yêu cầu bắt buộc

- Log service bật mặc định: timestamp, workbook fingerprint rút gọn, project, adapter, sheet/case counts, supported/blocked counts, parse duration và generation result; không log cell chứa secret hoặc toàn workbook; giảm được qua cấu hình log level.
- Không placeholder, không TODO stub, không mock trình bày như dữ liệu thật.
- Unit test: BỎ theo yêu cầu người dùng; validation bắt buộc là build xanh, CLI/preflight và Web smoke không gọi backend.
- Analyze/generate không gọi HTTP API `be-booking`; runtime backend chỉ xảy ra khi người dùng chủ động chạy draft/testcase qua flow hiện có.
- API upload truyền `CancellationToken` qua toàn bộ I/O; temp file được xóa ở cả nhánh thành công, lỗi và hủy.
- Tên testcase và step được sinh bằng tiếng Việt; identifier, code, command và path dùng tiếng Anh.
- Code viết ra bám SOLID, project-specific behavior ở adapter, shared reader/generator không chứa route, table, Campaign status hoặc tên sheet riêng của `be-booking`.

## 5. Ràng buộc & rủi ro

Ràng buộc kiến trúc phải giữ:
- "`projects/<project-name>/` contains only configuration and declarative test cases. No reusable module may branch on a project name or contain an ops-service route, database table, credential, or business rule." — project profile/descriptor giữ dữ liệu khai báo; reusable contract/reader không branch project; adapter code được đóng gói thành extension riêng thay vì nhét vào shared core.
- "Testcase project được gọi shared runner qua schema khai báo; không gọi trực tiếp production code." — testcase generated chỉ dùng schema v2 và executor hiện có.
- "Shared runner không được chứa business rule riêng của `be-booking` hoặc MTC." — parser/template Campaign không đặt trong `AutoTest.Core` hoặc runner execution.

| Rủi ro | Ảnh hưởng | Cách giảm |
|---|---|---|
| Workbook lớn hoặc ZIP bomb | Memory/CPU tăng hoặc service bị treo | Guard trước parse; giới hạn file, entries, uncompressed bytes, ratio, rows/cells và timeout/cancellation. |
| `VERIFY` có nhiều biến thể layout | Sinh testcase sai hoặc bỏ sót | Parse theo block boundary; diagnostic có coordinates; golden fixtures cho Case 54 và các variant supported/blocked. |
| Formula không có cached value hoặc workbook chưa recalculated | Giá trị đọc không đúng expectation | Giữ cả formula và cached raw value; adapter block case thiếu giá trị xác định, không tự đoán. |
| Stable ID trùng giữa sheet/case | Ghi đè nhầm testcase | ID gồm sheet/family/number/variant và kiểm tra unique trước generate. |
| Regenerate ghi đè chỉnh sửa tay | Mất công sửa testcase | Generated metadata/fingerprint, preview/diff bắt buộc và xác nhận activate; mismatch chặn overwrite. |
| TimeFrame tương lai không đủ | Runtime draft bị lỗi dữ liệu | Sinh fixture tìm chuỗi theo duration/offset và thời điểm hiện tại; thiếu candidate được phân loại test-data/environment, không đổi expectation. |
| Thêm project/assembly mới làm tăng wiring | Build hoặc Web/CLI resolve khác nhau | Một composition contract dùng chung; tests chạy cả CLI service và Web request boundary. |

Không cần model AI hoặc dịch vụ có phí. Cần tải NuGet `DocumentFormat.OpenXml` 3.5.1; đây là dependency mới duy nhất dự kiến cho khả năng XLSX dùng chung.

## 6. QC & Definition of Done

Mức QC: tùy chỉnh theo yêu cầu người dùng — không unit test; build + CLI/preflight/Web smoke và agent tự sửa lỗi đến khi xanh.

| # | Hạng mục kiểm | Điều kiện PASS |
|---|---|---|
| Q1 | Workbook guard smoke | CLI từ chối file không phải `.xlsx` và workbook vượt guard với diagnostic ổn định. |
| Q2 | Workbook model smoke | CLI đọc workbook Campaign và báo đúng 17 sheet cùng metadata merge/formula cần cho `VERIFY/54`. |
| Q3 | Adapter isolation | Shared modules không branch theo `be-booking`; project không có descriptor nhận thông báo không hỗ trợ workbook. |
| Q4 | Discovery | Workbook Campaign nhận diện family `VERIFY`, stable ID không trùng; case ngoài phạm vi blocked riêng. |
| Q5 | Case 54 output | Draft Case 54 bảo toàn quota A/B/C, không chứa giờ cố định 06:00 và có fixture TimeFrame động. |
| Q6 | Schema v2 | Mỗi case generated là JSON riêng, project/ID/coverage hợp lệ và Case 54 qua preflight. |
| Q7 | Regeneration safety | Preview phân biệt create/update/unchanged/conflict; conflict không bị ghi đè khi chưa xác nhận. |
| Q8 | UI/CLI parity | Cùng workbook và selection cho cùng case IDs, diagnostics và draft JSON qua smoke flow. |
| Q9 | Upload lifecycle | Temp file ngoài webroot, tên client không thành path, lỗi/hủy dọn temp và log không chứa workbook content. |
| Q10 | Build | Toàn solution build thành công; agent tự sửa compile error và build lại đến khi exit code 0. |
| Q11 | Project regression | Testcase viết tay không đổi; manual campaign và workbook-generated vẫn là hai source group. |
| Q12 | Documentation | README và architecture mô tả đúng command, UI flow, adapter boundary, output folder và giới hạn `VERIFY`. |

DoD:
- Sáu đầu ra ở §2 tồn tại và từng đầu ra có ít nhất một mục QC PASS.
- Người dùng có thể nạp workbook Campaign từ UI hoặc CLI, thấy toàn bộ case `VERIFY` nhận diện được cùng diagnostics, chọn case và sinh draft mà không gọi backend.
- Case 54 generated qua preflight, có TimeFrame động và tương đương logic testcase chuẩn đã dựng tay.
- Activate lưu một JSON/case dưới `workbook-generated/verify`; regenerate có preview/diff và không mất chỉnh sửa tay.
- Build solution PASS; CLI/preflight/Web smoke PASS; không chạy unit test theo yêu cầu người dùng; testcase manual không bị sửa ngoài phạm vi.
- Working log, QC report và tài liệu ghi đúng package, giới hạn hỗ trợ và kết quả kiểm chứng.

## 7. Câu hỏi còn mở

(Không còn câu hỏi.)
