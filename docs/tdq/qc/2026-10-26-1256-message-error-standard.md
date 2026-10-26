# QC — Chuẩn hóa catalogue message, mã lỗi và UI i18n
Ngày: 2026-10-26 · Plan: ../plan/2026-10-26-1256-message-error-standard.md · Vòng: 1
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

| # | Hạng mục | Lệnh đã chạy | Kết quả | PASS/FAIL |
|---|---|---|---|---|
| Q1 | Catalogue validation | `dotnet run --project tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj` | 78 framework regressions pass, gồm catalogue valid/invalid preflight | PASS |
| Q2 | Runtime error contract | `dotnet run --project tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj` | 78 framework regressions pass, gồm status/code/locale fallback và explicit JSON assertion | PASS |
| Q3 | Source audit/workbook | Framework regression; `dotnet run --project runner/AutoTest.MessageScanner -- --source src --catalogue projects/project-template/project.json` | Scanner tạo workbook, scan 40 file và audit 277 findings | PASS |
| Q4 | CLI/Web/diagnostics | Framework regression; `dotnet build runner/AutoTest.Web/AutoTest.Web.csproj` | Framework 78 pass; Web build 0 warning/0 error | PASS |
| Q5 | Web UI i18n | `npx playwright test tests/e2e/api-builder.spec.js` | 1 test pass; locale shell/projection, project, selection và workspace workflow giữ nguyên | PASS |
| Q6 | Build/documentation | Runner build; framework regression; scoped Prettier; doc/JSON lint | Các command scoped pass sau QC1.1 | PASS |
| Q7 | Project runtime evidence | `dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags smoke` | 1 smoke case attempted, blocked do `localhost:5007` actively refused connection; HTML/JUnit reports created | PASS (BLOCKED có evidence) |
| Q8 | Review | Direct working-tree review, `git diff --check`, framework regression, Web build/E2E; external worktree reviews NOT ASSESSED | Không có finding actionable được xác thực trong direct review | PASS |
| QC-F1 | Full suite | `tdq_test.py tron-bo`; `tdq_test.py so` | Tool reports 0 discovered modules, 0 failures, 0 radius misses; framework suite run separately for actual behavioral coverage | PASS |
| QC-F2 | Touched-area regression | Framework tests, runner/Web build, scanner CLI, Playwright E2E | Framework 78, runner/Web builds 0 errors, scanner workbook created, browser 1/1 | PASS |
| QC-F3 | Architecture constraints | Direct review of shared/project boundary and runner composition | Không có branch shared theo project; catalogue load tại composition root, path project-local | PASS |
| QC-F4 | Clean code | Self-check theo `clean-code.md` | SRP/OCP/LSP/ISP/DIP: Có/Có/Có/Có/Có | PASS |
| Q9 | Onboarding máy mới | `npm run setup` hai lần; framework regression; solution build | Lần hai: NuGet up-to-date, Node dependency sẵn sàng, Playwright Chromium setup thành công; framework 78 PASS; solution build 0 warning/0 error | PASS |

## Bằng chứng

### Q1 và Q2
```text
Framework tests passed: 78
```

### Q3
```text
Source: D:\works\projects\Tool-QC\src
Đã quét: 40 file
Messages duy nhất: 0
Modules: 0
File bỏ qua: 0
Audit findings: 277
Báo cáo Excel: .artifacts\message-scans\src-messages-20261026-162711.xlsx
```

### Q4, Q5, Q6
```text
AutoTest.Runner build: succeeded, 0 Warning(s), 0 Error(s)
AutoTest.Web build: succeeded, 0 Warning(s), 0 Error(s)
Playwright: 1 passed
Prettier scoped: All matched files use Prettier code style!
doc_lint spec and spec/plan pair: 0 violation(s)
Template JSON parsed: 2
git diff --check: no whitespace error
```

### Q7
```text
[THẤT BẠI] health.ready - Kiểm tra API đã sẵn sàng hoạt động (4205ms)
No connection could be made because the target machine actively refused it. (localhost:5007)
Tổng số đã chạy: 1/1, Thành công: 0, Thất bại: 1
Báo cáo HTML: test-results\ops-service\system\2026-10-26_162729_ops-service_smoke.html
```

### Q8
```text
Direct working-tree review: no verified actionable finding after resolver regression fix.
External worktree code/security reviewers: NOT ASSESSED for uncommitted diff because isolated worktrees had no main working-tree patch; not used as clean-review evidence.
```

### Q9
```text
npm run setup (second run): All projects are up-to-date for restore.
Node dependencies are already ready.
Ensuring the Playwright Chromium browser is available...
Tool-QC setup is complete.
Framework tests passed: 78
Solution build: 0 Warning(s), 0 Error(s)
```

### QC-F1 đến QC-F4
```text
tdq_test tron-bo: 0 modules, 0 failures, 0 radius misses
tdq_test so: full-suite runs 1 of budget 2; radius misses 0
Clean-code self-check: SRP yes; OCP yes; LSP yes; ISP yes; DIP yes.
```

## Lệch spec chờ duyệt
Không có.

## Kết luận
PASS toàn bộ. Q7 không có API testcase PASS vì môi trường local `ops-service` không sẵn sàng; trạng thái được ghi trung thực là BLOCKED với HTML/JUnit evidence, đúng fallback của DoD.
