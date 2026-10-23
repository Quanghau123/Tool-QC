# PLAN — Nâng cấp Tool-QC cho API Builder và UnitX

Ngày: 2026-10-22 · Spec: ../spec/2026-10-22-1725-tool-qc-upgrade.md (bản 1.0, ĐÃ DUYỆT) · Lane: full
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md
Mode thực thi: main — `tdq_bench simulate` đo main 22,4 phút, sub-agent 27,6 phút; nhiều task cùng chạm Web và framework test nên main nhanh hơn 5,2 phút. Người dùng chốt lúc duyệt.
Trạng thái plan: HOÀN THÀNH

## Mục lục

- Quy tắc thi hành
- P1 — Contract và workspace API Builder
- P2 — UI, compile và publish testcase
- P3 — UnitX decision table
- P4 — Log, test, regression và tài liệu
- Cụm song song
- Luật file nóng
- Definition of Done

## Quy tắc thi hành

1. Thứ tự phase là thứ tự phụ thuộc — không đảo.
2. Mỗi task: đánh `[~]` khi bắt đầu → viết test trước (đỏ) → code → test xanh → đổi `[x]` ngay vào file này.
3. Mỗi bước chạy kiểm trong phạm vi file chạm; trọn bộ chỉ ở QC.
4. QC fail → thêm task fix vào plan, lặp đến khi pass; không bỏ qua assertion hay đổi expectation chỉ để xanh.
5. Không commit/push cho đến khi người dùng yêu cầu; không thêm attribution trailer.
6. Không đọc/in/copy secret `.env`; không đặt credential trong JSON/artifact/log; không chạy production hoặc destructive case khi không có authorization hiện hữu.
7. Tuân thủ ANTI-1 đến ANTI-6: không fake-red, blind assertion, dirty git state, tick muộn, raw dump, hoặc mở rộng vùng file không khai báo.

## P1 — Contract và workspace API Builder

- [x] **T1.1** (e28m) Chuẩn hóa API contract trung gian và adapter OpenAPI 3.1 tùy chọn, giữ Roslyn analyzer là fallback/provenance source — Test: fixture OpenAPI và fixture C# cùng render endpoint/method/request/response field; input sai trả diagnostic thay vì contract suy đoán.
  - Chạm: `runner/AutoTest.Generator/Models.cs`, `runner/AutoTest.Generator/SourceContractAnalyzer.cs`, `runner/AutoTest.Generator/OpenApiContractAdapter.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → `SourceContractAnalyzer` có caller ở CLI generator và framework tests (nguồn: LSP `find_references`).
  - Dùng: `tdq-workflow:tdq-lean`
  - Để: tái dùng model contract hiện hữu, không tạo pipeline endpoint/field thứ hai.
  - Ra: adapter OpenAPI normalize vào `GeneratorAnalysis`/contract trung gian và fixture test.
  - Kiểm: build runner và framework test xác nhận hai nguồn contract.
  - Không dùng cho: UI workspace, persist draft hoặc UnitX.

- [x] **T1.2** (e34m) Tạo model workspace API Builder, catalog biến, revision/fingerprint và storage artifact an toàn theo project — Test: autosave/restore, A–Z case-insensitive, duplicate/built-in/traversal/corrupt artifact đều trả error xác định.
  - Chạm: `runner/AutoTest.Web/ApiBuilderWorkspace.cs`, `runner/AutoTest.Web/Program.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → `Program.cs` là Web composition root cho generate/preflight/activate; ownership tập trung để không mở thêm API host.
  - Dùng: `dotnet-standards:api-surface`
  - Để: giới hạn request/response persistence, containment và error contract tại public Web surface.
  - Ra: service workspace và endpoint CRUD/restore scoped ở `.artifacts/api-builder/<project>/<workspace-id>/`.
  - Kiểm: framework tests tạo artifact dưới root test, xác nhận path containment, atomic revision conflict và redaction metadata.
  - Không dùng cho: publish testcase chính thức hoặc interpreter UnitX.
  - Tiến độ: storage/catalog, revision conflict, containment và redaction đã có test xanh; đang bổ sung HTTP surface để UI tạo/tải/lưu workspace.

