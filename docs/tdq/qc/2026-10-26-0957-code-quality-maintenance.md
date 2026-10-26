# QC — Chất lượng code và khả năng bảo trì
Ngày: 2026-10-26 · Plan: ../plan/2026-10-26-0957-code-quality-maintenance.md · Vòng: 1
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

| # | Hạng mục | Lệnh đã chạy | Kết quả | PASS/FAIL |
|---|---|---|---|---|
| Q1 | Format source Web | `npm run check-format` | All matched files use Prettier code style | PASS |
| Q2 | ES modules và browser journey | `node tests/workbook-selection-ui.test.js` + `npm run test:e2e -- api-builder.spec.js` | Workbook self-check PASS; API Builder journey 1/1 PASS | PASS |
| Q3 | Workspace concurrency | `npm run test:framework` | API Builder/UnitX concurrent-save regressions và JSON readback PASS | PASS |
| Q4 | stderr-heavy process | `npm run test:framework` | Child `cmd.exe` stderr-heavy hoàn tất trong timeout và thu đủ hai stream | PASS |
| Q5 | OpenAPI malformed | `npm run test:framework` | array/missing/null/number/empty ném `InvalidDataException`; 3.1 vẫn PASS | PASS |
| Q6 | Build, review và runtime log | `npm run build`, `npm run test:framework`, runtime smoke | Build 0 warning/0 error; framework PASS; reviewer không còn finding xác minh được; Web trả HTTP 200 và log có timestamp ISO | PASS |
| QC-F1 | Full-suite workflow | `tdq_test.py tron-bo` + `tdq_test.py so` | 0 module Python được đăng ký; 0 miss, 1/2 lần full-suite budget; .NET/UI suite thực tế ở Q1–Q6 PASS | PASS |
| QC-F2 | Regression vùng chạm | Q1–Q6 commands | Framework, formatter, workbook UI và API Builder E2E có coverage thực thi | PASS |
| QC-F3 | Ràng buộc kiến trúc | Review diff + browser E2E | Không đổi HTTP routes/workflow; helper chỉ ở Web; UnitX core/shared runner không bị chạm; OpenAPI pipeline vẫn chung CLI/Web | PASS |
| QC-F4 | Clean code | Self-check | SRP: Có — persistence/process helpers có một trách nhiệm. OCP: Có — caller dùng shared helper. LSP: Có — error/result contracts nhất quán. ISP: Có — helper nhận đúng process/path/token cần dùng. DIP: Có — subprocess và persistence callers đi qua entry chung, không copy logic. | PASS |

## Bằng chứng
### Q1
```
Checking formatting...
All matched files use Prettier code style!
```

### Q2
```
workbook selection UI self-check: PASS
1 passed (6.8s)
```

### Q3–Q5
```
Framework tests passed: 78
```

### Q6
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
HTTP 200 OK
2026-10-26T11:46:20.988+07:00 info: Microsoft.Hosting.Lifetime[14]
Now listening on: http://127.0.0.1:5080
```

### QC-F1
```
tdq_test: tron-bo modules=0 failed=0 missed=0 ok=True
tdq_test: so request=2026-10-26-0957-code-quality-maintenance tron_bo=1 bo_sot=0 budget=2
```

## Lệch spec chờ duyệt
Không có.

## Kết luận
PASS toàn bộ.
