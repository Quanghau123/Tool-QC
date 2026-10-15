# SPEC — Nâng chất lượng flow và diagram Archify

Ngày: 2026-10-08 · Bản: 1.0 · Brief: ../brief/2026-10-08-0928-archify-flow-quality.md · Lane: full
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md
Trạng thái: CHỜ DUYỆT

## Mục lục

- 1. Mục tiêu & phạm vi
- 1b. Lộ trình
- 2. Đầu ra cụ thể
- 2b. Ranh giới module
- 3. Cách tiếp cận & lý do
- 3b. Năng lực & công cụ
- 4. Yêu cầu bắt buộc
- 5. Ràng buộc & rủi ro
- 6. QC & Definition of Done
- 7. Câu hỏi còn mở

## 1. Mục tiêu & phạm vi

- Mục tiêu: làm Archify sinh workflow/flow HTML đầy đủ nội dung tiếng Việt, tự thích ứng theo độ dài nội dung và không còn buộc tác giả phải cắt bớt node, edge hoặc nhãn chỉ để vượt validator.
- Mục tiêu đo được: fixture tiếng Việt dài phải giữ đủ 100% node, edge, label, sublabel và tag; artifact không cắt/chồng chữ, không tràn viewport desktop; regression hiện có vẫn pass.
- Trong phạm vi:
  - sửa contract authoring trong `SKILL.md` để bỏ giới hạn nội dung cứng và chuyển các quy tắc trình bày thành heuristic;
  - ưu tiên workflow v2, compiler/layout, text fitting/wrapping, viewBox và visual validation;
  - sửa module text dùng chung nếu thay đổi đó giúp architecture, dataflow hoặc lifecycle xử lý nhãn dài an toàn;
  - thêm fixture và test tiếng Việt dài, nhiều lane, nhánh, node và edge;
  - học nguyên tắc responsive SVG, typography, whitespace, grouping và viewer ổn định từ `example_flow.html`.
- NGOÀI phạm vi:
  - sao chép mã, asset, nội dung nghiệp vụ hoặc giao diện nguyên bản từ `example_flow.html`;
  - thay Archify bằng React/bundler của file mẫu;
  - đại tu topology riêng của sequence, dataflow, lifecycle hoặc architecture nếu không liên quan module dùng chung;
  - bỏ các invariant bảo vệ tính đúng như schema hợp lệ, edge không xuyên node, route không nhập nhằng, text không tràn và artifact không lỗi.

## 1b. Lộ trình

| Bước/phase | Chạy? | Vì sao |
|---|---|---|
| Research web | BỎ | nguồn chuẩn là code, fixture và file mẫu cục bộ; không cần thông tin internet để chốt giải pháp |
| Interview | CÓ | đã chốt phạm vi, cách học file mẫu, mức nới rule, tiêu chí nghiệm thu và nguồn fixture |
| Spec | CÓ | thay đổi contract authoring, shared text layout, compiler/validator và test cần được duyệt trước |
| Plan | CÓ | cần chia module skill contract, text layout, workflow compiler, fixture và QC |
| Implement | CÓ | sửa runtime và tài liệu skill |
| QC độc lập (agent) | CÓ | kiểm tra chéo tính đầy đủ nội dung, regression và chất lượng artifact trực quan |
| Report | CÓ | ghi rõ rule nào được nới, invariant nào được giữ và bằng chứng pass |

## 2. Đầu ra cụ thể

| # | Đầu ra | Đường dẫn/vị trí | Đo "xong" bằng |
|---|---|---|---|
| 1 | Contract authoring ưu tiên nội dung, không ép tối đa 12 node hoặc bắt cắt nội dung để đạt showcase | `.agents/skills/archify/SKILL.md`, tài liệu liên quan | hướng dẫn phân biệt invariant lỗi và heuristic cảnh báo; không còn giới hạn node cứng |
| 2 | Text layout dùng chung hỗ trợ nhãn dài mà không chỉ thu nhỏ một dòng | `.agents/skills/archify/renderers/shared/`, các renderer dùng chung cần thiết | test chứng minh wrap/đo kích thước ổn định, không cắt text và không giảm dưới ngưỡng đọc |
| 3 | Workflow v2 tự nới node, lane, rank gap và viewBox theo nội dung tiếng Việt | `.agents/skills/archify/renderers/workflow/` | fixture dài compile/deliver thành công, giữ đủ semantic content và không có overlap/overflow |
| 4 | Validation phân cấp đúng | `.agents/skills/archify/bin/`, checker/validator liên quan | lỗi tính đúng vẫn chặn; heuristic trình bày không buộc xoá nội dung; receipt nêu rõ severity |
| 5 | Bộ regression tiếng Việt và artifact mẫu | `.agents/skills/archify/test/`, `.artifacts/` hoặc thư mục test tạm phù hợp | test đỏ trước sửa, xanh sau sửa; HTML qua deliver và visual-check |
| 6 | Tài liệu contract workflow đồng bộ | `.agents/skills/archify/renderers/workflow/README.md` và tài liệu liên quan | mô tả đúng adaptive layout, wrapping và giới hạn còn giữ |

