# SPEC — Testcase chèn Campaign Duration vào chuỗi năm TimeFrame

Ngày: 2026-09-25 · Bản: 1.0 · Brief: ../brief/2026-09-25-1640-five-timeframe-insertion.md · Lane: full
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

- Mục tiêu: tạo một testcase schema v2 độc lập, dựa trên flow đã chạy của `frozen-frame-duration-insertion-flow.json`, để kiểm chứng Campaign A Spot có năm TimeFrame liên tiếp dài tám phút với quota `13 / 13 / 12 / 12 / 12`; khi thời điểm chạy còn cách thời điểm bắt đầu TF1 đúng một giờ, hệ thống đóng băng TF1; sau đó Campaign B Duration 15 spot được chèn từ TF2 mà không làm thay đổi TF1 và không làm mất quota của A.
- Trong phạm vi:
  - Tạo file testcase mới; giữ nguyên testcase gốc.
  - Chọn đúng năm TimeFrame vật lý liên tiếp, mỗi khung tám phút, trên Device/Field/Creative hợp lệ tại Booking và MTC.
  - Tạo Campaign A loại Spot với tổng 62 spot, duyệt Campaign A và đi qua dispatch/callback thật.
  - Chờ bằng dữ liệu PostgreSQL đến khi đạt mốc freeze-window một giờ trước TF1 và có bằng chứng TF1 frozen/dispatch; không dùng thời gian ngủ cố định.
  - Tạo Campaign B loại Duration, khai báo 15 spot tại TF2.
  - Đối chiếu trước và sau chèn: năm schedule của A, quota `13/13/12/12/12`, tổng quota A, mốc freeze đúng một giờ trước TF1, số lượng slot được chèn và dời, trạng thái bất biến của TF1, thứ tự slot, movement chỉ thuộc TF2–TF5, quota hiệu lực B bằng 15, dispatch history, Redis callback và trạng thái Campaign A/B.
  - Khai báo coverage schema v2 bằng tên bước thật và source backend đã đọc.
  - Parse JSON, preflight, build runner, chạy đúng testcase/tag destructive và đọc báo cáo HTML/JUnit.
- NGOÀI phạm vi:
  - Không sửa backend Booking hoặc MTC.
  - Không sửa shared framework Tool-QC.
  - Không sửa testcase `frozen-frame-duration-insertion-flow.json`.
  - Không thêm kiểm thử tranh chấp đồng thời; concurrency là suite độc lập.
  - Không cleanup dữ liệu tạo ra sau lần chạy, để giữ bằng chứng điều tra.

## 1b. Lộ trình

| Bước/phase | Chạy? | Vì sao |
|---|---|---|
| Phân tích testcase và source | CÓ | Cần xác nhận semantics freeze, Duration insertion, movement và quota trước khi viết assertion. |
| Research web | BỎ | Contract nội bộ có testcase, source Booking/MTC và dữ liệu runtime làm nguồn chuẩn. |
| Interview | BỎ | Yêu cầu đã xác định đủ: năm TF tám phút, quota A `13/13/12/12/12`, khi còn đúng một giờ trước TF1 thì TF1 frozen, rồi B Duration 15 spot tại TF2. |
| Spec | CÓ | Chốt file mới, assertion, phạm vi không sửa và điều kiện hoàn thành. |
| Plan | CÓ | Tách việc tạo fixture, lifecycle, assertion, preflight/build/runtime và đọc report. |
| Implement | CÓ | Tạo testcase mới trong project `be-booking`. |
| QC độc lập (agent) | BỎ | Không có yêu cầu dùng sub-agent; bằng chứng runtime với backend thật có giá trị trực tiếp hơn review tĩnh lặp lại. |
| Report | CÓ | Báo PASS/FAIL/BLOCKED, report mới và phân loại lỗi theo evidence. |

## 2. Đầu ra cụ thể

