# SPEC — Nâng cấp Tool-QC cho API Builder và UnitX

Ngày: 2026-10-22 · Bản: 1.0 · Brief: ../brief/2026-10-22-1725-tool-qc-upgrade.md · Lane: full
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

- Mục tiêu: tạo lộ trình triển khai một API testcase builder trực quan cho beta nội bộ 3–15 tester, nhiều dự án, không buộc người tạo testcase phải viết JSON/JavaScript; đồng thời xác định UnitX no-code cho logic nghiệp vụ thuần trước, rồi mới tới kiểm thử source .NET.
- Trong phạm vi:
  - UI dựng chuỗi API theo request/response field, chọn response field để lưu biến tạm có tên do tester đặt và gán lại vào request field bằng catalog A–Z.
  - Workspace draft bền vững trong `.artifacts`, có khôi phục an toàn, preflight và publish sang suite schema-v2 chính thức theo project.
  - Nguồn API contract chuẩn hóa từ OpenAPI 3.1 khi có đầu vào, hoặc Roslyn source analysis khi không có OpenAPI.
  - UnitX V1 theo decision table và whitelist toán tử/rule, tách biệt khỏi API runner; thiết kế sẵn đường sang xUnit/source .NET ở phase sau.
  - Safety, redaction, giới hạn dữ liệu, audit/log, kiểm thử và tiêu chí vận hành cho beta nội bộ.
- NGOÀI phạm vi:
  - Không thay thế engine testcase, cú pháp `${variable}`, JSONPath, schema-v2 hoặc các production/destructive guards hiện có.
  - Không hỗ trợ thực thi JavaScript/C#/shell/reflection/expression tự do trong UnitX V1.
  - Không thêm hệ thống tài khoản, phân quyền đa người dùng, đồng bộ cloud, kho testcase tập trung, OpenAPI generator phía backend, hay chạy xUnit tự động trong MVP nếu chưa có request riêng.
  - Không sửa backend nghiệp vụ của từng dự án, không đọc/in/ghi secret từ `.env`, không chạy production hay testcase destructive trong hoạt động đặc tả này.

## 1b. Lộ trình

| Bước/phase | Chạy? | Vì sao |
|---|---|---|
| Phân tích | CÓ | Đã đối chiếu luồng draft/activate Web, Roslyn generator, schema/runtime biến và redaction HTTP. |
| Nghiên cứu nguồn chuẩn | CÓ | Dùng OpenAPI 3.1, JSONPath RFC 9535 và nguyên tắc unit test .NET làm chuẩn thiết kế, không coi kết quả web chưa xác minh là bằng chứng. |
| Spec | CÓ | Chốt output, boundary, safety và Definition of Done trước khi lập kế hoạch. |
| Plan | CÓ sau khi duyệt spec | Chia MVP thành task có test và thứ tự dependency kiểm chứng được. |
| Implement | BỎ trong request này | Người dùng yêu cầu đánh giá; code chỉ bắt đầu sau khi duyệt spec và plan. |
| QC tài liệu | CÓ | Lint spec, đối chiếu source hiện hữu và kiểm tra đầy đủ các output/QC trước khi trình. |

## 2. Đầu ra cụ thể

| # | Đầu ra | Đường dẫn/vị trí | Đo "xong" bằng |
|---|---|---|---|
| 1 | Contract trung gian và adapter OpenAPI/Roslyn cho API builder | `runner/AutoTest.Generator/` và API Web liên quan | Một endpoint từ mỗi nguồn cho ra cùng model field/operation mà UI có thể render; thiếu hoặc lỗi source hiển thị diagnostic thay vì sinh contract đoán. |
| 2 | Workspace API Builder và catalog biến trực quan | `runner/AutoTest.Web/`, `.artifacts/api-builder/` | Tester tạo nhiều bước, lưu một response field với tên riêng, gán lại vào request khác từ danh sách A–Z, đóng/mở UI vẫn khôi phục workspace hợp lệ. |
| 3 | Compile/publish workspace thành testcase chính thức | `runner/AutoTest.Web/`, `projects/<project>/testcases/<group>/` | Bản publish là JSON schema-v2 vượt preflight; `save`/`${variable}` đúng thứ tự; overwrite vẫn cần SHA-256 confirmation. |
| 4 | UnitX V1 decision table | module UnitX mới tách biệt, UI Web và `.artifacts/unitx/` | Rule chỉ dùng whitelist; mỗi row có input, kết quả thực tế, expected và PASS/FAIL; toán tử không được phép bị từ chối rõ ràng. |
| 5 | Thiết kế phase UnitX source .NET | spec/plan và boundary UnitX | Có điểm vào chọn method/source, artifact sinh test để review, và điều kiện isolate build/run trước khi cho phép thực thi. |
| 6 | Test, redaction, observability và tài liệu vận hành | framework tests, UI tests, README/public contract khi có thay đổi | Các journey, biên và recovery bên §6 có bằng chứng PASS; log không lộ secret; tài liệu mô tả contract mới. |

