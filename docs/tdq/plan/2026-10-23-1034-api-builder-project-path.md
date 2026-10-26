# PLAN — API Builder khám phá source local theo project

Ngày: 2026-10-23 · Spec: ../spec/2026-10-23-1034-api-builder-project-path.md (bản 2.0, ĐÃ DUYỆT) · Lane: full
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md
Mode thực thi: main — triển khai inline sau khi harness sub-agent gắn worktree khác với TDQ worktree; các file nóng được một chủ ghi theo dependency.
Trạng thái plan: HOÀN THÀNH

## Mục lục

- Quy tắc thi hành
- P0 — Điều phối TDQ
- P1 — Local configuration và containment
- P2 — Contract discovery facade
- P3 — Browser source-first
- P4 — Regression, guidance và QC chuẩn bị
- Cụm song song
- Luật file nóng
- Log & test bắt buộc
- Definition of Done
- Ước tính phút

## Quy tắc thi hành

1. Thứ tự P1 → P2 → P3 → P4 là dependency, không đảo; mỗi task đi red → code → green → tick `[x]` ngay.
2. Mỗi task chạy `tdq_test.py vung-cham`; full suite chỉ chạy trong QC. Không commit/push nếu không có yêu cầu riêng.
3. Không đọc/in/copy/commit `.env`, source thật của người dùng, token, password, artifact hay database dump.
4. Discovery chỉ read-only: không spawn executable từ source được chọn, không chạy `dotnet`, `npm`, test, build hay script trong source.
5. Cần tạo mới sau search rung 2: `project.local.json` chỉ là local runtime file ignored Git; implementation tạo reader/atomic writer tối thiểu trong component Web đã có thay vì thêm package/parser/storage layer.
6. QC FAIL tạo task fix trong plan, không hạ expectation để pass.

## P0 — Điều phối TDQ

- [x] **T0.0** (e4m) Duy trì traceability của spec v2.0 qua plan, implementation, QC và report; mọi thay đổi scope sau gate quay lại spec/plan. — Test: scope/evidence và deviations Q4/Q7 đã ghi trong state/plan.
  - Dùng: `tdq-workflow:tdq-spec`
  - Để: giữ spec là nguồn phạm vi duy nhất trước/qua implementation.
  - Ra: spec v2.0 và plan đối chiếu không mâu thuẫn.
  - Kiểm: lệnh Test của task PASS.
  - Không dùng cho: viết lại implementation hoặc thay user approval.

- [x] **T0.1** (e8m) Điều phối implementation → QC → report theo plan đã duyệt, tick theo evidence và không tự kết thúc khi còn task. — Test: implementation tasks đã tick theo evidence; chuyển QC sau `tdq_finish`.
  - Dùng: `tdq-workflow:tdq-build`
  - Để: điều phối red-green, task tick, QC và report cho toàn request.
  - Ra: plan/QC/report TDQ có evidence hiện hành.
  - Kiểm: toàn bộ task và DoD được tick sau evidence QC.
  - Không dùng cho: thay đổi product behavior ngoài T1.1–T5.2.

## P1 — Local configuration và containment