| # | Đầu ra | Đường dẫn/vị trí | Đo "xong" bằng |
|---|---|---|---|
| 1 | Testcase schema v2 cho năm TimeFrame và Duration insertion sau freeze | `projects/be-booking/testcases/campaign/frozen-frame-five-timeframes-duration-insertion-flow.json` | JSON hợp lệ, preflight chấp nhận coverage và runner nhận đúng một case ID mới. |
| 2 | Bằng chứng runtime của testcase mới | `.artifacts/test-runs/be-booking/campaign/` | Có JUnit và HTML của lần chạy hiện tại, ghi rõ tổng pass/fail và dữ liệu từng bước. |
| 3 | Hồ sơ TDQ của request | `docs/tdq/` và `docs/workinglog/2026-09-25.md` | Spec, plan, QC/report và working log phản ánh đúng file, kiểm thử và kết quả thực tế. |

## 2b. Ranh giới module

| Module | Vùng file | Phụ thuộc module | Đầu ra §2 nào |
|---|---|---|---|
| Testcase campaign | `projects/be-booking/testcases/campaign/frozen-frame-five-timeframes-duration-insertion-flow.json` | Contract HTTP Campaigns, PostgreSQL Booking/MTC, Redis Stream và Elasticsearch đã có trong runner | 1, 2 |
| Hồ sơ request | `docs/tdq/`, `docs/workinglog/2026-09-25.md` | Kết quả của module Testcase campaign | 3 |

Ranh giới nghiệp vụ được xác định từ luồng gọi đã kiểm tra: Campaign API nhận `CampaignRequest`, `CampaignSlotAvailabilityService` nạp dữ liệu reallocation, `DurationInsertionPlanner` chỉ chọn slot Spot còn movable, `SlotStateClassifier` khóa cả TimeFrame theo thời điểm bắt đầu hoặc trạng thái Dispatched/Played, sau đó persistence thể hiện movement qua `campaign_schedule` và dispatch/callback qua các module hiện có. Testcase chỉ gọi các contract này qua runner, không tham chiếu production assembly.

## 3. Cách tiếp cận & lý do

- Chọn: sao chép cấu trúc flow đã chạy của testcase gốc, mở rộng truy vấn fixture từ ba lên năm TimeFrame bằng chuỗi `LEAD` liên tiếp, thêm `tf4` và `tf5`, đổi quota A thành `13 / 13 / 12 / 12 / 12`, đổi B thành 15 spot tại TF2, rồi tăng cường assertion DB cho cả A và B.
- Vì: cách này tái sử dụng chuỗi lifecycle đã được kiểm chứng gồm create → approve → freeze/dispatch → Redis callback → Scheduled → Duration insertion; phần thay đổi chỉ tập trung vào số khung, quota và bằng chứng movement.
- Assertion sau chèn:
  - Campaign A vẫn có đúng năm schedule và tổng quota gốc 62.
  - Quota hiệu lực của A, tính bằng `SpotCount - MovedOutSpotCount + MovedInSpotCount`, vẫn bằng 62.
  - TF1 có `DispatchedSpotCount > 0`, đồng thời `MovedOutSpotCount = 0` và `MovedInSpotCount = 0`.
  - Không schedule nào ngoài TF2–TF5 có movement do lần chèn này.
  - Campaign B có tổng quota hiệu lực 15 và không có schedule ở TF1.
  - Query diagnostic trả từng row TF1–TF5 để report giữ được dữ liệu thực tế, không chỉ trả một cờ thành công.
- Đã loại: sửa testcase gốc — vì cần giữ regression ba TimeFrame độc lập.
- Đã loại: sleep đến giờ cắt — vì thời gian môi trường và dispatch lead time thay đổi; retry theo DB là bằng chứng xác định hơn.
- Đã loại: hard-code kết quả movement chi tiết cho từng TF2–TF5 trước lần chạy — vì planner chọn vị trí vật lý theo capacity và slot đang chiếm; contract bắt buộc là không chạm TF1, bảo toàn A và cấp đủ 15 cho B.

