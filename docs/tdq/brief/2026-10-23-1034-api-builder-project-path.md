# BRIEF — API Builder project-relative contract path
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

## Nguyên văn

Người dùng yêu cầu thay API Builder: không bắt copy OpenAPI vào `.artifacts` hay nhập absolute path; dùng đường dẫn tương đối từ project nằm trong `Tool-QC/projects/<project>/`, tự nạp từ `project.json`, portable giữa máy.

Mục tiêu là để mỗi project sở hữu OpenAPI và source provenance của chính nó, ví dụ `projects/ops-service/openapi/openapi.json` và `projects/ops-service/source/`, thay vì phụ thuộc vào vị trí máy. Phạm vi gồm model cấu hình project, facade resolve contract, UI, fixture/E2E và tài liệu; giữ nguyên OpenAPI 3.1-first, Roslyn fallback, fingerprint workspace, coverage/preflight và tất cả guard bảo mật hiện có.

## Hiểu & kiến thức

### Hiện trạng đã kiểm chứng

- `ProjectSpec` chỉ có name/base URL/header/auth/safety tại `src/AutoTest.Abstractions/Models.cs`; `project.json` chưa có cấu hình API Builder.
- Endpoint `POST /api/projects/{project}/api-builder/contract` hiện xác minh project rồi lấy `openApiPath`/`sourcePath` từ body UI tại `runner/AutoTest.Web/Program.cs:68`.
- `BuildApiBuilderContract` ưu tiên OpenAPI 3.1 và fallback Roslyn; `SafeContractPath` đang neo relative path vào root Tool-QC, cấm URL HTTP(S), containment escape và `.env` tại `runner/AutoTest.Web/Program.cs:722-759`.
- UI bắt nhập hai path và lưu chúng trong `localStorage` tại `runner/AutoTest.Web/wwwroot/index.html:42` và `runner/AutoTest.Web/wwwroot/app.js:53,68`.
- E2E hiện nhập `.artifacts/api-builder-contract-fixture.json` thủ công tại `tests/e2e/api-builder.spec.js:8`.
- Workspace tiếp tục giữ revision, fingerprint, kiểm typed binding, source order và field sensitive; thay đổi path không được làm suy yếu các guard này.

### Quyết định thiết kế

1. Thêm cấu hình tùy chọn `apiBuilder` vào `project.json`:
   ```json
   {
     "apiBuilder": {
       "openApiPath": "openapi/openapi.json",
       "sourcePath": "source"
     }
   }
   ```
   Hai field là relative path từ chính `projects/<project>/`; project cũ không khai báo vẫn được parse và nhận trạng thái hướng dẫn rõ ràng.
2. API Builder chỉ đọc cấu hình server-side của project đã chọn. OpenAPI phải là file, source phải là directory; path rooted, URL, traversal, `.env` và target ngoài project đều bị từ chối sau canonicalization.
3. UI không còn workflow nhập/lưu path máy cục bộ. Khi mở API Builder hoặc đổi project, UI tải contract cấu hình của project; hiển thị nguồn đang dùng và thông báo actionable nếu project chưa cấu hình.
4. Giữ OpenAPI-first/Roslyn fallback và contract fingerprint. Workspace tồn tại không bị ghi đè hay silently rebound khi contract đổi.
5. Cập nhật `project-template`, project fixture/E2E, README và kiến trúc để người dùng clone ở máy khác chỉ cần giữ đúng layout trong project.

### Khả năng hiện có và khoảng trống

- Tái sử dụng: `ProjectSpec` deserialize trung tâm, `SafeProjectDirectory`, `OpenApiContractAdapter`, `SourceContractAnalyzer`, `ApiBuilderContractProjection`, workspace/compiler và Playwright host lifecycle.
- Không tạo parser OpenAPI, executor HTTP hay cơ chế storage thứ hai.
- Framework harness phủ projection/workspace/compiler nhưng chưa phủ HTTP facade của project-config resolution; E2E chưa phủ auto-load từ project config. Hai khoảng trống này là test bắt buộc.
- LSP C# đã hoạt động; JavaScript LSP thiếu dependency nên được người dùng cho phép bỏ qua cho request này. Frontend đã được khảo sát bằng grep và browser E2E hiện có.

### Lộ trình

- Cấu hình project → resolve contract an toàn từ project root → facade/UI auto-load.
- Chuyển fixture/E2E sang project-owned contract và thêm regression path boundary.
- Cập nhật template/tài liệu, build/harness/browser E2E và QC full.

## Hỏi đáp

- Người dùng đã duyệt hướng project-owned configuration trong `Tool-QC/projects/<project>/`, không dùng sibling backend ngoài Tool-QC.
- Người dùng đã chọn pipeline chuyên sâu và cho phép bỏ qua JavaScript LSP vì thiếu dependency; C# MCP hoạt động.
- Mức QC đã chốt: `full`.
- Không có câu hỏi còn mở làm thay đổi outcome: cấu hình là optional để không phá project hiện hữu; UI dùng auto-load thay cho path override; boundary là selected project directory để portability và safety cùng đúng.