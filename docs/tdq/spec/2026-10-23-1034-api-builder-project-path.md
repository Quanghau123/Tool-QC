# SPEC — API Builder khám phá source local theo project

Ngày: 2026-10-23 · Bản: 2.0 · Brief: ../brief/2026-10-23-1034-api-builder-project-path.md · Lane: full
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md
Trạng thái: CHỜ DUYỆT LẠI

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

- Mục tiêu: API Builder cho phép người dùng đưa trực tiếp đường dẫn thư mục của một source project local bất kỳ, ví dụ `D:\works\projects\mtc\be-booking` hoặc `D:\works\projects\ops-service`; Tool-QC tự khám phá contract, liệt kê operation và ghi nhớ mapping theo project Tool-QC mà không yêu cầu copy/sửa repository nguồn.
- Trong phạm vi:
  - Mỗi `projects/<project>/project.local.json` là local override bị Git ignore, lưu duy nhất `apiBuilder.localSourcePath` theo format cố định; UI nạp lại source này khi chọn đúng project.
  - API Builder cho nhập source directory local, server canonicalize và chỉ ghi local override sau khi source hợp lệ đã được phân tích thành contract.
  - Discovery ưu tiên OpenAPI 3.1 tìm thấy trong source project; nếu không có contract hợp lệ và source là .NET, dùng Roslyn analyzer hiện có để lấy controller/action/route/DTO/validation.
  - Source Node.js/TypeScript chỉ được hỗ trợ khi source chứa OpenAPI 3.1; thiếu contract sẽ nhận diagnostic hướng dẫn, không suy đoán endpoint Express/Nest/Fastify.
  - Quét read-only, không chạy executable/script/package manager trong source; bỏ qua reparse point/symlink-junction, `.env`, `.git`, `bin`, `obj`, `node_modules`, artifacts, logs và test output.
  - Giữ mode project-owned `apiBuilder.openApiPath`/`sourcePath` hiện có làm fallback tương thích cho project chưa có local override.
  - UI hiển thị source trạng thái, provider, số operation và diagnostic tiếng Việt; workspace chỉ giữ contract fingerprint/operation, không chứa local filesystem path.
  - Bổ sung framework regression, browser E2E, `.gitignore`, template/documentation và fixture cần thiết.
- NGOÀI phạm vi:
  - Không sửa repository source được chọn, không copy source vào Tool-QC, không chạy mã của source, không đọc `.env`/secret/log/database dump.
  - Không fetch URL OpenAPI, UNC/network share, WSL/network mount hay thêm cloud storage/credential.
  - Không tạo Node.js analyzer hoặc parser Express/Nest/Fastify/Koa/Hono; không mở rộng API executor, schema-v2 compiler, preflight/activation hay backend business source.
  - Không thay OpenAPI adapter hiện tại bằng thư viện/dependency mới; contract không phải OpenAPI 3.1 sẽ trả diagnostic rõ.

## 1b. Lộ trình

| Bước/phase | Chạy? | Skill/cách làm | Vì sao |
|---|---|---|---|
| Analyze bổ sung | CÓ | LSP + source review | Chốt merge local override, trust boundary và provider routing từ implementation hiện có. |
| Spec | CÓ | `tdq-workflow:tdq-spec` | Bản 2.0 thay đổi đầu ra và security boundary so với spec đã duyệt. |
| Plan | CÓ | TDQ full plan | Chia config/containment → facade/UI → regression/docs, có gate duyệt lại. |
| Implement | CÓ | `tdq-workflow:tdq-build`, `api-surface`, `dotnet-testing` | Cần sửa Web facade/UI/test/docs nhưng giữ shared runner độc lập. |
| QC full | CÓ | TDQ QC + independent reviewer | Kiểm source local, Node diagnostics, persistence và browser journey. |
| Research web | BỎ | — | Dùng Roslyn/OpenAPI và filesystem APIs đã có trong repo; không thêm protocol/thư viện. |
| Node analyzer | BỎ | — | Người dùng đã chốt Node.js chỉ OpenAPI/Swagger contract ở giai đoạn đầu. |

## 2. Đầu ra cụ thể

| # | Đầu ra | Đường dẫn/vị trí | Đo "xong" bằng |
|---|---|---|---|
| 1 | Local override per-project, không bị Git theo dõi | `projects/<project>/project.local.json`, `.gitignore`, Web config reader | File mới/lệch format/missing vẫn không làm project lỗi; valid local source chỉ được ghi sau discovery thành công. |
| 2 | Discovery facade cho một local project directory | `runner/AutoTest.Web/ApiBuilderProjectConfiguration.cs`, `runner/AutoTest.Web/Program.cs` | Nhập `D:\...\be-booking` hoặc `D:\...\ops-service` có contract endpoint; path không phải local directory, UNC, reparse escape hoặc source không được hỗ trợ trả diagnostic. |
| 3 | Provider routing dùng lại capability có sẵn | API Builder facade + Generator adapters | Ưu tiên OpenAPI 3.1; .NET không có/invalid OpenAPI dùng Roslyn; Node không có OpenAPI báo supported limitation. |
| 4 | Browser flow source-first | `runner/AutoTest.Web/wwwroot/index.html`, `app.js`, `app.css` | Nhập source → load operations → tạo/bind/save/reload/compile; reload project nạp local mapping mà không ghi path vào workspace/localStorage. |
| 5 | Regression và hướng dẫn | `tests/AutoTest.FrameworkTests/Program.cs`, `tests/e2e/api-builder.spec.js`, `README.md`, `docs/kien-truc.md` | Regression phủ persistence, containment, provider routing/diagnostic và journey browser; hướng dẫn nêu format local override và giới hạn Node/OpenAPI. |

