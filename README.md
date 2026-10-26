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

## Bắt đầu nhanh cho người mới

Tool-QC là framework kiểm thử API theo dữ liệu: cấu hình môi trường một lần, mô tả testcase bằng JSON, sau đó chạy runner và đọc báo cáo HTML. Bạn không cần viết test C# riêng cho từng dịch vụ.

1. Cài .NET SDK 8 và Node.js LTS.
2. Sao chép `.env.example` thành `.env`, chỉ điền cấu hình local/test của bạn; không commit file này.
3. Chọn một project có sẵn dưới `projects/`, hoặc sao chép `projects/project-template` thành project mới.
4. Khởi động UI bằng `npm start`, chọn project và bắt đầu từ smoke test không thay đổi dữ liệu.
5. Mở báo cáo HTML trong `.artifacts/test-runs/` sau mỗi lần chạy để xem kết quả từng case và step.

### Chọn đúng công cụ

| Nhu cầu | Dùng gì | Kết quả |
| --- | --- | --- |
| Chạy testcase JSON hiện có | **Chạy testcase** trên UI hoặc `toolqc.ps1 test` | Kết quả PASS/FAIL và báo cáo HTML |
| Tạo testcase từ API .NET | **Generator** | Bản nháp schema-v2, cần preflight và duyệt trước khi lưu |
| Dựng chuỗi API không cần sửa JSON | **Công cụ → API Builder** | Workspace contract-driven, biên dịch thành bản nháp schema-v2 |
| Kiểm bảng quyết định số học | **Công cụ → UnitX** | Draft decision table chạy an toàn với operator whitelist |
| Quét message backend ra Excel | **Công cụ → Cào message** | Workbook message để rà soát nội dung song ngữ |

> Bắt đầu bằng smoke test. Chỉ chạy case có `destructive: true` khi môi trường là local/test cô lập và bạn chủ động bật guard tương ứng.

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

Dùng UI hằng ngày không cần package JavaScript production. Chỉ khi chạy browser E2E cho người phát triển mới cần `npm install` để cài `@playwright/test`, sau đó chạy `npm run test:e2e`. Lệnh PowerShell
`.\toolqc.ps1` vẫn được giữ cho automation và các chức năng nâng cao.

UI hỗ trợ chọn chính xác testcase theo ID, dừng workload đang chạy, xem lịch sử và báo cáo cũ, chỉnh cấu hình dự án và kiểm tra nhanh các URL đã cấu hình. Runner hiện hỗ trợ HTTP JSON, multipart form, PostgreSQL fixture command, MQTT, request đồng thời trong một step, retry giới hạn, assertion JSON, biến liên bước, preflight, báo cáo HTML và JUnit tùy chọn. Redis assertions, JSON Schema/OpenAPI validation đầy đủ và parallel case scheduling chưa được triển khai.

### Catalogue mã lỗi và message

Mỗi project có thể khai báo `errorCatalogue` trong `project.json`; đường dẫn catalogue phải nằm trong chính thư mục project. Catalogue version 1 dùng prefix viết hoa và entry có mã ổn định, HTTP status, bản dịch `vi` và `en`:

```json
{
  "schemaVersion": 1,
  "prefix": "TQC",
  "entries": [
    {
      "code": "TQC-VALIDATION-001",
      "httpStatus": 400,
      "messages": {
        "vi": "Dữ liệu không hợp lệ.",
        "en": "The data is invalid."
      }
    }
  ]
}
```

Mã có dạng `<PREFIX>-<DOMAIN>-<NNN>`, ví dụ `TQC-VALIDATION-001`. Mã là contract machine-readable; HTTP status và `message` localized không thay thế cho mã. Project template đã có `error-catalogue.json` rỗng để khởi tạo catalogue mới. Không thêm mã business cho một API khi chưa có source hoặc response evidence hiện tại.

Testcase schema-v2 có thể khai báo semantic expectation thay vì lặp lại status/JSON path:

```json
{
  "expect": {
    "error": { "code": "TQC-VALIDATION-001", "locale": "vi" }
  }
}
```

Preflight kiểm tra entry tồn tại, status (nếu khai báo thêm) khớp catalogue và locale. Khi chạy, runner assert status catalogue, `$.errorCode` và `$.message`; mọi assertion `expect.json` sẵn có vẫn được giữ. Locale không phải `vi` hoặc `en` fallback về `en`.

UI mặc định là tiếng Việt. Người dùng có thể chọn English ở header; lựa chọn chỉ lưu cục bộ trong browser và thay đổi text hiển thị, không thay đổi request, project, workspace, testcase selection hoặc dữ liệu môi trường. Network/HTTP UI errors ưu tiên mã trả về từ API, sau đó mới dùng HTTP status hay fallback text.

