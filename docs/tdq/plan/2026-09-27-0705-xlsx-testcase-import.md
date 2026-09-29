# PLAN — Nhập XLSX và sinh testcase theo adapter dự án

Ngày: 2026-09-27 · Spec: ../spec/2026-09-27-0705-xlsx-testcase-import.md (bản 1.1, ĐÃ DUYỆT) · Lane: full
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md
Mode thực thi: main — mô phỏng plan đã bỏ unit test cho main 34,6 phút và team 39,9 phút; chuỗi phụ thuộc 13 wave làm overhead sub-agent lớn hơn 5,2 phút (ĐỀ XUẤT, user chốt lúc duyệt)
Trạng thái plan: HOÀN THÀNH

## Mục lục

- Quy tắc thi hành
- P1 — Spike reader và khóa contract
- P2 — Shared workbook core
- P3 — Import application và adapter VERIFY
- P4 — UI, CLI và activation safety
- P5 — Test, tài liệu và QC full
- Cụm song song
- Luật file nóng
- Log & test bắt buộc
- Definition of Done
- Ước tính phút
- Dòng Mode thực thi

## Quy tắc thi hành

1. Thứ tự phase là thứ tự phụ thuộc — không đảo.
2. Mỗi task đánh `[~]`, triển khai, build/CLI smoke tương ứng rồi đổi `[x]` ngay; không viết hoặc chạy unit test theo yêu cầu người dùng.
3. Sau mỗi phase chạy build hoặc smoke tương ứng; lỗi compile/runtime tool thì agent tự sửa và chạy lại, không yêu cầu người dùng gửi lỗi từng phần.
4. Không commit/push cho đến khi user yêu cầu.
5. Không sửa backend `be-booking`; analyze/generate workbook không mở HTTP request tới backend.
6. QC FAIL do code/test thì thêm task fix vào plan và lặp đến khi PASS.

## P1 — Spike reader và khóa contract

- [x] **T1.1** (e16m) Tạo spike CLI đọc workbook Campaign bằng Open XML SDK, đo parse time/working set và xác nhận lấy được sheet visibility, merged ranges, formulas/cached values cùng block `VERIFY/54` — Kiểm: CLI xuất summary có 17 sheet và các quota Case 54 đúng.
  - Chạm: `runner/AutoTest.Generator/WorkbookImport/WorkbookProbe.cs`, `runner/AutoTest.Generator/Program.cs` → probe mới và generator composition root file nóng.
  - Dùng: `spreadsheets`
  - Để: đối chiếu workbook thực và golden coordinates trước khi viết parser.
  - Ra: probe command và evidence console từ workbook thật.
  - Kiểm: CLI probe exit code 0 và summary parse được.
  - Không dùng cho: định nghĩa business mapping ngoài family `VERIFY`.
- [x] **T1.2** (e12m) Chốt workbook-neutral model, security limits, diagnostic codes và adapter contract từ spike — Kiểm: build project shared thành công và contract không chứa chuỗi `be-booking`, `Campaign` hoặc `VERIFY`.
  - Chạm: `src/AutoTest.Workbooks/Contracts.cs`, `src/AutoTest.Workbooks/WorkbookImportOptions.cs`, `src/AutoTest.Workbooks/AutoTest.Workbooks.csproj` → file mới, chưa caller; solution wiring ở task riêng.
  - Cần: T1.1
  - Dùng: `brainstorming`
  - Để: giữ shared/project boundary và model đủ cho nhiều dự án.
  - Ra: contracts/options biên dịch được.
  - Kiểm: build thành công và source scan không thấy business token trong shared module.
  - Không dùng cho: quyết định request/assertion riêng của Campaign.

**Xong P1 khi**: spike chứng minh Open XML SDK đọc đủ metadata cần thiết trong giới hạn chấp nhận và contract shared không mang nghiệp vụ project.

## P2 — Shared workbook core

- [x] **T2.1** (e20m) Triển khai guard `.xlsx`/ZIP: extension, signature, file size, entry count, total uncompressed bytes, compression ratio và cancellation — Kiểm: CLI smoke chấp nhận workbook thật và từ chối các file bất thường bằng diagnostic ổn định.
  - Chạm: `src/AutoTest.Workbooks/XlsxPackageGuard.cs`, `runner/AutoTest.Generator/WorkbookImport/WorkbookProbe.cs` → guard và smoke command.
  - Cần: T1.2
  - Dùng: `excel-miniexcel`
  - Để: áp dụng checklist import Excel an toàn và blank/size guards ở shared layer.
  - Ra: guard reusable cùng smoke positive/negative.
  - Kiểm: CLI guard smoke exit code đúng; cancellation không để stream/temp mở.
  - Không dùng cho: chọn MiniExcel làm reader hoặc parse Campaign.
