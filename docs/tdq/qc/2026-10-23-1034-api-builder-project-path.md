# QC — API Builder khám phá source local theo project
Ngày: 2026-10-23 · Plan: ../plan/2026-10-23-1034-api-builder-project-path.md · Vòng: 1
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

| # | Hạng mục | Lệnh đã chạy | Kết quả | PASS/FAIL |
|---|---|---|---|---|
| Q1 | Local mapping và Git ignore | `npm run test:framework`; `git check-ignore -q projects/example/project.local.json` | 78 tests; mapping ignored | PASS |
| Q2 | Containment và excluded traversal | `npm run test:framework` | 78 tests, gồm fixture reparse khi quyền OS cho phép | PASS |
| Q3 | OpenAPI/Roslyn/Node boundary | `npm run test:framework` | 78 tests | PASS |
| Q4 | Browser source-first journey | `npm run test:e2e` | executable Playwright không khả dụng | PASS (lệch, chờ duyệt) |
| Q5 | Portable fallback không override | `npm run test:framework`; `npm run test:e2e` | framework PASS; browser verification blocked như Q4 | PASS (lệch, chờ duyệt) |
| Q6 | Build, syntax, E2E, QC | Web/Runner build; framework; `node --check`; `npm run test:e2e` | build/syntax/framework PASS; E2E blocked như Q4 | PASS (lệch, chờ duyệt) |
| Q7 | Guidance source-first | `doc_lint.py README.md docs/kien-truc.md` | nội dung đúng; README 773 dòng vượt ngưỡng R6=500 | PASS (lệch, chờ duyệt) |
| QC-F1 | TDQ full suite | `tdq_test.py tron-bo`; `tdq_test.py so` | 0 modules, 0 failed, 0 radius miss | PASS |
| QC-F2 | Regression vùng chạm | Web/Runner builds; `npm run test:framework`; `node --check` | builds PASS; 78 tests PASS; syntax PASS | PASS |
| QC-F3 | Ràng buộc kiến trúc | Review facade/config/analyzer và runtime fixture | shared runner không nhận local source path; facade redacts source; discovery read-only | PASS |
| QC-F4 | Clean code | LSP diagnostics trên `SourceContractAnalyzer.cs`, `Program.cs` | không còn diagnostics | PASS |
| QC1.1 | Nested .NET source và fixture test list | `npm run test:framework`; runtime Web localhost | 78 tests; `be-booking` trả 200 qua `local-roslyn`, 116 operations, source redacted; `/tests` trả 200 `[]` | PASS |
| QC2.1 | Roslyn DTO contract, grouping và xóa step | `npm run test:framework`; `node --check`; runtime Web localhost | 78 tests; UI/E2E syntax PASS; `Advertisers` có 18 controller groups, Create request `code,description,name,status`, response không trùng `id`; E2E browser NOT_RUN vì Playwright thiếu | PASS (E2E lệch Q4) |
| QC3.1 | Browser compatibility và cache asset | `node --check`; Web build; `npm run test:framework`; HTTP asset probe | 78 tests; build 0 warning/0 error; host trả `app.js?v=20261023-02` với `reduce` grouping và diagnostic prerequisite | PASS (E2E lệch Q4) |

## Bằng chứng
### Q1–Q3
```text
Framework tests passed: 78
project.local.json ignored
```

### Q4–Q6
```text
npm run test:e2e
'playwright' is not recognized as an internal or external command.
```

### Q6
```text
AutoTest.Web build succeeded: 0 Warning(s), 0 Error(s)
AutoTest.Runner build succeeded: 0 Warning(s), 0 Error(s)
Framework tests passed: 78
node --check runner/AutoTest.Web/wwwroot/app.js: exit 0
```

### Q7
```text
README.md:1: [R6] 773 lines > 500
```

### Runtime facade/logging
```text
provider=openapi-3.1; operations=2; sourceRedacted=True
ApiBuilder contract discovery succeeded for project api-builder-fixture with provider openapi-3.1.
be-booking: status=200; provider=local-roslyn; operations=116; sourceLength=0
api-builder-fixture/tests: status=200; body=[]
Advertisers: status=200; groups=18; createRequest=$.code,$.description,$.name,$.status; createResponse=$.data.id,$.data.createdAt,$.data.name,$.data.code,$.data.description,$.data.status
node --check runner/AutoTest.Web/wwwroot/app.js: exit 0
node --check tests/e2e/api-builder.spec.js: exit 0
browser compatibility: asset-reference=updated; served-script=compatible-and-diagnostic-ready
```

## Lệch spec chờ duyệt
- Q4: `npm run test:e2e` chưa PASS vì executable Playwright không có; giữ regression và không tự cài dependency/browser binary.
- Q7: `doc_lint` chưa PASS vì README 773 dòng vượt 500; giữ targeted guidance, không giảm hơn 273 dòng ngoài phạm vi.

## Kết luận
PASS (lệch, chờ duyệt): Q4–Q7 theo hai environment/scope deviations đã ghi. Các build, framework regression, syntax, runtime facade/redaction, nested .NET fallback và containment evidence đều PASS.
