# Đánh giá nâng cấp Tool-QC
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Nguyên văn

> 1.Xây dựng bản testcase, giống swagger nhưng nâng cấp hơn đối với mỗi resquest và response từng fields sẽ hỗ trợ lưu giá trị lại để tái sử dụng cho api khác tự đặt tên fields để lưu tạm để tránh trùng (ví dụ api A lưu id và API B cũng lưu id, lúc này sẽ không phân biệt được cái nào).
> , người dùng muốn lưu giá trị của fields nào chỉ click vào và lưu nâng cấp hơn postman. Khi muốn gán giá trị đã lưu cho fields của API khác thì chỉ việc hiện thị list biến có giá trị, cho mình chọn để gán, danh sách biến này sắp xếp A-Z.
> Các giái trị này biến khi lưu chỉ là lưu tạm, còn muốn lưu giá trị của biến, toàn dộ danh sách API như swagger kèm dữ liệu đã truyền vào requesst cũng như response giống như bản testcase lưu lại để sau tắt source, bữa sau mở lên chọn lại testcase đã lưu có thể sử dụng lại thì người cần nhấn lưu để lưu tất cả , nó sẽ tạo file và lưu vào testcase của dự án, với thư mục riêng.
>
> 2. Hiện tại để code tạo 1 file test case rất là khó đặc biệt đối với người kiến thức code về js còn hạn chế, thì mình cần biến hóa trên UI giúp người dùng họ tự tạo testcase dự án 1 cách dễ dàng thay vì phải biết code để code từng bước trong testcase, hiện tại dự án phải tự code ra 1 file testcase bằng js đòi hỏi có kiến thức tốt mới được, rất khó cho người mới đặc biệt tester không có kiến thức gì về code, nên mình cần thiết kế, xây dựng 1 cách gì đó để hỗ trợ họ tiếp cận viết testcase hay thao tác tạo testcase đơn giản.
>
> 3. Mình cũng cần kết hợp 1 lớp UNITX để test các luồng logic phức tạp, như công thức tính toán phức tạp hoặc 1 logic nghiệp vụ tính toán gì đó, nên mình 1 tầng lớp đầu để đảm bảo tính logic của bất kì logic quan trọng nào trọng dự án mà người dùng mong muốn, cách để sinh UNITX cũng nên giống như testcase cần có 1 thiết kế để ai không hiểu rõ về code vẫn có thể tạo được để test logic nghiệp vụ phức tạp của dự án.
>
> hiện tại mình cần nâng cấp tool QC theo hướng này cần bạn đnáh giá để tiến hàn nâng cấp tool, xem phần nào có thể triển khai, phần nào chưa

**Cách hiểu ban đầu:** Đánh giá kiến trúc và lộ trình nâng cấp Tool-QC theo ba hướng: trình dựng API testcase trực quan có biến theo field; trải nghiệm no-code/low-code để tạo và lưu testcase theo dự án; UnitX trực quan để kiểm thử logic nghiệp vụ. Chưa triển khai mã trong request này.

**Phạm vi dự kiến:** Kiểm kê năng lực hiện hữu, xác định phần có thể tái sử dụng, chỉ rõ ràng buộc an toàn/kỹ thuật, đề xuất kiến trúc đích và các giai đoạn triển khai có ưu tiên.

**Điểm cần làm rõ:** Ưu tiên giữa API testcase và UnitX, định nghĩa chính xác UnitX (hàm thuần, HTTP/API, hay chạy mã .NET của dự án), và đối tượng backend/language mục tiêu của UnitX.

**Trạng thái tìm kiếm:** Xác minh LSP theo module đang không khả dụng (thiếu TypeScript language server và module chưa được chứng minh); người dùng cho phép dùng graphify cùng tìm kiếm mã nguồn thay thế cho đánh giá chỉ đọc.

## Hiểu & kiến thức