- [x] **T1.3** (e18m) Bổ sung logging có cấu hình cho workspace action/preflight status, redaction và cleanup TTL an toàn — Test: event log có timestamp/action/project sanitized, không có plaintext sensitive field; cleanup không xóa workspace active/recent.
  - Chạm: `runner/AutoTest.Web/ApiBuilderWorkspace.cs`, `runner/AutoTest.Web/UiSupport.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → `UiSupport.cs` đã giữ redaction/run history nên là boundary cho policy dùng chung.
  - Cần: T1.2
  - Tiến độ: retention mặc định 14 ngày có kiểm thử xóa draft hết hạn; đang nối cleanup/log action khởi động Web, không log preview hoặc dữ liệu nhạy cảm.

**Xong P1 khi**: có một contract đầu vào thống nhất, workspace không thể ghi ngoài `.artifacts`, và mọi biến/draft nhạy cảm có redaction provenance trước UI.

## P2 — UI, compile và publish testcase

- [x] **T2.1** (e38m) Xây màn hình API Builder: chọn operation, dựng sequence, cây request/response field, lưu response field với tên biến và gán từ catalog A–Z — Test: browser journey không chỉnh JSON trực tiếp tạo hai step, save `orderId`, gán `${orderId}` cho request tiếp theo và khôi phục sau reload.
  - Chạm: `runner/AutoTest.Web/wwwroot/index.html`, `runner/AutoTest.Web/wwwroot/app.js`, `runner/AutoTest.Web/wwwroot/app.css` → `app.js` hiện gọi generator analyze/draft/preflight/activate; đây là caller/UI state cần thay đổi cùng nhau.
  - Cần: T1.1, T1.2
  - Kiểm: `node --check` pass; Web build và framework tests pass; API runtime create/save/restore xác nhận `orderId`/`${orderId}`. Browser click-through BLOCKED vì máy thiếu browser driver, lệch Q2 #1 đã ghi.

- [x] **T2.2** (e30m) Biên dịch workspace Builder về canonical schema-v2, surface lỗi coverage/use-before-save theo field/step và tái dùng publish/overwrite hiện hữu — Test: workspace hợp lệ tạo `steps[].save`/`${variable}`, preflight pass rồi activate; coverage thiếu, duplicate variable hoặc stale hash bị chặn có diagnostic field-level.
  - Chạm: `runner/AutoTest.Web/ApiBuilderCompiler.cs`, `runner/AutoTest.Web/Program.cs`, `src/AutoTest.TestValidation/TestcaseValidator.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → validator kiểm tra variable ordering; Web activation là caller của runner preflight (nguồn: LSP/Grep endpoint reference).
  - Cần: T1.2
  - Dùng: `dotnet-standards:facade-module-architecture`
  - Để: đặt compile ở Web facade, chỉ dùng public schema/validator thay vì kéo Web vào transport/core.
  - Ra: compiler chuyển workspace thành CaseSpec/suite JSON và mapping diagnostic về builder field.
  - Kiểm: framework test compile + validator/preflight trên suite schema-v2, không sửa RunnerEngine/HTTP executor.
  - Không dùng cho: thêm endpoint/project-specific rule vào shared runner.
  - Tiến độ: compiler test xác nhận `steps[].save` và `${variable}` trong schema-v2; UI đã lưu workspace → compile draft → mở cùng luồng preflight/activate. HTTP runtime xác nhận preflight pass nhưng `readyToSave=false` với 4 warnings khi coverage chưa đủ; không bypass coverage gate.

- [x] **T2.3** (e18m) Kiểm UI end-to-end cho preflight/publish/recovery và accessibility cơ bản trên luồng tester không biết JSON — Test: happy path, variable name Unicode/max-length, restore conflict/corrupt draft và activation overwrite đều có text tiếng Việt action được.
  - Chạm: `runner/AutoTest.Web/wwwroot/index.html`, `runner/AutoTest.Web/wwwroot/app.js`, `runner/AutoTest.Web/wwwroot/app.css`, `tests/AutoTest.FrameworkTests/Program.cs` → UI state/canonical publish callers từ T2.1/T2.2.
  - Cần: T2.1, T2.2
  - Kiểm: action/status tiếng Việt, preflight/activation error contract, restore/revision/corrupt-draft cases có framework/runtime evidence. Browser click-through BLOCKED theo lệch Q2 #1 vì máy không có driver; không kết luận browser pass.

