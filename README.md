# API Auto Test

## Kiến trúc module

Shared framework được chia theo ranh giới mở rộng: `AutoTest.Abstractions` chứa
contract chung, `AutoTest.Core` điều phối suite, các project `AutoTest.Http`,
`AutoTest.PostgreSql`, `AutoTest.Elasticsearch`, `AutoTest.Mqtt` sở hữu transport,
`AutoTest.HttpStub` giả lập dịch vụ HTTP bên ngoài và ghi request thật, còn
`AutoTest.Reporting.Html` sở hữu báo cáo. Thiết kế dependency và quy tắc thêm tool
mới được mô tả tại [`docs/architecture.md`](docs/architecture.md).

Dự án mới luôn được thêm tại `projects/<project-name>/`, không thêm business rule
vào shared framework. Testcase nằm trong `projects/<project-name>/testcases/**/*.json`.

Báo cáo được phân thư mục tương ứng với nhóm testcase để dễ tra cứu. Ví dụ,
testcase trong `projects/ops-service/testcases/events/` sinh báo cáo tại
`.artifacts/test-runs/ops-service/events/`. Một lần chạy chứa nhiều nhóm sẽ được lưu tại
`.artifacts/test-runs/<project-name>/_combined/`.

Khung kiểm thử API dùng chung cho nhiều dự án. Cấu hình kết nối tập trung trong `.env`; logic riêng của từng dịch vụ nằm trong `projects/<project-name>`.

Load test JMeter dùng shared launcher tại `scripts/jmeter/Invoke-JMeter.ps1`.
Mỗi dự án giữ JMX, CSV adapter và properties riêng dưới `projects/<project-name>/load/`;
shared launcher chỉ quản lý tiến trình JMeter và cấu trúc JTL/HTML chung.

## Cấu hình

1. Sao chép `.env.example` thành `.env` để cấu hình dùng chung.
2. Nếu dự án có cấu hình riêng, sao chép `projects/<project>/.env.example` thành `projects/<project>/.env`.
3. Không commit `.env`; file đã được đưa vào `.gitignore`.
4. Chọn dự án bằng `ACTIVE_PROJECT`, hoặc truyền `--project` khi chạy.

Thứ tự ưu tiên là biến môi trường hệ điều hành/CI, `projects/<project>/.env`, rồi `.env` ở thư mục gốc.

Lệnh đơn giản nhất để mở Tool-QC:

```powershell
npm start
```

Không cần chạy `npm install` vì UI không dùng package JavaScript bên ngoài. Lệnh PowerShell
`.\toolqc.ps1` vẫn được giữ cho automation và các chức năng nâng cao.

UI hỗ trợ chọn chính xác testcase theo ID, dừng workload đang chạy, xem lịch sử và báo cáo cũ, chỉnh cấu hình dự án và kiểm tra nhanh các URL đã cấu hình. Runner hỗ trợ `MAX_PARALLEL_CASES`, báo cáo JUnit, bước Redis và `expect.jsonSchema` cho JSON Schema cơ bản.

Sinh testcase bản nháp từ controller .NET:

```powershell
.\toolqc.ps1 generate ops-service "D:\works\projects\ops-service"
```

Generator chỉ sinh bản nháp; các coverage chưa xác minh giữ trạng thái `blocked` để người kiểm thử hoàn thiện trước khi chạy.

Generator còn dùng Roslyn SemanticModel để nhận diện service call, guard và nhánh điều kiện trong controller. Nếu đã có báo cáo Coverlet dạng Cobertura, có thể đọc các line/branch chưa được cover:

```powershell
.\toolqc.ps1 coverage .\TestResults\coverage.cobertura.xml .\.artifacts\coverage-gaps.json
```

AutoFixture và xUnit không nằm trong luồng API testcase mặc định: chúng chỉ phù hợp với renderer unit-test C# tùy chọn, trong khi Tool-QC tiếp tục dùng testcase JSON và API/database thật.

Các lệnh khác:

```powershell
# Mở giao diện web để chọn và chạy testcase
npm start

# Cách tương đương
npm run ui

# Chạy tất cả testcase của dự án
.\toolqc.ps1 test ops-service

# Chạy dự án ACTIVE_PROJECT trong .env
.\toolqc.ps1 test

# Chạy một hoặc nhiều tag, phân cách bằng dấu phẩy
.\toolqc.ps1 test ops-service redemption-atomic-stock-quantity

# Build toàn bộ tool
.\toolqc.ps1 build
```

Các bước Elasticsearch chỉ dùng để đọc/đối chiếu projection và không tạo dữ liệu nghiệp vụ.
Cấu hình `ELASTICSEARCH_URL`, tùy chọn `ELASTICSEARCH_USERNAME`/`ELASTICSEARCH_PASSWORD`,
và `ELASTICSEARCH_INDEX_PREFIX` trong `.env`; testcase gọi `_doc`, `_search` hoặc `_count`
qua `request.elasticsearch` và vẫn dùng `expect.status`, `expect.json`, `save`, `retry` như HTTP.

Agent được ủy quyền chạy Tool-QC và kết nối API/database/MQTT của môi trường local/test để
tự xác minh, sửa testcase/fixture và chạy lại. Mọi cấu hình vẫn lấy tập trung từ `.env`
hoặc biến môi trường hệ điều hành; không sao chép URL, tag, timeout hay credential vào
instruction và testcase. Quyền này không áp dụng cho production và không vô hiệu hóa cơ
chế sandbox của ứng dụng chạy agent.

Agent sử dụng cấu hình `.env` thông qua runner làm nguồn cấu hình duy nhất. Khi môi trường
là local/test, production bị tắt và destructive test đã bật, agent tự yêu cầu quyền công
cụ rồi chạy; người dùng không cần nhập lại câu xác nhận hoặc chạy từng lệnh. Quyền sandbox
của ứng dụng Codex là lớp bên ngoài và không thể được source tự động vô hiệu hóa.

## Chạy

### Quét message từ một source bất kỳ ra Excel

Module `AutoTest.MessageScanner` quét đệ quy source, tự bỏ qua các thư mục build,
dependency và version-control, thu thập các key dạng `Mes.<Module>.<...>`, loại trùng,
phân nhóm theo module và xuất workbook `.xlsx`. Sheet `Messages` giữ đúng bốn cột
`STT`, `Messages`, `Mes tiếng Việt`, `Mes tiếng Anh` như file mẫu; mỗi module còn có
một sheet riêng. Nếu source chứa resource JSON, RESX hoặc `.properties` có hậu tố/thư
mục ngôn ngữ `vi` và `en`, nội dung dịch tương ứng cũng được điền tự động.

Chỉ cần cung cấp đường dẫn source:

```powershell
dotnet run --project runner/AutoTest.MessageScanner -- --source "D:\works\projects\ops-service"
```

Mặc định báo cáo nằm trong `.artifacts/message-scans/`. Có thể chọn tên file:

```powershell
dotnet run --project runner/AutoTest.MessageScanner -- --source "D:\works\projects\ops-service" --output "message-results\ops-service-messages.xlsx"

Trên UI, mở **Công cụ → Cào message**, nhập đường dẫn source, nhấn **Cào message** để
xem kết quả theo module, tìm kiếm và tải báo cáo Excel mà không cần nhớ lệnh CLI.
```

Scanner chỉ đọc source, không build hoặc chạy project được quét và không đọc `.env`.

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

Khi agent tạo hoặc sửa testcase, agent phải tự chạy tag nhỏ nhất, đọc report,
phân loại lỗi và tự sửa/chạy lại nếu nguyên nhân nằm ở testcase, fixture hoặc
shared framework. Vòng lặp chỉ dừng để báo người dùng khi đã xác nhận lỗi backend
hoặc gặp blocker môi trường/quyền cần người dùng xử lý. Agent không được thay đổi
expected chỉ để ép testcase thành công.

