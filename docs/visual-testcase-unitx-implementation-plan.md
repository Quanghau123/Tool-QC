# Kế hoạch triển khai API Workbench, Visual Testcase Builder và UNITX

**Ngày lập:** 17/09/2026  
**Phạm vi:** Tool-QC Web UI, testcase contract, shared runner, generator và tầng UNITX mới  
**Trạng thái:** Kế hoạch đề xuất

## 1. Mục tiêu

Nâng cấp Tool-QC để tester hoặc người không biết lập trình có thể:

1. Khám phá và gọi API tương tự Swagger/Postman.
2. Click field request/response để lưu thành biến có tên rõ ràng.
3. Gán biến cho API khác từ danh sách A-Z, không viết cú pháp thủ công.
4. Tạo assertion, chuỗi API và testcase hoàn chỉnh bằng UI.
5. Preflight và chạy thử trước khi lưu chính thức.
6. Lưu testcase vào dự án và mở lại sau khi tắt Tool-QC.
7. Tạo kiểm thử UNITX cho logic nghiệp vụ bằng dữ liệu khai báo.

UI chỉ là lớp authoring. Testcase vẫn là file có thể review, version control và chạy trong CI.

## 2. Nền tảng hiện có và khoảng trống

### Đã có

- HTTP JSON, multipart form, auth profile và environment variables.
- Lưu response bằng JSONPath qua `step.save`; tái sử dụng bằng ``.
- Sequential chain truyền biến theo thứ tự testcase.
- Assertion status, field, regex, count, numeric range và JSON Schema cơ bản.
- Schema v2, coverage manifest và preflight.
- Generator Roslyn/AI, draft review, repair, run tạm và activate.
- Web UI chạy/dừng testcase, lịch sử, báo cáo và cấu hình.
- HTML/JUnit reporting, safety guard và secret redaction.

### Còn thiếu

- API Workbench tương tác theo từng request/response field.
- Response tree cho phép click để tạo variable/assertion.
- Variable Workspace trực quan.
- Form builder thay thế việc chỉnh JSON.
- Mở testcase đã lưu thành visual model để chỉnh sửa.
- Tầng UNITX và runtime cho business logic.

## 3. Nguyên tắc kiến trúc

### 3.1. Một execution contract

```text
Visual Builder
  -> SuiteSpec / CaseSpec / StepSpec
  -> testcase JSON
  -> TestcaseValidator
  -> AutoTest.Runner
  -> HTML/JUnit report
```

### 3.2. Ranh giới shared/project

- Khả năng dùng chung đặt trong shared framework.
- Business data đặt trong `projects/<project>/`.
- API testcase đặt trong `projects/<project>/testcases/`.
- UNITX spec đặt trong `projects/<project>/unitx/`.
- Không hard-code tên dự án trong shared code.

### 3.3. Ba trạng thái dữ liệu

1. **Session:** dữ liệu tạm khi thử API.
2. **Draft artifact:** auto-save dưới `.artifacts/` để preflight/run.
3. **Persisted testcase:** file chính thức dưới `projects/<project>/`.

### 3.4. An toàn

- Không lưu token, password, API key hoặc connection string vào testcase.
- Chỉ tham chiếu secret qua auth profile hoặc ``.
- Visual testcase vẫn phải đạt schema v2 và official preflight.
- Mọi request chịu production/destructive guard.
- Không tự lấy response thực tế làm expected result nếu người dùng chưa xác nhận.

## 4. Phạm vi chức năng

### 4.1. API Workbench

- Hiển thị endpoint theo module/controller/tag; tìm theo method, route hoặc action.
- Nhập path, query, header, JSON body, raw body và multipart form.
- Chọn auth profile; gửi request và xem status, duration, headers, body.
- Render request/response JSON dạng cây.
- Click field để lưu biến, tạo assertion hoặc sao chép JSONPath.
- Thêm request đã chạy vào testcase draft và giữ lịch sử phiên.

### 4.2. Variable Workspace

| Loại | Thời gian sống | Nơi lưu |
|---|---|---|
| Built-in | Một lần chạy | Runner |
| Environment | Lâu dài | `.env` |
| Case variable | Một testcase | `case.variables` |
| Saved variable | Case/chain | `step.save` |
| Session variable | Phiên UI | Draft state |

Mỗi biến có tên, giá trị rút gọn, type, scope, nguồn API/step, JSONPath và trạng thái secret. Tên biến phải khớp `^[A-Za-z][A-Za-z0-9_]*$`; UI gợi ý `createCustomer_customerId`, `createEvent_eventId`, `scanCart_cartId`.