## 2b. Ranh giới module

| Module | Vùng file | Phụ thuộc thực | Đầu ra §2 |
|---|---|---|---|
| Local configuration & containment | `runner/AutoTest.Web/ApiBuilderProjectConfiguration.cs` | `Load` hiện được facade `Program.cs` và framework harness gọi; owner merge `project.json`/`project.local.json` và canonical source local. | 1, 2 |
| API Builder facade | `runner/AutoTest.Web/Program.cs` | `BuildApiBuilderContract` là sole endpoint path; dùng configuration, `OpenApiContractAdapter`, `SourceContractAnalyzer`, `ApiBuilderContractProjection`. | 2, 3 |
| Contract analyzers | `runner/AutoTest.Generator/OpenApiContractAdapter.cs`, `SourceContractAnalyzer.cs`, `ApiBuilderContractProjection.cs` | Facade gọi adapter/analyzer rồi projection; tái dùng, không tạo parser mới. | 3 |
| Browser API Builder | `runner/AutoTest.Web/wwwroot/index.html`, `app.js`, `app.css` | Chỉ gọi facade; workspace persistence hiện hữu vẫn là owner của workspace/fingerprint. | 4 |
| Evidence & public guidance | framework harness, E2E, README/architecture, `.gitignore` | Quan sát facade/browser, không phụ thuộc vào filesystem source thực ngoài fixture tạm. | 5 |

LSP xác nhận `ApiBuilderProjectConfiguration.Load` có caller runtime tại `Program.cs:74` và regression trong `tests/AutoTest.FrameworkTests/Program.cs`; `SourceContractAnalyzer.Analyze` hiện là implementation Roslyn độc lập. Vì vậy local source policy vẫn ở Web boundary, không lan vào `AutoTest.Core` hoặc `AutoTest.Runner`.

## 3. Cách tiếp cận & lý do

- Chọn: mở rộng `ApiBuilderProjectConfiguration` hiện có để merge optional `project.local.json`; dùng một request API Builder chứa source path tùy chọn. Server xác thực source, tạo contract read-only, sau đó mới ghi `localSourcePath` vào local override. Khi không có input, server ưu tiên mapping local đã lưu rồi mới dùng `apiBuilder` portable hiện có.
- Vì: người dùng thao tác một source project bất kỳ trên máy, không cần copy/sửa source repo; mapping vẫn tách khỏi Git và được nạp lại theo Tool-QC project. Tái dùng Web facade, OpenAPI adapter, Roslyn analyzer và workspace guards hiện có thay vì tạo library/parser/storage mới.
- Local path policy: chấp nhận local absolute directory do người dùng nhập làm project root, nhưng từ chối URI/UNC/network path và source root là reparse point; enumeration không theo reparse point và chỉ chọn source/contract files được allowlist. Server không trả source content/full path trong log, workspace hay testcase; response chỉ có provider, operation metadata và diagnostic an toàn.
- Provider order: tìm OpenAPI candidate an toàn trong source root trước, chỉ parse OpenAPI 3.1. Nếu absent/invalid và source có .NET indicators, dùng Roslyn; nếu source Node.js/TypeScript mà thiếu OpenAPI 3.1, không attempt heuristic parsing mà trả guidance export/generate contract.
- Đã loại: yêu cầu source nằm dưới `projects/<project>/` vì không đáp ứng source-first cross-machine use case. Lưu path trong `project.json` vì nó bị version-control và không portable. Lưu localStorage vì path không phải workspace state, không shared per project và không có schema quản lý. Cho phép fetch URL hoặc unrestricted recursive scan vì tăng phạm vi đọc/network không cần thiết.

## 3b. Năng lực & công cụ

| Skill/capability | Nguồn | Phán quyết | Dùng ở đâu / Lý do loại |
|---|---|---|---|
| `tdq-workflow:tdq-spec` | plugin | DÙNG | Cập nhật spec 2.0 và duyệt lại scope. |
| `tdq-workflow:tdq-build` | plugin | DÙNG | Thực thi plan, full QC/report sau approval. |
| `dotnet-standards:api-surface` | plugin | DÙNG | Thiết kế request/response facade có input local source nhưng không lộ filesystem detail. |
| `dotnet-standards:dotnet-testing` | plugin | DÙNG | Regression containment/provider/persistence và E2E observable flow. |
| Đã xét 29 skill khác | plugin/built-in | KHÔNG | khác lĩnh vực |

## 4. Yêu cầu bắt buộc