## 2b. Ranh giới module

| Module | Vùng file | Phụ thuộc module | Đầu ra §2 nào |
|---|---|---|---|
| Authoring contract | `.agents/skills/archify/SKILL.md`, `.agents/skills/archify/references/` | không | 1 |
| Shared text layout | `.agents/skills/archify/renderers/shared/` | không | 2 |
| Workflow adaptive layout | `.agents/skills/archify/renderers/workflow/` | Shared text layout | 3, 6 |
| Validation và delivery | `.agents/skills/archify/bin/`, `.agents/skills/archify/scripts/` | Workflow adaptive layout | 4 |
| Regression fixtures | `.agents/skills/archify/test/` | tất cả module trên | 5 |

Ranh giới được xác nhận từ import/call hiện tại: các renderer architecture, workflow, dataflow và lifecycle gọi `fittedNodeFontSize`/`minimumNodeTextWidth` từ shared text-fit; `render-workflow.mjs` gọi `compileWorkflow`; CLI validate/deliver gọi renderer và artifact checker. LSP MCP không khả dụng trong phiên này, nên bằng chứng call graph được lấy từ import/call trực tiếp trong source và sẽ được kiểm tra lại khi implement.

## 3. Cách tiếp cận & lý do

- Chọn: content-first adaptive layout.
- Cụ thể:
  - bỏ giới hạn authoring như “tối đa 12 node” và không mặc định coi mọi warning là lý do phải rút gọn nội dung;
  - thêm primitive text measurement/wrapping dùng chung, trả về lines, font size và chiều cao cần thiết thay vì chỉ shrink một dòng;
  - để workflow v2 lấy kích thước node từ nội dung khi tác giả không pin width/height, sau đó giải lại rank/lane/viewBox bằng cơ chế feedback sẵn có;
  - giữ pin explicit là ý định tác giả nhưng diagnostic phải đề xuất bỏ pin hoặc chuyển auto trước khi đề xuất xoá nội dung;
  - phân cấp diagnostic: schema, non-finite geometry, clipping, overlap, edge xuyên node và semantic loss là lỗi; density/rhythm/showcase polish là cảnh báo hoặc heuristic có thể báo cáo mà không làm mất nội dung;
  - dùng fixture tiếng Việt dài làm test hồi quy và deliver artifact thật để kiểm chứng browser.
- Vì: compiler hiện đã đo layout và feedback gap tốt; sửa đúng nút thắt text/node measurement ít rủi ro hơn viết lại renderer hoặc sao chép runtime 544 KB của file mẫu.
- Đã loại: gỡ toàn bộ validator — vì sẽ tái tạo lỗi edge xuyên node, clipping và artifact hỏng.
- Đã loại: sao chép runtime `example_flow.html` — vì phụ thuộc lớn, không cần thiết và không giải quyết contract JSON/layout gốc.
- Đã loại: chỉ tăng viewBox — vì text một dòng vẫn có thể quá nhỏ hoặc tràn node, chỉ làm canvas rộng hơn chứ không giải quyết bố cục nội dung.

## 3b. Năng lực & công cụ

| Skill | Nguồn | Phán quyết | Dùng ở đâu / Lý do loại |
|---|---|---|---|
| tdq-workflow:tdq-intake | plugin:tdq-workflow | NỀN | quản lý intake, phân tích, spec, plan, QC và report |
| archify | project | DÙNG | chuẩn nghiệp vụ hiện tại, renderer, validator, delivery và visual-check cần được sửa |
| computer-use:computer-use | built-in | DÙNG | mở và kiểm tra trực quan HTML mẫu cùng artifact sau sửa trên Windows |
| Đã xét 44 skill khác | user/plugin/built-in | KHÔNG | khác lĩnh vực |

## 4. Yêu cầu bắt buộc

