# PLAN — Hoàn thiện API Builder no-code
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

Ngày: 2026-10-23 · Spec: ../spec/2026-10-23-0136-api-builder-full.md (bản 1.0, ĐÃ DUYỆT) · Lane: full
Mode thực thi: main — `tdq_bench simulate` đo main 14,3 phút, sub-agent 15,3 phút; chuỗi task Web/workspace/framework test có 5 wave và main nhanh hơn 1,1 phút.
Trạng thái plan: HOÀN THÀNH

## Mục lục

- Quy tắc thi hành
- P1 — Projection contract và workspace typed
- P2 — Compiler, facade và UI Builder
- P3 — Browser E2E và regression
- Cụm song song
- Luật file nóng
- Definition of Done

## Quy tắc thi hành

1. Thứ tự phase là dependency, không đảo: contract → workspace/compiler → facade/UI → E2E/QC.
2. Mỗi task đánh `[~]` khi bắt đầu, có red/green check và chuyển `[x]` ngay sau evidence xanh.
3. Không thêm parser/UI framework; chỉ thêm `@playwright/test` dev dependency khi task E2E bắt đầu.
4. Không đọc/in `.env`, không chạy production/destructive case, không weakening coverage/preflight/activate guard.
5. Không commit/push khi chưa có yêu cầu rõ; không thêm attribution trailer.

## P1 — Projection contract và workspace typed

- [x] **T1.1** (e38m) Tạo Builder contract projection từ `GeneratorAnalysis`, OpenAPI-first/Roslyn fallback, có operation/field tree/provenance/fingerprint/diagnostic và budget tree — Test: fixture OpenAPI 3.1 và source Roslyn tạo DTO operation/field tương đương; input/budget sai trả diagnostic, không đoán.
  - Chạm: `runner/AutoTest.Generator/Models.cs`, `runner/AutoTest.Generator/OpenApiContractAdapter.cs`, `runner/AutoTest.Generator/SourceContractAnalyzer.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → `GeneratorAnalysis` và adapter/analyzer là nguồn contract có caller CLI generator/framework tests.
  - Dùng: `tdq-workflow:tdq-lean`
  - Để: tái dùng semantic model và giữ một parser/projection.
  - Ra: DTO Builder contract ổn định từ hai nguồn.
  - Tạo mới thay vì dùng `runner/AutoTest.Generator/OpenApiContractAdapter.cs` vì projection Builder là output chung cho cả OpenAPI và Roslyn, không thuộc parser riêng.
  - Kiểm: framework test projection, error diagnostic và budget.
  - Tiến độ: red test đã xác nhận projection thiếu; implementation đang sửa mapping `operationId` và giới hạn recursion tree trước lần green kế tiếp.
  - Không dùng cho: Web endpoint, persistence workspace hay DOM.

- [x] **T1.2** (e34m) Mở rộng API Builder workspace bằng operation reference, contract fingerprint và typed bindings; validate source trước target, field writable, type/location compatibility, sensitive restriction và drift — Test: duplicate/use-before-save/type mismatch/credential target/drift/revision conflict bị từ chối xác định.
  - Chạm: `runner/AutoTest.Web/ApiBuilderWorkspace.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → workspace store/compiler/framework tests tiêu thụ records này; không chạm HTTP executor.
  - Dùng: `dotnet-standards:api-surface`
  - Để: định nghĩa request/domain validation contract rõ trước khi UI gửi workspace.
  - Ra: workspace typed và diagnostics an toàn.
  - Kiểm: framework test Given–When–Then cho binding/drift/containment/redaction.
  - Tiến độ: red harness đã xác nhận record/binding domain chưa tồn tại; validation store đã chạy tới runtime và đang hoàn thiện guard tương thích workspace MVP không có metadata location/field ID.
  - Không dùng cho: compiler map schema-v2 hay UI rendering.

**Xong P1 khi**: một contract/provenance model dùng được cho operation/tree, workspace không persist binding mơ hồ hoặc credential nhạy cảm.

## P2 — Compiler, facade và UI Builder

