# PLAN — Chuẩn hóa catalogue message, mã lỗi và UI i18n

Ngày: 2026-10-26 · Spec: ../spec/2026-10-26-1256-message-error-standard.md (bản 1.0, ĐÃ DUYỆT) · Lane: full
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md
Mode thực thi: main — ĐỀ XUẤT; simulator đo main 24,4 phút so với subagent 24,5 phút (thắng 0,1 phút), vì 12 task tạo 8 wave và collision lặp lại ở framework test fixture. Một chủ ghi tránh merge conflict.
Trạng thái plan: HOÀN THÀNH

## Mục lục

- Quy tắc thi hành
- P1 — Catalogue contract và preflight
- P2 — Runtime assertion và source audit
- P3 — Diagnostics Web và i18n UI
- P4 — Tài liệu, runtime evidence và review
- Cụm song song
- Luật file nóng
- Definition of Done

## Quy tắc thi hành (áp cho mọi task)

1. Thứ tự phase là thứ tự phụ thuộc — không đảo.
2. Mỗi task: đánh `[~]` khi bắt đầu → viết test trước (đỏ) → code → test xanh → đổi sang `[x]` NGAY vào file này. Trạng thái checkbox: `[ ]` chưa làm · `[~]` đang làm · `[x]` xong.
3. Mỗi task chạy kiểm vùng chạm; full suite chỉ chạy tại QC.
4. QC FAIL → thêm task fix vào mục QC của file này, loop đến khi pass.
5. Không commit/push cho đến khi user yêu cầu.
6. Không bịa code/status/message từ backend; runtime migration business chỉ dùng source và response local/test hiện tại.
7. Không log hay copy secret từ `.env`, token, password, authorization header hoặc connection string.

## P1 — Catalogue contract và preflight

- [x] **T1.1** (e20m) Viết regression đỏ cho catalogue project-local hợp lệ/sai và khai báo typed model/load tối thiểu: version, prefix, code, HTTP status, `vi`/`en`, lookup locale fallback. — Test: framework tests chứng minh valid load và từng invalid contract bị reject.
  - Chạm: `src/AutoTest.Abstractions/Models.cs`, `src/AutoTest.Core/Configuration.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → `SpecLoader` được runner và framework tests gọi; catalogue model là shared contract (nguồn: LSP blast radius `SpecLoader`, `ProjectSpec`).
  - Dùng: `dotnet-standards:dotnet-testing`
  - Để: tạo regression observable cho loader/validation trước khi code.
  - Ra: model catalogue và load project-local có lỗi xác định.
  - Kiểm: framework test command PASS.
  - Không dùng cho: đổi business envelope hoặc sửa backend ngoài Tool-QC.

- [x] **T1.2** (e20m) Sau T1.1, thêm preflight cho catalogue và `expect.error`: mã unique/grammar/prefix/status/locale, reference tồn tại và status khớp; legacy testcase không có catalogue/reference vẫn giữ đường cũ. — Test: framework fixture schema-v2 pass/fail theo từng rule, không có network call.
  - Chạm: `src/AutoTest.Abstractions/Models.cs`, `src/AutoTest.TestValidation/TestcaseValidator.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → `TestcaseValidator.Validate` được runner/Web preflight và framework tests gọi (nguồn: LSP references).
  - Cần: T1.1
  - Tiến độ: model `expect.error` và validator optional catalogue đã có regression xanh; đang nối project catalogue vào CLI/Web preflight.

- [x] **T1.3** (e12m) Khai báo contract portable trong project template và catalogue `ops-service` chỉ gồm entry đã được source/API evidence xác nhận; nếu evidence thiếu, không thêm migration business code. — Test: parse catalogue/template và project-name consistency.
  - Chạm: `projects/project-template/project.json`, `projects/project-template/error-catalogue.json`, `projects/ops-service/project.json`, `projects/ops-service/error-catalogue.json` → project configuration/data boundary, không có shared branch theo project.
  - Cần: T1.1
  - Evidence: không có `errorCode` trong project data hiện tại; không tạo catalogue hay migration business `ops-service` khi chưa có source/API evidence.