### Phạm vi đã chốt
- Mặt CHỌN: trải nghiệm tester; an toàn & kiểm soát dữ liệu; khả năng mở rộng; vận hành & chất lượng.
- Mặt LOẠI: không có mặt nào trong bốn mặt trên bị loại.
- Bối cảnh: beta nội bộ trong 6–12 tháng, 3–15 tester, nhiều dự án; không phụ thuộc người biết code để tạo testcase.
- Mức đầu tư suy ra: vừa — beta nội bộ có nhiều dự án và các testcase cần được lưu/review/bảo trì.

### Năng lực hiện hữu và nhận định ban đầu
- `AutoTest.Web` đã có UI .NET 8 không phụ thuộc package JavaScript ngoài; có luồng source analysis → chọn endpoint → draft JSON → preflight → chạy thử → activate, cùng cơ chế phát hiện xung đột SHA-256 trước overwrite.
- `AutoTest.Generator` đã có contract từ Roslyn: endpoint, request/response type, property, validation, auth, service call và branch. Đây là nguồn dữ liệu tốt cho form request/response kiểu Swagger; còn thiếu mô hình field đã chọn, catalog biến và draft workspace.
- Schema testcase đã có `StepSpec.Save: Dictionary<string,string>` và HTTP executor lưu biến từ JSONPath; `RunnerEngine` giữ biến theo case/chạy chuỗi; `TestcaseValidator` chặn biến được dùng trước khi khai báo/save. Do đó lưu/gán biến theo field là mở rộng UI-first tương thích với runner, không cần tạo một engine biến thứ hai.
- Draft hiện chỉ là JSON textarea/artifact tạm. Lưu chính thức đã ghi vào `projects/<project>/testcases/<group>/<file>.json`; chưa có workspace bền vững theo người dùng, autosave hoặc version/history cho một testcase đang soạn.
- UnitX nên làm hai pha: trước là kiểm thử logic thuần theo dữ liệu (decision table/công thức/rule giới hạn, không thực thi C# hay expression tùy ý); sau mới cân nhắc sinh/chạy xUnit cho source .NET. Gộp hai loại vào API runner là sai ranh giới: API testcase kiểm tra integration/side effect, còn UnitX phải cô lập, quyết định được input/output và không cần API/secret.
- Bỏ B2 qua subagent: subagent không tuân thủ nhiệm vụ nghiên cứu; các nguồn ngoài được kiểm tra trực tiếp trong phiên này.

### Năng lực dùng được

Phân vân → DÙNG. Kiểm kê ngày 2026-10-22: 9 skill phù hợp trên đĩa, cộng skill built-in trong context.

| Skill | Nguồn | Phán quyết | Dùng ở đâu / Lý do loại |
|---|---|---|---|
| `tdq-workflow:tdq-intake` | plugin:tdq-workflow | NỀN | Điều phối phân tích, phỏng vấn, spec và plan. |
| `tdq-workflow:tdq-spec` | plugin:tdq-workflow | NỀN | Viết kiến trúc và tiêu chí quyết định đã được duyệt. |
| `tdq-workflow:tdq-plan` | plugin:tdq-workflow | NỀN | Chuyển lộ trình được duyệt thành kế hoạch kiểm chứng được. |
| `tdq-workflow:tdq-lean` | plugin:tdq-workflow | DÙNG | Chặn tạo engine/DSL/abstraction mới khi năng lực runner hiện có đã đáp ứng. |
| `dotnet-standards:facade-module-architecture` | built-in | DÙNG | Quyết định boundary giữa Web, generator, shared contract và UnitX runner. |
| `dotnet-standards:dotnet-testing` | built-in | DÙNG | Định nghĩa ranh giới UnitX và chiến lược sinh/chạy test .NET ở pha sau. |
| `dotnet-standards:api-surface` | built-in | DÙNG | Thiết kế API UI an toàn để lưu workspace, preflight và activate testcase. |
| `dataviz` | built-in | KHÔNG · khác lĩnh vực | Không tạo biểu đồ/visualization trong request này. |
| Đã xét các skill khác | plugin/built-in | KHÔNG · khác lĩnh vực | Không chạm cache, search, storage đối tượng, MediatR, database schema hay review code thay đổi. |

### Lộ trình

| Bước/phase | CÓ-BỎ | Vì sao |
|---|---|---|
| Phân tích | CÓ | Cần chốt nguồn contract, mức lưu bền vững, UnitX và các ranh giới an toàn. |
| Nghiên cứu nguồn chuẩn | CÓ | Cần chuẩn OpenAPI/JSONPath và giới hạn Unit test .NET làm cơ sở thiết kế. |
| Spec | CÓ | Đầu ra là kiến trúc/lộ trình có các quyết định không thể suy đoán. |
| Plan | CÓ | Phân tách MVP API builder khỏi UnitX và chỉ rõ các task/test. |
| Implement | BỎ trong request này | Người dùng yêu cầu đánh giá, chưa yêu cầu viết code. |
| QC tài liệu | CÓ | Đối chiếu từng kết luận với source hiện hữu, ràng buộc và nguồn chuẩn. |

## Hỏi đáp

- Người dùng chọn đóng request Campaign đang mở để ưu tiên đánh giá nâng cấp Tool-QC.
- Người dùng chọn cho phép bỏ qua cổng xác minh LSP cho đánh giá chỉ đọc, dùng graphify và tìm kiếm mã nguồn thay thế.
- 2026-10-22: Người dùng trả lời `1abcd 2a 3c 4a`: chọn cả bốn mặt đánh giá; bối cảnh beta nội bộ 3–15 tester, nhiều dự án; UnitX gồm cả logic thuần và source .NET nhưng logic thuần triển khai trước; QC `full`.
- 2026-10-22: Người dùng trả lời `1b 2a 3a 4a`: draft chưa lưu chính thức được đặt trong `.artifacts` trên máy chạy Tool-QC; OpenAPI/Swagger là đầu vào tùy chọn, với Roslyn source analysis làm fallback; UnitX giai đoạn đầu dùng decision table cùng phép toán whitelist; không bổ sung yêu cầu.

### Quyết định đã chốt
- MVP lưu draft theo project tại `.artifacts`, tách khỏi testcase chính thức. Draft cần metadata owner/workspace, project, schema, thời gian cập nhật, source contract/fingerprint và trạng thái preflight; không tự đưa vào `projects/`.
- API builder chọn OpenAPI 3.1 khi có file/URL hợp lệ để hiển thị operation, requestBody và response schema; không có OpenAPI thì dùng `SourceContractAnalyzer` hiện hữu. Cả hai phải quy về một model contract trung gian, không fork UI/runner theo nguồn contract.
- Catalog biến là phạm vi workspace/run: tên do người dùng đặt, không trùng không phân biệt hoa thường, mỗi biến gắn source step, JSONPath, kiểu/preview đã che secret và thời điểm thu; hiển thị A–Z khi gán vào field. Khi publish, catalog biên dịch về `steps[].save` và `${variableName}` của schema hiện hữu.
- UnitX V1 không nhận JavaScript, C#, reflection invocation, shell command hay expression tự do. Nó chỉ chạy decision table với toán tử và hàm toán học whitelist, kiểu dữ liệu xác định, giới hạn kích thước/bước tính, cùng báo cáo từng row. UnitX source .NET/xUnit là phase sau, chỉ sinh mã để người dùng review/build/run trong project đích.
- Các nguồn chuẩn dùng để đối chiếu: OpenAPI Specification 3.1 cho request body/response/schema; RFC 9535 cho cú pháp/ý nghĩa JSONPath; hướng dẫn unit testing .NET của Microsoft cho kiểm thử deterministic, tách dependency và không biến unit test thành integration test.