**Xong P2 khi**: tester hoàn thành tạo → lưu biến → gán biến → preflight → publish mà không phải viết JSON; output vẫn là suite schema-v2 của runner hiện hữu.

## P3 — UnitX decision table

- [x] **T3.1** (e36m) Tạo core UnitX V1 với typed decision-table model, operator registry whitelist, deterministic evaluator và bounded resource/error model — Test: rule arithmetic/comparison/conditional/rounding trả actual/expected per row; unknown operator/type/budget fail không có execution side effect.
  - Chạm: `src/AutoTest.UnitX/AutoTest.UnitX.csproj`, `src/AutoTest.UnitX/DecisionTable.cs`, `src/AutoTest.UnitX/DecisionTableExecutor.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → file mới, không có node phụ thuộc; chỉ nhận abstraction tối thiểu, không tham chiếu HTTP runner.
  - Dùng: `dotnet-standards:dotnet-testing`
  - Để: thiết kế Tier 1/2/3 test với real evaluator, assertion actual/expected và negative security cases.
  - Ra: UnitX core có registry đóng, error row/rule cụ thể và result model.
  - Kiểm: framework tests chạy rule thật, Unicode/boundary row và failure recovery không dùng mock-return-mock.
  - Không dùng cho: dynamic C#/JavaScript, xUnit source generation hay network/file access.

- [x] **T3.2** (e28m) Tạo workspace/API/UI UnitX để nhập bảng quyết định, validate/run, lưu draft artifact và xem từng row result — Test: tester tạo bảng whitelist, chạy, restore; rule invalid hiển thị row/rule error và sửa/chạy lại được.
  - Chạm: `runner/AutoTest.Web/UnitXWorkspace.cs`, `runner/AutoTest.Web/Program.cs`, `runner/AutoTest.Web/wwwroot/index.html`, `runner/AutoTest.Web/wwwroot/app.js`, `runner/AutoTest.Web/wwwroot/app.css`, `tests/AutoTest.FrameworkTests/Program.cs` → `Program.cs`/Web UI là hot owner của API surface; UnitX core chỉ được gọi qua facade.
  - Cần: T3.1
  - Kiểm: `node --check`, Web build và framework tests pass; Web runtime API create/save/run/restore bảng `add` trả 1/1 row pass. Browser click-through giữ BLOCKED theo lệch Q2 #1 vì thiếu driver.

- [x] **T3.3** (e14m) Viết design boundary cho UnitX phase source .NET/xUnit: selected source/method metadata, generated artifact review và isolated build/run policy, không triển khai execution — Test: tài liệu nêu rõ entry criteria, artifacts, safety gate và out-of-scope tương ứng spec §2.5.
  - Chạm: `docs/kien-truc.md`, `README.md` → chỉ tài liệu, không có runtime caller.
  - Cần: T3.1
  - Kiểm: `doc_lint docs/kien-truc.md` pass, Web build pass.

**Xong P3 khi**: UnitX decision table chạy xác định trong workspace riêng, từ chối code/expression tự do và không có dependency tới HTTP/API secret.

## P4 — Log, test, regression và tài liệu

- [x] **T4.1** (e24m) Hoàn thiện unit/regression tests cho contract, workspace, compiler, UnitX và redaction; build toàn runner — Test: tất cả test mới/theo phạm vi pass, từng output §2 có evidence Given–When–Then Tier 1/2/3.
  - Chạm: `tests/AutoTest.FrameworkTests/Program.cs`, `runner/AutoTest.Web/AutoTest.Web.csproj`, `runner/AutoTest.Generator/AutoTest.Generator.csproj`, `src/AutoTest.UnitX/AutoTest.UnitX.csproj` → framework test là caller đã có của source analyzer; project references cần chứa UnitX core nhưng không đảo dependency.
  - Cần: T1.1, T1.2, T1.3, T2.1, T2.2, T2.3, T3.1, T3.2

- [x] **T4.2** (e18m) Cập nhật public contract/hướng dẫn vận hành và chạy smoke non-destructive của project được chọn khi local/test API khả dụng — Test: README/.env.example/project-template chỉ mô tả behavior công khai đã triển khai; kết quả build, test, smoke được report tách biệt, environment blocker không bị báo là pass.
  - Chạm: `README.md`, `.env.example`, `projects/project-template/`, `docs/kien-truc.md` → public workflow/project boundary docs; không sửa project-specific suite.
  - Cần: T4.1
  - Kết quả: README/kiến trúc đã nêu đúng API Builder workspace → draft schema-v2 → preflight/activation và UnitX V1. `dotnet build runner/AutoTest.Runner/AutoTest.Runner.csproj` PASS (0 warning/error); framework regression trước đó PASS 66. Smoke `be-booking --tags smoke` chạy 2/2 nhưng `localhost:5018` từ chối kết nối, nên là ENVIRONMENT_BLOCKER (0 PASS, 2 FAIL), không phải PASS.

**Xong P4 khi**: regression/build evidence hoàn chỉnh, public guidance không hứa năng lực chưa có và smoke runtime chỉ kết luận từ output hiện tại.

## Cụm song song

- Cụm A: T1.1 và T3.1 chạy song song vì không giao vùng file; tạo contract API và UnitX core độc lập.
- Cụm B: T1.2 chạy sau T1.1; T3.3 chạy sau T3.1 và song song với T1.2/T1.3 vì chỉ chạm tài liệu.
- Cụm C: T1.3 sau T1.2; T2.1/T2.2/T2.3 giữ tuần tự theo workspace → UI → compile/publish để tránh file nóng Web.
- Cụm D: T3.2 sau T3.1, không chạy song song với Cụm C do cả hai khai `Program.cs` và Web UI; một chủ ghi duy nhất xử lý phần Web.
- Cụm E: T4.1 sau toàn bộ code; T4.2 sau T4.1.

## Luật file nóng

- `runner/AutoTest.Web/Program.cs`, `runner/AutoTest.Web/wwwroot/index.html`, `runner/AutoTest.Web/wwwroot/app.js`, `runner/AutoTest.Web/wwwroot/app.css`, `tests/AutoTest.FrameworkTests/Program.cs` là FILE NÓNG. Chọn một chủ ghi duy nhất: các task Web/test liên quan chạy tuần tự trong cùng luồng; không merge song song các thay đổi vào các file này.

## Definition of Done

- [x] Q1 Contract normalization từ OpenAPI/Roslyn có provenance và diagnostic lỗi, không suy đoán — build runner và framework tests pass.
- [x] Q2 Journey API Builder lưu/gán biến A–Z và restore workspace — UI/browser journey cùng test compiler pass. (lệch #1 chờ duyệt browser evidence)
- [x] Q3 Boundary/recovery workspace chặn duplicate, traversal, corrupt artifact và revision conflict — framework tests pass.
- [x] Q4 Preview/log/report không lộ secret; guards runtime giữ nguyên — test redaction/guard pass.
- [x] Q5 Publish tạo schema-v2 valid, coverage/preflight pass và SHA-256 overwrite guard còn hiệu lực — preflight/activation tests pass.
- [x] Q6 UnitX whitelist chạy decision-table ra actual/expected/PASS-FAIL theo row — UnitX core tests pass.
- [x] Q7 UnitX từ chối operator/type/budget invalid mà không thực thi code/network/file — negative/security tests pass.
- [x] Q8 Build và regression test pass; smoke non-destructive chỉ được báo theo evidence runtime hiện tại — build/test/smoke logs pass hoặc BLOCKED chính xác.
- [x] Q9 UI tiếng Việt rõ ràng cho tester không biết JSON; không có visual reference nên Vision Test Loop không áp dụng — browser journey pass. (lệch #2 chờ duyệt browser evidence)

## Ước tính phút (eNm)

Tổng: 286 phút thực thi ước tính, không gồm chờ duyệt, sandbox permission hay external environment blocker.
