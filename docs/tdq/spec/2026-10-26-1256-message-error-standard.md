# SPEC — Chuẩn hóa catalogue message và mã lỗi

Ngày: 2026-10-26 · Bản: 1.0 · Brief: ../brief/2026-10-26-1256-message-error-standard.md · Lane: full
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

- Mục tiêu: Tool-QC có catalogue JSON versioned theo từng project để định nghĩa mã lỗi ổn định, HTTP status và thông điệp `vi`/`en`; audit source read-only và preflight/runtime testcase cùng dùng catalogue đó thay vì lặp chuỗi message hoặc suy luận error code từ text.
- Trong phạm vi:
  - Catalogue project-local tại `projects/<project>/error-catalogue.json`, có `schemaVersion`, `prefix`, entries `code`, `httpStatus`, `messages.vi`, `messages.en`; code phải theo `<PREFIX>-<DOMAIN>-<NNN>`.
  - Khai báo catalogue bằng đường dẫn tương đối trong `project.json`; project template ghi contract trống, không mang business code mẫu.
  - Shared model/loader/preflight để kiểm schema/version, prefix/grammar, code trùng, HTTP status hợp lệ, đủ locale và mọi reference catalogue từ testcase.
  - Field declarative `expect.error` cho HTTP step, tham chiếu một code catalogue và tùy chọn locale; preflight resolve thành status/errorCode/message contract, executor HTTP dùng assertion sẵn có.
  - Default locale `vi`; `en` là fallback khi request locale không có bản dịch. Một code luôn giữ nguyên `errorCode` và HTTP status dù locale thay đổi.
  - Source Message Scanner audit read-only catalogue/key/translation, report literal display/error string theo heuristic giới hạn và mismatch catalogue; workbook xuất thêm audit data mà vẫn giữ sheet legacy.
  - Catalogue nội bộ Tool-QC cho `TQC-<DOMAIN>-<NNN>` và `UiSupport.Diagnose` đọc code/message/fix hint từ catalogue; text matching chỉ là fallback cho lỗi legacy chưa có structured signal.
  - Runtime i18n cho toàn bộ message hiển thị của AutoTest.Web: loading, empty, success, validation, actionable error và browser/network fallback. `runner/AutoTest.Web/wwwroot/ui/messages/` là ownership directory duy nhất cho catalogue UI; mọi UI module chỉ gọi key/lookup, không giữ chuỗi tiếng Việt/Anh business-visible rải rác. Locale `vi` mặc định, `en` fallback và người dùng đổi locale không cần tải lại workspace.
  - Khi API trả `errorCode`, UI map code qua catalogue locale trước; `message` từ API chỉ là fallback khi code chưa có catalogue. UI không branch behavior theo localized text.
  - Regression framework, browser UI i18n journey, CLI/Web scanner compatibility, documentation README, `.env.example` nếu locale cần cấu hình, `project-template`, và hướng dẫn public contract.
  - Một migration representative, schema-v2, của error testcase `ops-service` chỉ khi source/API local hiện hữu chứng minh response contract; không đoán code hoặc message từ testcase cũ.
- NGOÀI phạm vi:
  - Sửa `ops-service` hay bất kỳ backend ngoài Tool-QC, gồm middleware, DTO, HTTP envelope, message resource và locale negotiation.
  - Đổi HTTP assertion engine, thêm transport, thêm package hay tạo runtime i18n cho backend.
  - Bắt buộc migrate mọi testcase hoặc đổi toàn bộ legacy `expect.json` trong một lần.
  - Parse localized message ở client để quyết định business behavior.

## 1b. Lộ trình

