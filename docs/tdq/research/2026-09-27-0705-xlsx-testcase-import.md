# Research — XLSX testcase import

Ngày nghiên cứu: 2026-09-27

## ClosedXML — merged cells và formulas

- Nguồn: tài liệu/API và repository chính thức `ClosedXML/ClosedXML`.
- Kết luận: object model thuận tiện cho merged ranges và formula/cached value. Cách dùng thông thường tải workbook vào bộ nhớ, nên phải giới hạn upload và benchmark workbook thật.
- Ảnh hưởng: phù hợp release đầu nếu spike đạt thời gian và peak-memory mong muốn.

## Open XML SDK — SpreadsheetML

- Nguồn: Microsoft Learn về SpreadsheetML, `MergeCells`, `CellFormula` và workbook/worksheet parts.
- Kết luận: biểu diễn chính xác cấu trúc XLSX nhưng không tự chuẩn hoá shared strings, styles, date serials, sparse cells hoặc merged-anchor propagation.
- Ảnh hưởng: dùng khi cần streaming/kiểm soát memory tốt hơn và chấp nhận parser phức tạp hơn.

## MiniExcel — import tabular

- Nguồn: repository và tài liệu chính thức `mini-software/MiniExcel`.
- Kết luận: mạnh ở đọc/ghi dữ liệu tabular nhẹ và streaming; chưa có bằng chứng phù hợp hơn cho workbook Campaign phụ thuộc hàng nghìn merged ranges, style/legend và formula.
- Ảnh hưởng: chỉ chọn nếu spike chứng minh lấy đủ metadata cần thiết.

## An toàn upload XLSX

- Nguồn: Microsoft Learn về ASP.NET Core file uploads; OWASP File Upload Cheat Sheet và Zip Bomb guidance.
- Kết luận: không tin filename/content-type; allowlist `.xlsx`, giới hạn request/file, dùng tên temp do server tạo, không lưu dưới webroot, giới hạn ZIP entry count và tổng uncompressed bytes/compression ratio.
- Ảnh hưởng: guard thuộc shared XLSX layer; adapter project chỉ nhận workbook model đã qua kiểm tra.

## Khuyến nghị

1. Spike ClosedXML và Open XML SDK trên workbook Campaign, đo thời gian, peak working set và độ đầy đủ metadata.
2. Nếu ClosedXML đạt guard/benchmark, dùng cho release đầu để giảm code parser; nếu không, dùng Open XML SDK.
3. Giữ adapter contract độc lập thư viện để thay reader không ảnh hưởng profile `be-booking` hoặc testcase schema.
