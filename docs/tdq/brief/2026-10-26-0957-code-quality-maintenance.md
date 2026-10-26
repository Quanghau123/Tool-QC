# BRIEF — Chất lượng code và khả năng bảo trì
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Nguyên văn

Người dùng yêu cầu kiểm tra code hiện tại theo SOLID, quy tắc từng ngôn ngữ và khả năng bảo trì/mở rộng; thống kê rõ file nào chưa chuẩn. Người dùng đặc biệt yêu cầu xử lý tình trạng HTML/JS/CSS source viết trên một dòng, khó đọc, và sau đó yêu cầu lập plan cụ thể để triển khai tất cả. Người dùng đã chọn mở request độc lập thay vì mở rộng request API Builder đang ở QC.

Cách hiểu ban đầu: phạm vi là cải thiện maintainability của Tool-QC mà không đổi hành vi sản phẩm, gồm format UI source, cơ chế cưỡng chế format, phân tách trách nhiệm frontend theo feature, và sửa các lỗi correctness đã được audit: concurrent workspace save, child-process stream drain, OpenAPI malformed-version validation cùng regression tương ứng. Cần chốt mức độ tái cấu trúc frontend và mức QC trước khi viết spec/plan.

## Hiểu & kiến thức

### Năng lực dùng được

Phân vân → DÙNG. Kiểm kê ngày 2026-10-26: 39 skill trên đĩa, cộng skill built-in trong context.

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

### Hiện trạng đã kiểm chứng

- `runner/AutoTest.Web/wwwroot/index.html:10-54`, `app.js:1-75` và `app.css:1-27` là source được viết nén thủ công nhưng được Web host phục vụ trực tiếp; không có frontend bundle/minify pipeline tách source phát triển và artifact phát hành.
- `app.js` đồng thời sở hữu HTTP transport, global state, navigation, testcase, environment, generator, workbook, message scanner, UnitX, API Builder và DOM rendering. Đây là điểm vi phạm Single Responsibility ở cấp module và là file nóng cho mọi UI feature.
- `ApiBuilderWorkspaceStore.SaveAsync` tại `runner/AutoTest.Web/ApiBuilderWorkspace.cs:61-74` và `UnitXWorkspaceStore.SaveAsync` tại `runner/AutoTest.Web/UnitXWorkspace.cs:31-40` đều có read-check-write không nguyên tử và file tạm cố định `workspace.json.tmp`; save đồng thời có thể mất update hoặc trả lỗi I/O thay vì conflict được thiết kế.
- `runner/AutoTest.Web/Program.cs:452` và `1071-1081` đọc stdout rồi stderr tuần tự, trong khi `RunProcessAsync:815-823` đã có mẫu đọc hai stream song song an toàn hơn.
- `runner/AutoTest.Generator/OpenApiContractAdapter.cs:10-12` gọi `version.GetString()!` trước khi xác thực kind/value; OpenAPI có `openapi: null` hoặc non-string có thể thành `NullReferenceException` thay vì `InvalidDataException` và luồng fallback/diagnostic có kiểm soát.
- `tests/AutoTest.FrameworkTests/Program.cs` đã phủ API Builder/OpenAPI cơ bản nhưng chưa phủ simultaneous save, stderr-heavy child process hoặc OpenAPI version malformed. E2E hiện có Playwright test nhưng chưa chạy được vì executable không có.
- `dotnet build runner/AutoTest.Web/AutoTest.Web.csproj --no-restore` đã PASS với 0 warning và 0 error. Không có `.editorconfig`, `Directory.Build.props`, Prettier config hoặc script format được Git quản lý.
- `docs/kien-truc.md` xác định `Program.cs`/`app.js` là hub Web UI, `ApiBuilderWorkspace.cs` và `UnitXWorkspace.cs` là persistence boundary; graphify đã refresh 2026-10-26 và LSP readiness đạt 8/8 bậc.

### Quyết định đã chốt

1. Dùng Prettier làm `devDependency`, thêm cấu hình version-control và script `format`/`check-format`; source HTML/JS/CSS luôn dễ đọc, chỉ minify nếu sau này có publish pipeline đo được lợi ích.
2. Giữ vanilla JavaScript, không thêm frontend framework. Đổi entry script sang ES module và tách `app.js` theo feature: bootstrap/state dùng chung, API client, UI testcase, generator, API Builder, UnitX và tool độc lập; không thay đổi endpoint/API hoặc browser workflow.
3. Tạo một persistence helper tối thiểu ở Web boundary để hai workspace store dùng chung critical section per workspace và temporary filename duy nhất; không đưa filesystem locking vào shared runner/abstractions.
4. Chuẩn hóa toàn bộ subprocess generator theo pattern đã có ở `RunProcessAsync`: bắt đầu đọc stdout/stderr đồng thời rồi chờ exit.
5. Validate OpenAPI version là non-empty string trước khi gọi `StartsWith`; dữ liệu malformed phải đi theo controlled `InvalidDataException` path.
6. Không cài Playwright/browser hoặc package không thuộc Prettier nếu chưa được lệnh implementation yêu cầu. Nếu Prettier install cần network, thực hiện như dependency đã được user phê duyệt trong request này.

### Phương án đã loại

- Không dùng format thủ công/script nội bộ: không có parser formatter đáng tin cậy cho cả HTML/JS/CSS và khó enforce ổn định.
- Không chỉ tách API Builder: không giải quyết file trung tâm `app.js` đang ôm các feature khác.
- Không thêm React/Vue/bundler: vượt phạm vi, đổi runtime/toolchain không cần thiết để đạt modularity.
- Không chỉ thêm lock trong từng store: dễ drift concurrency contract; helper Web tối thiểu tái dùng cho hai store là đủ.

### Nguồn tham chiếu

- Prettier documentation: cấu hình chia sẻ, `--write` để format và `--check` để CI kiểm format.
- MDN JavaScript modules: `type="module"`, `import`/`export`, module tự defer và không làm ô nhiễm global scope.
- Microsoft .NET documentation: atomic file replacement và cơ chế đồng bộ liên tiến trình phù hợp cho persistence file-backed.

### Lộ trình

| Bước/phase | CÓ-BỎ | Vì sao |
|---|---|---|
| Phân tích & spec/plan | CÓ | Chạm UI, build tooling, file persistence và generator contract nên phải tách boundaries/DoD trước. |
| Implement theo task tuần tự | CÓ | Formatter trước để giảm conflict, sau đó modular UI và C# correctness có regression. |
| Review độc lập | CÓ | Audit ban đầu đã phát hiện race/deadlock; cần kiểm lại sau thay đổi. |
| QC full | CÓ | User chọn full: build, framework tests, format check, regression vùng chạm và architecture/clean-code review. |
| Runtime/API production test | BỎ | Request không đổi API business; local browser E2E giữ blocker riêng nếu Playwright chưa sẵn sàng. |
| Frontend framework/bundler | BỎ | Vanilla ES modules đáp ứng modularity mà không đổi stack. |

## Hỏi đáp

- Hỏi: Formatter frontend dùng cách nào? Trả lời: `1A` — Prettier làm dev dependency, có `format` và `check-format`.
- Hỏi: Tách frontend đến mức nào? Trả lời: `2A` — giữ vanilla JS, tách `app.js` theo feature bằng ES modules.
- Hỏi: Mức QC? Trả lời: `3A` — `full`.
- Hỏi: Có cần mở rộng scope API Builder request cũ không? Trả lời: không; người dùng chọn request độc lập A để giữ request cũ tách biệt.