## 3b. Năng lực & công cụ

| Skill | Nguồn | Phán quyết | Dùng ở đâu / Lý do loại |
|---|---|---|---|
| `tdq-intake` và `tdq-conventions` | plugin:tdq-workflow | NỀN | Điều phối request, approval gate, state và working log. |
| `tdq-spec` | plugin:tdq-workflow | NỀN | Chốt contract và Definition of Done trước khi sửa repository. |
| `api-test-orchestrator` | project | DÙNG | Điều phối schema v2, coverage, source verification và vòng chạy thực tế. |
| `api-test-contract-validation` | project | DÙNG | Xác minh route, payload Campaign Spot/Duration, response ID và quota số nguyên. |
| `api-test-auth-scope` | project | DÙNG | Xác minh admin auth và phạm vi Facility/Organization/Device của fixture. |
| `api-test-data-integrity` | project | DÙNG | Kiểm tra schedule, movement counter, quota, history và callback. |
| `api-test-lifecycle` | project | DÙNG | Kiểm tra freeze boundary, dispatch và Campaign Scheduled. |
| `api-test-reporting` | project | DÙNG | Parse, preflight, build, chạy, đọc report, phân loại và sửa lỗi testcase/data. |
| Đã xét 35 skill khác | user/plugin/built-in | KHÔNG | khác lĩnh vực hoặc không cần cho một testcase campaign tuần tự. |

## 4. Yêu cầu bắt buộc

- Log service: BỎ — request không tạo hoặc sửa mã runtime; report runner hiện có chịu trách nhiệm lưu request, response, SQL, Redis và diagnostic data.
- Không placeholder, không TODO stub, không mock trình bày như dữ liệu thật.
- Testcase dùng biến `${unique}` cho dữ liệu sở hữu riêng và không phụ thuộc case khác.
- Mọi bước `name` viết tiếng Việt rõ nghĩa.
- Testcase mới khai báo `schemaVersion: 2`, `destructive: true` và coverage đầy đủ.
- Mỗi phần của flow có bước kiểm chứng riêng: fixture, create A, snapshot A, freeze TF1, dispatch/callback, create B và integrity sau chèn.
- Không đọc hoặc ghi secret trực tiếp; chỉ tham chiếu biến môi trường hiện có.
- Cấu trúc testcase giữ tính khai báo và không đưa business rule `be-booking` vào shared framework.

## 5. Ràng buộc & rủi ro

Ràng buộc kiến trúc phải giữ:
- "Testcase project được gọi shared runner qua schema khai báo; không gọi trực tiếp production code." — áp dụng tại file testcase mới.
- "Shared runner không được chứa business rule riêng của `be-booking` hoặc MTC." — request này không sửa `src/` hoặc runner.
- "QC chỉ đọc backend source và dữ liệu runtime; không sửa backend PROD trong request này." — source Booking/MTC chỉ dùng để xác minh contract.

| Rủi ro | Ảnh hưởng | Cách giảm |
|---|---|---|
| Không có chuỗi năm TimeFrame tám phút liên tiếp trên Device hợp lệ | Fixture không chọn được row, testcase không chạy đến API | Truy vấn chọn candidate theo source scope, kiểm tra cả năm khung và loại candidate đã có dữ liệu QC trùng. |
| TF1 chưa frozen khi tạo B | Kịch bản kiểm sai thời điểm và cho phép planner di chuyển TF1 | Chỉ tiếp tục sau khi DB chứng minh TF1 đã dispatch/frozen, với retry có timeout. |
| Capacity TF2–TF5 không đủ bù 15 spot | API B bị từ chối do dữ liệu môi trường | Query candidate loại scope không đủ cửa sổ vật lý; nếu runtime vẫn thiếu thì phân loại `TEST_DATA_ERROR`, điều chỉnh fixture chứ không nới assertion. |
| Callback MTC đến chậm hoặc service không hoạt động | Case dừng trước bước chèn B | Giữ retry hữu hạn, evidence Redis/Elasticsearch/PostgreSQL và phân loại `ENVIRONMENT_BLOCKER` khi hạ tầng không phản hồi. |
| Planner phân bổ movement khác dự đoán theo từng khung | Assertion quá cứng gây false failure | Chỉ khóa invariants từ source: TF1 bất biến, A bảo toàn 62, B đủ 15, movement nằm trong TF2–TF5; report vẫn hiển thị từng row. |
| Test destructive tạo dữ liệu thật | Tăng dữ liệu test trong môi trường | Chỉ chạy khi safety cho phép, dùng tên `${unique}`, không bật production và giữ fixture để điều tra. |

