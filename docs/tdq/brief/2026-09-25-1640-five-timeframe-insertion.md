# Testcase chèn Campaign Duration vào chuỗi năm TimeFrame
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Nguyên văn

> dựa vào testcase "D:\works\projects\Tool-QC\projects\be-booking\testcases\campaign\frozen-frame-duration-insertion-flow.json" sinh ra 1 testcase mới tạo 5 tf liên tiếp nhau mỗi tf 8 phút 13:13:12:12:12, lúc tới thời gian cắt tf1 tức lúc này đong băng thì sẽ chèn vào campaign A ở tf2 bằng campaign B duration với 15 sport
>
> Người dùng chọn 1a: đóng request UI xóa dữ liệu hiện tại và mở request mới riêng cho testcase 5 TimeFrame.

Cách hiểu ban đầu:
- Tạo một file testcase mới, giữ nguyên testcase gốc.
- Chọn năm TimeFrame liên tiếp, mỗi TimeFrame dài tám phút.
- Campaign A phân bổ spot theo năm khung với kỳ vọng 13 / 13 / 12 / 12 / 12.
- Chờ đến mốc cắt của TF1 để TF1 trở thành khung đã đóng băng.
- Sau đó tạo Campaign B loại Duration với 15 spot, bắt đầu chèn từ TF2.
- Đối chiếu B không tác động TF1, việc phân bổ từ TF2 trở đi đúng contract, và lifecycle/dispatch/callback thực tế vẫn thành công.
- Cần kiểm tra source backend hai phía và cấu trúc request hiện tại trước khi chốt chính xác ý nghĩa 13:13:12:12:12, công thức quota sau chèn và điều kiện thành công.

## Hiểu & kiến thức

Request trước `2026-09-25-1625-campaign-reset-ui` được đóng để tránh trộn công cụ UI destructive với testcase campaign lifecycle. Request mới độc lập, dựa trên testcase `projects/be-booking/testcases/campaign/frozen-frame-duration-insertion-flow.json` và sẽ tuân thủ schema v2, coverage, source verification, preflight, build và runtime verification của Tool-QC.

### Source và testcase đã kiểm tra

- Testcase gốc có 15 bước: chọn fixture Creative/Field/Device, tìm ba TimeFrame tám phút, tạo Campaign A Spot, duyệt, chờ TF1 đóng băng và dispatch thật, đọc callback Redis MTC, tạo Campaign B Duration rồi kiểm tra bảo toàn quota.
- Campaign A hiện dùng quota `12 / 12 / 11`; testcase mới sẽ thay bằng năm schedule `13 / 13 / 12 / 12 / 12`, tổng 62 spot.
- Campaign B hiện chỉ khai báo TF2 với 9 spot; testcase mới sẽ khai báo TF2 với 15 spot theo yêu cầu.
- `SlotStateClassifier` xác nhận cả TimeFrame bị xem là frozen khi `TimeFrameStartTime <= now + freezeWindow`, hoặc slot đã Dispatched/Played. Vì vậy bước chờ phải chứng minh TF1 thực sự frozen trước khi tạo B.
- `DurationInsertionPlanner` lấy tổng `SpotCount` khai báo trên schedule làm số lượng Duration cần chèn và chỉ di chuyển các slot Spot còn movable. Do đó B khai báo 15 tại TF2 là cách diễn đạt đúng số lượng yêu cầu; backend có thể dùng các khung sau làm vùng bù nhưng không được di chuyển ngược vào TF1 đã frozen.
- Kịch bản mới cần lưu `tf4`, `tf5`; kiểm tra snapshot A có đúng 5 row và tổng 62; sau khi B được tạo, kiểm tra TF1 không có moved-in/moved-out, TF1 đã dispatch, quota hiệu lực của A vẫn bằng 62, B có tổng quota hiệu lực 15 và các thay đổi chỉ thuộc TF2–TF5.
- Không sửa shared framework và không sửa testcase gốc. File mới dự kiến: `projects/be-booking/testcases/campaign/frozen-frame-five-timeframes-duration-insertion-flow.json`.

### Ma trận năng lực

| Năng lực | Phán quyết | Lý do |
|---|---|---|
| `api-test-orchestrator` | DÙNG | Điều phối schema v2, coverage, source verification và vòng chạy thực tế. |
| `api-test-contract-validation` | DÙNG | Kiểm tra route, request Campaign Spot/Duration, response ID và quota số nguyên. |
| `api-test-auth-scope` | DÙNG | API tạo/duyệt Campaign dùng auth admin và fixture bị giới hạn theo Facility/Organization/Device. |
| `api-test-data-integrity` | DÙNG | Cần chứng minh snapshot, movement counter, quota, dispatch history và callback không bị sai lệch. |
| `api-test-lifecycle` | DÙNG | Hành vi phụ thuộc thời điểm cắt TF1, freeze window, dispatch và Campaign Scheduled. |
| `api-test-concurrency` | KHÔNG ÁP DỤNG | Kịch bản là chèn tuần tự sau freeze; tranh chấp đồng thời thuộc suite riêng. |
| `api-test-query` | KHÔNG ÁP DỤNG | Không kiểm tra endpoint list/search/filter/pagination. |
| `api-test-reporting` | DÙNG | Phải parse, preflight, build, chạy tag nhỏ nhất, đọc HTML/JUnit và sửa lỗi testcase/data đến khi đạt hoặc gặp blocker thật. |

### Ràng buộc kiểm chứng

- Không dùng sleep cố định; dùng PostgreSQL retry để chờ dấu hiệu TF1 frozen/dispatch như testcase gốc.
- Không nới assertion chỉ để khớp kết quả chạy. Phân bổ chi tiết sau chèn phải dựa trên source hiện tại và dữ liệu DB thực tế.
- Không cleanup dữ liệu sau chạy để giữ bằng chứng điều tra.
- LSP ladder báo thiếu đăng ký MCP/quyền trong cấu hình Claude và Lumen model; môi trường Codex vẫn có LSP MCP trực tiếp, còn các symbol chính đã được kiểm tra bằng source hiện tại. Không tự sửa cấu hình người dùng.

## Hỏi đáp

- 2026-09-25 16:40 (Asia/Saigon): Người dùng chọn phương án A, đóng request UI xóa dữ liệu và mở request riêng cho testcase năm TimeFrame.
- Không còn câu hỏi làm thay đổi sản phẩm: cụm `13:13:12:12:12` được hiểu là `SpotCount` của Campaign A trên TF1–TF5; `15 sport` được hiểu là 15 spot của Campaign B Duration khai báo tại TF2.


- 2026-09-25 16:50 (Asia/Saigon): Người dùng chốt bổ sung: mốc đóng băng là khi thời điểm chạy cách thời điểm bắt đầu TF1 đúng một giờ; testcase phải kiểm chứng số lượng chèn, số lượng dời, quota, thứ tự, dispatch history, Redis callback và trạng thái Campaign A/B.