- Thêm catalogue JSON versioned ở `projects/<project>/`, với prefix cấu hình theo project, entry code/domain/status và message `vi`/`en`; `vi` là default, `en` fallback. Mã Tool-QC nội bộ theo `TQC-<DOMAIN>-<NNN>`; mã backend theo prefix catalogue, ví dụ `OPS-<DOMAIN>-<NNN>`.
- Mở rộng shared loader/preflight để reject catalogue/reference sai: schema/version, mã trùng, sai grammar/prefix, HTTP status không khớp, thiếu `vi`, thiếu fallback `en`, hoặc testcase reference không tồn tại. Reference catalogue chỉ bổ sung vào testcase khi cần kiểm full error contract; testcase cũ giữ `expect.json` tương thích.
- Mở rộng MessageScanner và workbook/Web/CLI output để audit key/translation hiện có, hard-coded display/error strings theo heuristic minh bạch, và mismatch với catalogue; scanner không tự suy luận hay phát hành mã cho backend.
- Thêm Web i18n boundary và locale switch: các UI module thay string display bằng key/catalogue lookup, bao phủ loading, empty, success, validation, error và network fallback. Khi API trả `errorCode`, Web resolve code trước; localized backend `message` chỉ là fallback.
- Giữ `expect.json` làm executor duy nhất; thêm declarative catalogue reference để preflight resolve thành status/error-code/message assertion. Chạy fixture framework cho validation, fallback và runtime HTTP contract; migrate một tập testcase lỗi representative của `ops-service` bằng catalogue dự án, rồi chạy đúng tag liên quan khi local/test API có sẵn.
- Cập nhật README, `.env.example` nếu contract công khai cần biến locale, `project-template`, và hướng dẫn repository khi shared contract thay đổi; không chạm source backend ngoài Tool-QC.

| Bước/phase | Chạy? | Vì sao |
|---|---|---|
| Research web | BỎ | Contract là capability nội bộ; source, caller LSP và hai audit độc lập đã đủ evidence. |
| Interview | BỎ | User đã chốt scope Tool-QC, catalogue/audit + runtime verification, prefix, nơi lưu và locale fallback. |
| QC độc lập (agent) | CÓ | Thay đổi shared loader/preflight/scanner cần review độc lập về compatibility, security và contract. |

## 2. Đầu ra cụ thể

| # | Đầu ra | Đường dẫn/vị trí | Đo "xong" bằng |
|---|---|---|---|
| 1 | Contract catalogue và typed load/resolve | `src/AutoTest.Abstractions/`, `src/AutoTest.Core/`, `projects/project-template/`, `projects/ops-service/` | Catalogue hợp lệ nạp được; catalogue sai hoặc reference sai bị preflight từ chối với diagnostic xác định. |
| 2 | Runtime error expectation tương thích | `src/AutoTest.Abstractions/`, `src/AutoTest.Http/`, `src/AutoTest.TestValidation/` | `expect.error` xác nhận status, `errorCode` và localized message từ catalogue; testcase chỉ dùng `expect.json` vẫn pass regression. |
| 3 | Scanner/catalogue audit và workbook compatible | `src/AutoTest.MessageScanner/`, `runner/AutoTest.MessageScanner/`, `runner/AutoTest.Web/` | CLI/Web scanner trả audit catalogue; workbook còn sheet/cột legacy và có audit findings riêng. |
| 4 | Taxonomy nội bộ Tool-QC có nguồn chân lý | `runner/AutoTest.Web/UiSupport.cs`, catalogue Tool-QC | Diagnostic `TQC-*` lấy display/fix text từ catalogue; matching string legacy vẫn phân loại được. |
| 5 | I18n runtime cho Web UI | `runner/AutoTest.Web/wwwroot/ui/`, `runner/AutoTest.Web/wwwroot/` | UI lấy text qua boundary chung; đổi `vi`/`en` cập nhật message visible không mất workspace; API code được map trước backend message fallback. |
| 6 | Regression và tài liệu | `tests/AutoTest.FrameworkTests/`, `tests/e2e/`, `README.md`, `.env.example`, `CLAUDE.md`, `projects/project-template/` | Framework/browser assertions bao phủ validation, fallback, runtime, scanner, workbook và UI locale; tài liệu mô tả contract/safety không lộ secret. |

## 2b. Ranh giới module

| Module | Vùng file | Phụ thuộc module | Đầu ra §2 nào |
|---|---|---|---|
| Catalogue contract | `src/AutoTest.Abstractions/`, `src/AutoTest.Core/`, `projects/project-template/`, `projects/ops-service/` | không | 1 |
| Testcase contract | `src/AutoTest.Http/`, `src/AutoTest.TestValidation/` | Catalogue contract | 2 |
| Source audit export | `src/AutoTest.MessageScanner/`, `runner/AutoTest.MessageScanner/` | Catalogue contract | 3 |
| Web diagnostics | `runner/AutoTest.Web/UiSupport.cs`, `runner/AutoTest.Web/Program.cs` | Catalogue contract, Source audit export | 3, 4 |
| Web UI i18n | `runner/AutoTest.Web/wwwroot/ui/`, `runner/AutoTest.Web/wwwroot/app.js` | Catalogue contract, Web diagnostics | 5 |
| Evidence and documentation | `tests/AutoTest.FrameworkTests/`, `tests/e2e/`, `README.md`, `.env.example`, `CLAUDE.md` | Catalogue contract, Testcase contract, Source audit export, Web diagnostics, Web UI i18n | 6 |

