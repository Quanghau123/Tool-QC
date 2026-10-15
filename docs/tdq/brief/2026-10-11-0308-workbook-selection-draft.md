# 2026-10-11-0308-workbook-selection-draft
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Nguyên văn

> hiện tại tool khi đưa file ecxel vào sinh testcase như nào và mình chạy test kết quả ra sao , hiện tại mình có dùng tool tạo testcase "C:\Users\quanghau\Downloads\Campaign (1).xlsx" verify thì bộ test như nào cho ví dụ test case 1
>
> "Công cụ
> Chọn công cụ cần dùng; quá trình quét và sinh file được chạy ngầm.
>
> Sinh testcase
> Nhập XLSX
> Sơ đồ kiến trúc
> Cào message
> Sinh testcase từ workbook XLSX
> Release đầu hỗ trợ family VERIFY của workbook Campaign. Việc phân tích và sinh draft không gọi API backend.
>
> Campaign (1).xlsx
> Phân tích workbook
> Đã sinh 1 case; blocked 0. Output: D:\works\projects\Tool-QC\.artifacts\generated-testcases\be-booking\workbook-20261011-025558
> 1 case được chọn
> Chọn tất cả
> Bỏ chọn
> Sinh bản nháp
>
> VERIFY - Case 1: Ngày 1 không cần chèn; Ngày 2 phải chènVERIFY · A1:Z21
>
> VERIFY - Case 2: Ngày 1 phải chèn 7; Ngày 2 đủ Trống nên không được tiếp tục dùng chèn.VERIFY · A22:Z42" mình nhấn sinh bản nháp không được khi chọn 1 trong các testcase, nút chọn tất cả thì bị khóa hết
>
> không có
>
> Python executable đã được phép dùng và ghi đè request TDQ hiện tại , vì request ccux đã tạm dừng
>
> cho phép tiếp tục trên nhánh hiện tại bugfix/archify-flow-quality

Đọc ban đầu: cần giải thích flow nhập workbook, tạo/chạy testcase, nêu ví dụ Case 1; đồng thời tái hiện và sửa lỗi chọn một/nhiều testcase rồi sinh draft. File XLSX là fixture tái hiện, không được xác nhận là tài liệu BA.

## Hiểu & kiến thức

- User xác nhận dự án không có tài liệu BA; `Campaign (1).xlsx` chỉ là fixture tái hiện, không đăng ký làm nguồn BA. SHA-256 lúc kiểm tra: `608d78161d0199bb1c187a805c0795368821e83399446c71195eedaa0561ed07`, khớp fingerprint trong draft.
- Artifact `.artifacts/generated-testcases/be-booking/workbook-20261011-025558/workbook-verify-1.json` tồn tại và chứa `campaign.workbook.verify-case-1`; UI báo `Đã sinh 1 case; blocked 0`. Vì vậy lần sinh đã thành công, không phải generate thất bại.
- Root cause UI: `runner/AutoTest.Web/wwwroot/app.js` đặt `state.workbookUploadId=null` sau generate; `renderWorkbookCases` khóa chọn/sinh khi upload ID rỗng nhưng giữ nguyên case đã chọn và danh sách. Backend `runner/AutoTest.Web/Program.cs` tiêu thụ upload một lần bằng `TryRemove` rồi xóa thư mục upload. Muốn chọn batch khác phải phân tích workbook lại.
- Scope fix nhỏ: sau generate, xóa lựa chọn stale, khóa checkbox khi upload đã dùng và hiện hướng dẫn phân tích lại; không đổi API hoặc vòng đời upload.
- Case 1: `destructive: true`, 6 bước; lấy fixture và TimeFrame từ DB, tạo campaign nền, gọi `POST /api/Campaigns/VerifySlots`, kỳ vọng HTTP 200 và `$.data.isValid=true`, sau đó kiểm tra DB. Có thao tác ghi dữ liệu và không có bước cleanup trong draft. Chưa chạy API vì testcase destructive và chỉ đang hỏi cách hoạt động.
- Không có testcase đã commit trong `projects/be-booking/testcases/`. `project.json`, README, `.env.example` (chỉ tên biến) đã kiểm tra; không đọc `.env`.
- LSP effect check chưa đạt: references thấy 1 file, tìm văn bản chính xác thấy 2 file; tiếp tục kiểm tra bằng tìm văn bản. JavaScript LSP chưa cấu hình; Lumen thiếu model `ordis/jina-embeddings-v2-base-code` và semantic search không trả kết quả trong thời gian chờ; không cài/sửa cấu hình.
- Không mở UI được trong Chrome đang kết nối; tái hiện dựa trên output artifact hiện có và đường đi state trong UI/API. Working tree vốn có artifact TDQ Archify chưa theo dõi; user cho phép tiếp tục trên `bugfix/archify-flow-quality`.

## Hỏi đáp

- 2026-10-11: Người dùng xác nhận dự án không có tài liệu BA.
- 2026-10-11: Người dùng cho phép ghi đè request TDQ cũ đang tạm dừng và tiếp tục trên nhánh `bugfix/archify-flow-quality`.