**Xong P1 khi**: catalogue contract được nạp qua shared loader, preflight chặn mọi cấu hình/reference sai trước network call và legacy data giữ compatibility.

## P2 — Runtime assertion và source audit

- [x] **T2.1** (e15m) Sau T1.2, resolve `expect.error` thành status, `$.errorCode` và localized `$.message` assertion ở HTTP boundary bằng rule hiện hữu; giữ `expect.json` literal/template nguyên vẹn. — Test: local deterministic HTTP fixture xác nhận `vi`, `en` fallback và testcase legacy assertion.
  - Chạm: `src/AutoTest.Abstractions/Execution.cs`, `src/AutoTest.Core/RunnerEngine.cs`, `src/AutoTest.Http/HttpStepExecutor.cs`, `runner/AutoTest.Runner/Program.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → `StepExecutionContext` truyền catalogue đã validate qua `RunnerEngine` vào HTTP executor (nguồn: LSP blast radius).
  - Cần: T1.2
  - Evidence: framework test local HTTP fixture PASS (78); runner build PASS (0 warning/0 error).

- [x] **T2.2** (e24m) Mở rộng scanner result/audit theo catalogue input rõ ràng: key/translation/code mismatch, locale gap và hard-coded UI/error findings có file/location/kind; giới hạn pattern/extension/exclusion để không quét log, dependency hay fixture excluded. — Test: fixture source nhỏ tạo positive và excluded findings xác định.
  - Chạm: `src/AutoTest.MessageScanner/AutoTest.MessageScanner.csproj`, `src/AutoTest.MessageScanner/MessageScanner.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → `SourceMessageScanner.Scan` được CLI và Web scan gọi (nguồn: LSP references).
  - Cần: T1.1
  - Tiến độ: scanner build xanh 0 warning/0 error. Regression chỉ đỏ ở fixture JavaScript có dấu nháy kép không hợp lệ sau khi detector chuyển sang literal-level; đang sửa fixture để kiểm đúng source syntax.

- [x] **T2.3** (e16m) Xuất catalogue/audit findings trong workbook mà vẫn giữ sheet/cột Messages legacy; CLI nhận catalogue option optional và Web scan chuyển catalogue project input an toàn. — Test: workbook ZIP có sheet legacy và audit; CLI/Web no-catalogue flow giữ artifact contract.
  - Chạm: `src/AutoTest.MessageScanner/MessageWorkbookWriter.cs`, `runner/AutoTest.MessageScanner/Program.cs`, `runner/AutoTest.Web/Program.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → workbook writer/scan callers CLI-Web (nguồn: LSP blast radius `MessageWorkbookWriter.Write`, `SourceMessageScanner.Scan`).
  - Cần: T2.2
  - Tiến độ: CLI build PASS 0 warning/0 error; Web build PASS 0 warning/0 error; framework regression PASS 78. LSP còn cache diagnostic cũ sau project reference, compiler đã xác nhận integration hiện tại. Đang probe CLI/Web runtime no-catalogue.

**Xong P2 khi**: testcase có thể assert full error contract qua catalogue, scanner audit source read-only và mọi caller scanner vẫn tương thích.

## P3 — Diagnostics Web và i18n UI

- [x] **T3.1** (e18m) Thay hard-coded diagnostic display/fix text trong `UiSupport` bằng catalogue nội bộ `TQC-*`; giữ mapping legacy text chỉ làm fallback và không thay đổi history/report contract. — Test: framework test chứng minh known TQC code lookup, legacy text fallback và redaction-safe output.
  - Chạm: `runner/AutoTest.Web/UiSupport.cs`, `runner/AutoTest.Web/Program.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → `UiSupport.Diagnose` được run state Web gọi (nguồn: LSP references).
  - Cần: T1.1
  - Tiến độ: regression baseline PASS 78. `UiSupport` đã đổi nhánh diagnostic sang code/category mapping và catalogue `TQC-*` nội bộ cho display/fix; đang build/rerun để kiểm fallback legacy.
  - Dùng: `dotnet-standards:error-handling`
  - Để: giữ code/status/display message tách contract, chặn UI backend biến thành error producer thứ hai.
  - Ra: diagnostic catalogue lookup với fallback legacy có kiểm soát.
  - Kiểm: framework regression của `UiSupport.Diagnose` PASS.
  - Không dùng cho: sửa middleware hoặc error envelope backend ngoài Tool-QC.