Mọi lần chạy đều giữ lại dữ liệu test để người dùng kiểm tra lại qua API hoặc database.
Runner không thực thi các bước `cleanup`; nếu testcase có khai báo cleanup, console sẽ
ghi rõ các bước đó đã được bỏ qua. Khi agent gặp lỗi testcase hoặc fixture, agent phải
tự sửa và tự chạy lại đến khi đạt, không yêu cầu người dùng chạy từng lệnh trung gian.
Agent chỉ dừng khi xác nhận lỗi backend hoặc gặp blocker quyền/môi trường bên ngoài.

Kết quả kiểm thử phải phản ánh đúng bằng chứng từ lần chạy hiện tại. Không được tạo giả,
chỉnh sửa, suy diễn hoặc lấy kết quả cũ để báo PASS; build, parse JSON và review source
không được xem là testcase đã chạy thành công. Báo cáo phải phân biệt `PASS`, `FAIL`,
`NOT_RUN` và `BLOCKED`, đồng thời giữ bằng chứng cần thiết để tái hiện lỗi. Chỉ được che
secret, không được làm thay đổi dữ liệu nghiệp vụ.

Trước khi gửi request, runner chạy preflight validation để phát hiện ID testcase
trùng, biến chưa được khai báo, transport/executor mâu thuẫn, thiếu `expect`, cấu
hình song song không hợp lệ và file tạm như `*.working.json` khi chạy toàn bộ dự
án. Preflight thất bại trả exit code `3`; không có testcase phù hợp trả `2`.

Assertion JSON hỗ trợ `equals`, `notEquals`, `exists`, `type`, `contains`,
`matches`, `oneOf`, `count`, `greaterThan`, `greaterThanOrEqual`, `lessThan` và
`lessThanOrEqual`. JSON path hỗ trợ index mảng và wildcard, ví dụ
`$.data.pagedData.*.status`.

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

Chạy test quản lý người dùng và hồ sơ cá nhân:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags users,profiles
```

Chạy test quản lý vai trò:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags roles
```

Chạy test quản lý khu vực và gán thiết bị:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags booths
```

Chạy test kết nối, publish, subscribe và kiểm tra nội dung MQTT:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags mqtt
```

Chạy toàn bộ App đổi quà:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags redemptions
```

Chạy riêng luồng đổi quà tuần tự cho một người:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags redemption-single
```

Chạy App đổi quà, bao gồm hai bước tải đồng thời 100 request:

```powershell
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags redemptions
```

Kịch bản tự đăng nhập quản trị, tạo sự kiện, quầy, thiết bị có `functionTypes: [2]`,
quà, đăng ký và RFID. Điểm của khách được cấp trực tiếp trong PostgreSQL bằng
`DB_CONNECTION_STRING`, sau đó kịch bản tự đăng nhập thiết bị và chạy luồng đổi quà.

MQTT test đọc `MQTT_HOST`, `MQTT_PORT`, `MQTT_PREFIX`, `MQTT_USERNAME`,
`MQTT_PASSWORD`, `MQTT_CLIENT_ID` và `MQTT_TIMEOUT_MS` từ `.env`. Topic tương đối
trong test case được tự động ghép với `MQTT_PREFIX`. Kịch bản có thể ghi đè
`username`, `password`, `clientId` ngay trong bước MQTT bằng biến đã lưu từ API;
nhờ đó mỗi thiết bị có thể dùng đúng tài khoản MQTT động do dịch vụ cấp.

MQTT hỗ trợ các action `connect`, `publish`, `subscribe`, `roundtrip` và
`lastwill`. Action `lastwill` tạo một client quan sát subscribe trước, kết nối
client thiết bị với Will trong gói CONNECT, rồi cố ý đóng client thiết bị mà
không gửi MQTT DISCONNECT. Broker phải phát Will thật; graceful disconnect sẽ
không phát Will. Topic tương đối trong `will.topic` vẫn được ghép `MQTT_PREFIX`.