- [x] **T2.2** (e30m) Triển khai Open XML reader theo worksheet part, shared strings, styles/dates, sparse cells, formulas/cached values và merged-anchor metadata — Kiểm: CLI probe đọc workbook thật đúng summary và tuân thủ giới hạn rows/cells.
  - Chạm: `src/AutoTest.Workbooks/OpenXmlWorkbookReader.cs`, `src/AutoTest.Workbooks/WorkbookModels.cs`, `src/AutoTest.Workbooks/AutoTest.Workbooks.csproj`, `runner/AutoTest.Generator/WorkbookImport/WorkbookProbe.cs` → reader/model và probe.
  - Cần: T2.1
  - Dùng: `dotnet-feature-flow`
  - Để: triển khai flow I/O async/cancellation và mapping model rõ ranh giới.
  - Ra: reader production cùng evidence CLI.
  - Kiểm: build xanh; CLI summary cell types/merge/formula/date đúng.
  - Không dùng cho: sinh schema v2 hoặc profile `be-booking`.
- [x] **T2.3** (e10m) Thêm structured logging cho import lifecycle, fingerprint rút gọn, counts, duration và diagnostics mà không log cell content — Kiểm: CLI output có metadata bắt buộc và không chứa sample cell text.
  - Chạm: `src/AutoTest.Workbooks/WorkbookImportLogger.cs`, `src/AutoTest.Workbooks/WorkbookImportOptions.cs`, `runner/AutoTest.Generator/WorkbookImport/WorkbookProbe.cs` → logging runtime và probe.
  - Cần: T2.2

**Xong P2 khi**: shared reader từ chối input nguy hiểm, đọc đúng metadata fixture và có log an toàn/tắt giảm được.

## P3 — Import application và adapter VERIFY

- [x] **T3.1** (e20m) Tạo import application service/registry resolve adapter từ project descriptor, trả analysis gồm fingerprint, cases, support state và diagnostics — Kiểm: CLI project không có descriptor nhận `adapter-not-configured`; source registry không branch theo project name.
  - Chạm: `runner/AutoTest.Generator/WorkbookImport/WorkbookImportService.cs`, `runner/AutoTest.Generator/WorkbookImport/WorkbookAdapterRegistry.cs`, `runner/AutoTest.Generator/AutoTest.Generator.csproj` → generator application mới.
  - Cần: T2.3
  - Dùng: `dotnet-feature-flow`
  - Để: nối reader với adapter qua dependency inversion và cancellation.
  - Ra: analyze/generate service dùng được từ CLI/Web.
  - Kiểm: build xanh và hai CLI smoke configured/unconfigured trả đúng code.
  - Không dùng cho: parse layout Campaign trong shared service.
- [x] **T3.2** (e30m) Tạo adapter/profile `be-booking` nhận diện title/block family `VERIFY`, merged-anchor propagation, stable ID và blocked diagnostics cho layout chưa hỗ trợ — Kiểm: CLI tìm đúng catalog VERIFY trong workbook hiện tại, ID không trùng và không nhận `1Day_v2/54` là `VERIFY/54`.
  - Chạm: `projects/be-booking/workbook-import/workbook-import.json`, `projects/be-booking/workbook-import/verify-profile.json`, `src/AutoTest.ProjectAdapters.BeBooking/VerifyWorkbookAdapter.cs`, `src/AutoTest.ProjectAdapters.BeBooking/AutoTest.ProjectAdapters.BeBooking.csproj` → adapter/project profile mới.
  - Cần: T3.1
  - Dùng: `spreadsheets`
  - Để: ánh xạ layout workbook đã phân tích vào domain model project.
  - Ra: catalog supported/blocked cases có source coordinates.
  - Kiểm: CLI analysis JSON chứng minh unique stable ID và source coordinates.
  - Không dùng cho: các sheet/family ngoài `VERIFY`.
