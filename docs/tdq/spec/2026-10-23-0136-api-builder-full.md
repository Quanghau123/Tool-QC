# SPEC — Hoàn thiện API Builder no-code
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

Ngày: 2026-10-23 · Bản: 1.0 · Brief: ../brief/2026-10-23-0136-api-builder-full.md · Lane: full
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

- Mục tiêu: hoàn thiện API Builder thành trải nghiệm no-code cho tester nội bộ: chọn operation từ contract thực, dựng step bằng cây field động, lưu response field thành biến và bind có kiểm tra vào request của step sau, sau đó đi qua lifecycle schema-v2 hiện hữu.
- Trong phạm vi:
  - OpenAPI 3.1 là nguồn contract ưu tiên; Roslyn làm enrichment provenance/risk và fallback khi không có OpenAPI hợp lệ.
  - Projection contract dùng chung cho operation, parameter theo location và cây request/response lồng nhau, có type, requiredness, collection, JSON path và provenance ổn định.
  - API Builder chọn operation, thêm/xóa/sắp xếp step, hiển thị request/response tree, tạo biến từ response và bind vào path/query/header/body của request sau.
  - Workspace lưu contract fingerprint/provenance, operation ID và typed binding; phát hiện contract drift/revision conflict mà không tự đoán hoặc ghi đè.
  - Compiler chuyển workspace về schema-v2 canonical, dùng `save`/`${variable}` hiện hữu và map đúng path/query/header/body.
  - Browser E2E bằng `@playwright/test` ở dev dependency, chạy Chromium cô lập cho journey create → select operation → save variable → bind → save → reload → compile/preflight.
  - Build, framework regression và kiểm chứng browser thực; tài liệu public cập nhật khi API Builder contract thay đổi.
- NGOÀI phạm vi:
  - Không thay runner, `${variable}`, JSONPath, schema-v2, coverage evidence, preflight/activate hoặc guard production/destructive hiện hữu.
  - Không tạo parser OpenAPI/Roslyn thứ hai, không thêm UI framework, và không thêm dependency production cho `AutoTest.Web`.
  - Không tự chạy testcase destructive, không đọc/in secret `.env`, không lưu plaintext response nhạy cảm, không thêm cloud/multi-user ownership.
  - UnitX và Phase 2 source .NET/xUnit không thuộc request này.

## 1b. Lộ trình

| Bước/phase | Chạy? | Vì sao |
|---|---|---|
| Phân tích | CÓ | Đã xác nhận adapter OpenAPI/Roslyn, workspace/compiler và UI step mẫu là khoảng trống chính. |
| Nghiên cứu nguồn chuẩn | CÓ | Kiểm chứng cách cài/chạy Playwright; dùng contract hiện hữu làm boundary. |
| Spec | CÓ | Chốt projection, binding, drift, E2E và evidence trước code. |
| Plan | CÓ sau khi duyệt spec | Chia task contract → workspace/compiler → UI → E2E theo dependency. |
| Implement | CÓ sau khi duyệt plan | Làm trực tiếp hoặc theo mode người dùng chọn ở cổng kế hoạch. |
| QC Full | CÓ | Chạy build, framework regression, browser E2E và probe lifecycle. |

## 2. Đầu ra cụ thể

| # | Đầu ra | Đường dẫn/vị trí | Đo "xong" bằng |
|---|---|---|---|
| 1 | Builder contract projection | `runner/AutoTest.Generator/` | OpenAPI 3.1 hoặc Roslyn fallback trả operation và field tree cùng DTO; contract có source/provenance/fingerprint/diagnostic. |
| 2 | Contract endpoint và drift guard | `runner/AutoTest.Web/` | Workspace chỉ nhận contract đã chọn; fingerprint đổi thì trả diagnostic review, không tự map field mới. |
| 3 | Workspace/compiler typed | `runner/AutoTest.Web/` | Typed binding chỉ cho phép response field từ step trước, tương thích target type/location; compile map path/query/header/body đúng schema-v2. |
| 4 | API Builder UI động | `runner/AutoTest.Web/wwwroot/` | Không còn default step mẫu; tester chọn operation, thấy trees, tạo variable và bind không cần chỉnh JSON. |
| 5 | Browser E2E | `package.json`, test tooling | Chromium chạy journey thật create/save/reload/compile; evidence là output Playwright hiện tại. |
| 6 | Regression và hướng dẫn | tests, `README.md`, `.env.example` nếu cần | Framework/build/E2E pass; tài liệu không hứa auto-publish hoặc coverage không có evidence. |

## 2b. Ranh giới module

