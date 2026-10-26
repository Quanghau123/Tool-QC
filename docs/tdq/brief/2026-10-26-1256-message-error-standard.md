# BRIEF — Chuẩn hóa message và mã lỗi
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Nguyên văn

> hiện tại source đnag trả mess bằng tiếng việt và hash cứng trả mess ở nhiều nơi bao gồm mess trả lỗi cũng như mess hiện thị đều đâng hard cứng khó bảo trì sau này nhu cầu dùng đa ngôn ngữ thì gần như đập đi xây lại, với lại mess trả lỗi đang không chuẩn nên làm bảng mã lỗi trả về giống như mommo hoặc vnpay theo 1 chuẩn nhất định không làm bừa

Mục tiêu: thay cách phân tán chuỗi tiếng Việt hard-code bằng một cơ chế message có thể mở rộng đa ngôn ngữ, đồng thời chuẩn hóa error code ổn định để client tích hợp theo hợp đồng rõ ràng. Phạm vi dự kiến: shared error/message abstractions, response lỗi, nơi sinh message, tài liệu bảng mã, migration và regression coverage. Điểm cần khảo sát trước khi thiết kế: contract lỗi đang phát hành, nguồn message UI và API, mức tương thích ngược cần giữ, cùng quy ước mã lỗi theo domain/module.

## Hiểu & kiến thức

- Phạm vi đã được người dùng đính chính: triển khai trong repository Tool-QC, không sửa backend `ops-service`. Tool-QC không sở hữu `ErrorResultWrapper` hoặc exception middleware của backend nghiệp vụ; do đó capability mới phải audit, catalog hóa và kiểm contract cho backend ngoài, không áp đặt hoặc tự sửa source business.
- `src/AutoTest.MessageScanner/MessageScanner.cs` đã quét key dạng `Mes.<Module>...`, nhận translation JSON/RESX/properties và sinh workbook gồm key, tiếng Việt, tiếng Anh. Đây là capability audit/migration hữu ích, không phải runtime i18n/error-response implementation.
- `runner/AutoTest.Web/Program.cs` đã expose `POST /api/message-scans`; CLI và Web cùng gọi `SourceMessageScanner.Scan`. `UiSupport.Diagnose` đã có taxonomy code nội bộ `TQC-...`, nhưng hiện phân loại bằng chuỗi lỗi hard-code, không phải catalogue contract cho backend được quét.
- `README.md` xác nhận scanner chỉ đọc source ngoài repository, không chạy/build source đó; API Builder cũng đọc source local ở chế độ read-only.
- `ExpectSpec.Json` và `HttpStepExecutor.AssertAndSave` đã nhận JSON-path assertion `equals`, `exists`, `type`, `matches`, `oneOf` cùng `expect.status`; vì vậy một testcase có thể kiểm `$.errorCode` và `$.message` ngay, không cần transport/assertion engine mới. `TestcaseValidator` hiện chỉ kiểm cấu trúc chung và coverage, chưa hiểu catalogue.
- Runner nạp `project.json` và testcase từ `projects/<project>/`; cả runner và Web preflight đều gọi `TestcaseValidator.Validate`, nên catalogue phải được load/validate ở shared preflight boundary để mọi đường chạy có cùng verdict.
- Nhiều testcase `ops-service` hiện chỉ assert `$.message` tồn tại khi 400/403. Đây là evidence cho migration dần sang code catalogue, không phải căn cứ để Tool-QC bịa code hay đổi API business.
- Chuẩn cần tách ba contract độc lập: mã lỗi ổn định cho machine/client, HTTP status, và localized display message. Client phải xử lý theo code, không parse message; đổi locale không được đổi code hoặc HTTP status.

### Năng lực & công cụ

| Năng lực/công cụ | Verdict | Cách dùng trong lộ trình |
|---|---|---|
| `AutoTest.MessageScanner` | DÙNG | Audit source read-only, ghép key/translation và báo hard-code/catalogue gap. |
| Generic `expect.json` HTTP assertion | DÙNG | Runtime kiểm status, `errorCode`, localized message qua JSON path có sẵn. |
| Shared `TestcaseValidator` + runner/Web preflight | DÙNG | Validate catalogue, reference và locale fallback thống nhất trước network call. |
| Backend middleware/envelope | KHÔNG DÙNG | Thuộc backend ngoài Tool-QC; Tool-QC không sửa hoặc áp đặt. |
| Excel workbook | DÙNG | Export kết quả audit/catalogue để người dùng xem; JSON catalogue mới là source of truth. |

### Lộ trình

- Thêm catalogue JSON versioned ở `projects/<project>/`, với prefix cấu hình theo project, entry code/domain/status và message `vi`/`en`; `vi` là default, `en` fallback. Mã Tool-QC nội bộ theo `TQC-<DOMAIN>-<NNN>`; mã backend theo prefix catalogue, ví dụ `OPS-<DOMAIN>-<NNN>`.
- Mở rộng shared loader/preflight để reject catalogue/reference sai: schema/version, mã trùng, sai grammar/prefix, HTTP status không khớp, thiếu `vi`, thiếu fallback `en`, hoặc testcase reference không tồn tại. Reference catalogue chỉ bổ sung vào testcase khi cần kiểm full error contract; testcase cũ giữ `expect.json` tương thích.
- Mở rộng MessageScanner và workbook/Web/CLI output để audit key/translation hiện có, hard-coded display/error strings theo heuristic minh bạch, và mismatch với catalogue; scanner không tự suy luận hay phát hành mã cho backend.
- Giữ `expect.json` làm executor duy nhất; thêm declarative catalogue reference để preflight resolve thành status/error-code/message assertion. Chạy fixture framework cho validation, fallback và runtime HTTP contract; migrate một tập testcase lỗi representative của `ops-service` bằng catalogue dự án, rồi chạy đúng tag liên quan khi local/test API có sẵn.
- Cập nhật README, `.env.example` nếu contract công khai cần biến locale, `project-template`, và hướng dẫn repository khi shared contract thay đổi; không chạm source backend ngoài Tool-QC.

## Hỏi đáp

- Người dùng chọn pipeline deep; sau đó đính chính target: triển khai trong Tool-QC, không sửa `ops-service`.
- Người dùng chọn cả catalogue/audit source và runtime contract verification.
- Locale chốt: `vi` mặc định, `en` fallback.
- Namespace chốt: Tool-QC dùng `TQC-<DOMAIN>-<NNN>`; từng project khai prefix backend cấu hình được, ví dụ `OPS-<DOMAIN>-<NNN>`.
- Nguồn chân lý chốt: JSON catalogue versioned dưới `projects/<project>/`; workbook chỉ là export/audit.
- QC chốt: đầy đủ, gồm contract/integration, i18n fallback và error path.
- Không còn câu hỏi nào làm đổi output: Tool-QC kiểm catalogue/source/runtime contract; backend target tự implement envelope và locale theo catalogue, rồi Tool-QC xác thực bằng evidence thực thi.