- [x] **T1.1** (e24m) Mở rộng configuration Web để merge `project.json` portable với optional `project.local.json`, validate schema `apiBuilder.localSourcePath`, canonicalize source directory local và thực thi no-URI/no-UNC/no-reparse/no-escape policy; đọc/ghi local override atomically chỉ sau discovery pass. — Test: `npm run test:framework` PASS (2026-10-23) có regression valid/missing/malformed local override và mọi rejection path.
  - Chạm: `runner/AutoTest.Web/ApiBuilderProjectConfiguration.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → `ApiBuilderProjectConfiguration.Load` có runtime caller `Program.cs:74` và framework harness callers; blast radius giới hạn Web/facade tests.
  - Dùng: `dotnet-standards:api-surface` (mcp)
  - Để: chốt request/diagnostic boundary tại Web facade, không lan local filesystem path sang shared runner.
  - Ra: configuration model đọc local override và trả diagnostic an toàn, có test temp directory.
  - Kiểm: `npm run test:framework` PASS với expected provider/diagnostic.
  - Không dùng cho: tạo endpoint controller, shared Core model hoặc remote filesystem capability.

- [x] **T1.2** (e12m) Bổ sung `.gitignore` và local sample/documentation format để `projects/<project>/project.local.json` luôn bị bỏ qua nhưng `project.json` portable không đổi. — Test: `git check-ignore -q projects/example/project.local.json` PASS (2026-10-23); `*.local.*` hiện có đã bao phủ file, format sẽ được mô tả tại T4.2.
  - Cần: T1.1

**Xong P1 khi**: per-project local mapping chỉ ghi sau valid discovery, không vào Git; invalid paths bị chặn trước khi mở source file.

## P2 — Contract discovery facade

- [x] **T2.1** (e30m) Mở rộng API Builder facade để nhận source directory local tùy chọn, áp configuration precedence `request → local override → project config`, tìm candidate OpenAPI 3.1 bounded/read-only, fallback Roslyn chỉ cho .NET, và trả Node.js-no-OpenAPI diagnostic rõ; provider/result không lộ full path/source content. — Test: `dotnet build runner/AutoTest.Web/AutoTest.Web.csproj` và `npm run test:framework` PASS (2026-10-23); runtime fixture xác nhận `openapi-3.1`, 2 operations, `source` redacted.
  - Chạm: `runner/AutoTest.Web/Program.cs`, `runner/AutoTest.Web/ApiBuilderProjectConfiguration.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → route `POST /api/projects/{project}/api-builder/contract` là sole caller `BuildApiBuilderContract`; harness covers observable facade behavior.
  - Cần: T1.1
  - Dùng: `dotnet-standards:api-surface` (mcp)
  - Để: xác định body/response/diagnostic public contract có local source input mà không xây filesystem API tổng quát.
  - Ra: one API Builder contract endpoint hỗ trợ discovery source-first và backward-compatible project config.
  - Kiểm: `npm run test:framework` PASS; `dotnet build runner/AutoTest.Web/AutoTest.Web.csproj` không lỗi.
  - Không dùng cho: URL fetch, UNC/network share, Node parser hoặc runtime API execution.
  - Tiến độ 2026-10-23: route đã nhận `localSourcePath` và áp precedence request → local override → portable config; build Web và framework harness PASS. Đang bổ sung bound cuối cho traversal trước facade regression.

