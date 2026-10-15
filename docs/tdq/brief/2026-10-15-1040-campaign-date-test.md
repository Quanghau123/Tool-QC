# Yêu cầu tạo testcase Campaign dùng chung ngày
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Nguyên văn

> mình cần bạn sinh testcase giống "D:\works\projects\Tool-QC.artifacts\generated-testcases\be-booking\workbook-20261013-164409\workbook-verify-1.json" trong "D:\works\projects\Tool-QC\projects\be-booking\testcases\campaign" với ngày tạo thì chỉ cânf thay đổi 1 chỗ là các campaign cùng ăn theo

### Cách hiểu ban đầu

- Tạo một testcase JSON schema v2 trong thư mục `projects/be-booking/testcases/campaign`, theo cấu trúc của workbook-generated testcase.
- Một biến ngày chung được đặt một lần và tái sử dụng cho lịch của tất cả Campaign.
- Dữ liệu case theo ảnh: 8 Campaign cùng ngày gồm Spot 960, Spot 150, ba Duration 120, Spot 240, Duration 200 và Duration 10; từ chối Campaign 8 rồi tạo bản copy với allocation khác.
- Trả lời `1b 2a`: dùng quick lane, tiếp tục trên branch hiện tại và giữ nguyên các thay đổi đang có. Loại request: `chore`.

## Hiểu & kiến thức

- Tham chiếu tồn tại tại `D:\works\projects\Tool-QC\.artifacts\generated-testcases\be-booking\workbook-20261013-164409\workbook-verify-1.json`; nó dùng schema v2, fixture động từ DB và biến `${...}`.
- Checkout backend sạch phù hợp với các đường dẫn trong testcase là `D:\works\projects\mtc\sb\be-booking`, commit `cc786126`. Checkout `D:\works\projects\mtc\be-booking` có file đang sửa nên chỉ giữ nguyên, không dùng làm nguồn kiểm chứng.
- `CampaignsController` công khai `POST /api/Campaigns` và `POST /api/Campaigns/RejectMany`, không có API copy riêng. Trong testcase API, copy được mô phỏng bằng POST tạo mới với dữ liệu lịch của Campaign 8 và tên mới.
- `CampaignService.CreateAsync` chạy reallocation sau khi lưu Campaign và schedules. `RejectAsync` chuyển trạng thái, trả quota, khôi phục allocation Spot và tính lại quota trong transaction; planner nhận các Campaign đang hoạt động theo scope thiết bị/ngày.
- Đã đọc `docs/BA/README.md` và đối chiếu SHA-256 nguồn Campaign workbook; hash khớp. Không có dữ liệu case này trong phần VERIFY của workbook, nên số liệu lấy từ ảnh người dùng.
- B1 đã rà controller, request/validator, service, restoration service và reallocation planner. LSP chạy nhưng không tìm được symbol; Lumen không khả dụng do thiếu model embedding, nên dùng đọc file nguồn trực tiếp.
- Bỏ B0: báo cáo `docs/tdq/reports/2026-09-25-1640-five-timeframe-insertion.md` đã tạo testcase trong đúng thư mục Campaign. Bỏ scope round: yêu cầu chỉ thêm một testcase trong thư mục hiện hữu. Bỏ B2: không có hành vi bên thứ ba cần xác minh.

## Hỏi đáp

- Người dùng yêu cầu bám theo `case54-duration-insertion-lifecycle.json` để giữ testcase gọn. Giữ nguyên chuỗi 8 Campaign cùng ngày, từ chối Campaign 8 và copy; bỏ nhánh từ chối lý do trống và lần VerifySlots lặp, dùng allocation được đề xuất trực tiếp cho bước tạo bản sao.

- `1b 2a`: quick lane; tiếp tục branch `bugfix/archify-flow-quality`, không chuyển nhánh và không đụng các file bẩn hiện có.
- Chưa cần hỏi thêm trước khi lên kế hoạch. Giả định “copy” dùng POST tạo mới được nêu rõ vì backend không có endpoint copy riêng.
