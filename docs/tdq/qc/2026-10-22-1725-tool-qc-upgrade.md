# QC — Nâng cấp Tool-QC cho API Builder và UnitX
Ngày: 2026-10-22 · Plan: ../plan/2026-10-22-1725-tool-qc-upgrade.md · Vòng: 1
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

| # | Hạng mục | Lệnh đã chạy | Kết quả | PASS/FAIL |
|---|---|---|---|---|
| Q1 | Contract normalization | `dotnet run --project tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj` | Framework harness kiểm OpenAPI/Roslyn và diagnostic, `Framework tests passed: 66`. | PASS |
| Q2 | Journey API Builder | Framework harness + API runtime create/save/load/compile/preflight; browser click-through không có driver. | Runtime đã xác minh save/load 2 step, 1 biến `orderId`; compile/preflight `passed=True`, `readyToSave=False`, warnings=4. Browser journey không có evidence. | PASS (lệch, chờ duyệt #1) |
| Q3 | Biên và recovery workspace | `dotnet run --project tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj` | Harness kiểm duplicate, reserved name, traversal, corrupt artifact và revision conflict; 66 tests pass. | PASS |
| Q4 | Safety/redaction | `dotnet run --project tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj`; grep secret keys trong Builder/UnitX | Regression pass; không thấy secret-key literal trong Builder/UnitX surface. Runtime guards giữ ở `RunnerEngine.Guard` và redaction. | PASS |
| Q5 | Publish testcase | API Builder compile/preflight runtime; `dotnet run --project tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj` | Compiler tạo schema-v2 và `save`/`${variable}`; preflight đúng là không cho official save khi coverage còn blocked, không bypass SHA-256 activation. | PASS |
| Q6 | UnitX journey | `dotnet run --project tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj` | Decision table whitelist chạy actual/expected/pass-fail; framework harness pass 66. | PASS |
| Q7 | UnitX negative/recovery | `dotnet run --project tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj` | Unknown operator, invalid type và vượt rule budget bị từ chối bởi harness; không mở đường code/network/file. | PASS |
| Q8 | Regression | `dotnet build ApiAutoTest.sln`; framework harness; smoke `be-booking --tags smoke` | Solution build 0 warning/error; framework 66 pass. Smoke 2/2 bị `localhost:5018` từ chối kết nối: ENVIRONMENT_BLOCKER, tách biệt khỏi build/test. | PASS |
| Q9 | UI quality | `node --check runner/AutoTest.Web/wwwroot/app.js`; build Web; đọc UI text; browser click-through không có driver. | Syntax JS và Web build pass; UI text Việt có cho Builder/UnitX/preflight. Không có evidence thao tác browser. | PASS (lệch, chờ duyệt #2) |
| QC-F1 | Full suite | `dotnet run --project tests/AutoTest.FrameworkTests/AutoTest.FrameworkTests.csproj`; `tdq_test.py tron-bo`; `tdq_test.py so` | Full framework harness .NET PASS 66. TDQ Python harness phát hiện 0 module test, kết thúc 0 failed; full-suite budget 1/2, radius misses 0. | PASS |
| QC-F2 | Regression vùng chạm | `tdq_test.py vung-cham --files ...`; framework harness | TDQ fallback toàn bộ vì C# nằm ngoài universe Python, 0 module; framework .NET harness PASS 66 và solution/Web/Runner builds PASS. | PASS |
| QC-F3 | Ràng buộc kiến trúc §5 | `grep` Builder/UnitX về nhánh project-specific/secret; API runtime evidence | Không có hard-code tên project; các `project.Equals` chỉ bind workspace với project request. Không thấy secret-key literal trong Builder/UnitX. Builder dùng canonical draft/preflight/activation, UnitX tách HTTP runner. | PASS |
| QC-F4 | Clean code | Self-check | Source code đã thay đổi trong request: SRP Có; OCP Có (operator/contract models đóng); LSP Có (error contracts xác định); ISP Có; DIP Có (Builder tái dùng draft lifecycle, UnitX qua Web facade). | PASS |

## Bằng chứng
### Q1/Q3/Q4/Q5/Q6/Q7
```text
Framework tests passed: 66
```

### Q2/Q5
```text
API Builder save/load verified: id=443afa48447a4966beabfaecb35c2426; steps=2; variables=1
API Builder compile/preflight verified:
caseId=api-builder-verification;
preflight=True;
readyToSave=False;
warnings=4
```

### Q8
```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

[THẤT BẠI] system.booking-and-mtc-integration-health ... actively refused it. (localhost:5018)
[THẤT BẠI] system.health-contract ... actively refused it. (localhost:5018)
Tổng số đã chạy: 2/2, Thành công: 0, Thất bại: 2
```

### Q9
```text
node --check runner/AutoTest.Web/wwwroot/app.js
# Exit code 0

AutoTest.Web build: 0 Warning(s), 0 Error(s)
```

### QC-F1/QC-F2
```text
tdq_test tron-bo: Ran 0 tests; failed=0; missed=0
tdq_test so: full-suite runs 1 of budget 2; radius misses 0
tdq_test vung-cham: fallback full suite, 0 modules; ok=True
```

## Lệch spec chờ duyệt
- #1 Q2 · Browser journey API Builder click-through chưa có driver/browser automation; áp dụng: ghi BLOCKED browser journey, giữ API runtime/compiler/preflight evidence.
- #2 Q9 · UI journey tiếng Việt chưa có driver/browser automation; áp dụng: ghi BLOCKED browser journey, giữ syntax/build/text review evidence.

## Kết luận
PASS có lệch chờ duyệt #1 và #2. Build, framework regression và các guard/contract có evidence hiện tại. Smoke `be-booking` là ENVIRONMENT_BLOCKER do local API không lắng nghe tại `localhost:5018`, không phải testcase PASS. TDQ Python test harness không có test module nên chỉ là telemetry; full regression evidence là framework harness .NET (66 PASS).