## 2b. Ranh giới module

| Module | Vùng file | Phụ thuộc module | Đầu ra §2 |
|---|---|---|---|
| Contract API | `runner/AutoTest.Generator/` | Abstractions testcase; không phụ thuộc Web | 1 |
| API Builder Web | `runner/AutoTest.Web/` | Contract API, validation, runner invocation | 2, 3 |
| Runtime testcase hiện hữu | `src/AutoTest.Abstractions/`, `src/AutoTest.Core/`, `src/AutoTest.Http/`, `src/AutoTest.TestValidation/` | không phụ thuộc Builder/UnitX | 3, 6 |
| UnitX | `src/AutoTest.UnitX/`, `runner/AutoTest.UnitX/` | Abstractions tối thiểu dùng chung; không gọi HTTP runner | 4, 5 |
| Kiểm chứng và tài liệu | `tests/AutoTest.FrameworkTests/`, `README.md`, `docs/` | tất cả module ở trên theo phạm vi test/tài liệu | 6 |

Ranh giới được lập từ quan hệ đã kiểm chứng: `SourceContractAnalyzer` chỉ được CLI generator và framework tests gọi; `StepExecutionContext` được Core và các transport executors cùng tiêu thụ; `Program.cs` của Web là composition root cho các endpoint generate/activate. Vì vậy Builder chỉ biên dịch về schema hiện hữu, còn UnitX không đi qua `StepExecutionContext`/HTTP executor.

## 3. Cách tiếp cận & lý do

- Chọn: xây một editor domain/UI bền vững trên runner hiện có, có workspace artifact và catalog biến riêng; chỉ biên dịch thành canonical testcase JSON khi preflight hoặc publish.
- Vì: `StepSpec.Save`, `${variable}`, `HttpStepExecutor.AssertAndSave` và `TestcaseValidator` đã thực hiện lưu/resolve/chặn use-before-save. Tạo runtime biến thứ hai sẽ làm lệch preflight, reporting và safety guard. `generate/activate` đã có containment/sanitization, preflight và SHA-256 overwrite protection để tái sử dụng.
- Chọn: OpenAPI là adapter tùy chọn, Roslyn vẫn là fallback/source enrichment. Adapter nào cũng normalize vào contract trung gian; UI không rẽ nhánh theo nguồn.
- Vì: OpenAPI mô tả operation/request/response tốt, còn Roslyn hiện hữu bổ sung validation, authorization, service call, branch và source evidence.
- Chọn: UnitX V1 là interpreter có whitelist cho decision table, kiểu dữ liệu xác định, giới hạn row/step; UnitX source .NET là phase sau với generated xUnit được review và isolate build/run.
- Vì: no-code logic cần deterministic, không secret và không side effect; expression tự do sẽ tạo bề mặt RCE, kết quả khó tái lập và không phù hợp tester không biết code.
- Đã loại: thay testcase bằng DSL/UI format riêng — vì làm mất tương thích runner và yêu cầu hai bộ validation/reporting.
- Đã loại: chỉ nhận OpenAPI — vì nhiều dự án hiện dùng nguồn C# và contract OpenAPI có thể thiếu metadata kiểm thử.
- Đã loại: cho phép C#/JavaScript expression ở UnitX MVP — vì rủi ro thực thi mã và khó giới hạn dữ liệu/quyền.

## 3b. Năng lực & công cụ