- Log service: giữ cơ chế diagnostic/receipt hiện có; thay đổi runtime phải trả evidence đủ chi tiết, có code, severity, subject và supported fixes phù hợp.
- Không placeholder, không TODO stub, không mock trình bày như dữ liệu thật.
- Mỗi thành phần thay đổi có unit/regression test riêng, chạy được bằng lệnh Node hiện có.
- Code bám SOLID, ưu tiên hàm đo text/layout thuần, deterministic và có thể test độc lập.
- Không làm mất dữ liệu semantic: không tự xoá node, edge, label, sublabel, tag, card hoặc lane để đạt validation.
- Tương thích ngược: workflow schema v1 giữ geometry/byte baseline; workflow v2 cũ không pin kích thước phải tiếp tục compile ổn định hoặc có migration/diagnostic rõ ràng.
- Output vẫn là HTML standalone, theme/pan/zoom/search/export hiện có không bị suy giảm.
- Nội dung tiếng Việt phải giữ nguyên Unicode và đo text ổn định.

## 5. Ràng buộc & rủi ro

Ràng buộc kiến trúc phải giữ: `docs/kien-truc.md` không mô tả Archify; request này nằm trong skill cục bộ `.agents/skills/archify` và không được làm thay đổi shared API automation framework hoặc project testcase.

| Rủi ro | Ảnh hưởng | Cách giảm |
|---|---|---|
| Wrap text làm tăng node/lane height và đổi route | regression hình học rộng | giữ v1 bất biến; test snapshot/determinism cho v2; giải layout sau khi đo text |
| Nới warning quá mức che lỗi thật | artifact vẫn xấu nhưng pass | lập ma trận severity; invariant clipping/overlap/semantic loss vẫn là error |
| Thay shared text-fit ảnh hưởng loại diagram khác | architecture/dataflow/lifecycle đổi output | test từng renderer dùng chung, chỉ bật adaptive sizing nơi có contract rõ |
| Canvas quá lớn làm chữ bị thu nhỏ khi fit desktop | người xem khó đọc | kiểm tra projected text tại 1440×900, 1600×1000, 1920×1080; ưu tiên vertical growth và wrapping hơn horizontal sprawl |
| Font metric heuristic sai với tiếng Việt | vẫn tràn hoặc thừa khoảng | fixture dấu tiếng Việt, chuỗi dài, chữ hoa, số và ký hiệu; browser visual-check bổ sung |
| File mẫu có runtime rất khác | bắt chước sai trọng tâm | chỉ lấy nguyên tắc trình bày đã chốt, không lấy implementation |

## 6. QC & Definition of Done

| # | Hạng mục kiểm | Điều kiện PASS |
|---|---|---|
| Q1 | Baseline và regression | toàn bộ test Archify hiện có pass; nhóm 123 test mục tiêu không regress |
| Q2 | Shared text layout | test wrap/measure Unicode tiếng Việt deterministic; không dòng nào vượt available width ở font tối thiểu |
| Q3 | Semantic completeness | fixture giữ đúng số lane, node, edge, label, sublabel, tag và card từ JSON vào SVG/HTML |
| Q4 | Workflow geometry | không node overlap, edge xuyên node, label che route, non-finite geometry hoặc viewBox clipping |
| Q5 | Desktop browser | visual-check không overflow; projected text đạt ngưỡng đọc tại các viewport desktop được contract yêu cầu |
| Q6 | Delivery | `validate` và `deliver` exit 0, receipt đầy đủ; output HTML standalone mở được |
| Q7 | Compatibility | baseline v1 byte-for-byte pass; workflow v2 fixture cũ pass hoặc thay đổi snapshot được giải thích |
| Q8 | Skill contract | authoring path không còn giới hạn nội dung cứng; diagnostic không khuyên xoá semantic content để pass |
| Q9 | Review độc lập | agent/reviewer không phát hiện thiếu nội dung, rule mâu thuẫn hoặc lỗi layout blocker |
| Q10 | Nhật ký | working log, QC và report ghi đủ file đổi, lệnh kiểm, kết quả và rủi ro còn lại |

Definition of Done:
- Fixture tiếng Việt dài được thêm và ban đầu tái hiện ít nhất một hạn chế text/layout hiện tại.
- Sau sửa, fixture được validate, deliver và kiểm tra browser thành công, giữ đủ 100% semantic content.
- Các invariant tính đúng vẫn chặn artifact hỏng; heuristic trình bày không còn ép tác giả cắt nội dung.
- Test Archify liên quan và full test phù hợp đều xanh; không có lỗi đỏ chưa giải thích.
- Tài liệu authoring/workflow đồng bộ với hành vi thực tế.

## 7. Câu hỏi còn mở

Không còn.
