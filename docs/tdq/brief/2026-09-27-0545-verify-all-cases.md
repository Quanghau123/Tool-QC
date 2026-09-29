# BRIEF — Sinh toàn bộ testcase VERIFY từ workbook
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Nguyên văn

> 1A

Ngữ cảnh: người dùng chọn phương án A từ trao đổi trước — xây parser tổng quát để đọc workbook Campaign và sinh testcase cho toàn bộ 69 case thuộc family `VERIFY`, thay vì chỉ hỗ trợ Case 54 và đánh dấu các case khác là blocked.

Mục tiêu ban đầu: mở rộng adapter `be-booking` để hiểu dữ liệu từng block `VERIFY`, sinh schema-v2 JSON đúng nghiệp vụ cho từng case, giữ TimeFrame động theo thời điểm chạy và không đơn giản gỡ blocked khi chưa ánh xạ đúng expectation.

Điểm cần phân tích: các variant layout/merge/formula trong 69 block; cách biểu diễn Campaign A/B/C...; expected feasible/reject/allocation; lifecycle steps; stable output identity; chiến lược rollout và validation không gọi backend khi import.

## Hiểu & kiến thức

### Phạm vi đã chốt
- Mặt CHỌN: parser toàn bộ family `VERIFY`, sinh schema-v2, UI/CLI batch, TimeFrame động, diagnostics nghiêm ngặt và activation an toàn.
- Mặt LOẠI: không hỗ trợ sheet/family ngoài `VERIFY`; không sửa backend `be-booking`; không gọi API backend trong analyze/generate; không copy workbook vào repo.
- Bối cảnh: workbook 3.837.231 byte, 17 sheet; sheet `VERIFY` có 1.367 dòng, 2.249 merge, 64 formula và 69 case; chạy trên máy QC nội bộ Windows.
- Mức đầu tư suy ra: đầy đủ — vì parser tạo testcase nghiệp vụ phá dữ liệu, có 69 biến thể và phải bảo trì lâu dài.

### Phát hiện kỹ thuật
- Adapter hiện tại chỉ dò title/range và hard-code `verify/54`; nguyên nhân 68 case blocked nằm tại `VerifyWorkbookAdapter` chứ không phải workbook lỗi.
- `tools/booking-studio/importer/extract_verify_v21.py` của backend đã có thuật toán parse hai bảng ngày, Campaign Spot/Duration, range, Requested/Maximum/allocation, thứ tự tạo, chiều sync và expected validity.
- Chạy lại extractor trên đúng `Campaign (1).xlsx` tạo đủ 69 case: 1–30, 33–57, 61–66, 68–75. File `verify-v2.1.cases.json` đang commit chỉ có 53 case vì là artifact cũ, không phải giới hạn của parser.
- 69 case có 2–5 campaign/case; 64 case expected valid và 5 case expected invalid theo dữ liệu extractor mới. Layout dùng chung đủ ổn định để port thành model C# trong adapter.
- Hướng chọn: port thuật toán parser sang adapter C# của Tool-QC; không gọi Python/backend tool lúc runtime. Python extractor chỉ dùng làm oracle đối chiếu trong QC.
- Generator cần một template/schema builder dùng model trung gian thay vì 69 template thủ công. Case 54 hiện tại là chuẩn về fixture động, auth/scope, create/verify và database evidence nhưng không thể copy nguyên cho mọi topology.
- UI/backend hiện chỉ cho chọn đúng một case; batch 69 case cần contract chọn nhiều case, sinh một JSON/case và preview trạng thái từng file.
- Ràng buộc testcase: schema v2, tên bước tiếng Việt, coverage manifest đủ, case invalid phải có DB evidence không lưu partial state, dữ liệu fixture giữ lại để điều tra.
- QC đã được người dùng đổi cho request này sang `full` chuẩn: bổ sung unit test parser/builder, chạy trọn suite, parser parity, CLI generation, preflight toàn batch và Web smoke; agent tự sửa đến khi xanh.

### Nguồn đã đọc
- `C:/Users/quanghau/Downloads/Campaign (1).xlsx`, sheet `VERIFY`.
- `D:/works/projects/mtc/be-booking/tools/booking-studio/importer/extract_verify_v21.py`.
- `D:/works/projects/mtc/be-booking/tools/booking-studio/importer/verify-v2.1.cases.json`.
- `D:/works/projects/mtc/be-booking/tools/booking-studio/regression/verify-v2.1.js`.
- `D:/works/projects/mtc/be-booking/BUSINESS-RULES.md`, `CONVENTION.md`, `docs/architectures/CAMPAIGN-MODULE-MAP.md` và `docs/task-note/NOTES.md`.
- Tool-QC: `VerifyWorkbookAdapter`, workbook contracts/import service và Case 54 viết tay.

## Hỏi đáp

- Người dùng chọn hướng A: parser tổng quát cho toàn bộ 69 case `VERIFY`.
- Người dùng chọn pipeline chuyên sâu (`1A`).`r`n- Batch: chọn nhiều hoặc chọn tất cả, sinh một JSON riêng cho mỗi case (`1A`).`r`n- Case lỗi: block riêng case đó, các case hợp lệ vẫn sinh (`2A`).`r`n- Nguồn chuẩn: workbook upload; extractor cũ chỉ là oracle QC (`3A`).`r`n- QC: `full` chuẩn, có unit test và trọn suite (`4B`).`r`n- Không bổ sung phạm vi khác (`5A`).