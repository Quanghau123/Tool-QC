# BRIEF — Nhập XLSX và sinh testcase theo dự án
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Nguyên văn

> bạn vừa làm chính là case 54 trong file "C:\Users\quanghau\Downloads\Campaign (1).xlsx", vì thế trong "D:\works\projects\Tool-QC\projects\be-booking\testcases" mình muốn tạo thư mục testcase riêng cho file .xlsx nghĩa là mình chỉ cần nạp file này vao tool sẽ xâu dựng testcase dựa trên file .xlsx, file .xlsx dc thiết kế theo cấu tất cả các case QC đều tạo bám theo cấu trúc giống như case 54, công việc của mình là đưa file này vào tool sinh testcase giống như case 54, để thuận tiện test nhanh thay vì phải test bằng tay, chú ý phần này nếu có thể hỗ trợ phần dùng chung cho nhiều dự án thì cái đó nên viết vào core của toolQC còn nhưng cái riêng chỉ từng dự án mới có thì nên đưa vào dự án đó ví dụ file .xlsx liên quan campaign thì đưa vào dự án be-booking, hay cấu trúc trong file .xlsx cũng đưa vào be-booking vì mỗi dự án file xlssx sẽ có cấu trúc ứng vs nghiệp vụ khác nhau, nhưng phần xử lý file ví dụ import thư viện để đọc , để xử lý riêng file .xlsx thì đưa vào cỏe từng dự án sẽ gọi đến nếu dùng vì phần này dùng chung , và testcase nên tạo thư mục riêng không để chung thư mục campaign có sẵn trong "D:\works\projects\Tool-QC\projects\be-booking\testcases" vì 2 thư mục phục vụ testcase ccash thức khác nhau

Đọc ban đầu:

- Mục tiêu là bổ sung một luồng import workbook `.xlsx` vào Tool-QC để sinh testcase schema v2 tự động, thay cho việc dựng tay từng case như Case 54.
- Phần dùng chung cần nằm trong core Tool-QC: đọc workbook, trích xuất sheet/cell, mô hình dữ liệu trung gian, validation và cơ chế gọi bộ chuyển đổi theo dự án.
- Phần riêng của `be-booking` cần nằm dưới project `projects/be-booking`: hiểu cấu trúc workbook Campaign, ánh xạ các case nghiệp vụ thành fixture/request/assertion và cấu hình nguồn backend liên quan.
- Testcase sinh từ workbook phải nằm trong một thư mục riêng dưới `projects/be-booking/testcases`, không trộn với thư mục `campaign` đang chứa testcase viết tay.
- Workbook nguồn hiện tại là `C:\Users\quanghau\Downloads\Campaign (1).xlsx`, dung lượng khoảng 3.8 MB. Cần phân tích toàn bộ sheet, merged cells, công thức, màu/legend và cấu trúc các case trước khi chốt contract importer.
- Đây là thay đổi nhiều module, có API/UI/CLI và lựa chọn kiến trúc chưa được chốt nên đề xuất pipeline chuyên sâu.

## Hiểu & kiến thức

### Năng lực dùng được

Phân vân → DÙNG. Kiểm kê ngày 2026-09-27: 42 skill trên đĩa, cộng skill built-in trong context.

| Skill | Nguồn | Phán quyết | Dùng ở đâu / Lý do loại |
|---|---|---|---|
| `tdq-intake`, `tdq-spec`, `tdq-plan`, `tdq-build` | plugin:tdq-workflow | NỀN | Điều phối phân tích, approval, implementation và QC. |
| `spreadsheets` | built-in | DÙNG | Phân tích workbook, sheet, merged range và công thức. |
| `excel-miniexcel` | plugin | DÙNG | Đối chiếu thư viện đọc XLSX và guard import. |
| `brainstorming` | plugin | DÙNG | Chốt ranh giới shared core với adapter nghiệp vụ. |
| `dotnet-feature-flow` | plugin | DÙNG | Triển khai xuyên shared library, generator, Web API và UI. |
| `dotnet-testing` | plugin | DÙNG | Test reader, adapter và pipeline sinh draft. |
| Đã xét 36 skill khác | user/plugin/built-in | KHÔNG | khác lĩnh vực |

### Hiện trạng Tool-QC

- Tool-QC chạy .NET 8, shared framework tách khỏi nghiệp vụ trong `projects/<project>/`.
- `SpecLoader.Cases` đọc đệ quy JSON; thư mục đầu tiên thành `SourceGroup`. Do đó `testcases/workbook-generated/` tự tách khỏi `testcases/campaign/` mà không đổi runner.
- Web UI đã có analyze → draft → preflight → run draft → activate. Luồng workbook nên tái sử dụng review/chạy thử/lưu hiện có.
- Generator hiện phân tích source C# bằng Roslyn; chưa có workbook importer hay package Excel.
- LSP đã chạy nhưng effect check chưa trả symbol cho `SpecLoader`/generator; đã dùng fallback đọc file/tìm tên chính xác. Graphify chưa có graph để lấy degree.