Không cần tải hoặc cài package mới. LSP ladder ngoài Codex còn thiếu đăng ký MCP/quyền Claude và model Lumen; không thay đổi cấu hình người dùng trong request này.

## 6. QC & Definition of Done

| # | Hạng mục kiểm | Điều kiện PASS |
|---|---|---|
| Q1 | JSON và định danh | File parse được, project là `be-booking`, ID mới duy nhất, schema v2 và destructive được khai báo. |
| Q2 | Coverage preflight | Mọi vùng bắt buộc có trạng thái hợp lệ; `evidenceSteps` trỏ đúng tên bước tồn tại; source verification dùng file thực đã đọc và timestamp ISO hiện tại. |
| Q3 | Fixture năm TimeFrame | Bước chọn fixture trả đúng năm TF liên tiếp, mỗi TF tám phút, cùng Device và ngày chạy hợp lệ. |
| Q4 | Campaign A | API tạo A thành công; DB có đúng năm schedule với `SpotCount` lần lượt `13, 13, 12, 12, 12` và tổng 62. |
| Q5 | Freeze/lifecycle | Campaign A được duyệt; bước chờ xác nhận thời điểm hiện tại đã vào freeze window đúng một giờ trước `tf1_start`, TF1 có bằng chứng frozen/dispatch trước khi tạo B; dispatch history, Redis callback và Campaign Scheduled khớp correlation ID. |
| Q6 | Campaign B | API tạo B Duration thành công với 15 spot khai báo tại TF2 và trả ID tồn tại. |
| Q7 | Integrity sau chèn | Report hiển thị được số slot B đã chèn, số slot A bị dời và thứ tự slot; TF1 không moved-in/moved-out; A giữ quota hiệu lực 62; B giữ quota hiệu lực 15; B không có TF1; movement của A chỉ nằm trong TF2–TF5. |
| Q8 | Build | Runner build không có error do testcase mới. |
| Q9 | Runtime report | Lần chạy hiện tại của đúng testcase tạo HTML/JUnit; kết quả được báo là PASS, hoặc dừng với `BACKEND_BUG`/`ENVIRONMENT_BLOCKER` có evidence cụ thể. `TEST_SCRIPT_ERROR` và `TEST_DATA_ERROR` phải được sửa và chạy lại. |
| Q10 | Không hồi quy phạm vi | Testcase gốc và shared framework không có diff. |

DoD:
- File testcase mới tồn tại và testcase gốc không đổi.
- JSON, schema-v2 preflight và build đều PASS.
- Testcase mới được chạy trên môi trường hiện tại với destructive safety hợp lệ.
- Nếu backend và hạ tầng hoạt động đúng contract, toàn bộ case PASS và có report mới.
- Nếu phát hiện backend bug hoặc environment blocker, report giữ đầy đủ request/response/SQL/Redis evidence, nêu chính xác bước dừng và không sửa expectation trái source.
- Working log và TDQ report ghi đúng file, lệnh kiểm, totals và đường dẫn artifact.

## 7. Câu hỏi còn mở

(Không còn câu hỏi.)