Khi trùng tên, người dùng phải chọn đổi tên, ghi đè hoặc hủy. Danh sách biến sắp A–Z, có tìm kiếm và lọc theo type/scope/source.

### 4.3. Visual Testcase Builder

Quy trình:

1. Chọn và sắp xếp endpoint.
2. Nhập request hoặc gán biến.
3. Chạy thử request.
4. Chọn response field cần assertion hoặc lưu biến.
5. Tạo negative/auth/scope/lifecycle/concurrency cases.
6. Review coverage, preflight và chạy thử toàn chuỗi.
7. Lưu testcase chính thức.

Assertion hỗ trợ contract hiện có: status, exists, equals, notEquals, contains, regex, type, numeric range, count, oneOf, JSON Schema và response time.

Template không cần code gồm required/null/empty, min/max, enum/ID sai, ID không tồn tại, 401/403, sai scope, sai lifecycle, duplicate, idempotency và concurrent request.

### 4.4. Persisted Testcase Editor

- Mở testcase đã lưu thành visual model.
- Save, Save As, clone và diff trước khi ghi.
- Cảnh báo file bị thay đổi ngoài UI.
- Giữ field nâng cao mà visual editor chưa hỗ trợ.
- Advanced JSON mode phải parse và preflight lại sau khi sửa.

### 4.5. UNITX

UNITX v1 chỉ hỗ trợ logic pure/near-pure có input/output rõ ràng:

- Public scenario được đăng ký chủ động.
- Input/output JSON-serializable; hỗ trợ sync/async.
- Expected result hoặc expected exception; nhiều data rows.
- Fixed clock, actor context và dependency preset đơn giản.

Ngoài phạm vi v1: private method, mock tùy ý mọi interface, global state phức tạp, file/network trực tiếp, EF graph phức tạp và tự động gọi mọi production method.

## 5. Kiến trúc mục tiêu

```text
Tool-QC Web UI
├── API Explorer / Workbench
├── Variable Workspace
├── Visual Testcase Builder
├── Persisted Testcase Editor
└── UNITX Builder
       ├── API Visual Compiler -> SuiteSpec -> API Validator -> API Runner
       └── UNITX Compiler -> UnitXSpec -> UnitX Validator -> UnitX Runner
                                      └── Shared HTML/JUnit reporting
```

Draft API lưu tại `.artifacts/testcase-workbench/<project>/<draft-id>/draft.json`. Runner không nạp draft trực tiếp; server compile thành testcase JSON rồi mới preflight/run.

## 6. Contract dữ liệu đề xuất

### 6.1. Workbench draft

```json
{
  "version": 1,
  "project": "sample-service",
  "name": "Tạo và đọc khách hàng",
  "selectedEndpoints": [],
  "steps": [],
  "sessionVariables": [],
  "generatedSuite": {},
  "createdAt": "2026-09-17T00:00:00Z",
  "updatedAt": "2026-09-17T00:00:00Z"
}
```

### 6.2. UNITX spec v1

```json
{
  "schemaVersion": 1,
  "project": "sample-service",
  "scenario": "calculate-discount",
  "sourceVerification": {
    "files": ["src/Pricing/DiscountCalculator.cs"],
    "verifiedAt": "2026-09-17T00:00:00Z"
  },
  "cases": [{
    "id": "pricing.vip-10-percent",
    "name": "Khách VIP được giảm mười phần trăm",
    "input": { "customerLevel": "VIP", "amount": 1000000 },
    "context": { "now": "2026-09-17T00:00:00Z" },
    "expect": { "result": { "discount": 100000, "finalAmount": 900000 } }
  }]
}
```

## 7. Web API đề xuất

### Endpoint catalog và execution

```text
POST /api/projects/{project}/workbench/analyze
GET  /api/projects/{project}/workbench/endpoints
GET  /api/projects/{project}/workbench/endpoints/{endpointId}
POST /api/projects/{project}/workbench/execute
```

### Draft lifecycle

```text
POST   /api/projects/{project}/workbench/drafts
GET    /api/projects/{project}/workbench/drafts
GET    /api/projects/{project}/workbench/drafts/{draftId}
PUT    /api/projects/{project}/workbench/drafts/{draftId}
DELETE /api/projects/{project}/workbench/drafts/{draftId}
POST   /api/projects/{project}/workbench/drafts/{draftId}/compile
POST   /api/projects/{project}/workbench/drafts/{draftId}/preflight
POST   /api/projects/{project}/workbench/drafts/{draftId}/run
POST   /api/projects/{project}/workbench/drafts/{draftId}/activate
```

