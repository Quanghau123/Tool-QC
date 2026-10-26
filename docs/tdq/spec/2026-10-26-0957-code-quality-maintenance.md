# SPEC — Chất lượng code và khả năng bảo trì

Ngày: 2026-10-26 · Bản: 1.0 · Brief: ../brief/2026-10-26-0957-code-quality-maintenance.md · Lane: full
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

- Mục tiêu: đưa source Web phục vụ trực tiếp về dạng dễ đọc, có formatter được kiểm tra tự động; tách `app.js` thành ES module theo feature mà không đổi browser workflow hay HTTP contract.
- Mục tiêu: loại ba lỗi correctness đã audit: race khi lưu workspace, nghẽn child process do drain stream tuần tự và OpenAPI version malformed đi sai diagnostic path; mỗi lỗi có regression tự động.
- Trong phạm vi:
  - Prettier version-controlled cho HTML/JS/CSS cùng script format và check-format.
  - Entry script ES module, shared bootstrap/state/API client và các module UI theo feature: testcase, environment/history, generator/workbook/message scanner, API Builder, UnitX, architecture.
  - Helper persistence tối thiểu trong `AutoTest.Web` cho critical section per workspace và temporary filename duy nhất, dùng bởi API Builder và UnitX.
  - Đọc stdout/stderr song song ở mọi subprocess generator còn đọc tuần tự.
  - Validate OpenAPI version là non-empty string trước `StartsWith`.
  - Regression framework test, format check, build và smoke UI phát triển nếu browser host hiện có.
- NGOÀI phạm vi:
  - Đổi endpoint/API, business rule, browser workflow hoặc thêm frontend framework/bundler.
  - Minify source, cài Playwright/browser, hay thêm dependency khác ngoài Prettier.
  - Chạy runtime/API production test, sửa backend service, shared runner hoặc mở rộng request API Builder đang ở QC.

## 1b. Lộ trình

| Bước/phase | Chạy? | Vì sao |
|---|---|---|
| Phân tích & spec/plan | CÓ | Chạm UI, build tooling, file persistence và generator contract nên cần ranh giới/DoD trước. |
| Implement theo task tuần tự | CÓ | Formatter chạy trước để giảm conflict; tiếp theo là UI module và C# correctness có regression. |
| Review độc lập | CÓ | Audit đã phát hiện race và nghẽn stream; cần kiểm lại sau thay đổi. |
| QC full | CÓ | Bao gồm build, framework tests, format check, regression vùng chạm và architecture/clean-code review. |
| Runtime/API production test | BỎ | Không thay đổi API business; browser E2E development-only giữ blocker riêng khi Playwright chưa sẵn sàng. |
| Frontend framework/bundler | BỎ | Vanilla ES modules đạt modularity mà không đổi stack. |

## 2. Đầu ra cụ thể

| # | Đầu ra | Đường dẫn/vị trí | Đo "xong" bằng |
|---|---|---|---|
| 1 | Quy ước format frontend được version-control và cưỡng chế | `package.json`, cấu hình Prettier, `runner/AutoTest.Web/wwwroot/` | Formatter format được HTML/JS/CSS Web và check-format chấp nhận source đã commit. |
| 2 | UI vanilla JS tách theo feature qua ES modules | `runner/AutoTest.Web/wwwroot/index.html`, `runner/AutoTest.Web/wwwroot/*.js` | Entry module khởi tạo đủ feature hiện có; journey API Builder giữ nguyên hành vi đã có. |
| 3 | Lưu workspace tuần tự theo từng workspace, atomic và không dùng temporary path cố định | `runner/AutoTest.Web/` persistence boundary | Save cạnh tranh trả conflict có chủ đích hoặc giữ nội dung nhất quán; API Builder và UnitX cùng dùng contract helper. |
| 4 | Subprocess generator drain hai stream đồng thời | `runner/AutoTest.Web/Program.cs` | Child process có stderr lớn hoàn tất và output/diagnostic được thu đầy đủ. |
| 5 | OpenAPI malformed version vào controlled validation path | `runner/AutoTest.Generator/OpenApiContractAdapter.cs` | `openapi` thiếu, null hoặc khác string sinh `InvalidDataException`, không phải null dereference. |
| 6 | Regression và QC có bằng chứng thực thi | `tests/AutoTest.FrameworkTests/`, toolchain Web | Có regression cho ba lỗi, build/framework test/format check pass; smoke browser được chạy hoặc báo blocker có bằng chứng. |

## 2b. Ranh giới module

