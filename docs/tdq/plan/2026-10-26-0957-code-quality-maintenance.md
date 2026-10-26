# PLAN — Chất lượng code và khả năng bảo trì

Ngày: 2026-10-26 · Spec: ../spec/2026-10-26-0957-code-quality-maintenance.md (bản 1.0, ĐÃ DUYỆT) · Lane: full
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md
Mode thực thi: main — ĐỀ XUẤT; simulator đo 14,3 phút so với 15,3 phút của subagent (thắng 1,1 phút) vì 7 task tạo 5 wave và cùng chạm các file nóng. User chốt lúc duyệt.
Trạng thái plan: HOÀN THÀNH

## Mục lục

- Quy tắc thi hành
- P1 — Formatter và UI module
- P2 — Persistence workspace
- P3 — Subprocess và OpenAPI validation
- P4 — Regression, log và review
- Cụm song song
- Luật file nóng
- Definition of Done

## Quy tắc thi hành (áp cho mọi task)

1. Thứ tự phase là thứ tự phụ thuộc — không đảo.
2. Mỗi task: đánh `[~]` khi bắt đầu → viết test trước (đỏ) → code → test xanh → đổi sang `[x]` NGAY vào file này. Trạng thái checkbox: `[ ]` chưa làm · `[~]` đang làm · `[x]` xong.
3. Mỗi bước chạy kiểm vùng chạm; trọn bộ chỉ ở QC.
4. QC FAIL → thêm task fix vào mục QC của file này, loop đến khi pass.
5. Không commit/push cho đến khi user yêu cầu.
6. Tuân thủ 6 mã cấm cơ học: không fake red, không blind assertion, không dirty state Git, tick trước code, không raw dump, và khai file cụ thể trong `Chạm:`.

## P1 — Formatter và UI module

- [x] **T1.1** (e12m) Thêm Prettier devDependency, cấu hình version-control và script `format`/`check-format`; format source Web HTML/JS/CSS. — Test: check-format chấp nhận đúng source Web và source cố ý sai format bị từ chối trước khi format.
  - Chạm: `package.json`, `package-lock.json`, `.prettierrc.json`, `.prettierignore`, `runner/AutoTest.Web/wwwroot/index.html`, `runner/AutoTest.Web/wwwroot/app.css`, `runner/AutoTest.Web/wwwroot/app.js` → entry assets Web; `index.html` và `app.js` là hub (nguồn: `docs/kien-truc.md` §Hub).
  - Dùng: `tdq-workflow:tdq-lean`
  - Để: chọn cấu hình/glob nhỏ nhất chỉ bao phủ source phát triển.
  - Ra: cấu hình Prettier và script npm được Git quản lý.
  - Kiểm: format/check-format có exit code đúng trên source thật và fixture sai format.
  - Không dùng cho: thêm bundler, minifier hoặc frontend framework.

- [x] **T1.2** (e35m) Đổi entry script thành ES module; tách bootstrap/state/API client và UI feature testcase, environment/history, generator/workbook/message scanner, API Builder, UnitX, architecture thành các module vanilla JS dễ đọc; giữ ID DOM, route và browser workflow. — Test: static module load không có global-reference error; journey API Builder hiện có vẫn pass khi browser host sẵn sàng.
  - Chạm: `runner/AutoTest.Web/wwwroot/index.html`, `runner/AutoTest.Web/wwwroot/app.js`, `runner/AutoTest.Web/wwwroot/ui/bootstrap.js`, `runner/AutoTest.Web/wwwroot/ui/api-client.js`, `runner/AutoTest.Web/wwwroot/ui/testcases.js`, `runner/AutoTest.Web/wwwroot/ui/environment-history.js`, `runner/AutoTest.Web/wwwroot/ui/generator.js`, `runner/AutoTest.Web/wwwroot/ui/api-builder.js`, `runner/AutoTest.Web/wwwroot/ui/unitx.js`, `runner/AutoTest.Web/wwwroot/ui/workbook-message.js`, `runner/AutoTest.Web/wwwroot/ui/architecture.js` → Web UI entry and feature callers (nguồn: `index.html` script entry; `app.js` DOM/API ownership audit).
  - Cần: T1.1
  - Dùng: `run`
  - Để: chạy host thực và điều khiển golden path API Builder sau tách module.
  - Ra: entry module và các module feature không có frontend framework.
  - Kiểm: browser smoke tương tác được UI hoặc ghi `BLOCKED` cùng diagnostic khi thiếu executable.
  - Không dùng cho: cài browser/Playwright hoặc thay đổi HTTP contract.
  - Tiến độ: tất cả feature owner đã có module riêng gồm testcase, environment/history, generator, architecture, generator repair, UnitX, workbook/message và API Builder. API Builder browser journey PASS theo contract fixture hiện hành. Entry đang được rewiring sang API initializer của ba module mới; chưa kiểm syntax hoặc browser sau bước này.