| Module | Vùng file | Phụ thuộc module | Đầu ra §2 |
|---|---|---|---|
| Generator contract | `runner/AutoTest.Generator/` | Roslyn analyzer và OpenAPI adapter; không phụ thuộc Web | 1 |
| API Builder domain | `runner/AutoTest.Web/ApiBuilderWorkspace.cs`, `ApiBuilderCompiler.cs` | Generator projection qua facade request/response, schema-v2 JSON; không gọi transport executor | 2, 3 |
| Web facade/UI | `runner/AutoTest.Web/Program.cs`, `wwwroot/` | Builder domain + existing generator/draft endpoints | 2, 4 |
| Browser verification | `package.json`, E2E test files | Launches Web externally; không phải production Web dependency | 5 |
| Regression/docs | `tests/AutoTest.FrameworkTests/`, `README.md`, `docs/` | các module trên theo phạm vi kiểm/tài liệu | 6 |

`OpenApiContractAdapter` và `SourceContractAnalyzer` đã chuẩn hóa vào `GeneratorAnalysis`; projection Builder phải nằm cùng Generator để Web không fork parser. `Program.cs` là composition root endpoint/draft lifecycle; workspace/compiler tiếp tục là facade trước canonical schema-v2. Browser test chỉ drive HTTP/UI như user, không tham chiếu assembly Web.

## 3. Cách tiếp cận & lý do

- Chọn: thêm projection Builder từ `GeneratorAnalysis`, không để Web đọc OpenAPI hoặc source trực tiếp.
- Vì: một model chuẩn giữ OpenAPI/Roslyn có output cùng hình dạng, chẩn đoán nguồn rõ và không nhân đôi semantic parser.
- Chọn: OpenAPI 3.1 ưu tiên; Roslyn bổ sung provenance/risk khi có source và fallback khi OpenAPI không hợp lệ/không được chọn.
- Vì: OpenAPI mô tả operation/schema tốt nhất cho form; Roslyn có authorization, service call, validation và branch evidence.
- Chọn: workspace lưu `operationId`, fingerprint và binding `sourceVariable → targetFieldId`, field có location/type/path ổn định.
- Vì: binding theo tên hoặc JSON string tự do dễ sai thứ tự/type/location và không thể phát hiện drift.
- Chọn: compiler là nơi duy nhất chuyển target location thành request path/query/header/body.
- Vì: giữ UI declarative và tái dùng runner/preflight/activation thay vì tạo request executor thứ hai.
- Chọn: dùng Playwright ở `devDependencies` cho browser E2E.
- Vì: Node/npm đã có; Playwright cung cấp runner và Chromium cô lập, không đưa dependency vào Web production. Không cần UI framework hoặc parser ngoài.
- Đã loại: Selenium .NET — thêm package/browser binding vào solution cho năng lực chỉ phục vụ E2E, trong khi Node test boundary đã có.
- Đã loại: chỉ framework/API test — không chứng minh được journey tester không cần JSON.

## 3b. Năng lực & công cụ

| Skill/công cụ | Nguồn | Phán quyết | Dùng ở đâu / Lý do loại |
|---|---|---|---|
| `tdq-workflow:tdq-intake` | plugin TDQ | NỀN | Điều phối deep analysis, approval và state. |
| `tdq-workflow:tdq-spec` | plugin TDQ | NỀN | Chốt output/boundary/QC trước plan. |
| `tdq-workflow:tdq-plan` | plugin TDQ | NỀN | Biến spec thành task có check và file zone. |
| `tdq-workflow:tdq-lean` | plugin TDQ | DÙNG | Giữ một projection/parser/compiler, không tạo DSL/runtime mới. |
| `dotnet-standards:api-surface` | plugin | DÙNG | Thiết kế endpoint contract/workspace diagnostics an toàn. |
| `dotnet-standards:dotnet-testing` | plugin | DÙNG | Thiết kế framework tests và boundary E2E. |
| Playwright Test | external dev dependency | DÙNG | Browser E2E Chromium cô lập; chỉ thêm sau approval plan. |
| UI framework/parser ngoài | external | KHÔNG | spec §3 đã chọn cách khác tốt hơn: DOM hiện hữu và .NET parser chuẩn hóa contract; thêm dependency không tạo output mới. |

## 4. Yêu cầu bắt buộc

- Endpoint contract chỉ nhận source/path OpenAPI và source directory từ boundary đã kiểm tra; mọi path containment/sanitization, error diagnostic và timeout phải rõ, không scan hoặc đọc `.env`.
- Contract DTO phải biểu diễn operation ID, HTTP method/path, parameter location, field ID, name/json name, type/format/enum, required/nullability/collection, JSONPath và provenance/fingerprint; tree phải có giới hạn depth/node xác định.
- Không hiện response value trong contract; preview biến tiếp tục redaction theo tên field nhạy cảm. Field nhạy cảm không được bind vào authorization/header credential hoặc persist plaintext.
- Workspace chỉ cho phép một project binding, revision optimistic concurrency và operation/contract fingerprint rõ; contract drift phải yêu cầu chọn lại/review, không tự sửa target JSONPath.
- Binding phải trỏ field response có thật của step trước, source/target type tương thích theo bảng allowlist xác định, target field writable và location hợp lệ; dùng biến trước khi save bị từ chối.
- Compiler không suy đoán default value; phải map request location chính xác và giữ canonical schema-v2, coverage/preflight/activate/SHA-256 guard hiện có.
- UI dùng tiếng Việt, keyboard-accessible label/status, trạng thái loading/error/recovery; không cần JSON editor cho happy path.
- Playwright chỉ là dev dependency; test khởi động host local, dùng dữ liệu/contract fixture không secret và dừng host sau run.
- Mỗi phần có test Given–When–Then; build/test/browser result báo tách biệt, không coi browser thiếu là PASS.