- [x] **T2.1** (e32m) Compiler map typed field binding đến path/query/header/body trong schema-v2 canonical và giữ coverage/preflight safety — Test: compile multi-location interpolation, invalid field/contract mismatch fail; basic preflight pass nhưng full coverage không bị bypass.
  - Chạm: `runner/AutoTest.Web/ApiBuilderCompiler.cs`, `runner/AutoTest.Web/ApiBuilderWorkspace.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → compiler là sole mapper sang suite JSON; tests đã gọi compiler.
  - Dùng: `tdq-workflow:tdq-lean`
  - Để: không tạo request/runtime thứ hai và không dời semantic sang DOM.
  - Ra: schema-v2 path/query/header/body mapping.
  - Kiểm: framework compiler/validator and API preflight test.
  - Không dùng cho: thay `AutoTest.Http` executor hoặc coverage rule.

- [x] **T2.2** (e40m) Bổ sung endpoint API Builder để lấy/chọn contract, trả diagnostics và kiểm drift; chỉ nhận source/OpenAPI qua safe policy — Test: project/contract selection, invalid/out-of-bound path và stale fingerprint trả status/code tiếng Việt rõ.
  - Chạm: `runner/AutoTest.Web/Program.cs`, `runner/AutoTest.Web/ApiBuilderWorkspace.cs`, `runner/AutoTest.Generator/Models.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → `Program.cs` là composition root, workspace owns validation, Generator owns DTO.
  - Dùng: `dotnet-standards:api-surface`
  - Để: giới hạn public API surface/containment, không để frontend đọc filesystem.
  - Ra: contract facade and drift diagnostics.
  - Kiểm: local HTTP/runtime probe and framework tests.
  - Tiến độ: route contract/reference/containment helper đã build; HTTP probe phát hiện Windows path bị URI guard từ chối, đang thu hẹp guard chỉ HTTP/HTTPS rồi rerun probe.
  - Không dùng cho: public remote OpenAPI URL fetch, secret/config scanning hoặc source generator mới.

- [x] **T2.3** (e48m) Thay step demo bằng operation chooser, dynamic request/response field tree, variable dialog/catalog và typed binding UI tiếng Việt; save/reload/recovery rõ ràng — Test: static syntax plus browser journey selects two operations, saves `orderId`, binds request target, reloads and restores workspace.
  - Chạm: `runner/AutoTest.Web/wwwroot/index.html`, `runner/AutoTest.Web/wwwroot/app.js`, `runner/AutoTest.Web/wwwroot/app.css`, `runner/AutoTest.Web/Program.cs` → app state calls Builder facade and draft lifecycle; Program is hot owner of endpoint contract.
  - Dùng: `tdq-workflow:tdq-lean`
  - Để: render directly from Builder contract, no UI framework or JSON editing path.
  - Ra: no-code dynamic Builder journey.
  - Kiểm: `node --check`, Playwright journey after T3.1, Vietnamese action/status and recovery.
  - Không dùng cho: arbitrary JSON editor, JS expression, free-form JSONPath or direct testcase write.
  - Tiến độ: UI đã bỏ demo step, có controls nhập contract local/OpenAPI-source/state contract, renderer location-aware, payload binding theo `targetFieldId`, create/recovery/event binding và syntax xanh; đang giới hạn catalog binding theo thứ tự/type để khớp guard server.

**Xong P2 khi**: tester tạo chain từ operation thật và compile trở về lifecycle hiện hữu mà không sửa JSON.

## P3 — Browser E2E và regression

- [x] **T3.1** (e36m) Thêm Playwright test tooling ở dev dependency, script host lifecycle và Chromium E2E cho create → operation → tree → variable → typed binding → save → reload → compile/preflight — Test: `npm run test:e2e` chạy Chromium thật, xuất trace/screenshot khi fail và cleanup host.
  - Chạm: `package.json`, `package-lock.json`, `tests/e2e/`, `scripts/` nếu cần host launcher → test tooling độc lập Web production; `npm` là UI lifecycle entrypoint.
  - Dùng: `dotnet-standards:dotnet-testing`
  - Để: E2E chạy app qua browser thay vì link assembly; evidence current/isolated.
  - Ra: Playwright Chromium journey và script xác định.
  - Kiểm: `npm install`, browser install/setup, `npm run test:e2e`.
  - Không dùng cho: production package/reference hoặc runtime API credentials.
  - Dùng: `Playwright Test`
  - Để: drive Chromium như tester, có trace/screenshot khi journey lỗi.
  - Ra: `@playwright/test` dev dependency và E2E evidence.
  - Kiểm: Chromium chạy trong lệnh `npm run test:e2e`.
  - Không dùng cho: source parser, Web production dependency hoặc production API credentials.
  - Tiến độ: Playwright/Chromium đã cài dev-only. Journey đỏ xác nhận relative OpenAPI path bị facade resolve theo process directory thay vì repository root; đang sửa và rerun.

