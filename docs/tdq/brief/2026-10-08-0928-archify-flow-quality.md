# BRIEF — Nâng chất lượng flow và diagram Archify

Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Nguyên văn

> hiện tại tool của mình có hỗ trợ vẽ flow, nhưng đnag rất gà và mỗi lần sinh diagram hay flow đều lỗi, hoặc nội dung không đầy đủ, thiếu sót rất nhiều mình cần đập sửa lại để ra kết quả chuẩn như file  ""C:\Users\quanghau\Downloads\example_flow.html"" và hiện tại các skill đnag ràng buộc nhiều thứ nên vẽ ra gây lỗi vì thế cũng nên gỡ luôn, vì nội dung mình bằng tiếng việt nên rule ràng buộc vị trí hình dạng quá cứng thì luôn dễ gây cvowx layout rất khó vì thế nên học theo chuẩn ""C:\Users\quanghau\Downloads\example_flow.html""

Mục tiêu ban đầu: sửa skill và engine Archify để sinh workflow/flow/diagram ổn định, đầy đủ nội dung tiếng Việt, ít lỗi layout và đạt chất lượng trình bày tương đương hoặc học theo cấu trúc của `C:\Users\quanghau\Downloads\example_flow.html`. Phạm vi dự kiến gồm rà soát các ràng buộc trong skill, schema/validator, compiler, renderer và test; nới hoặc loại bỏ các quy tắc hình dạng/tọa độ quá cứng khi chúng gây vỡ bố cục; bổ sung khả năng chống tràn/chồng lấn/cắt chữ; giữ output HTML độc lập, tương tác và có thể kiểm chứng. Cần phân biệt rõ việc “học theo chuẩn” là tái sử dụng nguyên tắc trình bày hay sao chép mã/tài sản cụ thể.

## Hiểu & kiến thức

- Skill Archify hiện đặt nhiều ràng buộc authoring trực tiếp lên nội dung: tối đa 12 node chính, mặc định `showcase`, cấm chỉnh nhiều geometry control trong một vòng và yêu cầu không còn warning. Các quy tắc này có thể khiến tác giả rút gọn hoặc bỏ nội dung để đạt validator thay vì để renderer thích ứng.
- Workflow v2 đã có compiler đo layout, tự tăng rank/lane gap, đo label và viewBox; 123 test mục tiêu hiện pass. Vì vậy hướng sửa không phải xoá các invariant chống edge xuyên node, route mơ hồ hay overflow, mà tách rõ lỗi tính đúng khỏi cảnh báo trình bày và cải thiện khả năng thích ứng với text dài.
- Điểm yếu dùng chung đã xác định: `renderers/shared/text-fit.mjs` chỉ shrink một dòng, không wrap; label, sublabel và tag tiếng Việt dài có thể phải thu nhỏ tới ngưỡng tối thiểu hoặc bị validator chặn. Module này được architecture, workflow, dataflow và lifecycle dùng chung.
- `example_flow.html` dùng viewer standalone responsive, bố cục có nhiều tab/khối nội dung và cho nội dung quyết định kích thước. Request chỉ học nguyên tắc responsive, typography, whitespace, grouping và đóng gói; không sao chép mã hoặc asset.
- Không có input production cụ thể theo lựa chọn `5b`; bộ tái hiện sẽ dùng fixture hiện có và thêm fixture tiếng Việt dài, nhiều lane/nhánh/node/edge để chứng minh nội dung không bị cắt, chồng hoặc bỏ.
- Baseline mục tiêu đã chạy: 123/123 test compiler, semantic contract và visual-check pass trước thay đổi.

### Năng lực dùng được

Phân vân → DÙNG. Kiểm kê ngày 2026-10-08: 45 skill trên đĩa, cộng skill built-in trong context.

| Skill | Nguồn | Phán quyết | Dùng ở đâu / Lý do loại |
|---|---|---|---|
| tdq-workflow:tdq-intake | plugin:tdq-workflow | NỀN | quản lý intake, phân tích, spec, plan, QC và report |
| archify | project | DÙNG | chuẩn nghiệp vụ hiện tại, renderer, validator, delivery và visual-check cần được sửa |
| computer-use:computer-use | built-in | DÙNG | mở và kiểm tra trực quan HTML mẫu cùng artifact sau sửa trên Windows |
| Đã xét 44 skill khác | user/plugin/built-in | KHÔNG | khác lĩnh vực |

### Lộ trình

| Bước/phase | Chạy? | Vì sao |
|---|---|---|
| Research web | BỎ | nguồn chuẩn là code, fixture và file mẫu cục bộ; không cần thông tin internet để chốt giải pháp |
| Interview | CÓ | đã chốt phạm vi, cách học file mẫu, mức nới rule, tiêu chí nghiệm thu và nguồn fixture |
| Spec | CÓ | thay đổi contract authoring, shared text layout, compiler/validator và test cần được duyệt trước |
| Plan | CÓ | cần chia module skill contract, text layout, workflow compiler, fixture và QC |
| Implement | CÓ | sửa runtime và tài liệu skill |
| QC độc lập (agent) | CÓ | kiểm tra chéo tính đầy đủ nội dung, regression và chất lượng artifact trực quan |
| Report | CÓ | ghi rõ rule nào được nới, invariant nào được giữ và bằng chứng pass |
## Hỏi đáp
### Vòng phạm vi — 2026-10-08 09:33 +07:00

1. Phạm vi diagram cần sửa?
- Người dùng chọn nguyên văn: `1a`.
- Chốt: ưu tiên `workflow/flow`, đồng thời sửa phần dùng chung ảnh hưởng các loại diagram khác.

2. Mức học theo `example_flow.html`?
- Người dùng chọn nguyên văn: `2a`.
- Chốt: học nguyên tắc trình bày và runtime, không sao chép code hoặc tài sản cụ thể.

3. Xử lý các rule ràng buộc hiện tại?
- Người dùng chọn nguyên văn: `3a`.
- Chốt: chuyển rule cứng thành heuristic/diagnostic mềm; giữ invariant cần cho tính đúng.

4. Tiêu chí nghiệm thu chính?
- Người dùng chọn nguyên văn: `4a`.
- Chốt: HTML không lỗi, không cắt/chồng chữ, không tràn viewport, đủ node/edge/nội dung và pass validation/test ở viewport desktop.

5. Input lỗi cụ thể?
- Người dùng chọn nguyên văn: `5b`.
- Chốt: dùng fixture hiện có và tự tạo bộ tiếng Việt dài để tái hiện.