## 5. Ràng buộc & rủi ro

| Rủi ro | Ảnh hưởng | Cách giảm |
|---|---|---|
| OpenAPI/source drift | Chọn field/path cũ rồi compile sai | Fingerprint/provenance, diagnostic và yêu cầu review workspace. |
| Tree lớn/recursive | UI chậm hoặc stack overflow | Bounded depth/node, collapse lazy UI và error rõ khi vượt ngưỡng. |
| Binding sai type/location | Request invalid hoặc lộ credential | Allowlist typed binding, source-before-target, cấm sensitive/credential field. |
| OpenAPI thiếu metadata business | Testcase thiếu auth/validation/risk | Roslyn enrichment/fallback; coverage vẫn blocked nếu evidence thiếu. |
| Playwright/browser thiếu | Không có click-through evidence | Cài browser qua Playwright trong test setup; nếu platform chặn thì báo BLOCKED/ENVIRONMENT_BLOCKER. |
| Contract endpoint đọc path tùy ý | File disclosure hoặc scan lớn | Containment/policy source path, no `.env`, budget/diagnostic; không follow symlink ngoài allowed root. |
| Thay runtime cũ | Regression/safety bypass | Compiler tái dùng schema-v2/preflight/activate, không sửa HTTP executor. |

## 6. QC & Definition of Done

| # | Hạng mục kiểm | Điều kiện PASS | Đo trước | Dự phòng nếu trượt |
|---|---|---|---|---|
| Q1 | Contract projection | Given OpenAPI và Roslyn fixture, When lấy builder contract, Then operations/field tree/provenance/fingerprint có DTO hợp lệ; input lỗi có diagnostic. | Framework test | Sửa projection/diagnostic, không fallback suy đoán. |
| Q2 | Contract drift/boundary | Given workspace chọn operation, When fingerprint/source/path sai hoặc contract đổi, Then save/compile bị chặn để review và không đọc ngoài boundary. | Framework/API test | Sửa guard/containment. |
| Q3 | Typed binding | Given response string/int/id và request target, When bind, Then chỉ source trước/type-location tương thích được lưu; invalid/sensitive/type mismatch fail rõ. | Framework test | Sửa validation matrix. |
| Q4 | Compiler mapping | Given path/query/header/body binding, When compile, Then draft schema-v2 đặt interpolation đúng request location và preflight basic pass. | Framework/API test | Sửa compiler, không đổi runner expectation. |
| Q5 | UI operation/tree journey | Given tester không biết JSON, When chọn operation/add step/save variable/bind, Then tree/status/catalog hiển thị tiếng Việt và không còn step demo. | Playwright Chromium | Sửa UI/selector/accessibility. |
| Q6 | Reload/recovery | Given saved workspace, When reload hoặc revision/drift conflict, Then UI restore đúng hoặc hướng dẫn review, không overwrite. | Playwright + framework test | Sửa state/recovery. |
| Q7 | Lifecycle | Given valid draft, When compile → preflight, Then existing draft lifecycle hoạt động và coverage block vẫn không cho activate chính thức. | API runtime test | Sửa facade; không weaken coverage. |
| Q8 | Regression | Given code changed, When build + framework regression, Then zero error và toàn suite pass. | `dotnet build` + framework runner | Fix loop tối đa ba vòng. |
| Q9 | Browser evidence | Given local Web host, When Playwright run Chromium, Then report có journey pass hiện tại và screenshot/trace khi fail. | `npm` E2E script | ENVIRONMENT_BLOCKER nếu browser install/host permission bị chặn. |

DoD:
- Contract projection, workspace, compiler và UI dùng cùng một contract/provenance model; không có parser/runtime/DSL thứ hai.
- Operation chooser và field tree phủ path/query/header/body cùng nested/collection metadata trong supported depth/budget.
- Typed binding chặn use-before-save, field không tồn tại, drift, type/location mismatch và sensitive credential target; tất cả có evidence test.
- Compiler vẫn sinh schema-v2 canonical và không bypass coverage/preflight/activate/SHA-256 protection.
- Playwright nằm ở dev dependency, chạy browser journey thật; browser thiếu/chặn luôn báo BLOCKED hoặc ENVIRONMENT_BLOCKER.
- Build, framework regression, browser E2E và runtime/preflight được báo bằng evidence tách biệt; README/public contract chính xác.

## 7. Câu hỏi còn mở