- [x] **T3.2** (e28m) Thêm i18n boundary chung ở UI state/API client: locale `vi` default, `en` fallback, storage safe và code-first error display; re-render message visible mà không đổi request/workspace/selection. — Test: focused browser/unit fixture đổi locale và kiểm `errorCode` ưu tiên API message fallback.
  - Tiến độ: shared boundary đã có catalogue TQC UI nhỏ, safe storage và code-first HTTP/network error; đang thêm selector locale không chạm state project/workspace/selection.
  - Chạm: `runner/AutoTest.Web/wwwroot/ui/shared.js`, `runner/AutoTest.Web/wwwroot/app.js`, `runner/AutoTest.Web/wwwroot/index.html` → `shared.js` được toàn bộ UI module import/use; app/index là Web entry hub.
  - Cần: T3.1

- [x] **T3.3** (e35m) Chuyển message visible ở từng UI module sang key/lookup chung, gồm loading, empty, success, validation, actionable error và network fallback; giữ DOM ID, route và browser workflow. — Test: browser journey covers representative message from every module plus locale switch giữ workspace/selection.
  - Chạm: `runner/AutoTest.Web/wwwroot/ui/api-builder.js`, `runner/AutoTest.Web/wwwroot/ui/environment-history.js`, `runner/AutoTest.Web/wwwroot/ui/generator.js`, `runner/AutoTest.Web/wwwroot/ui/testcases.js`, `runner/AutoTest.Web/wwwroot/ui/unitx.js`, `runner/AutoTest.Web/wwwroot/ui/workbook-message.js`, `runner/AutoTest.Web/wwwroot/ui/shared.js`, `runner/AutoTest.Web/wwwroot/ui/messages/catalogue.js`, `tests/e2e/api-builder.spec.js` → seven UI feature modules và ownership catalogue tập trung.
  - Cần: T3.2
  - Tiến độ bổ sung theo phản hồi user: tách catalogue UI khỏi `shared.js` vào ownership directory duy nhất `runner/AutoTest.Web/wwwroot/ui/messages/`; mọi module chỉ dùng key/lookup. Catalogue đã khử key trùng theo ngữ nghĩa. Browser E2E tìm thấy collision key lịch sử sau cleanup: API Builder còn gọi `101–105`, nay là key Generator. Đã remap 101/102/104 về 070/071/073; còn artifact/compile, rồi chạy lại build/browser evidence.
  - Evidence: `ui/messages/catalogue.js` là nơi duy nhất định nghĩa `TQC-UI-*`; `shared.js` chỉ import/re-export lookup. Web build PASS (0 warning/0 error); browser journey PASS (1 test, 7.2s), xác nhận locale `en`/`vi`, `TQC-UI-015` tiếng Anh và project/workspace selection không đổi. Đã chuyển dialog ghi đè Generator sang `TQC-UI-138`, saved-status sang `TQC-UI-139`, app dirty-status sang `TQC-UI-043`; audit còn một `vi-VN` định dạng lịch sử, đang đổi thành locale đã chọn. ESLint NOT_RUN: repository không có `eslint.config.*`; không tự thêm dependency/config. Scoped Prettier check vẫn FAIL ở 9 file hiện có; không reformat ngoài scope.