- Log service bật mặc định qua Web logging hiện có: log project, provider và kết quả discovery; không log full path, source text, token, body login, secret hoặc contents của `project.local.json`.
- `project.local.json` có schema cố định, chỉ giữ local setting cần thiết, ghi UTF-8 atomically; malformed/missing file không làm portable project configuration lỗi.
- Source path không được lưu trong localStorage, API Builder workspace, testcase JSON, report hay documentation sample có source thực của người dùng.
- Source được quét read-only; không spawn process từ source, không run `npm`, `dotnet`, build, test hay script hook.
- Không follow reparse point/junction/symlink; skip `.env`, secret-like filename, source-control, dependencies/build/log/artifact/test-output directories trước khi đọc file.
- Contract/provider/result metadata phải tiếp tục giữ fingerprint và typed binding; changing source contract không tự overwrite workspace.
- Mọi output/diagnostic UI có tiếng Việt actionable: source missing, not local directory, unsupported Node no OpenAPI, invalid OpenAPI and Roslyn fallback.
- Mỗi task có test red→green; fixture source phải tạo trong test temp directory, không đọc source thật của người dùng.

## 5. Ràng buộc & rủi ro

| Rủi ro | Ảnh hưởng | Cách giảm |
|---|---|---|
| Arbitrary local path trở thành file reader | Lộ source/secret qua local Web facade | Chỉ nhận directory local explicit, no UNC/URI/reparse root, file allowlist+directory denylist, no recursive reparse traversal, không trả/log contents hoặc full path. |
| `project.local.json` hỏng/đang ghi | API Builder crash hoặc mất config | Parse optional; fallback portable config/guidance; atomic replace chỉ sau discovery pass. |
| OpenAPI candidate không hợp lệ | Danh sách API thiếu/sai | OpenAPI 3.1-first; .NET fallback Roslyn; Node report limitation, không đoán route. |
| Contract source có nhiều OpenAPI files | Chọn nhầm API | Dùng thứ tự candidate xác định, trả source/provider status; diagnostics yêu cầu user chọn explicit candidate nếu ambiguity không giải được. |
| Junction/symlink đi ra ngoài project | Quét filesystem ngoài expected directory | Reject root reparse point và skip all child reparse points trước enumeration/read. |
| Source lớn | UI treo hoặc quá nhiều I/O | Bounded candidate discovery/file count; diagnostic hướng user export OpenAPI; không tự mở rộng scan. |
| Existing project-owned contract regression | Các project cũ mất operation chooser | Merge precedence rõ: request input → local override → portable project config; framework/E2E giữ test compatibility. |

## 6. QC & Definition of Done

| # | Hạng mục kiểm | Điều kiện PASS | Đo trước | Dự phòng nếu trượt |
|---|---|---|---|---|
| Q1 | Local mapping | Valid input chỉ được persist vào ignored `project.local.json`; reload same Tool-QC project tự nạp; portable `project.json` không đổi. | — | Diagnostic, không ghi mapping. |
| Q2 | Local containment | Local project directory hợp lệ load contract; URI, UNC, missing directory, root/child reparse point, `.env`/ignored trees bị reject/skip. | — | Stop discovery và trả lỗi tiếng Việt. |
| Q3 | Provider routing | OpenAPI 3.1 ưu tiên; .NET absent/invalid OpenAPI fallback Roslyn; Node source no OpenAPI returns limitation diagnostic. | — | Không tạo operation suy đoán. |
| Q4 | Browser flow | Nhập source → operation list → create/bind/save/reload/compile; không có path local trong workspace/localStorage. | — | E2E failure là blocker trước completion. |
| Q5 | Compatibility | Existing `apiBuilder.openApiPath`/`sourcePath` project vẫn auto-load khi thiếu local override. | — | Regression framework/E2E phải pass trước merge. |
| Q6 | Quality | Web/Runner build, framework harness, frontend syntax, Chromium E2E và independent review pass. | — | TDQ QC fix loop tối đa 3 vòng; report blocker thật. |
| Q7 | Guidance | README/architecture mô tả format local override, read-only limitations, OpenAPI 3.1 and Node boundary; không chứa path source thật. | — | Documentation lint/review fail. |

DoD:
- Người dùng có thể chọn `D:\...\<one-project>` bất kỳ, không sửa/copy source repository, và API Builder tự tạo danh sách operation từ OpenAPI/.NET Roslyn theo routing đã chốt.
- Mapping local tái sử dụng theo Tool-QC project qua `project.local.json`, không bị Git theo dõi và không đi vào workspace/testcase/localStorage.
- .NET có OpenAPI hợp lệ dùng OpenAPI; .NET thiếu OpenAPI dùng Roslyn; Node.js chỉ OpenAPI 3.1 và limitation phải rõ.
- Không chạy source, không quét secret/dependency/build artifact/reparse escape, không remote/network fetch, không rò filesystem path/content qua observable output.
- Full QC có evidence build, harness, browser, containment, persistence, provider routing và documentation; Vision Test Loop không có visual reference nên BỎ theo trigger table.

## 7. Câu hỏi còn mở