| Module | Vùng file | Phụ thuộc module | Đầu ra §2 nào |
|---|---|---|---|
| Toolchain format | `package.json`, cấu hình Prettier | không | 1 |
| UI ES modules | `runner/AutoTest.Web/wwwroot/index.html`, `runner/AutoTest.Web/wwwroot/app.js`, `runner/AutoTest.Web/wwwroot/ui/` | Toolchain format; Web API routes | 1, 2 |
| Persistence workspace | `runner/AutoTest.Web/WorkspacePersistence.cs`, `runner/AutoTest.Web/ApiBuilderWorkspace.cs`, `runner/AutoTest.Web/UnitXWorkspace.cs` | .NET file system; `Program.cs` composition root | 3 |
| Web subprocess | `runner/AutoTest.Web/Program.cs` | generator process contracts | 4 |
| OpenAPI adapter | `runner/AutoTest.Generator/OpenApiContractAdapter.cs` | generator model contracts | 5 |
| Regression & verification | `tests/AutoTest.FrameworkTests/Program.cs`, `tests/e2e/api-builder.spec.js` | Persistence workspace; Web subprocess; OpenAPI adapter; UI ES modules | 2, 3, 4, 5, 6 |

## 3. Cách tiếp cận & lý do

- Chọn: Prettier là formatter duy nhất; giữ vanilla JavaScript và đổi entry thành ES module, sau đó dịch từng feature ra module có dependency rõ ràng. C# chỉ thêm helper persistence tại Web boundary, tái sử dụng mẫu drain stream song song sẵn có và thắt validation adapter trước khi truy cập string.
- Vì: Prettier có parser ổn định cho cả HTML/JS/CSS và `--write`/`--check`; ES modules tự defer, có import/export rõ ràng và không tạo global state. LSP xác nhận `Program.cs` là composition/caller của API Builder và UnitX, nên helper chỉ ở Web boundary, không lan sang shared engine.
- Đã loại: format thủ công/script nội bộ — không đáng tin cậy trên ba định dạng; chỉ tách API Builder — không xử lý `app.js` là module nóng; React/Vue/bundler — đổi stack vượt phạm vi; lock riêng từng store — làm drift concurrency contract.

## 3b. Năng lực & công cụ

| Skill | Nguồn | Phán quyết | Dùng ở đâu / Lý do loại |
|---|---|---|---|
| `tdq-workflow:tdq-spec` | plugin | NỀN | Viết spec có ranh giới source/UI/persistence và DoD đo được. |
| `tdq-workflow:tdq-plan` | plugin | NỀN | Chia implementation thành task có test và blast radius. |
| `tdq-workflow:tdq-build` | plugin | NỀN | Thực thi task red-green, QC và report sau approval. |
| `tdq-workflow:tdq-lean` | plugin | DÙNG | Giữ refactor ở mức cần thiết, tránh framework/layer không cần thiết. |
| `dotnet-standards:dotnet-testing` | plugin | DÙNG | Thiết kế regression cho save đồng thời, stderr-heavy process và OpenAPI malformed. |
| `dotnet-standards:dotnet-code-review` | plugin | DÙNG | Rà chất lượng/refactor trước QC độc lập. |
| `run` | built-in | DÙNG | Smoke UI sau khi tách ES modules nếu host/browser khả dụng. |
| Đã xét 32 skill khác | plugin/built-in | KHÔNG | khác lĩnh vực |

## 4. Yêu cầu bắt buộc

- Log service bật mặc định: timestamp, đủ chi tiết debug, tắt/giảm được qua config.
- Không placeholder, không TODO stub, không mock trình bày như dữ liệu thật.
- Mỗi thành phần có unit test riêng, chạy được bằng một lệnh.
- Code viết ra bám 5 nguyên tắc SOLID theo `skills/tdq-conventions/references/clean-code.md`, và bám rule ngôn ngữ trong `skills/tdq-build/references/rules/`.
- Không log token, password, connection string, source path local hoặc nội dung source khi thực hiện UI/refactor này.

## 5. Ràng buộc & rủi ro

Ràng buộc kiến trúc phải giữ:
- `AutoTest.Generator`: một pipeline analyze/generate dùng chung cho CLI và Web; output là schema-v2 draft, chưa gọi backend. — việc này chạm tại `OpenApiContractAdapter.cs`.
- `runner/AutoTest.Web/Program.cs` và `runner/AutoTest.Web/wwwroot/app.js` — upload, catalog, draft review và activation. — việc này chạm entry Web UI và subprocess handler, nhưng không đổi API route/workflow.
- `runner/AutoTest.Web/ApiBuilderWorkspace.cs` + `ApiBuilderCompiler.cs` — workspace API Builder tách theo project tại `.artifacts/api-builder/` và không ghi testcase chính thức trực tiếp. — việc này chạm persistence helper; compiler và storage contract giữ nguyên.
- `runner/AutoTest.Web/UnitXWorkspace.cs` — persistence draft `.artifacts/unitx` và Web facade gọi evaluator. — việc này chạm persistence helper; evaluator không đổi.
- `src/AutoTest.UnitX` — evaluator typed, deterministic và operator whitelist; không tham chiếu HTTP, source compiler hay I/O. — helper không được đưa vào UnitX core.