### Workbook nguồn

- `C:\Users\quanghau\Downloads\Campaign (1).xlsx` khoảng 3.8 MB, 17 sheet: 8 ẩn, 9 hiển thị.
- Workbook không có Excel Table nhưng có nhiều merged cells, formulas và drawing. `1Day_v2 Từ chối` khoảng 62.455 hàng/38.489 merges; `VERIFY` 1.367 hàng/2.249 merges; `PERF` 172 hàng/375 formulas.
- Có nhiều family: `VERIFY` khoảng 69 case, `PERF` 13, `ManyDays` 17; không thể dùng một parser bảng phẳng cho toàn workbook.
- Có ít nhất hai Case 54 khác nhau (`VERIFY/54`, `1Day_v2/54`), nên stable ID phải gồm sheet/family/case number/variant.
- `VERIFY - Case 54` ở hàng 1032–1050: A Duration `[8,8,0,0,8,0]`, B Spot `[4,4,4,4,4,4]`, C Duration gồm hai range `[6,5]` và `[7]`.
- Mốc `06:00` chỉ mô tả vị trí tương đối. Testcase phải ánh xạ TF1…TF6 và chọn sáu TimeFrame 8 phút liên tiếp phù hợp lúc chạy.

### Hướng kiến trúc đề xuất

- **Shared XLSX layer:** đọc file/stream an toàn, trả model trung lập gồm sheet, cell, formula/cached value, merges và metadata; có giới hạn file/ZIP và cancellation.
- **Adapter contract:** shared host gọi adapter project để detect/analyze/generate; không có `if project == "be-booking"`.
- **Be-booking adapter/profile:** hiểu sheet Campaign, block case, legend, relative timeframe, fixture/request/assertion và source verification.
- **Output:** lưu dưới group riêng `projects/be-booking/testcases/workbook-generated/<sheet>/`; không chạm testcase viết tay trong `campaign/`.
- **UX/CLI:** UI nhận workbook, hiển thị case/diagnostics và chọn case; CLI dùng cùng pipeline. JSON review, preflight, run draft và activate được tái sử dụng.
- **Thư viện:** MiniExcel phù hợp bảng phẳng; ClosedXML thuận tiện cho merge/formula nhưng dùng memory; Open XML SDK kiểm soát tốt hơn nhưng parser phức tạp. Cần spike trước khi chốt dependency.

### Nghiên cứu ngoài

- ClosedXML có object model cho merged ranges và formula/cached value; cần giới hạn upload vì xử lý in-memory.
- Open XML SDK biểu diễn SpreadsheetML chính xác nhưng adapter phải tự chuẩn hoá shared strings, styles, dates, sparse cells và merged anchors.
- MiniExcel mạnh ở import tabular/streaming, chưa có lợi thế rõ cho workbook Campaign nhiều merge/style/formula.
- Upload phải allowlist `.xlsx`, giới hạn kích thước/entry/tổng bytes giải nén, dùng tên temp phía server và không lưu dưới webroot.
- Ghi chú nguồn: `docs/tdq/research/2026-09-27-0705-xlsx-testcase-import.md`.

### Các cách đã loại

- Không parse ZIP/XML ad-hoc trong Web endpoint; không sinh vào `testcases/campaign/`; không dùng case number đơn lẻ làm ID; không hard-code `06:00`; không đưa business rule Campaign vào core.

### Lộ trình

- Flow 1 — Import workbook an toàn và tạo workbook-neutral model.
- Flow 2 — Adapter `be-booking` nhận diện case, stable ID và diagnostics.
- Flow 3 — Sinh schema-v2 draft dùng relative timeframe và coverage đúng contract.
- Flow 4 — Review, preflight, run draft rồi activate vào group workbook riêng.
- Flow 5 — Regenerate có preview/diff, không ghi đè chỉnh sửa tay im lặng.

## Hỏi đáp

- 2026-09-27 07:03 (Asia/Saigon): Người dùng chọn `1a` — pipeline chuyên sâu (`deep`/lane `full`).
- 2026-09-27 07:03 (Asia/Saigon): Người dùng chọn `2a` — cho phép cấu hình LSP; đã backup cấu hình và khôi phục thay đổi project ngoài ý muốn.
- 2026-09-27: Người dùng chốt release đầu gồm UI upload và CLI, chỉ triển khai đầy đủ family `VERIFY`; mỗi case sinh một JSON dưới `testcases/workbook-generated/<sheet>/`; regenerate phải preview/diff và cần xác nhận trước khi ghi đè; case chưa hỗ trợ bị chặn riêng bằng diagnostics; không copy workbook vào project mà lưu fingerprint/metadata; mức QC là `full`.
- 2026-09-27: Khi duyệt plan, người dùng đổi phạm vi QC: bỏ unit test và không yêu cầu gửi lỗi từng phần; chỉ build toàn solution, agent tự đọc lỗi, sửa trực tiếp và build lại đến khi xanh. Các kiểm tra CLI/preflight/smoke không mở request backend vẫn được giữ để xác nhận feature dùng được.