| Skill | Nguồn | Phán quyết | Dùng ở đâu / Lý do loại |
|---|---|---|---|
| `tdq-workflow:tdq-intake` | plugin:tdq-workflow | NỀN | Điều phối phân tích, phỏng vấn, spec và plan. |
| `tdq-workflow:tdq-spec` | plugin:tdq-workflow | NỀN | Viết kiến trúc và tiêu chí quyết định đã được duyệt. |
| `tdq-workflow:tdq-plan` | plugin:tdq-workflow | NỀN | Chuyển lộ trình được duyệt thành kế hoạch kiểm chứng được. |
| `tdq-workflow:tdq-lean` | plugin:tdq-workflow | DÙNG | Chặn tạo engine/DSL/abstraction mới khi năng lực runner hiện có đã đáp ứng. |
| `dotnet-standards:facade-module-architecture` | built-in | DÙNG | Quyết định boundary giữa Web, generator, shared contract và UnitX runner. |
| `dotnet-standards:dotnet-testing` | built-in | DÙNG | Định nghĩa ranh giới UnitX và chiến lược sinh/chạy test .NET ở pha sau. |
| `dotnet-standards:api-surface` | built-in | DÙNG | Thiết kế API UI an toàn để lưu workspace, preflight và activate testcase. |
| Đã xét các skill khác | plugin/built-in | KHÔNG | khác lĩnh vực |

## 4. Yêu cầu bắt buộc

- Log service bật mặc định: timestamp, workspace/action/project đã được sanitize, diagnostic/preflight status và lỗi debug; có thể giảm qua config; không log request authorization, login body, token, password, connection string hoặc response field nhạy cảm.
- Workspace chỉ được chứa trong `.artifacts/api-builder/<project>/<workspace-id>/` hoặc `.artifacts/unitx/<project>/<workspace-id>/`; mọi segment kiểm tra containment; draft không được runner tự phát hiện/chạy như testcase chính thức.
- Workspace có version/schema, project binding, updated timestamp, source fingerprint, trạng thái preflight; autosave nguyên tử và recovery đọc được lỗi corrupt/stale thay vì overwrite.
- Catalog biến phân biệt namespace workspace/run với biến environment; tên người dùng đặt không rỗng, không trùng không phân biệt hoa-thường, không đè built-in/system variable; metadata gồm source step, JSONPath, kiểu suy luận, created order và preview đã redacted.
- Catalog luôn hiển thị theo A–Z không phân biệt hoa-thường; UI không render plaintext của key/field nhạy cảm theo danh sách redaction hiện hữu hoặc response đã bị redaction.
- Publish chỉ tạo suite schema-v2 sau preflight thành công; giữ project name, coverage manifest, `save` mapping, validation use-before-save, safe group/file name và confirmation khi overwrite.
- UnitX chỉ nhận operators/hàm đã đăng ký rõ ràng; input/output type hữu hạn; giới hạn number of rows và interpreter steps có diagnostics; không có network, file write, environment secret, reflection hay dynamic compilation ở V1.
- Không placeholder, không TODO stub, không mock trình bày như dữ liệu thật. Mỗi thành phần có unit test riêng, chạy được bằng một lệnh. Code bám SOLID và rule ngôn ngữ của TDQ.

## 5. Ràng buộc & rủi ro

Ràng buộc kiến trúc phải giữ:
- Shared framework không được chứa endpoint, credential, schema hay business rule theo project; hành vi project-specific ở `projects/<project-name>/` — Builder/UnitX shared không được branch theo project name.
- `.env` là nguồn secret cục bộ, environment variable ghi đè; không ghi token/password/key/connection string vào project/testcase JSON — mọi auth/secret dùng `${env:VARIABLE_NAME}`.
- Activation chính thức cần preflight và bảo vệ fingerprint/overwrite; draft artifact không thay thế `projects/<project>/testcases/`.
- API testcase kiểm tra integration/side effect; UnitX V1 phải deterministic, isolated và không dùng API credential mặc định.

