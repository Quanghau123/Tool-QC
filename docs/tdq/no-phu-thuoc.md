# Nợ phụ thuộc

Sinh bởi `scripts/tdq_setup.py` và bước `reindex` của `scripts/tdq_finish.py`. Mỗi dòng là một
món KHÔNG tự xử lý được — vì lệnh của nó nằm ngoài danh sách đã khai, vì nó cần quyền của người,
hoặc vì nó hỏng theo cách máy không đoán được. Xử xong thì xoá dòng đó đi; file này chỉ có nghĩa
khi nó rỗng dần.

Ghi kèm tên máy vì một món có thể thiếu trên máy này mà đủ trên máy khác.

- 2026-10-22 · DESKTOP-ETFVQ9E · bậc 3 (language server theo project): cài thất bại — `npm i -g typescript-language-server typescript` → [WinError 2] The system cannot find the file specified
- 2026-10-22 · DESKTOP-ETFVQ9E · bậc 7 (đồ thị graphify): lệnh có ký tự của shell — không chạy chuỗi ghép, hãy khai từng lệnh một — `cd D:\works\projects\Tool-QC && graphify extract . --code-only`
- 2026-10-22 · DESKTOP-ETFVQ9E · tầng LSP không trả lời được: agent-lsp doctor