### Persisted testcase và UNITX

```text
GET  /api/projects/{project}/testcases/{caseId}/source
PUT  /api/projects/{project}/testcases/{caseId}
POST /api/projects/{project}/testcases/{caseId}/clone

POST /api/projects/{project}/unitx/analyze
GET  /api/projects/{project}/unitx/scenarios
POST /api/projects/{project}/unitx/drafts
POST /api/projects/{project}/unitx/drafts/{draftId}/preflight
POST /api/projects/{project}/unitx/drafts/{draftId}/run
POST /api/projects/{project}/unitx/drafts/{draftId}/activate
GET  /api/projects/{project}/unitx/specs
```

Server phải resolve file bằng safe path; không nhận đường dẫn tùy ý từ browser.

## 8. Thiết kế UI

### Điều hướng

- Testcase hiện có.
- API Workbench.
- Testcase Builder.
- UNITX.
- Kết quả.
- Cấu hình.

### API Workbench

```text
┌────────────────┬───────────────────────────────┬───────────────────┐
│ Endpoint list  │ Request / Response workspace  │ Variable Workspace│
│ GET /customers │ Path, query, header, body     │ A-Z, search/filter│
│ POST /customers│ Send và response tree         │ source/value/type │
└────────────────┴───────────────────────────────┴───────────────────┘
```

Response node có hành động tạo assertion, lưu biến, copy JSONPath, dùng làm expected value hoặc so sánh với request field. Request field nhận literal, saved variable, environment reference, unique value, relative time hoặc missing/null/empty.

Builder dùng bố cục: trái là step/order; giữa là request/expect editor; phải là variables/coverage/issues; cuối trang là preflight, chạy thử, xem JSON và lưu.

## 9. Kế hoạch triển khai theo giai đoạn

### Giai đoạn 0 — Chuẩn hóa contract (1–2 tuần)

- Inventory toàn bộ testcase schema hiện tại.
- Tạo fixture cho HTTP, form, auth, save, retry, concurrency và coverage.
- Tách service đọc/ghi testcase và preflight khỏi minimal API handler.
- Chuẩn hóa error envelope và naming convention.
- Xây round-trip parser/compiler tests.

**Hoàn thành khi:** testcase hiện tại parse/serialize không mất dữ liệu; generator/preflight/run/activate không regression.

### Giai đoạn 1 — Variable Workspace (2–3 tuần)

- Workbench execute service, JSON tree có JSONPath và session variable I/O.
- Safety guard và secret redaction.
- Endpoint selector, request form, response tree và modal lưu biến.
- Variable panel A–Z, picker và cảnh báo trùng tên/type mismatch.

**Hoàn thành khi:** gọi API A, lưu `$.data.id` thành `createA_id`, gán cho API B và gửi đúng giá trị.

### Giai đoạn 2 — Visual Testcase Builder MVP (3–5 tuần)

- Step list/reorder; request, assertion, save mapping và auth editor.
- Case metadata và coverage wizard cơ bản.
- Visual compiler sang schema v2.
- Preflight issue gắn đúng field UI.
- Run draft và activate.

**Hoàn thành khi:** tạo chuỗi create → get → update bằng UI; file đạt official preflight và chạy được bằng CLI.

### Giai đoạn 3 — Persisted Editor và Draft Recovery (2–3 tuần)

- Auto-save theo debounce; danh sách và restore draft.
- Mở testcase đã lưu thành visual model.
- Save, Save As, clone, diff và conflict detection.
- Preserve advanced/unknown fields.

**Hoàn thành khi:** tắt Tool-QC rồi mở lại vẫn tiếp tục draft; chỉnh testcase cũ không mất field nâng cao.

### Giai đoạn 4 — Negative/Auth/Scope/Lifecycle (3–4 tuần)

- Validation, 401/403 và auth-profile templates.
- Scope/tenant/owner và lifecycle/status scenarios.
- Integrity verification builder.
- Coverage assistant và evidence-step auto-linking.

**Hoàn thành khi:** không cho activate nếu coverage bắt buộc còn blocked.

### Giai đoạn 5 — Concurrency và Advanced Steps (3–4 tuần)

- Retry, `parallelRequests` và `concurrentRequests` editor.
- PostgreSQL, Redis, MQTT, HTTP stub và Elasticsearch editor.
- JSON Schema editor cơ bản.

