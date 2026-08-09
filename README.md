# API Auto Test

Khung kiểm thử API dùng chung cho nhiều dự án. Cấu hình kết nối tập trung trong `.env`; logic riêng của từng dịch vụ nằm trong `projects/<project-name>`.

## Cấu hình

1. Sao chép `.env.example` thành `.env` nếu chưa có.
2. Điền URL API và thông tin môi trường cần dùng.
3. Không commit `.env`; file đã được đưa vào `.gitignore`.
4. Chọn dự án bằng `ACTIVE_PROJECT`, hoặc truyền `--project` khi chạy.

Biến môi trường hệ điều hành hoặc CI luôn ưu tiên hơn `.env`.

## Chạy

### Chuẩn bị trước khi chạy

Điền các biến cần thiết trong `.env`:

```env
TEST_ENV=local
ACTIVE_PROJECT=ops-service
API_BASE_URL=http://localhost:5000
AUTH_TYPE=static-token
AUTH_TOKEN=<access-token-của-tài-khoản-quản-trị>
ALLOW_PRODUCTION=false
ALLOW_DESTRUCTIVE_TESTS=true
```

Thay `API_BASE_URL` bằng địa chỉ API thực tế. Chỉ bật
`ALLOW_DESTRUCTIVE_TESTS=true` trên môi trường local/test độc lập vì các test
integration sẽ tạo, cập nhật và xóa dữ liệu. Không bật trên production.

Build runner trước khi chạy:

```powershell
dotnet build runner/AutoTest.Runner/AutoTest.Runner.csproj
```

### Chạy smoke test

Smoke test kiểm tra API và các dependency đã sẵn sàng, không thay đổi dữ liệu và
không cần access token:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags smoke
```

### Chạy riêng từng module

Chạy test quản lý sự kiện:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags events
```

Chạy test quản lý và xác thực thiết bị:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags devices
```

Chạy test quản lý khu vực và gán thiết bị:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags booths
```

### Chạy toàn bộ integration test

Lệnh dưới đây chạy tất cả test có tag `integration` của dự án, bao gồm Event,
Device và Booth:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags integration
```

### Chạy toàn bộ test case của dự án

Không truyền `--tags` để chạy mọi test case, bao gồm smoke và integration:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service
```

### Chạy nhiều nhóm test cùng lúc

Phân cách các tag bằng dấu phẩy:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags events,devices
```

Sau khi chạy, xem tổng số thành công/thất bại trên terminal và mở file được in ở
dòng `Báo cáo HTML:` trong thư mục `test-results/`.

Sau mỗi lần chạy, runner tự tạo báo cáo HTML có timestamp trong `test-results/` và in
đường dẫn file ở dòng `Báo cáo HTML:`. Báo cáo gồm từng test case và từng bước, phương
thức/đường dẫn/dữ liệu gửi đi, kết quả mong đợi, mã HTTP/phản hồi thực tế, thời gian và lỗi.
Các trường nhạy cảm như password, token, authorization, secret và connection string
được che bằng `***`. Thư mục báo cáo là artifact cục bộ và không được commit.

Tên báo cáo có dạng `yyyy-MM-dd_HHmmss_<project>_<tags>.html`, giúp tìm theo ngày,
dự án và nhóm test đã chạy. Ví dụ:

```text
2026-08-09_153015_ops-service_events.html
2026-08-09_154200_ops-service_integration.html
```

Nếu không chọn tag, phần cuối tên file là `tat-ca-test-case`.

Build toàn bộ solution:

```powershell
dotnet build ApiAutoTest.sln
```

Thư mục báo cáo mặc định là `test-results/` tương đối theo repository, không phụ thuộc
đường dẫn máy. Có thể cấu hình `TEST_RESULTS_DIR` bằng đường dẫn tương đối hoặc tuyệt đối.
Thư mục báo cáo và `projects/*/testcases/` được ignore để không đẩy lên Git ngoài ý muốn.

## Thêm test case

Mỗi dự án chỉ sửa nội dung dưới `projects/<project-name>` và `.env`. Không sửa `src` hoặc `runner`.
Tạo dự án mới bằng cách sao chép `projects/project-template`. Engine hỗ trợ nhiều bước,
`${unique}` để sinh dữ liệu không trùng, lưu giá trị response bằng JSON path và dùng lại ở bước sau.

Trong `testcases`, chia file theo module nghiệp vụ để dễ tìm và bảo trì. Mỗi module dùng
một thư mục riêng, ví dụ `users/`, `devices/`, `booths/`; các kiểm tra dùng chung như
health/readiness đặt trong `system/`. Runner tự tìm các file JSON trong toàn bộ thư mục con.

```text
projects/ops-service/testcases/
├── system/
│   └── health.json
├── users/
├── devices/
└── booths/
    ├── booth-management.json
    └── booth-management-regression.json
```

```json
{
  "project": "my-service",
  "cases": [{
    "id": "items.create-and-get",
    "name": "Create then get item",
    "tags": ["smoke"],
    "destructive": true,
    "steps": [{
      "name": "Create",
      "request": {
        "method": "POST",
        "path": "/api/items",
        "body": { "name": "Auto-${unique}" }
      },
      "expect": { "status": 201 },
      "save": { "itemId": "$.data.id" }
    }, {
      "name": "Get",
      "request": { "method": "GET", "path": "/api/items/${itemId}" },
      "expect": { "status": 200, "json": { "$.data.id": { "equals": "${itemId}" } } }
    }]
  }]
}
```

JSON path hiện hỗ trợ thuộc tính lồng nhau như `$.data.id`. Test làm thay đổi dữ liệu phải khai báo `destructive: true` và chỉ chạy khi `ALLOW_DESTRUCTIVE_TESTS=true`. Authentication được cấu hình trong `project.json`; secret chỉ đặt trong `.env` hoặc CI.