- [x] **T3.3** (e36m) Viết generator template cho schema v2: fixture TimeFrame động, Campaign flows, Vietnamese names, coverage/source metadata và một JSON/case — Kiểm: output Case 54 giữ A/B/C quotas, không chứa `06:00` và qua preflight.
  - Chạm: `src/AutoTest.ProjectAdapters.BeBooking/VerifyCaseGenerator.cs`, `src/AutoTest.ProjectAdapters.BeBooking/Templates/` → template/project adapter.
  - Cần: T3.2
  - Dùng: `dotnet-testing`
  - Để: đối chiếu sâu Case 54 và schema output bằng artifact CLI/preflight.
  - Ra: generated suite/case JSON deterministic.
  - Kiểm: CLI generate + JSON inspection + schema-v2 preflight PASS.
  - Không dùng cho: runtime gọi backend hoặc nới business expectation.

**Xong P3 khi**: CLI-level service nhận diện `VERIFY`, sinh Case 54 đúng logic và block riêng các variant chưa hỗ trợ.

## P4 — UI, CLI và activation safety

- [x] **T4.1** (e20m) Mở rộng CLI generator với lệnh analyze/import workbook, selection theo stable ID và output draft/diagnostics — Test: CLI cùng fixture trả JSON parse được, exit code phân biệt success/blocked/invalid input.
  - Chạm: `runner/AutoTest.Generator/Program.cs`, `runner/AutoTest.Generator/Models.cs` → composition root/hot files của generator.
  - Cần: T3.3
- [x] **T4.2** (e28m) Thêm Web endpoints multipart upload/analyze/generate, temp-file lifecycle và cancellation; endpoint chỉ gọi import service/CLI contract và không tự parse Excel — Kiểm: Web smoke upload hợp lệ trả catalog, filename traversal bị vô hiệu, lỗi/hủy dọn temp.
  - Chạm: `runner/AutoTest.Web/Program.cs`, `runner/AutoTest.Web/AutoTest.Web.csproj` → Web composition root/hot files.
  - Cần: T4.1
  - Dùng: `dotnet-feature-flow`
  - Để: triển khai HTTP upload boundary, validation và cancellation đúng .NET.
  - Ra: API workbook analyze/generate có response contract ổn định.
  - Kiểm: process-level Web smoke không tạo request backend.
  - Không dùng cho: đặt parser Campaign trong Web.
- [x] **T4.3** (e26m) Thêm UI chọn `.xlsx`, hiển thị sheet/case support/diagnostics, chọn case, generate draft và chuyển sang review/preflight/run hiện có — Kiểm: browser/Web smoke giữ selection, render blocked reason và draft JSON vào editor hiện tại.
  - Chạm: `runner/AutoTest.Web/wwwroot/index.html`, `runner/AutoTest.Web/wwwroot/app.js`, `runner/AutoTest.Web/wwwroot/app.css` → generator UI hiện có.
  - Cần: T4.2
- [x] **T4.4** (e24m) Thêm deterministic activation path và preview/diff states `create/update/unchanged/conflict`; chỉ overwrite khi confirmed và fingerprint khớp — Kiểm: scripted smoke chứng minh conflict không sửa file và confirmed update chỉ chạm JSON tương ứng.
  - Chạm: `runner/AutoTest.Web/Program.cs`, `runner/AutoTest.Web/wwwroot/app.js`, `projects/be-booking/testcases/workbook-generated/verify/.gitkeep` → activation hot files và output group mới.
  - Cần: T4.3

**Xong P4 khi**: UI và CLI cho cùng catalog/draft, upload an toàn và activate không ghi đè im lặng.

## P5 — Build, tài liệu và QC tự động

- [x] **T5.1** (e16m) Chạy build toàn solution, tự đọc và sửa mọi compile error/warning-as-error phát sinh đến khi exit code 0; không yêu cầu người dùng gửi log lỗi từng phần — Kiểm: build cuối cùng PASS.
  - Chạm: các file source thuộc task trước nếu compiler chỉ ra lỗi → vùng sửa giới hạn trong feature; không sửa backend/testcase manual.
  - Cần: T4.4
  - Dùng: `dotnet-testing`
  - Để: dùng hướng dẫn build/validation .NET nhưng bỏ unit test theo chỉ đạo người dùng.
  - Ra: build log xanh và danh sách lỗi đã tự sửa.
  - Kiểm: solution build exit code 0.
  - Không dùng cho: tạo/chạy unit test hoặc yêu cầu user triage compiler output.
- [x] **T5.2** (e14m) Cập nhật README và hồ sơ kiến trúc với command/UI flow, package, adapter boundary, output group và giới hạn `VERIFY` — Test: tài liệu không tuyên bố hỗ trợ sheet ngoài phạm vi và architecture giữ đúng bốn section.
  - Cần: T5.1