**Hoàn thành khi:** tạo được last-stock/idempotency case và HTTP + data verification bằng UI.

### Giai đoạn 6 — UNITX Foundation (4–6 tuần)

Tạo:

```text
src/AutoTest.UnitX.Abstractions/
src/AutoTest.UnitX.Core/
src/AutoTest.UnitX.TestValidation/
runner/AutoTest.UnitX.Runner/
```

Adapter đề xuất:

```csharp
public interface IUnitXScenario
{
    string Name { get; }
    Type InputType { get; }
    Type OutputType { get; }
    Task<object?> ExecuteAsync(
        object? input,
        UnitXExecutionContext context,
        CancellationToken cancellationToken);
}
```

Backend đăng ký adapter trong test-support project riêng; Tool-QC không tự gọi tùy ý production method.

**Hoàn thành khi:** chạy calculation scenario, nested result comparison, expected exception và HTML/JUnit report.

### Giai đoạn 7 — UNITX Visual Builder (3–4 tuần)

- Scenario catalog và form sinh từ input schema.
- Expected result tree, exception selector và data table nhiều case.
- Fixed time, actor context và dependency preset.
- Preflight, run, activate và mở lại spec.

### Giai đoạn 8 — CI và Coverage (2–3 tuần)

- CLI chạy UNITX theo project/tag/scenario.
- Publish JUnit; Coverlet/Cobertura mapping.
- Hiển thị source line/branch chưa cover.
- CI templates và regression suite cho visual compiler.

Testcase viết tay, generator sinh và UI tạo đều chạy bằng cùng runner.

## 10. Phân rã module dự kiến

```text
runner/AutoTest.Web/
├── Program.cs
├── Workbench/
│   ├── EndpointCatalogService.cs
│   ├── WorkbenchExecutionService.cs
│   ├── WorkbenchDraftService.cs
│   ├── VisualSuiteCompiler.cs
│   ├── TestcaseFileService.cs
│   └── Models.cs
└── UnitX/
    ├── UnitXCatalogService.cs
    ├── UnitXDraftService.cs
    └── Models.cs

runner/AutoTest.Web/wwwroot/
├── app.js
├── app.css
├── workbench/
│   ├── api-workbench.js
│   ├── request-editor.js
│   ├── response-tree.js
│   ├── variable-workspace.js
│   ├── testcase-builder.js
│   ├── coverage-editor.js
│   └── draft-store.js
└── unitx/
    ├── unitx-builder.js
    └── unitx-result-editor.js
```

Không tiếp tục dồn feature mới vào `Program.cs` và `app.js` hiện tại.

## 11. Chiến lược kiểm thử

### Framework tests

- Variable name, trùng tên và JSONPath save mapping.
- Visual model → SuiteSpec và reverse parsing.
- Round-trip không mất field nâng cao.
- Safe path, secret redaction và draft conflict.
- Coverage evidence mapping.
- UNITX result và expected-exception comparison.

### Web API tests

- CRUD draft và cross-project isolation.
- Chặn path traversal.
- Không activate khi preflight lỗi.
- Production/destructive guard.
- Không lộ secret trong response/history.

### UI acceptance

- Create → get bằng saved ID.
- Hai API cùng trả `id` nhưng lưu hai alias.
- Variable list đúng A–Z và có type warning.
- Refresh/restore draft.
- Open/edit/Save As testcase cũ.
- Preflight issue dẫn đúng field.
- Run draft rồi activate.

### Regression bắt buộc

- Build `AutoTest.Web` và `AutoTest.Runner`.
- Chạy `AutoTest.FrameworkTests`.
- Chạy generator/preflight fixtures.
- Chạy smoke không destructive khi môi trường khả dụng.

## 12. Bảo mật và vận hành

- Không trả secret thực về browser sau khi đã lưu.
- Không ghi Authorization header hoặc login password vào artifact.
- Redact token/password/secret/apiKey/connectionString.
- Chặn `..`, rooted path và encoded traversal.
- Giới hạn kích thước request/response, số draft và thời gian retention.
- Không cho UNITX load arbitrary assembly/path từ request.
- Audit project, draft ID, output file, time, source evidence và run ID; không audit secret value.

## 13. Tương thích ngược

- Testcase hiện tại tiếp tục chạy không đổi.
- Schema v1 chỉ read-only hoặc migrate có xác nhận; testcase mới dùng schema v2.
- Generator draft hiện tại mở được trong Visual Builder.
- Endpoint cũ chỉ deprecate sau khi UI mới đạt feature parity.
- UNITX dùng thư mục và runner riêng, không trộn với API testcase.