| Rủi ro | Ảnh hưởng | Cách giảm |
|---|---|---|
| OpenAPI thiếu hoặc sai contract | Form field/expectation sai | Giữ Roslyn fallback; normalize có provenance; chặn publish khi contract thiếu field bắt buộc và trình diagnostic nguồn. |
| Workspace cũ/corrupt hoặc traversal | Mất draft, ghi ngoài artifact | Metadata version, atomic write, path containment, recovery read-only, cleanup chỉ theo TTL policy được cấu hình và không xóa artifact đang mở. |
| Biến nhạy cảm xuất hiện ở preview/log | Lộ secret/test data | Reuse redaction policy của HTTP executor; preview masked; không persist plaintext của field nhạy cảm trong UI diagnostic/log. |
| No-code UI sinh testcase không đủ coverage | Suite preflight thất bại hoặc assertion hời hợt | Biên dịch canonical schema-v2; surface validation/coverage errors cạnh field/step; không publish khi preflight đỏ. |
| UnitX thành cơ chế thực thi mã | RCE/side effect trên máy chạy Tool-QC | AST/model đóng, operator registry whitelist, không reflection/dynamic compilation/network/file write, resource limits và test negative. |
| Đồng thời nhiều tester trên cùng máy | Ghi đè draft | Workspace ID bất biến, optimistic revision/fingerprint, conflict UI; MVP không tuyên bố multi-user shared ownership. |

## 6. QC & Definition of Done

| # | Hạng mục kiểm | Điều kiện PASS | Đo trước | Dự phòng nếu trượt |
|---|---|---|---|---|
| Q1 | Contract normalization | Given OpenAPI hợp lệ hoặc source C# hợp lệ, When analyze một operation, Then UI nhận endpoint/method/request/response field cùng provenance; input lỗi có diagnostic, không contract đoán. | — | — |
| Q2 | Journey API Builder | Given workspace mới, When tester thêm API A, lưu `$.data.id` là `orderId`, rồi gán vào API B, Then catalog có `orderId`, danh sách A–Z, JSON publish chứa đúng `save` và `${orderId}`. | — | — |
| Q3 | Biên & recovery workspace | Given tên biến trùng khác hoa-thường, path traversal, artifact corrupt hoặc xung đột revision, When autosave/restore, Then không ghi sai vị trí/không overwrite và UI nêu cách recover. | — | — |
| Q4 | Safety/redaction | Given response có password/token/secret và draft có `${env:...}`, When preview, log, run report hoặc artifact diagnostic được tạo, Then không có plaintext bí mật; preflight/runtime guards vẫn hoạt động. | — | — |
| Q5 | Publish testcase | Given workspace hợp lệ, When publish, Then schema-v2 coverage và validator pass trước ghi file; group/file containment và SHA-256 overwrite confirmation giữ nguyên. | — | — |
| Q6 | UnitX journey | Given decision table với rule whitelist, When chạy bảng, Then mỗi row có actual/expected/PASS-FAIL xác định; output đúng với rule. | — | — |
| Q7 | UnitX negative/recovery | Given operator lạ, type không hợp lệ, vượt resource budget hoặc row lỗi, When validate/run, Then không có code/network/file execution, error chỉ rõ row/rule và workspace còn mở để sửa. | — | — |
| Q8 | Regression | Given suite hiện hữu, When build và chạy framework tests cùng smoke non-destructive của project được chọn (nếu API khả dụng), Then các feature cũ không suy giảm và kết quả build/test được báo riêng. | — | — |
| Q9 | UI quality | Given tester không biết JSON, When hoàn thành journey Q2/Q6, Then thao tác field/biến/preflight/publish hiển thị bằng tiếng Việt rõ ràng; không có reference screenshot/mockup nên Vision Test Loop không áp dụng. | — | — |

DoD:
- Có plan được duyệt, các output §2 được triển khai đúng boundary §2b, không phát sinh engine biến thứ hai hoặc bypass runner safety.
- Có test theo Given–When–Then cho Tier 1 journey, ít nhất hai Tier 2 boundary và một Tier 3 failure/recovery cho API Builder và UnitX; không có ANTI-1 đến ANTI-6.
- Tất cả testcase chính thức mới/materially migrated là schema-v2, có coverage manifest đầy đủ và preflight pass.
- Build runner không lỗi; test liên quan pass; smoke non-destructive chỉ được chạy trong local/test environment nếu cấu hình sẵn an toàn và kết quả runtime được báo tách biệt với build.
- README, `.env.example`, `project-template` và hướng dẫn thay đổi khi public contract/shared behavior thực sự thay đổi; không cập nhật chúng khi chỉ thêm UI nội bộ không làm đổi contract.
- Không commit/push trong phạm vi request này trừ khi người dùng yêu cầu rõ.

## 7. Câu hỏi còn mở