Sinh testcase bản nháp từ controller .NET:

```powershell
.\toolqc.ps1 generate ops-service "D:\works\projects\ops-service"
```

Có thể ép chế độ sinh bằng tham số cuối: `auto`, `ai` hoặc `local`:

```powershell
.\toolqc.ps1 generate ops-service "D:\works\projects\ops-service" auto
```

Generator chỉ sinh bản nháp; các coverage chưa xác minh giữ trạng thái `blocked` để người kiểm thử hoàn thiện trước khi chạy. Trên UI, bản nháp có thể được preflight và chạy trực tiếp từ thư mục artifact tạm trước khi kích hoạt; kết quả vẫn xuất hiện trong panel chạy, lịch sử và báo cáo mà không ghi vào `projects/<project>/testcases`.

### API Builder

API Builder nhận trực tiếp một thư mục source local của một project và chỉ quét read-only. Khi người dùng nhập source trong **Công cụ → API Builder**, Tool-QC ưu tiên tìm `openapi.json` hoặc `swagger.json` chuẩn OpenAPI 3.1; nếu không có contract hợp lệ nhưng source là .NET, Tool-QC dùng Roslyn để đọc controller/action/DTO/validation. Source Node.js/TypeScript chỉ được hỗ trợ khi có OpenAPI 3.1, không suy đoán route từ Express/Nest/Fastify.

Mapping local được lưu riêng theo Tool-QC project tại `projects/<project>/project.local.json` và đã bị Git ignore:

```json
{
  "apiBuilder": {
    "localSourcePath": "<thu-muc-source-local>"
  }
}
```

Không cần sửa hoặc copy repository nguồn. `project.local.json` không được commit; `project.json` vẫn có thể giữ `apiBuilder.openApiPath`/`sourcePath` relative như fallback portable. Tool-QC từ chối URL, UNC/network path, reparse point và không quét `.env`, `.git`, dependencies, build output, artifacts, logs hoặc test output. Source path và source content không được ghi vào workspace, testcase, report hay browser localStorage. Workspace chỉ lưu contract fingerprint/operation tại `.artifacts/api-builder/<project>/<workspace-id>/workspace.json`; catalog biến không phân biệt hoa/thường, hiển thị theo A–Z và workspace dùng revision để phát hiện xung đột khi lưu.

Để bắt đầu, chọn Tool-QC project, mở **Công cụ → API Builder**, nhập thư mục source project local rồi tải operation. Khi để trống input, API Builder dùng mapping local đã lưu hoặc portable configuration của project.

Mỗi step giữ `operationId` và contract fingerprint. Variable chỉ bind vào field request có `targetFieldId` tại step sau, cùng kiểu tương thích; credential hoặc field nhạy cảm bị từ chối. Contract fingerprint thay đổi yêu cầu chọn lại operation, không ghi đè workspace cũ. Browser journey dùng `@playwright/test` ở dev dependency: `npm run test:e2e` chạy Chromium local, giữ trace/screenshot khi thất bại và không dùng credential hay API production.

Nút **Biên dịch và kiểm tra** chỉ chuyển workspace thành bản nháp testcase schema-v2 với `steps[].save` và `${variable}`, rồi đưa bản nháp vào cùng luồng **1. Kiểm tra** → **2. Chạy thử API** → **3. Lưu testcase** của Generator. API Builder không ghi trực tiếp vào `projects/<project>/testcases`. Preflight cơ bản có thể hợp lệ nhưng vẫn không cho lưu chính thức khi coverage evidence còn `blocked`; chỉ activation sau preflight đầy đủ mới áp dụng guard SHA-256 khi ghi đè.

Generator hỗ trợ ba chế độ: `auto` ưu tiên AI rồi tự quay về Roslyn local, `ai` bắt buộc AI, và `local` chỉ dùng Roslyn. Provider hỗ trợ `codex-cli` và `claude-cli`; chọn bằng `TESTCASE_GENERATOR_PROVIDER` hoặc UI. AI chạy read-only/restricted trên source backend, không được đọc `.env` hoặc secret; kết quả vẫn phải qua preflight, chạy thử và người dùng lưu chính thức.

Máy dùng Codex cần cài và đăng nhập `codex`; máy dùng Claude cần cài `claude` và đăng nhập `claude auth login`. Tool-QC chỉ gọi CLI đã chọn và không lưu API key của provider.

`TESTCASE_GENERATOR_TIMEOUT_SECONDS=0` nghĩa là không giới hạn thời gian AI. Tool chỉ dừng khi người dùng dừng UI/process hoặc khi đặt một số giây lớn hơn `0`; phân tích source lớn có thể chạy hàng chục phút hoặc lâu hơn.