## 14. Ước lượng

| Giai đoạn | Ước lượng |
|---|---:|
| 0. Chuẩn hóa contract | 1–2 tuần |
| 1. Variable Workspace | 2–3 tuần |
| 2. Builder MVP | 3–5 tuần |
| 3. Persisted Editor | 2–3 tuần |
| 4. Negative/Auth/Scope | 3–4 tuần |
| 5. Advanced Steps | 3–4 tuần |
| 6. UNITX Foundation | 4–6 tuần |
| 7. UNITX Builder | 3–4 tuần |
| 8. CI/Coverage | 2–3 tuần |

Tổng tuần tự khoảng **23–34 tuần** cho nhóm nhỏ. UNITX Foundation có thể chạy song song sau giai đoạn 0, nhưng chỉ nên có một Visual Builder architecture.

## 15. Mốc phát hành

- **Release A — API Workbench MVP:** gọi API, response tree, session variable và picker A–Z.
- **Release B — No-code Testcase MVP:** happy path chain, assertion/save, preflight, run và activate.
- **Release C — Testcase Authoring:** persisted editor, recovery, negative/auth/scope/lifecycle/concurrency và advanced steps.
- **Release D — UNITX MVP:** adapter, spec, validator, runner, report và visual builder.
- **Release E — Quality Platform:** CI, coverage và source/API/UNITX traceability.

## 16. Definition of Done

Một feature chỉ hoàn thành khi:

- Contract và tài liệu đã cập nhật.
- Có server-side validation.
- Có framework tests cho logic quan trọng.
- Có safe-path và secret-redaction tests.
- Build không lỗi và không phá testcase hiện tại.
- Có UI acceptance cho happy path và failure path.
- Báo cáo phân biệt PASS, FAIL, NOT_RUN và BLOCKED.
- Không gọi preflight/build là testcase PASS.

## 17. Rủi ro chính

| Rủi ro | Mức | Giảm thiểu |
|---|---|---|
| UI model lệch testcase schema | Cao | Một compiler duy nhất và round-trip fixtures |
| Mất field nâng cao khi save | Cao | Preserve unknown fields và diff |
| Lưu secret vào testcase | Cao | Server-side detection và env reference |
| Dependency thứ tự bị ẩn | Cao | Dependency graph và explicit chain mode |
| Generator đoán sai nghiệp vụ | Cao | Source evidence, blocked coverage và review |
| UNITX cố hỗ trợ mọi method | Cao | Scenario adapter và giới hạn v1 |
| UNITX load code không an toàn | Cao | Allowlist và isolated host |
| `Program.cs`/`app.js` tiếp tục phình | Trung bình | Tách module trước khi mở rộng |
| Artifact tăng không giới hạn | Trung bình | Retention và size limit |

## 18. Backlog ưu tiên

### P0

- Visual model/compiler và draft storage an toàn.
- Response tree có JSONPath.
- Variable Workspace và picker.
- Preflight error mapping và activate testcase.

### P1

- Persisted editor và draft recovery.
- Assertion builder và coverage wizard đầy đủ.
- Validation/auth/scope templates và dependency graph.

### P2

- Database/Redis/MQTT/stub editor.
- Advanced JSON Schema.
- UNITX Foundation/Builder và coverage mapping.

## 19. Quyết định cần chốt trước khi code

1. Endpoint catalog dùng source analyzer, OpenAPI hay kết hợp cả hai.
2. Draft auto-save mặc định bật hay opt-in.
3. Retention của `.artifacts/testcase-workbench`.
4. Có cho ghi đè biến hay luôn yêu cầu alias mới.
5. Cách biểu diễn query parameter nếu cần mở rộng request contract.
6. UNITX adapter dùng assembly riêng hay test-host executable.
7. Mức process isolation của UNITX.
8. V1 chỉ local single-user hay cần concurrent editing.

## 20. Vertical slice nên làm đầu tiên

```text
Chọn POST endpoint
  -> nhập request
  -> gửi request
  -> xem response tree
  -> lưu $.data.id thành createEntity_id
  -> chọn GET endpoint
  -> gán createEntity_id vào path
  -> tạo assertion
  -> compile schema v2
  -> preflight
  -> chạy thử
  -> lưu projects/<project>/testcases/generated/*.json
```

Vertical slice này kiểm chứng endpoint catalog, request editor, response tree, variable workspace, visual compiler, preflight, runner và persistence trước khi mở rộng sang negative cases và UNITX.
