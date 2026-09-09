# Reusable load-testing tool

`AutoTest.LoadTesting` owns reusable timing models, percentile calculation, identifier
hashing, `Server-Timing` parsing, JSONL reading, diagnostic Markdown and multi-run
comparison. Project-specific routes, payloads, authentication and fixture creation stay
in an adapter under `runner/` and `projects/<project>/load/`.

## Safety

- The safe default is two workers and one measured iteration.
- A run with more than ten measured operations requires `CONFIRM_LOAD_RUN=true`.
- Reports never need credentials and can be rebuilt offline from JSONL.
- Identifiers in the shared diagnostic model are hashes, not raw RFID, customer, cart or
  resource IDs.
- Client duration cannot prove lock wait or lock hold. For that evidence the backend must
  return `Server-Timing` or structured logs.

## Offline report

```powershell
dotnet run --project .\projects\ops-service\load\runner\OpsService.LoadRunner.csproj -c Release -- `
  --report-only `
  --input .\.artifacts\test-runs\ops-service\load\redemption-load-contention.jsonl
```

## Compare runs

```powershell
dotnet run --project .\projects\ops-service\load\runner\OpsService.LoadRunner.csproj -c Release -- `
  --compare `
  --inputs ".\run-1.jsonl,.\run-2.jsonl,.\run-3.jsonl" `
  --output .\.artifacts\test-runs\load-comparison.md
```

## Adding another project

1. Reuse `AutoTest.LoadTesting` for records and reports.
2. Add a small adapter containing only the project's workflow and response paths.
3. Put non-secret manifests under `projects/<project>/load/`; keep tokens in `.env`.
4. Preserve the generic correlation headers `X-Test-Run-Id`, `X-Correlation-Id` and the
   optional diagnostic request header `X-Test-Diagnostics`.
5. Do not add project names, routes or database tables to `AutoTest.LoadTesting`.

Project adapters may provide an optional read-only commit verifier. The shared module only
stores `CommitCompleted`; table names and lifecycle rules remain in the adapter.

## Onsite customer diagnostics

Adapter `--onsite-customers` đo `GET /api/OnsiteOperations/Customers`, gồm TTFB gần đúng,
body-read, response bytes, vị trí chuỗi 5 call, ramp concurrency, search và pagination.

```powershell
dotnet run --project .\projects\ops-service\load\runner\OpsService.LoadRunner.csproj -c Release --no-build -- `
  --onsite-customers --mode baseline
```

Danh sách lệnh đầy đủ nằm tại `projects/ops-service/load/onsite-customer-load-commands.md`.