```json
{
  "name": "Broker phát trạng thái ngắt kết nối bằng Last Will",
  "request": {
    "mqtt": {
      "action": "lastwill",
      "timeoutMs": 15000,
      "username": "${mqttUsername}",
      "password": "${mqttPassword}",
      "clientId": "${mqttClientId}",
      "will": {
        "topic": "device/${deviceId}/status/update",
        "payload": "{\"connectionStatus\":2,\"connectSessionId\":${timestampMs}}",
        "qos": 1,
        "retain": true
      }
    }
  },
  "expect": {
    "mqtt": {
      "topic": "device/${deviceId}/status/update",
      "payload": "{\"connectionStatus\":2,\"connectSessionId\":${timestampMs}}"
    }
  }
}
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
dòng `Báo cáo HTML:` trong thư mục `.artifacts/test-runs/`.

Sau mỗi lần chạy, runner tự tạo báo cáo HTML có timestamp trong `.artifacts/test-runs/` và in
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

Thư mục báo cáo mặc định là `.artifacts/test-runs/` tương đối theo repository, không phụ thuộc
đường dẫn máy. Có thể cấu hình `TEST_RESULTS_DIR` bằng đường dẫn tương đối hoặc tuyệt đối.
Thư mục báo cáo và `projects/*/testcases/` được ignore để không đẩy lên Git ngoài ý muốn.

## Thêm test case

Mỗi dự án chỉ sửa nội dung dưới `projects/<project-name>` và `.env`. Không sửa `src` hoặc `runner`.
Tạo dự án mới bằng cách sao chép `projects/project-template`. Engine hỗ trợ nhiều bước,
`${unique}` để sinh dữ liệu không trùng, lưu giá trị response bằng JSON path và dùng lại ở bước sau.
Runner còn cung cấp `${nowIso}`, `${futureStartIso}` và `${futureEndIso}` để tạo
thời gian ISO 8601 hiện tại, sau một giờ và sau hai giờ cho dữ liệu kiểm thử.
Các biến `${guid1}` đến `${guid32}` là các GUID khác nhau trong phạm vi một case,
phù hợp khi fixture cần ID riêng cho event, customer, history, transaction và request.
Các biến `${futureDay1Iso}`, `${futureDay4Iso}`, `${futureDay5Iso}`,
`${futureDay6Iso}`, `${futureDay8Iso}`, `${futureDay9Iso}`, `${futureDay10Iso}`
`${pastStartIso}` hỗ trợ tạo Event đang Active; `${futureDay15Iso}` hỗ trợ các kịch bản kiểm tra khoảng ngày.

Các biến `${shortUniqueA}`, `${shortUniqueB}`, `${shortUniqueC}` là chuỗi chữ-số
ngẫu nhiên dài đúng 6 ký tự, phù hợp với fixture có mã ngắn nhưng vẫn cần độc lập giữa các lần chạy.

### Coverage manifest bắt buộc cho testcase mới

Suite mới hoặc suite được migrate phải dùng `"schemaVersion": 2` (đây cũng là mặc
định khi bỏ trường version). Mỗi case phải có
`coverage` để preflight cưỡng chế đầy đủ các bước thiết kế, thay vì chỉ phụ thuộc vào
checklist của người viết. Suite v1 chỉ được giữ cho testcase cũ chưa migrate.

- `sourceVerification` ghi các file backend thực sự đã đối chiếu và thời điểm ISO.
- Mỗi mục nhận `covered`, `notApplicable` hoặc `blocked`.
- `covered` phải trỏ tới tên step có thật trong `evidenceSteps`.
- `notApplicable`/`blocked` phải có `reason` cụ thể.
- `contract`, `validation`, `reporting` luôn phải `covered`.
- Mutation/destructive bắt buộc `integrity`; API có auth bắt buộc `authentication`;
  route `${eventId}` bắt buộc `scope`; request song song bắt buộc `concurrency`.
- HTTP không phải health không được chỉ assert status. Mutation dự kiến bị từ chối
  phải có bước database phía sau chứng minh dữ liệu không đổi.

Ví dụ đầy đủ nằm tại `projects/project-template/testcases/system/health.json`.

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

Với endpoint nhận `[FromForm]`, dùng `form` thay cho `body`. Runner gửi dữ liệu dưới
dạng `multipart/form-data`, ví dụ:

```json
"request": {
  "method": "POST",
  "path": "/api/Users",
  "form": {
    "Username": "auto_${unique}",
    "Password": "Admin@123",
    "Name": "Người dùng kiểm thử"
  }
}
```

Để kiểm tra malformed JSON, dùng `rawBody`. Không được khai báo đồng thời `rawBody`
với `body` hoặc `form`; runner vẫn gửi content type `application/json` nhưng giữ nguyên
chuỗi sai cú pháp để xác minh API trả lỗi client và không phát sinh HTTP 500:

```json
"request": {
  "method": "POST",
  "path": "/api/items",
  "rawBody": "{\"name\":"
}
```

Một bước HTTP có thể dùng token đã lưu từ bước đăng nhập trước bằng `authToken`:

```json
{
  "name": "Lấy cấu hình bằng token của thiết bị",
  "auth": "device",
  "authToken": "${deviceAccessToken}",
  "request": { "method": "GET", "path": "/api/Devices/me/mqtt-account" },
  "expect": { "status": 200 },
  "save": { "mqttUsername": "$.data.username" }
}
```

Để kiểm tra nhiều request HTTP đồng thời, đặt `parallelRequests` ở cấp bước. Mỗi
request được đối chiếu độc lập với cùng cấu hình `expect`; bước song song không hỗ
trợ `save` hoặc `retry`. Ví dụ tải đồng thời 100 request:

```json
{
  "name": "Gửi đồng thời 100 yêu cầu xác nhận",
  "parallelRequests": 100,
  "request": { "method": "POST", "path": "/api/items/${itemId}/confirm" },
  "expect": { "status": 200 }
}
```

Khi các request đồng thời cần path, payload hoặc kết quả mong đợi khác nhau, dùng
`concurrentRequests`. Tất cả phần tử được khởi chạy trước khi chờ kết quả; mỗi phần
tử có `request`, `authToken` và `expect` riêng. `statusOneOf` cho phép chấp nhận một
tập status trong bài kiểm tra tranh chấp:

```json
{
  "name": "Hai yêu cầu khác payload cùng tranh một tài nguyên",
  "concurrentRequests": [
    {
      "name": "Yêu cầu A",
      "auth": "admin",
      "authToken": "${adminToken}",
      "request": { "method": "POST", "path": "/api/items/${itemId}", "body": { "quantity": 2, "requestId": "${requestA}" } },
      "expect": { "statusOneOf": [200, 400] }
    },
    {
      "name": "Yêu cầu B",
      "auth": "admin",
      "authToken": "${adminToken}",
      "request": { "method": "POST", "path": "/api/items/${itemId}", "body": { "quantity": 3, "requestId": "${requestB}" } },
      "expect": { "statusOneOf": [200, 400] }
    }
  ]
}
```

Tool-QC không thể tự chèn lỗi vào transaction của API black-box và không thể đếm
EF SQL command nếu backend không xuất telemetry/log có correlation ID. Hai kiểm tra
này cần hook chỉ bật ở môi trường test hoặc nguồn telemetry do backend cung cấp;
nếu không có thì kết quả phải là `BLOCKED`, không được suy diễn là PASS.

Với bước PostgreSQL dùng để đối chiếu dữ liệu, khai báo `expect.database.scalarEquals`
để runner thực thi scalar query và in cả expected lẫn actual trong báo cáo. Không dùng
`SELECT` chỉ nhằm kiểm tra câu lệnh chạy thành công vì `RowsAffected = -1` không chứng
minh dữ liệu đúng:

```json
{
  "name": "Đối chiếu số dòng được tạo",
  "request": { "database": { "command": "SELECT COUNT(*) FROM item WHERE \"OwnerId\"=CAST(@ownerId AS uuid)", "parameters": { "ownerId": "${ownerId}" } } },
  "expect": { "database": { "scalarEquals": "3" } }
}
```

Khi cần lưu bằng chứng nhiều dòng hoặc payload JSON thực tế vào báo cáo HTML, dùng
`expect.database.resultSet`. Runner đọc toàn bộ result set và hiển thị JSON gồm
`rowCount` và `rows`; giá trị cột `json/jsonb` được giữ dưới dạng JSON lồng nhau:

```json
{
  "name": "Ghi payload outbox thực tế vào báo cáo",
  "request": {
    "database": {
      "command": "SELECT \"TransactionCode\" AS \"transactionCode\", \"Payload\" AS \"actualPayload\" FROM bulk_sync_outbox WHERE \"EventId\"=CAST(@eventId AS uuid)",
      "parameters": { "eventId": "${eventId}" }
    }
  },
  "expect": { "database": { "resultSet": true } }
}
```

Không khai báo đồng thời `scalarEquals` và `resultSet` trong cùng một bước.

### Giả lập hệ thống HTTP bên ngoài

`AutoTest.HttpStub` dùng chung cho mọi dự án và mọi kiểu đồng bộ HTTP. Module không
hard-code route hoặc payload nghiệp vụ. Testcase tự start server, cấu hình response,
sau đó inspect request thật mà ứng dụng đã gửi. Header nhạy cảm không được lưu vào report.

Các action gồm `start`, `configure`, `reset`, `inspect`, `stop`. `delayMs` giả lập
phản hồi chậm/timeout; `status`, `response` và `responseHeaders` giả lập response tùy ý.

```json
{
  "name": "Cấu hình hệ thống đích trả thành công",
  "request": { "httpStub": { "action": "configure", "method": "POST", "path": "/api/sync", "status": 200, "response": { "data": { "status": "inserted" } } } },
  "expect": { "httpStub": {} }
},
{
  "name": "Đối chiếu request ứng dụng đã gửi",
  "request": { "httpStub": { "action": "inspect", "method": "POST", "path": "/api/sync", "timeoutMs": 10000 } },
  "expect": { "httpStub": { "receivedCount": 1, "json": { "$.activities.0.transactionCode": { "exists": true } } } }
}
```

Ứng dụng cần được cấu hình trỏ integration URL tới `HTTP_STUB_HOST:HTTP_STUB_PORT`;
nếu backend nạp cấu hình lúc khởi động thì phải restart trước integration test.

### Chạy HTTP Stub như một dự án đối tác độc lập

Khi cần service giả lập chạy liên tục trước và trong suốt nhiều lần test, dùng runner
độc lập thay vì action `httpStub.start` trong suite:

```powershell
dotnet run --project runner/AutoTest.HttpStub -- --config projects/ops-service/http-stubs/bulk-sync-inserted.json --url http://127.0.0.1:2669
```

Service giữ API mở đến khi nhấn `Ctrl+C`. Endpoint quản trị dùng để kiểm tra bằng chứng:

- `GET /__autotest/health`: trạng thái sẵn sàng.
- `GET /__autotest/requests`: toàn bộ request thật đã nhận, đã che header nhạy cảm.
- `GET /__autotest/requests?transactionCode=<code>`: chỉ lấy request chứa đúng
  `transactionCode` ở bất kỳ cấp JSON nào; dùng cách này để các testcase Hangfire
  không đọc nhầm request retry của case khác.
- `DELETE /__autotest/requests`: xóa request đã ghi trước một lượt test mới.

Response có thể lấy dữ liệu động từ request bằng chuỗi
`${request:$.data.activities.0.transactionCode}`. Nhờ vậy service phản hồi đúng
transaction của từng request giống hệ thống đối tác xử lý contract, không hard-code ID.

### Integration Host dùng chung

Cấu hình một integration tại:

```text
projects/<project>/integrations/<integration-name>/integration.json
```

Sau đó chỉ cần lệnh ngắn:

```powershell
.\toolqc.ps1 integration start ops-service point-exchange
.\toolqc.ps1 integration status ops-service point-exchange
.\toolqc.ps1 integration stop ops-service point-exchange
.\toolqc.ps1 integration inspect ops-service point-exchange
.\toolqc.ps1 integration list
```

T3 giả lập cho job điểm thưởng hoàn thiện hồ sơ có profile riêng:

```powershell
.\toolqc.ps1 integration start ops-service profile-completion-bonus
.\toolqc.ps1 integration status ops-service profile-completion-bonus
.\toolqc.ps1 integration inspect ops-service profile-completion-bonus
.\toolqc.ps1 integration stop ops-service profile-completion-bonus
dotnet run --project runner/AutoTest.Runner -- --project ops-service --tags profile-completion-bonus-sync
```

Host nhận `GET /api/integration/profile-completion-bonuses` tại URL khai báo trong
`projects/ops-service/integrations/profile-completion-bonus/integration.json` và lưu
request/response thật dưới `.artifacts/integration-runs/ops-service/profile-completion-bonus/`.
Khai báo `PROFILE_BONUS_STUB_BASE_URL` cho Tool-QC. Backend test phải được cấu hình
`RegistrationIntegration` dùng scheme `http`, host của stub và route trên trước khi
backend khởi động. Token chỉ đặt trong cấu hình môi trường backend; artifact che header
Authorization.

`start` mặc định chạy foreground; `Ctrl+C` dừng host sạch. Chỉ khi cần chạy nền mới dùng:

```powershell
.\toolqc.ps1 integration start ops-service point-exchange -Background
```

Host lưu bằng chứng thật tại:

```text
.artifacts/integration-runs/<project>/<integration>/<timestamp>/
├── session.json
├── index.html
├── requests/000001.json
└── responses/000001.json
```

Request artifact chứa method/path, payload, timestamp, rule đã khớp và header đã che
secret. Response artifact chứa status và payload thực tế host trả cho backend. Vì dữ liệu
được ghi ngay xuống đĩa, người dùng vẫn xem được sau khi host dừng.

Profile hỗ trợ `maxRequestBodyBytes`, `maxInMemoryExchanges`, `matchJson` và `sequence`.
`sequence` mô phỏng nhiều lần gọi liên tiếp, ví dụ lần đầu HTTP 500 và lần sau HTTP 200;
response cuối cùng được lặp lại khi số request vượt độ dài sequence. Payload quá giới hạn
trả HTTP 413 và vẫn được ghi bằng chứng. `status/stop` đối chiếu project, integration và
instance token, nên không dừng nhầm process khác đang chiếm cùng cổng.

Profile có trường `transport`. Hiện provider chạy được là `http`; thiết kế dành chỗ cho
`redis-streams`, Kafka hoặc RabbitMQ dưới module transport riêng. Một dự án đồng bộ media
qua HTTP chỉ cần thêm profile mới, không sửa shared code.

Bước MQTT hỗ trợ `connect`, `publish`, `subscribe`, `roundtrip` và tài khoản động:

```json
{
  "name": "Gửi trạng thái thiết bị qua MQTT",
  "request": {
    "mqtt": {
      "action": "publish",
      "topic": "device/${deviceId}/status/update",
      "payload": "{\"connectionStatus\":1,\"connectSessionId\":${timestampMs}}",
      "qos": 1,
      "retain": true,
      "username": "${mqttUsername}",
      "password": "${mqttPassword}",
      "clientId": "${mqttClientId}"
    }
  },
  "expect": {}
}
```

Với xử lý bất đồng bộ, thêm `retry` vào bước kiểm tra. Runner sẽ gọi lại bước đó
cho tới khi đạt kết quả hoặc hết `timeoutMs`:

```json
"retry": { "timeoutMs": 10000, "intervalMs": 500 }
```