## 3. Cách tiếp cận & lý do

- Chọn: một catalogue project-local, versioned làm source of truth; model shared chỉ diễn tả contract; `SpecLoader` nạp catalogue, `TestcaseValidator` kiểm reference trước network call, và HTTP executor resolve `expect.error` thành assertion đang có. Scanner nhận catalogue như input rõ ràng, chỉ audit source read-only và xuất findings; Excel là presentation, không là nguồn dữ liệu. Web UI dùng một i18n lookup boundary, giữ locale trong browser state và render lại message visible khi locale đổi.
- Vì: `SpecLoader` đã là boundary nạp `project.json`/testcase cho runner; `TestcaseValidator.Validate` được runner và Web preflight dùng chung; `HttpStepExecutor` đã support status + JSON-path assertions; `ui/shared.js` là module dùng chung phù hợp để giữ UI locale/catalogue lookup. Cách này giữ một đường execution, không có nhánh theo project và không tạo assertion engine hoặc i18n helper riêng theo feature.
- Đã loại: backend middleware/error-envelope implementation — vì Tool-QC không sở hữu backend business. Đã loại literal `${error:...}` trong template chung — vì field `expect.error` rõ semantic, dễ preflight và không làm `Templates` trở thành catalogue service. Đã loại Excel catalogue — vì workbook chỉ là export và không có validation/versioning contract. Đã loại dịch từng module UI độc lập — vì sẽ lặp fallback và làm locale state phân tán.

## 3b. Năng lực & công cụ

| Skill | Nguồn | Phán quyết | Dùng ở đâu / Lý do loại |
|---|---|---|---|
| `tdq-workflow:tdq-intake` | plugin:tdq-workflow | NỀN | Đã mở và hoàn tất intake/analyze cho request full. |
| `tdq-workflow:tdq-spec` | plugin:tdq-workflow | NỀN | Viết, tự soát, lint và trình spec này. |
| `dotnet-standards:error-handling` | plugin:dotnet-standards | DÙNG | Giữ code/status/message là contract riêng; không biến Tool-QC thành backend error producer. |
| `dotnet-standards:dotnet-testing` | plugin:dotnet-standards | DÙNG | Thiết kế regression observable cho shared loader, preflight, scanner và runtime HTTP path. |
| `dotnet-standards:dotnet-code-review` | plugin:dotnet-standards | DÙNG | Rà compatibility, cancellation, duplication và coverage sau implementation. |
| Đã xét 15 skill khác | user/plugin/built-in | KHÔNG | khác lĩnh vực |

## 4. Yêu cầu bắt buộc

- Log service giữ bật mặc định: timestamp, đủ context để debug catalogue/preflight/scanner, có thể giảm qua config; tuyệt đối không log token, password, authorization header, connection string hoặc nội dung `.env`.
- Không placeholder, không TODO stub, không mock trình bày như dữ liệu thật.
- Mỗi thành phần có regression riêng trong framework tests; runtime HTTP contract dùng local stub/fixture xác định, không yêu cầu backend ngoài.
- Code viết ra bám 5 nguyên tắc SOLID theo `CLAUDE.md` và `skills/tdq-conventions/references/clean-code.md`; không branch shared code theo project name.
- Existing `expect.json`, CLI scanner và Web scan route giữ compatible khi project không khai catalogue hoặc testcase không khai `expect.error`.
- UI locale mặc định `vi`, fallback `en`; locale switch chỉ đổi content hiển thị, không đổi error code, HTTP status, request, workspace state hay selected data. Browser preferences chỉ lưu locale per-viewer và luôn render đúng khi storage unavailable.
- API `errorCode` được UI lookup trước; API localized `message`, raw response text và browser exception detail chỉ là fallback display có redaction, không phải control flow.
- Migration testcase business chỉ được thực hiện sau khi source contract hiện tại và response local/test xác nhận code/status/message; nếu không có evidence, ghi `NOT_RUN` hoặc `BLOCKED`, không tự tạo mã.