Bước phân tích source cũng có chế độ nhanh bằng Roslyn hoặc chuyên sâu bằng AI. Với source lớn, chọn `Theo module`; Tool-QC chỉ nạp controller, DTO, validator và service thuộc module đã chọn, sau đó AI làm giàu dependency, thứ tự API và các rủi ro auth, scope, integrity, lifecycle và concurrency.

Generator còn dùng Roslyn SemanticModel để nhận diện service call, guard và nhánh điều kiện trong controller. Nếu đã có báo cáo Coverlet dạng Cobertura, có thể đọc các line/branch chưa được cover:

```powershell
.\toolqc.ps1 coverage .\TestResults\coverage.cobertura.xml .\.artifacts\coverage-gaps.json
```

AutoFixture và xUnit không nằm trong luồng API testcase mặc định: chúng chỉ phù hợp với renderer unit-test C# tùy chọn, trong khi Tool-QC tiếp tục dùng testcase JSON và API/database thật.

### UnitX

UnitX V1 cho phép tester tạo bảng quyết định decimal bằng operator whitelist: `add`, `subtract`, `multiply`, `divide`, `round`, `ifGreaterThan`; kết quả trả actual/expected theo từng dòng. Draft UnitX lưu dưới `.artifacts/unitx`. UnitX không thực thi JavaScript, C#, shell hoặc biểu thức tự do. Hỗ trợ source .NET/xUnit là Phase 2 chỉ có design boundary/review/sandbox cô lập, chưa được triển khai.

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

Khi truyền project catalogue, scanner còn kiểm source read-only để phát hiện `missing-vi`,
`missing-en`, `unregistered-error-code` và `hard-coded-message`. Findings có file, dòng và
chi tiết; workbook giữ sheet `Messages` legacy và chỉ thêm sheet `Audit` khi có finding. Scanner
không sửa source, không chạy source và không đọc `.env`.

Chỉ cần cung cấp đường dẫn source:

```powershell
dotnet run --project runner/AutoTest.MessageScanner -- --source "D:\works\projects\ops-service"
```

Để đối chiếu source với catalogue của một Tool-QC project:

```powershell
dotnet run --project runner/AutoTest.MessageScanner -- --source "D:\works\projects\ops-service" --catalogue "projects\my-project\project.json"
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
trùng, biến chưa được khai báo, transport/executor mâu thuẫn và thiếu `expect`.
Nó cũng chặn cấu hình song song không hợp lệ và file tạm như `*.working.json` khi
chạy toàn bộ dự án. Preflight thất bại trả exit code `3`; không có testcase phù hợp trả `2`.

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

## Nhập testcase từ workbook XLSX

Tool-QC có shared module `AutoTest.Workbooks` dùng Open XML để kiểm tra gói `.xlsx`, đọc
sheet/cell/formula/merged range và tạo fingerprint SHA-256. Module này không chứa rule nghiệp
vụ hoặc nhánh theo tên dự án. Mỗi dự án tự đăng ký adapter; release đầu có adapter
`AutoTest.ProjectAdapters.BeBooking` cho family `VERIFY` của workbook Campaign.

Phân tích workbook bằng CLI, không gọi API backend:

```powershell
dotnet run --project runner/AutoTest.Generator/AutoTest.Generator.csproj -c Release -- `
  --workbook "C:\Users\quanghau\Downloads\Campaign (1).xlsx" `
  --project be-booking `
  --analyze-workbook
```

Sinh một case hoặc batch nhiều case với TimeFrame tính động tại thời điểm chạy:

```powershell
dotnet run --project runner/AutoTest.Generator/AutoTest.Generator.csproj -c Release -- `
  --workbook "C:\Users\quanghau\Downloads\Campaign (1).xlsx" `
  --project be-booking `
  --case-ids verify/1,verify/54 `
  --repository-root "$PWD" `
  --output ".artifacts/generated-testcases/be-booking/verify"
```

Trên UI, mở `Công cụ` → `Nhập XLSX`, chọn file, phân tích, chọn một, nhiều hoặc tất cả case rồi sinh batch. Nếu chỉ chọn một case, UI chuyển JSON sang quy trình preflight/run/lưu hiện có; batch nhiều case được ghi trực tiếp vào thư mục output riêng để preflight toàn bộ. Khi lưu chính thức, testcase đi vào
`projects/be-booking/testcases/workbook-generated/verify/`; file tồn tại phải được xác nhận
và fingerprint phải còn khớp trước khi ghi đè.

Adapter version 2 phát hiện và sinh đủ 69 case hiện có trong sheet `VERIFY`; mỗi case tạo một JSON schema v2 theo thứ tự workbook. Case lỗi hoặc thiếu được blocked riêng, không làm hỏng các case còn lại. Workbook
gốc không được copy vào repository; Tool-QC chỉ giữ metadata/fingerprint và dọn file upload tạm.