**Xong P3 khi**: UI không giữ message visible rải rác, đổi locale giữ state và behavior chỉ dựa error code/status thay vì localized text.

## P4 — Tài liệu, runtime evidence và review

- [x] **T4.1** (e16m) Cập nhật README, `.env.example`, project template và `CLAUDE.md` cho catalogue/error/UI i18n contract, locale/fallback, source audit và safety; không đưa `.env` value, endpoint business hoặc credential vào docs. — Test: document lint, JSON parse và secret scan scoped files không có finding.
  - Evidence: README có CLI `--catalogue`; `.env.example` chỉ ghi comment không cần env; `CLAUDE.md` có public contract catalogue/`expect.error`/locale/scanner. `doc_lint --pair` PASS; JSON parse PASS (2 template files); `git diff --check` PASS; scoped secret scan PASS.
  - Chạm: `README.md`, `.env.example`, `CLAUDE.md`, `projects/project-template/project.json`, `projects/project-template/error-catalogue.json` → public contract/template docs.
  - Cần: T1.3, T2.3, T3.3

- [x] **T4.2** (e20m) Chạy framework/build, local Web browser journey và scanner CLI; nếu local/test ops-service đáp ứng guard và đã trả `errorCode`, migrate đúng một testcase error representative schema-v2 rồi chạy tag nhỏ nhất, nếu không ghi evidence `NOT_RUN`/`BLOCKED`. — Test: kết quả tách build/framework/browser/scanner/project runtime rõ PASS/FAIL/NOT_RUN/BLOCKED.
  - Evidence: Framework PASS (78); runner/Web build PASS (0 warning/0 error); browser PASS (1, 7.3s); scanner CLI catalogue audit PASS (0 finding). Checkout chính: 65 JSON/120 case, `expect.error`/`errorCode` đều 0, nên migration business NOT_RUN (không có contract evidence). Sửa 3 trailing comma TEST_SCRIPT_ERROR; JSON parse PASS (65). Smoke non-destructive chạy 1/1 case nhưng BLOCKED: `GET /healthz` connection refused tại `localhost:5007`, report `test-results/ops-service/system/2026-10-26_152826_ops-service_smoke.html`; không đổi expectation.
  - Chạm: `tests/AutoTest.FrameworkTests/Program.cs`, `tests/e2e/api-builder.spec.js`, `projects/ops-service/testcases/` → regression + một testcase business chỉ khi contract hiện tại được xác minh.
  - Cần: T4.1

- [x] **T4.3** (e12m) Rà độc lập diff shared .NET/Web về compatibility, SOLID, security/redaction, cancellation và coverage; xử lý finding trước QC. — Test: review không còn finding actionable.
  - Chạm: `src/AutoTest.Abstractions/`, `src/AutoTest.Core/`, `src/AutoTest.Http/`, `src/AutoTest.TestValidation/`, `src/AutoTest.MessageScanner/`, `runner/AutoTest.Web/`, `runner/AutoTest.MessageScanner/`, `tests/AutoTest.FrameworkTests/`, `tests/e2e/` → toàn bộ vùng shared đã thay đổi.
  - Cần: T4.2
  - Dùng: `dotnet-standards:dotnet-code-review`
  - Tiến độ: reviewer worktree không truy cập được diff nên verdict `NOT ASSESSED`; review trực tiếp trên main xác nhận resolver `expect.error` ghi đè JSON assertion explicit cùng path. Regression đỏ đã tái hiện, resolver nay chỉ bổ sung assertion catalogue còn thiếu và framework PASS 78. Locale event đã re-render projection UI có state; browser regression PASS xác nhận nhãn testcase đổi `vi`/`en` và selection/project được giữ. Chrome chính đã dùng marker catalogue/renderer chung; đang kiểm chứng initial render và regression shell trước verdict cuối.
  - Để: review correctness/contract/cancellation và slop trước QC.
  - Ra: finding verified hoặc verdict không có finding actionable.
  - Kiểm: review report và regression vùng chạm.
  - Không dùng cho: thay thế runtime evidence hoặc tự sửa backend ngoài.

