# Công cụ UI xóa dữ liệu Campaign theo khoảng ngày
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Nguyên văn

> Đóng request cũ và mở request mới cho công cụ UI xóa dữ liệu.
>
> Yêu cầu gắn với công cụ UI riêng dựa trên testcase `projects/be-booking/testcases/campaign/reset-campaign-range.json`: cho phép người dùng chọn khoảng ngày cần xóa, nhập môi trường cơ sở dữ liệu của SB và MTC, xem trước dữ liệu, xác nhận xóa và dùng được bởi nhiều người khác.

Cách hiểu ban đầu:
- Mục tiêu: tạo một công cụ UI độc lập hoặc màn hình UI có thể phân phối cho nhiều người để xóa dữ liệu Campaign theo khoảng ngày một cách có kiểm soát.
- Phạm vi dự kiến: tái sử dụng logic và thứ tự xóa trong testcase reset hiện có; hỗ trợ kết nối SB và MTC; preview trước khi xóa; xác nhận rõ ràng; hiển thị dữ liệu, số lượng, lỗi và nhật ký thao tác.
- Điểm chưa chốt: hình thức phân phối (file chạy độc lập hay route trong Tool-QC), cách nhập kết nối, phạm vi dữ liệu preview, transaction/rollback giữa SB và MTC, quyền truy cập và yêu cầu nền tảng.

## Hiểu & kiến thức

Đây là request mới, độc lập với request cũ `2026-09-25-0505-tqc-api-001`. Request cũ cần được đóng sổ trước khi request này hoạt động. Chưa triển khai code trong request mới; cần kiểm tra đầy đủ testcase reset, source runner/web và các ràng buộc dữ liệu trước khi viết spec.

## Hỏi đáp

- 2026-09-25 16:25 (Asia/Saigon): Người dùng yêu cầu đóng request cũ và mở request mới cho công cụ UI xóa dữ liệu; pipeline dự kiến là chuyên sâu vì đây là công cụ destructive, có UI, kết nối hai database và yêu cầu an toàn dữ liệu.