| Rủi ro | Ảnh hưởng | Cách giảm |
|---|---|---|
| Tách module bỏ sót DOM event hoặc state dùng chung | UI feature không khởi tạo hoặc hành vi khác trước | Chuyển theo feature, giữ API client/state rõ ràng, chạy journey API Builder và smoke browser. |
| Format hàng loạt tạo diff khó review | Lẫn thay đổi hành vi với whitespace | Tách task format/config trước logic; format check ngăn source nén quay lại. |
| Lock quá rộng làm serialize workspace độc lập | Giảm khả năng lưu song song | Key critical section theo canonical workspace path; regression kiểm workspace cạnh tranh và workspace khác nhau. |
| Child process test phụ thuộc OS buffer | Regression không tái hiện ổn định | Dùng process fixture tạo stderr lớn, timeout hữu hạn và assert exit/output. |
| Browser E2E chưa có executable | Không có bằng chứng UI runtime đầy đủ | Chạy smoke khi host sẵn sàng; nếu không, báo BLOCKED cùng lỗi cài đặt, không coi build là E2E pass. |

## 6. QC & Definition of Done

| # | Hạng mục kiểm | Điều kiện PASS | Đo trước | Dự phòng nếu trượt |
|---|---|---|---|---|
| Q1 | Toolchain format | Prettier format toàn bộ source HTML/JS/CSS Web và check-format không báo source chưa chuẩn. | Source hiện có ba file frontend viết nén thủ công: `index.html`, `app.js`, `app.css` (brief, 2026-10-26). | Sửa cấu hình/glob để chỉ quét source Web, không format artifact hoặc dependency. |
| Q2 | UI module boundary | Tất cả feature hiện có được bootstrap từ ES module; không còn một module sở hữu transport, navigation, testcase, environment, generator, API Builder và UnitX cùng lúc. | `app.js` hiện sở hữu ít nhất chín nhóm trách nhiệm (brief, 2026-10-26). | Gộp các helper cùng feature, không thêm abstraction/framework; giữ entry mỏng. |
| Q3 | Concurrent workspace persistence | Hai save cùng revision trên một workspace có outcome xác định; dữ liệu JSON không hỏng; API Builder và UnitX dùng chung helper. | Hai store hiện có read-check-write và temporary path `workspace.json.tmp` (brief, 2026-10-26). | Thu hẹp lock theo workspace canonical path và dùng temporary filename duy nhất. |
| Q4 | Subprocess stream drain | Process có stdout/stderr lớn hoàn tất trong timeout của regression, giữ đủ output và diagnostic. | `RunProcessAsync` đã có mẫu đọc hai stream song song; các call site cũ đọc tuần tự (brief, 2026-10-26). | Áp dụng đúng mẫu đã có cho call site còn lại, không đổi generator protocol. |
| Q5 | OpenAPI malformed validation | Missing/null/non-string/empty OpenAPI version ném `InvalidDataException`; OpenAPI 3.1 hợp lệ vẫn được phân tích. | Adapter hiện gọi `GetString()!` trước validation (brief, 2026-10-26). | Validate `ValueKind` và giá trị trước `StartsWith`, giữ thông điệp contract rõ ràng. |
| Q6 | Build, regression và review | Web build, framework regression, format check và review độc lập đều PASS; browser smoke có evidence PASS hoặc BLOCKED. | Web build hiện có 0 warning và 0 error; E2E có test nhưng browser executable chưa sẵn (brief, 2026-10-26). | Sửa lỗi test/code trong phạm vi; nếu thiếu browser executable, ghi BLOCKED với diagnostic thay vì suy diễn PASS. |

DoD:
- Source frontend Web có formatter version-controlled, source dễ đọc và check-format PASS.
- Mọi feature UI hiện có khởi tạo qua ES module mà không đổi endpoint hoặc browser workflow; journey API Builder được kiểm chứng theo mức host cho phép.
- API Builder và UnitX lưu workspace an toàn khi cạnh tranh; regression chứng minh contract này.
- Mọi subprocess generator chạm tới dùng drain stdout/stderr song song; regression stderr-heavy PASS.
- OpenAPI malformed version đi qua `InvalidDataException`; OpenAPI 3.1 hợp lệ không regress.
- Build, framework regression, format check và review độc lập có evidence hiện tại; browser smoke phân biệt PASS với BLOCKED.

## 7. Câu hỏi còn mở