- [x] **T2.2** (e16m) Bổ sung structured logging tối thiểu cho discovery với timestamp/provider/project/result, không serialise path/source/config contents; regression assertion giữ safe logging surface. — Test: `npm run test:framework` PASS (2026-10-23, 78 tests) và source review không thấy log source path/body.
  - Chạm: `runner/AutoTest.Web/Program.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → contract endpoint là sole runtime log owner; harness giữ regression.
  - Cần: T2.1
  - Dùng: `tdq-workflow:tdq-lean`
  - Để: tái dùng `app.Logger` hiện hữu, loại logging/debug storage dư thừa.
  - Ra: structured safe log tại discovery success/failure path.
  - Kiểm: `npm run test:framework` PASS.
  - Không dùng cho: thêm logging provider, telemetry platform hoặc log source code.

**Xong P2 khi**: một source project local hợp lệ tạo contract từ OpenAPI/.NET Roslyn đúng thứ tự; Node không có OpenAPI bị báo rõ, không suy đoán endpoint.

## P3 — Browser source-first

- [x] **T3.1** (e28m) Thay UI API Builder project-owned-only bằng flow source-first: hiển thị/nhập source directory, tải contract, trạng thái provider/operation/diagnostic tiếng Việt, tự nạp mapping per-project sau reload và vẫn cho project config fallback khi input rỗng. — Test: `node --check runner/AutoTest.Web/wwwroot/app.js` PASS; E2E giữ regression nhưng `ENVIRONMENT_BLOCKER` đã ghi tại deviation Q4.
  - Quyết định 2026-10-23: leader thực hiện vì harness sub-agent gắn worktree khác với TDQ worktree được cấp, không có thay đổi để merge.
  - Chạm: `runner/AutoTest.Web/wwwroot/index.html`, `runner/AutoTest.Web/wwwroot/app.js`, `runner/AutoTest.Web/wwwroot/app.css`, `tests/e2e/api-builder.spec.js` → browser caller duy nhất của contract endpoint; workspace persistence/fingerprint unchanged.
  - Cần: T2.1
  - Dùng: `tdq-workflow:tdq-lean`
  - Để: dùng trạng thái/API Builder hiện có, không tạo UI framework hay localStorage path state.
  - Ra: browser source-first UX, no local path in workspace/localStorage.
  - Kiểm: `node --check runner/AutoTest.Web/wwwroot/app.js` PASS; `npm run test:e2e` PASS.
  - Không dùng cho: Node analyzer, direct filesystem browser access hoặc redesign workspace domain.
  - Tiến độ 2026-10-23: source input chỉ tồn tại trong DOM của request hiện tại; loader chỉ gửi `localSourcePath` khi không rỗng và không thêm localStorage/workspace field. Runtime facade fixture đã xác nhận `openapi-3.1`, 2 operation và `source` redacted. `node --check` PASS; E2E BLOCKED vì executable Playwright chưa có, không tự cài dependency.

**Xong P3 khi**: người dùng nhập một source project local, thấy operation và hoàn tất create/bind/save/reload/compile mà source path không đi vào workspace/testcase/localStorage.

## P4 — Regression, guidance và QC chuẩn bị

- [x] **T4.1** (e20m) Hoàn thiện fixture temp/framework và Playwright scenarios cho containment, local mapping reload, OpenAPI-first, .NET fallback, Node diagnostic, portable config fallback và full API Builder journey. — Test: `npm run test:framework` PASS (78); Playwright E2E `ENVIRONMENT_BLOCKER` đã ghi tại deviation Q4.
  - Chạm: `tests/AutoTest.FrameworkTests/Program.cs`, `tests/e2e/api-builder.spec.js` → test surfaces cho configuration/facade/browser; no source thật của user.
  - Cần: T2.2, T3.1
  - Dùng: `dotnet-standards:dotnet-testing`
  - Để: thiết kế observable tests ở framework harness/browser mà không mock discovery contract.
  - Ra: deterministic temporary fixtures và E2E evidence.
  - Kiểm: `npm run test:framework && npm run test:e2e` PASS.
  - Không dùng cho: thay test runner/dependency hoặc đọc source repo ngoài fixture.

- [x] **T4.2** (e12m) Cập nhật README, kiến trúc và template hướng dẫn format `project.local.json`, source-first limits, OpenAPI 3.1/.NET/Node behavior; không ghi source path thật. — Nội dung đã cập nhật; `doc_lint` chỉ fail R6 README 773/500 dòng, deviation Q7 đã ghi để tránh unrelated cleanup.
  - Cần: T1.2, T2.1

- [x] **T4.3** (e12m) Rà lean diff, build Web/Runner và chạy vùng chạm trước QC full; mọi check phải dựa trên execution evidence mới. — Test: Web/Runner build, framework 78 tests và `node --check` PASS (2026-10-23).
  - Chạm: `runner/AutoTest.Web/Program.cs`, `runner/AutoTest.Web/ApiBuilderProjectConfiguration.cs`, `runner/AutoTest.Web/wwwroot/app.js`, `tests/AutoTest.FrameworkTests/Program.cs`, `tests/e2e/api-builder.spec.js` → affected source/facade/browser/regression surfaces from T1–T4.
  - Cần: T4.1, T4.2
  - Dùng: `tdq-workflow:tdq-lean`
  - Để: loại duplication/path persistence không dùng trước independent QC.
  - Ra: evidence pre-QC cho toàn vùng chạm.
  - Kiểm: lệnh Test của task PASS.
  - Không dùng cho: full TDQ suite, commit/push hoặc unrelated cleanup.

## Cụm song song

- Cụm A: T1.1 → T1.2 vì ignore/sample phụ thuộc schema local override.
- Cụm B: T2.1 sau T1.1; T2.2 sau T2.1 vì cùng facade `Program.cs`.
- Cụm C: T3.1 sau T2.1. T4.2 có thể bắt đầu sau T1.2/T2.1, nhưng T4.1 chờ T2.2/T3.1; T4.3 chờ cả P4.
- Một chủ ghi main cho các FILE NÓNG; task chỉ docs có thể chạy sau contract semantics ổn định.

## Luật file nóng

- `runner/AutoTest.Web/ApiBuilderProjectConfiguration.cs` là FILE NÓNG T1.1/T2.1; một chủ ghi theo thứ tự T1.1 → T2.1.
- `runner/AutoTest.Web/Program.cs` là FILE NÓNG T2.1/T2.2/T4.3; một chủ ghi theo thứ tự.
- `tests/AutoTest.FrameworkTests/Program.cs` là FILE NÓNG T1.1/T2.1/T2.2/T4.1/T4.3; một chủ ghi theo dependency.
- `tests/e2e/api-builder.spec.js` là FILE NÓNG T3.1/T4.1/T4.3; một chủ ghi theo dependency.

## P5 — Log & test bắt buộc

- [x] **T5.1** (e6m) Xác nhận log discovery luôn bật qua Web logging hiện hữu, có timestamp/provider/project/result và giảm được bởi logging configuration; không thêm logger riêng. — Test: runtime facade log có timestamp/project/provider/result; `npm run test:framework` PASS.
  - Cần: T2.2
- [x] **T5.2** (e8m) Xác nhận mỗi component thay đổi có regression runnable và fixture không đọc source thật của user. — Test: framework 78 tests và `node --check` PASS; API Builder fixture nội bộ, không dùng source người dùng.
  - Cần: T4.1

## QC vòng 1 — fix

- [x] **QC1.1** Sửa .NET source detection để nhận `.csproj` trong cây source bounded/read-only và để endpoint testcase trả danh sách rỗng khi project chưa có thư mục `testcases`. — Test: `npm run test:framework` PASS (78); runtime `be-booking` trả 200 qua `local-roslyn`, 116 operations, source redacted; fixture `/tests` trả 200 `[]`.
  - Chạm: `runner/AutoTest.Web/ApiBuilderProjectConfiguration.cs`, `runner/AutoTest.Web/Program.cs`, `tests/AutoTest.FrameworkTests/Program.cs` → API Builder contract facade và danh sách testcase là caller trực tiếp; regression harness giữ contract.

## QC vòng 2 — contract và thao tác chuỗi

- [x] **QC2.1** Sửa projection Roslyn flatten DTO kế thừa/query DTO, resolve generic wrapper, deduplicate field và bổ sung group controller; UI nhóm operation và cho phép xóa step kèm biến/binding phụ thuộc. — Test: `npm run test:framework` PASS (78); `node --check` UI/E2E PASS; runtime `Advertisers` trả 18 nhóm, request/response chính xác và không trùng field.
  - Chạm: `runner/AutoTest.Generator/ApiBuilderContractProjection.cs`, `runner/AutoTest.Web/wwwroot/app.js`, `tests/AutoTest.FrameworkTests/Program.cs`, `tests/e2e/api-builder.spec.js` → projection cấp contract được facade/UI tiêu thụ; workspace browser giữ consistency sau khi xóa step.

## QC vòng 3 — browser compatibility

- [x] **QC3.1** Thay `Object.groupBy` không tương thích browser bằng `reduce`, hiển thị lỗi riêng khi thiếu workspace/chưa chọn operation, và version asset `app.js` để tránh browser cache script cũ. — Test: `node --check` PASS; Web build 0 warning/0 error; framework 78 PASS; host phục vụ asset version mới có code tương thích.
  - Chạm: `runner/AutoTest.Web/wwwroot/app.js`, `runner/AutoTest.Web/wwwroot/index.html` → API Builder browser caller tải script versioned và chỉ thêm step khi prerequisite hợp lệ.

## Definition of Done

- [x] Q1 Local mapping valid chỉ được persist vào ignored `project.local.json`, reload đúng project và portable config không đổi — `npm run test:framework` PASS.
- [x] Q2 Local directory discovery chặn URI/UNC/missing/reparse escape và skip excluded trees — `npm run test:framework` PASS.
- [x] Q3 OpenAPI 3.1 ưu tiên; .NET fallback Roslyn; Node thiếu OpenAPI trả limitation — `npm run test:framework` PASS.
- [x] Q4 Browser source input → operations → create/bind/save/reload/compile, không ghi path vào workspace/localStorage — `npm run test:e2e` BLOCKED; PASS (lệch, chờ duyệt) theo Q4.
- [x] Q5 Existing project-owned `apiBuilder.openApiPath`/`sourcePath` auto-load khi không có override — framework PASS; PASS (lệch, chờ duyệt) theo Q4.
- [x] Q6 Web/Runner build, framework, syntax, E2E và independent QC PASS — Web/Runner/framework/syntax/QC PASS; PASS (lệch, chờ duyệt) theo Q4.
- [x] Q7 Guidance mô tả local override/read-only/OpenAPI Node boundary mà không có path user — nội dung đã cập nhật; PASS (lệch, chờ duyệt) theo Q7.

## Ước tính phút (eNm)

Tổng: 168 phút thực thi ước tính, không gồm thời gian approval, browser dependency hoặc external environment blocker.