**Xong P1 khi**: formatter check pass và UI được bootstrap từ ES modules, không thay đổi browser workflow.

## P2 — Persistence workspace

- [x] **T2.1** (e28m) Viết helper persistence tối thiểu tại Web boundary để critical section theo canonical workspace path và ghi/replace qua temporary filename duy nhất; chuyển API Builder và UnitX store sang helper mà giữ revision conflict contract. — Test: hai save đồng thời cùng revision cho cùng workspace có đúng một save thành công và JSON cuối đọc được; workspace khác vẫn không chia critical section.
  - Chạm: `runner/AutoTest.Web/WorkspacePersistence.cs`, `runner/AutoTest.Web/ApiBuilderWorkspace.cs`, `runner/AutoTest.Web/UnitXWorkspace.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → `SaveAsync` API Builder/UnitX, facade route registration tại `Program.cs` và framework callers (nguồn: LSP `find_references` SaveAsync).
  - Dùng: `dotnet-standards:dotnet-testing`
  - Để: thiết kế regression observable cho concurrent save và atomic persistence.
  - Ra: helper Web-only dùng chung cho hai store và regression framework.
  - Kiểm: framework test chứng minh conflict/integrity cho cả API Builder lẫn UnitX.
  - Không dùng cho: thêm file-system locking vào shared runner hoặc UnitX evaluator.

**Xong P2 khi**: hai store dùng chung helper và regression đồng thời pass mà không hỏng JSON.

## P3 — Subprocess và OpenAPI validation

- [x] **T3.1** (e18m) Chuyển mọi subprocess generator còn đọc stdout rồi stderr tuần tự sang tạo hai read task trước `WaitForExitAsync`, giữ nguyên exit-code và diagnostic contract. — Test: fixture child process tạo stderr lớn hoàn tất trong timeout hữu hạn và cả stdout/stderr được trả về.
  - Chạm: `runner/AutoTest.Web/Program.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → generator endpoint, `RunGeneratorAsync`, process helper/callers và framework regression (nguồn: LSP `find_references` RunProcessAsync; source scan của các `ReadToEndAsync`).
  - Cần: T2.1
  - Tiến độ: đã xác nhận ba path subprocess còn đọc stdout rồi stderr tuần tự (`generate`, generator wrapper và runner preflight); helper Web-only đã có concurrent drain, endpoint generate và generic process runner đã dùng helper. Đang chuyển generator wrapper/preflight và thêm fixture stderr-heavy trực tiếp.