- [x] **T3.2** (e28m) Hoàn thiện regression, documentation và QC evidence; build, framework tests, browser E2E và non-destructive lifecycle probe theo capability — Test: zero-error builds, framework suite pass, E2E pass; report tách BUILD/FRAMEWORK/E2E/RUNTIME hoặc blocker.
  - Chạm: `tests/AutoTest.FrameworkTests/Program.cs`, `README.md`, `docs/kien-truc.md`, `.env.example` nếu public key thực sự cần, `docs/tdq/qc/2026-10-23-0136-api-builder-full.md` → framework tests cover new boundary; public docs describe implemented contract.
  - Dùng: `dotnet-standards:dotnet-testing`
  - Để: evidence truthful and docs do not overclaim browser/runtime results.
  - Ra: regression evidence and public guidance.
  - Kiểm: documented commands, fixture/runtime report and `doc_lint`.
  - Không dùng cho: changing backend business API or production execution.

**Xong P3 khi**: browser journey có evidence thật, regressions xanh và public guidance mô tả đúng capability/limit.

## Cụm song song

- Cụm A: T1.1 bắt buộc trước T1.2 vì workspace cần stable field ID/type/provenance.
- Cụm B: T1.2 → T2.1 → T2.2 → T2.3 tuần tự do cùng chạm `ApiBuilderWorkspace.cs`, `Program.cs`, framework tests và Web UI.
- Cụm C: T3.1 sau T2.3 vì E2E cần selectors/journey thật; T3.2 sau T3.1 để báo evidence cuối.

## Luật file nóng

- `runner/AutoTest.Web/Program.cs`, `runner/AutoTest.Web/ApiBuilderWorkspace.cs`, `tests/AutoTest.FrameworkTests/Program.cs`, `runner/AutoTest.Web/wwwroot/app.js`, `runner/AutoTest.Web/wwwroot/index.html`, `runner/AutoTest.Web/wwwroot/app.css` là FILE NÓNG. Chỉ một chủ ghi thực hiện theo chuỗi B; không merge song song.

## Definition of Done

- [x] Q1 Builder contract projection từ OpenAPI-first/Roslyn fallback có operation/tree/provenance/fingerprint/diagnostic và budget — framework test pass.
- [x] Q2 Workspace typed binding chặn source sau target, field missing, type/location mismatch, credential-sensitive target, contract drift và revision conflict — framework tests pass.
- [x] Q3 Compiler map path/query/header/body về canonical schema-v2, basic preflight không làm bypass full coverage/activation guard — framework/API test pass.
- [x] Q4 UI không còn demo step; operation chooser/tree/variable/binding/recovery tiếng Việt không cần JSON — Playwright Chromium journey pass.
- [x] Q5 Reload workspace khôi phục đúng hoặc yêu cầu review khi drift/conflict, không overwrite — Playwright + framework tests pass.
- [x] Q6 Playwright chỉ là dev dependency và xuất evidence fail (trace/screenshot) — `npm run test:e2e` pass.
- [x] Q7 Build và framework regression pass; runtime/preflight evidence tách riêng với blocker — commands chạy hiện tại pass/blocked chính xác.
- [x] Q8 README/kiến trúc mô tả đúng contract, browser evidence và giới hạn coverage — doc lint pass.

## QC vòng 1 — fix

- [x] **QC1.1** Khôi phục npm scripts khởi động/UI bị ghi đè khi thêm Playwright, giữ `test:e2e` dev-only — Test: `npm start` mở `AutoTest.Web`, `npm run test:e2e` PASS.
  - Evidence: `npm start` build generator/runner 0 warning/0 error và host lắng nghe `http://127.0.0.1:5080`; Chromium E2E 1/1 PASS.
  - Chạm: `package.json`, `README.md` → manifest npm là entrypoint UI/E2E; README phải chỉ lệnh chạy đúng.

## Ước tính phút (eNm)

Tổng: 256 phút thực thi ước tính, không gồm approval, download browser hay external environment blocker.
