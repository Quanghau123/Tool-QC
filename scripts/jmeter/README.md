# Shared JMeter runner

`Invoke-JMeter.ps1` chạy một JMX bất kỳ ở chế độ non-GUI, ghi JTL, log và HTML
dashboard theo cùng một cấu trúc. Script không chứa endpoint, authentication,
payload hoặc quy tắc nghiệp vụ của dự án.

Thiết lập `JMETER_HOME`, hoặc truyền `-JMeterHome`:

```powershell
$env:JMETER_HOME = "C:\tools\apache-jmeter-5.6.3"

.\scripts\jmeter\Invoke-JMeter.ps1 `
  -TestPlan .\projects\my-service\load\scenario.jmx `
  -PropertiesFile .\projects\my-service\load\scenario.local.properties `
  -Properties @{ WORKERS = 15; ITERATIONS = 1 }
```

Mặc định output nằm tại `.artifacts/jmeter/<plan>-<timestamp>/`. Properties chứa
credential phải dùng tên `*.local.*` để Git bỏ qua.