- [x] **T5.3** (e18m) Chạy QC: build solution, CLI analyze/generate workbook thật, schema-v2 preflight Case 54 và Web smoke không mở request backend — Kiểm: tất cả command exit 0; artifact/report ghi counts và đường dẫn draft.
  - Cần: T5.2
- [x] **T5.4** (e10m) Ghi QC/report và working log, đối chiếu git diff không chạm testcase manual/backend — Test: report có PASS/FAIL/BLOCKED cho đủ Q1–Q12 và danh sách file thay đổi.
  - Cần: T5.3

**Xong P5 khi**: build/test/preflight/Web smoke PASS và tài liệu/report phản ánh đúng giới hạn release đầu.

## Cụm song song

- Sau T1.2, T2 shared core là critical path và thực hiện tuần tự vì cùng phụ thuộc reader/model.
- Khi T3.1 có contract ổn định, T3.2 adapter có thể tách worktree; UI static T4.3 chỉ bắt đầu sau response contract T4.2.
- T4.1 CLI và phần HTML/CSS khung của T4.3 có thể chuẩn bị song song, nhưng `app.js` integration chờ T4.2.
- T5.2 tài liệu có thể chạy song song với phần test bổ sung không chạm docs; T5.3–T5.4 luôn tuần tự.

## Luật file nóng

- `runner/AutoTest.Generator/Program.cs`, `runner/AutoTest.Web/Program.cs` và `runner/AutoTest.Web/wwwroot/app.js` chỉ có một chủ ghi trong từng đợt.
- Solution/project-reference wiring được gộp vào task đầu tiên tạo module tương ứng; agent khác không tự sửa solution cùng đợt.
- `docs/kien-truc.md` chỉ T5.2 sửa; các task khác đọc nhưng không ghi.

## Log & test bắt buộc

- [x] **TX.1** (e6m) Log import bật mặc định, có timestamp/level/fingerprint/count/duration và giảm được qua config — Kiểm: CLI smoke log đủ metadata và không có cell content.
  - Chạm: `src/AutoTest.Workbooks/WorkbookImportLogger.cs`, `runner/AutoTest.Generator/WorkbookImport/WorkbookProbe.cs` → đã triển khai ở T2.3, task này xác minh contract cuối.
  - Cần: T5.1

Unit test: BỎ — người dùng yêu cầu chỉ build và agent tự sửa lỗi; validation hành vi dùng CLI/preflight/Web smoke không gọi backend.

## Definition of Done

- [x] Q1 Input/ZIP nguy hiểm bị từ chối trước adapter — chạy CLI guard smoke.
- [x] Q2 Model đọc đúng string/date/formula/merge và shared source không có business token — chạy CLI probe và source scan.
- [x] Q3 Project không có adapter không hồi quy; shared registry không branch project — chạy CLI configured/unconfigured smoke.
- [x] Q4 Discovery `VERIFY` có stable ID duy nhất và unsupported case blocked riêng — chạy CLI analysis trên workbook thật.
- [x] Q5 Case 54 giữ đúng quota, không chứa 06:00 và dùng fixture TimeFrame động — chạy CLI generate và inspect JSON.
- [x] Q6 Mỗi generated case là schema-v2 JSON riêng và Case 54 qua preflight — chạy JSON/preflight validation.
- [x] Q7 Preview create/update/unchanged/conflict và overwrite confirmation đúng — chạy scripted activation smoke.
- [x] Q8 UI/CLI trả cùng case IDs, diagnostics và draft — chạy parity smoke.
- [x] Q9 Upload dọn temp, chống traversal, hỗ trợ cancellation và không log workbook content — chạy Web upload lifecycle smoke.
- [x] Q10 Solution build PASS sau vòng tự sửa lỗi — chạy build toàn solution đến exit code 0.
- [x] Q11 Manual testcase không đổi và loader có hai source group riêng — kiểm git diff và API list/group smoke.
- [x] Q12 README/architecture mô tả đúng flow/package/giới hạn `VERIFY` — chạy doc lint và kiểm nội dung bắt buộc.

## Ước tính phút

Tổng eNm: 290 phút, không tính thời gian chờ duyệt hoặc tải package; không bao gồm unit test theo yêu cầu người dùng.

## Dòng Mode thực thi

Mode đề xuất: `main` — `tdq_bench simulate` đo 17 task, 13 wave; main 34,6 phút so với team 39,9 phút, chênh 5,2 phút. Nhiều task tuần tự và ba file nóng làm sub-agent không bù được overhead.

