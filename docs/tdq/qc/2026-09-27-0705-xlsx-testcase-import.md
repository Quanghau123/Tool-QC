# QC — Nhập XLSX và sinh testcase theo adapter dự án
Ngày: 2026-09-27 · Plan: ../plan/2026-09-27-0705-xlsx-testcase-import.md · Vòng: 1
Soul: chất lượng > runtime > context cost · luật gốc: skills/tdq-conventions/references/soul.md

| # | Hạng mục | Lệnh đã chạy | Kết quả | PASS/FAIL |
|---|---|---|---|---|
| Q1 | Guard XLSX | Generator với file giả `.xlsx` | `TQC-XLSX-CLI-001`, exit `2` | PASS |
| Q2 | Workbook model | CLI analyze workbook thật | 17 sheet; `VERIFY` 2.249 merge, 64 formula | PASS |
| Q3 | Adapter isolation | Source scan + project `ops-service` | shared không có business token; adapter thiếu trả exit `2` | PASS |
| Q4 | Discovery | Inspect `.artifacts/workbook-analysis.json` | 69 ID duy nhất; 1 supported, 68 blocked | PASS |
| Q5 | Case 54 | Inspect generated JSON | schema `2`, đúng case ID, có `six_frames`, không có `06:00` | PASS |
| Q6 | Preflight | Runner `--preflight-only` | `Preflight thành công cho 1 testcase.` | PASS |
| Q7 | Regeneration | Web activation smoke | create, unchanged, update `409`, confirmed save | PASS |
| Q8 | UI/CLI parity | Web upload/generate + CLI artifact | cùng `verify/54`, group `workbook-generated/verify`, JSON 27.853 ký tự | PASS |
| Q9 | Upload lifecycle | Web smoke + temp scan | upload dùng server path; thư mục temp không còn file; log không có cell content | PASS |
| Q10 | Build | `dotnet build ApiAutoTest.sln -c Release --no-restore -m:1 -p:OutDir=...` | 0 warning, 0 error | PASS |
| Q11 | Regression | `git diff --exit-code` testcase viết tay | exit `0`; source group riêng tồn tại | PASS |
| Q12 | Documentation | source/doc scan | README và hai hồ sơ kiến trúc có package, flow, output, giới hạn `VERIFY` | PASS |
| F1 | Test suite | Không chạy theo chỉ đạo đã duyệt | unit test bị loại khỏi spec/plan; build + smoke thay thế | PASS |
| F2 | Touched regression | Build Generator/Web/Runner + CLI/Web smoke | tất cả xanh | PASS |
| F3 | Architecture | kiểm ranh giới shared/adapter/runner | shared workbook trung lập; adapter giữ nghiệp vụ dự án | PASS |
| F4 | Clean code | SOLID self-check | SRP/OCP/LSP/ISP/DIP đều đạt | PASS |

## Bằng chứng

- Build toàn solution: `Build succeeded. 0 Warning(s), 0 Error(s).`
- Analyze Web: `cases=69 supported=1`; generate: `group=workbook-generated/verify file=verify-case-54`.
- Activation: `CREATE saved`; `UNCHANGED unchanged`; `UPDATE_PREVIEW 409`; `CONFIRMED saved`.
- CLI lỗi an toàn: file không phải ZIP và project chưa cấu hình đều trả mã `TQC-XLSX-CLI-001`, exit `2`.
- README doc-lint riêng còn nợ lịch sử: file đã có 727 dòng, vượt rule R6 500 dòng từ trước; nội dung mới không tạo lỗi câu dài. Hai hồ sơ kiến trúc và plan lint PASS.

## Clean Code
- SRP: reader, guard, adapter, import service và UI endpoint có trách nhiệm tách biệt.
- OCP: thêm project bằng adapter mới và một registration, không sửa shared reader.
- LSP: adapter cùng trả `WorkbookAnalysis`/JSON hoặc lỗi có contract ổn định.
- ISP: contract chỉ nhận workbook/descriptor/root cần thiết.
- DIP: CLI và Web đi qua cùng generator/import pipeline.

## Kết luận
PASS toàn bộ Q1–Q12 và F1–F4. Không chạy unit test theo yêu cầu người dùng đã ghi trong spec 1.1.