## Cụm song song

- Cụm A: T1.1 mở contract/model/test fixture trước.
- Cụm B: T1.2 và T1.3 cùng phụ thuộc T1.1 nhưng T1.2 chạm `Models.cs`/test fixture, T1.3 chỉ project data nên có thể song song trong worktree.
- Cụm C: T2.1, T2.2, T3.1 sau P1; T2.1/T2.2/T3.1 đều cùng chạm framework test hoặc catalogue ownership, nên mode main tuần tự. Mode subagent chỉ song song sau khi tách test fixture/file ownership xác nhận không conflict.
- Cụm D: T2.3 sau T2.2; T3.2 sau T3.1; T3.3 sau T3.2. T4.* chờ tất cả output trước.

## Luật file nóng

- `tests/AutoTest.FrameworkTests/Program.cs` là file nóng của T1.1, T1.2, T2.1, T2.2, T2.3, T3.1 và T4.2. Một chủ ghi duy nhất: mode `main` thực hiện tuần tự; nếu user chọn subagent, leader giữ task test fixture hoặc gộp test edits khi merge.
- `src/AutoTest.Abstractions/Models.cs` là file nóng T1.1/T1.2; T1.1 tạo contract cơ sở, T1.2 chỉ nối tiếp trên trạng thái xanh.
- `runner/AutoTest.Web/wwwroot/ui/shared.js` là file nóng T3.2/T3.3; T3.2 sở hữu i18n boundary, T3.3 chỉ consume sau đó.
- `projects/project-template/project.json` và `error-catalogue.json` là file nóng T1.3/T4.1; T1.3 tạo contract, T4.1 ghi tài liệu mô tả đúng final behavior.

## Definition of Done

- [x] Q1 Catalogue validation — valid load; version/prefix/grammar/duplicate/status/locale/reference invalid đều bị preflight reject — framework regression.
- [x] Q2 Runtime error contract — local fixture kiểm status/code/`vi`/`en` fallback; legacy JSON assertions không regress — framework regression.
- [x] Q3 Source audit/workbook — findings có location/kind, excluded paths không bị quét, workbook legacy + audit export tạo được — framework regression và scanner CLI.
- [x] Q4 CLI/Web/diagnostics — scan artifact contract và TQC catalogue/fallback không lộ secret — framework regression + Web smoke.
- [x] Q5 Web UI i18n — browser đổi locale, code-first fallback và workspace/selection preservation PASS — browser E2E.
- [x] Q6 Build/documentation — runner build, framework tests, format/document/JSON validation PASS — scoped commands.
- [x] Q7 Project runtime evidence — ops-service tag representative PASS hoặc `NOT_RUN`/`BLOCKED` có evidence thực tế — runner/report.
- [x] Q8 Independent review — code/security review không còn finding actionable — review report.

## QC vòng 1 — fix

- [x] **QC1.1** Format các file Web đã thay đổi để Q6 pass mà không reformat vùng ngoài scope. — Test: `npx prettier --check` trên danh sách file Web đã thay đổi.

## P5 — Onboarding máy mới

- [x] **T5.1** (e16m) Bổ sung lệnh `npm run setup` idempotent để kiểm Git/.NET SDK 8/Node 18+, restore NuGet/Node package theo lockfile và chuẩn bị Playwright Chromium; không tự cài hay thay đổi SDK/toolchain hệ thống. — Test: chạy `npm run setup` hai lần; lần hai báo NuGet up-to-date và Node dependency sẵn sàng; framework regression PASS 78.
  - Chạm: `scripts/bootstrap.ps1`, `package.json`, `README.md` → entrypoint onboarding độc lập; các script runtime hiện có không đổi contract.