## 5. Ràng buộc & rủi ro

Ràng buộc kiến trúc phải giữ: không chạm dòng nào — `docs/kien-truc.md` chỉ quy định workbook/API Builder/UnitX; capability này không thuộc các hub và vẫn phải tuân thủ repository boundary shared/project.

| Rủi ro | Ảnh hưởng | Cách giảm |
|---|---|---|
| Hard-code heuristic bắt nhầm log, fixture hoặc metadata | Audit gây false positive | Chỉ scan extension/language/pattern được khai báo, gắn file/line/kind và regression cho positive lẫn excluded fixture. |
| Backend response chưa có `errorCode` | Không thể migrate testcase runtime truthfully | Catalogue/preflight vẫn hoàn chỉnh; testcase business giữ legacy assertion và báo `NOT_RUN`/`BLOCKED` cho migration. |
| Catalogue/status drift với backend | Test báo lỗi nhưng không biết bên nào stale | Không tự đổi expectation; inspect source và response hiện tại, phân loại TEST_SCRIPT_ERROR hoặc BACKEND_BUG. |
| Thay đổi shared loader làm hỏng caller CLI/Web | Preflight/scan regress | Reuse `SpecLoader`/`TestcaseValidator`, giữ default no-catalogue mode và cover runner/Web caller path. |
| Locale text xuất hiện trong report/log | Có thể lộ secret qua interpolation | Catalogue cấm secret/config values; giữ sanitizer/redaction hiện có và test redaction path. |
| UI đổi locale làm mất workspace hoặc dùng text để branch | Regression thao tác UI hoặc behavior phụ thuộc ngôn ngữ | Locale ở shared browser state; re-render chỉ content; E2E đổi locale giữa workflow, xác nhận selection/workspace giữ nguyên và code không đổi. |

## 6. QC & Definition of Done

| # | Hạng mục kiểm | Điều kiện PASS | Đo trước | Dự phòng nếu trượt |
|---|---|---|---|---|
| Q1 | Catalogue validation | Valid catalogue nạp được; sai version, prefix/grammar, code trùng, status ngoài HTTP range, locale thiếu và reference không tồn tại đều bị preflight từ chối. | — | — |
| Q2 | Runtime error contract | Local deterministic HTTP fixture chứng minh `expect.error` kiểm đúng status, code, `vi`; locale yêu cầu không có translation nhận `en` fallback; legacy `expect.json` không regress. | — | — |
| Q3 | Source audit và workbook | Scanner trả findings có source location/kind cho mismatch và hard-code trong fixture; bỏ qua directories/files excluded; workbook legacy vẫn tạo được cùng audit export. | — | — |
| Q4 | CLI/Web/internal diagnostics | CLI và Web message scan vẫn trả artifact; TQC diagnostic catalogue lookup và legacy text fallback đều tạo code/fix hint không lộ secret. | — | — |
| Q5 | Web UI i18n journey | Browser đổi `vi`/`en` cho loading, empty, success, validation, error và network fallback; `errorCode` map trước API message; workspace/selection còn nguyên sau locale switch. | — | — |
| Q6 | Framework/build/documentation | Build shared runner không lỗi; framework regression pass; README/template/instructions mô tả catalogue, UI i18n, migration and safety boundaries đúng. | — | — |
| Q7 | Project runtime evidence | Nếu local/test `ops-service` đáp ứng guards và response contract đã có `errorCode`, tag representative chạy PASS; nếu chưa có contract hoặc environment, ghi `NOT_RUN`/`BLOCKED` với evidence, không claim PASS. | — | — |
| Q8 | Independent review | Code/security review không còn finding actionable trong vùng thay đổi. | — | — |

DoD: catalogue JSON là source of truth; Tool-QC preflight, runtime contract và Web UI i18n dùng cùng catalogue; scanner/workbook/CLI/Web vẫn compatible; localized text không dùng để branch client behavior; locale switch không mất workspace state; không sửa backend ngoài Tool-QC; build, regression, browser journey, review và runtime evidence được báo tách bạch theo PASS/FAIL/NOT_RUN/BLOCKED.

## 7. Câu hỏi còn mở