- [x] **T3.2** (e10m) Validate trường `openapi` là string không rỗng trước khi gọi `StartsWith`, giữ support OpenAPI 3.1. — Test: missing, null, number và empty version ném `InvalidDataException`; fixture 3.1 vẫn tạo analysis hợp lệ.
  - Chạm: `runner/AutoTest.Generator/OpenApiContractAdapter.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → `Analyze` adapter, generator projection caller và framework regression (nguồn: LSP `find_references` Analyze; test call tại framework Program).
  - Cần: T2.1

**Xong P3 khi**: process stderr-heavy và OpenAPI malformed regressions pass, không đổi generator protocol.

## P4 — Regression, log và review

- [x] **T4.1** (e8m) Xác nhận logging runtime Web đang bật mặc định, có timestamp/chi tiết chẩn đoán và có thể giảm qua cấu hình; bổ sung đúng mức cần thiết nếu refactor làm mất contract này. — Test: startup/diagnostic output hiện timestamped logging hoặc configuration verification chứng minh level override.
  - Chạm: `runner/AutoTest.Web/Program.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → Web composition root và framework verification; `Program.cs` là hub (nguồn: `docs/kien-truc.md` §Hub).
  - Cần: T3.1, T3.2

- [x] **T4.2** (e15m) Rà toàn bộ diff C# và UI sau regressions; loại duplicate/dead code do tách module, kiểm SOLID, cancellation và dependency direction trước QC độc lập. — Test: review không còn finding actionable trong vùng scope; mọi framework regression mới nằm trong cùng một lệnh test.
  - Chạm: `runner/AutoTest.Web/`, `runner/AutoTest.Generator/OpenApiContractAdapter.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → toàn bộ vùng thay đổi đã có regression.
  - Cần: T1.2, T3.1, T3.2, T4.1
  - Dùng: `dotnet-standards:dotnet-code-review`
  - Để: review breadth về correctness, concurrency, test coverage và slop của diff C#.
  - Ra: kết quả review có finding đã xử lý hoặc xác nhận không có finding.
  - Kiểm: review đọc diff cuối và build/framework test vẫn pass.
  - Không dùng cho: thay thế security/architecture review ngoài phạm vi spec.

**Xong P4 khi**: log contract được giữ, regression được review và không còn finding actionable trong scope.

## Cụm song song

- Cụm A: T1.1 → T1.2 vì cùng chạm `index.html`/`app.js`.
- Cụm B: T2.1 là một cụm riêng vì test program và persistence stores cùng thay đổi.
- Cụm C: T3.1 và T3.2 có thể song song sau T2.1 nếu dùng worktree; chúng cùng khai `tests/AutoTest.FrameworkTests/Program.cs`, nên mode main thực hiện tuần tự với một chủ ghi duy nhất.
- Cụm D: T4.1 → T4.2 sau các cụm trước vì `Program.cs` và framework test là file nóng.

## Luật file nóng

- `runner/AutoTest.Web/Program.cs` và `tests/AutoTest.FrameworkTests/Program.cs` là file nóng của T2.1, T3.1, T3.2, T4.1 và T4.2. Chọn một chủ ghi duy nhất trong mode main; task sau chỉ sửa tiếp trên trạng thái xanh của task trước.
- `runner/AutoTest.Web/wwwroot/index.html` và `runner/AutoTest.Web/wwwroot/app.js` là file nóng của T1.1/T1.2. T1.1 hoàn tất formatter trước, T1.2 là chủ ghi logic duy nhất.

## Definition of Done

- [x] Q1 Prettier format source Web và check-format PASS; source HTML/JS/CSS không còn viết nén — `npm run check-format`.
- [x] Q2 ES modules bootstrap đủ UI feature; API Builder journey PASS hoặc browser smoke được ghi BLOCKED với diagnostic — `npm run test:e2e` hoặc evidence host/browser blocker.
- [x] Q3 Concurrent workspace save trả outcome xác định và JSON luôn đọc được cho API Builder/UnitX — `npm run test:framework`.
- [x] Q4 Subprocess stderr-heavy hoàn tất, giữ output/diagnostic — `npm run test:framework`.
- [x] Q5 OpenAPI malformed version ném `InvalidDataException`, OpenAPI 3.1 hợp lệ không regress — `npm run test:framework`.
- [x] Q6 Web build, framework regression, format check và review độc lập PASS; browser smoke tách PASS/BLOCKED — `npm run build`, `npm run test:framework`, `npm run check-format`, review evidence.
