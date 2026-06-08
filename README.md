# Open Thermal Print Agent

Open Thermal Print Agent is an experimental open source local print agent for web applications and POS systems that need to print thermal receipts from the browser.

The MVP is Windows-first and targets installed ESC/POS-compatible thermal printers. It exposes a local HTTP API bound to `127.0.0.1`, renders generic print jobs into ESC/POS bytes, and sends those bytes to a selected Windows printer.

## Status

MVP / experimental. The project is suitable for development and hardware validation, not production deployment without additional security, installer, monitoring, and printer compatibility work.

## Requirements

- Windows 10 or later for the initial printer adapter.
- .NET 8 SDK or later.
- An installed thermal printer driver or raw-capable printer queue.
- A browser or POS frontend allowed by the agent CORS configuration.

## Run

```powershell
dotnet restore
dotnet build
dotnet run --project src/OpenThermalPrintAgent.Host
```

By default the agent listens on:

```text
http://127.0.0.1:17890
```

Configuration is in `src/OpenThermalPrintAgent.Host/appsettings.json`.

Print endpoints can optionally require a local token. Token security is disabled by default for development. Enable `Agent:Security:RequireToken` and set `Agent:Security:Token` to require either `X-OpenThermalPrintAgent-Token` or `Authorization: Bearer <token>` on print requests.

## Endpoints

Canonical API v1 endpoints:

- `GET /api/v1/health`
- `GET /api/v1/printers`
- `POST /api/v1/print/test`
- `POST /api/v1/print`

Legacy MVP aliases without `/api/v1` are still available for compatibility.

See [docs/api.md](docs/api.md) for request and response details.

## PowerShell Examples

Health:

```powershell
Invoke-RestMethod http://127.0.0.1:17890/api/v1/health
```

List printers:

```powershell
Invoke-RestMethod http://127.0.0.1:17890/api/v1/printers
```

Print a test receipt:

```powershell
$body = @{
  printerName = "POS-80"
  paperWidth = "80mm"
  cut = $true
  cutMode = "full"
  encodingProfile = "latin1"
  openDrawer = $false
} | ConvertTo-Json

Invoke-RestMethod http://127.0.0.1:17890/api/v1/print/test -Method Post -ContentType "application/json" -Body $body
```

With token security enabled:

```powershell
Invoke-RestMethod http://127.0.0.1:17890/api/v1/print/test `
  -Method Post `
  -ContentType "application/json" `
  -Headers @{ "X-OpenThermalPrintAgent-Token" = "your-local-token" } `
  -Body $body
```

Print a generic ESC/POS job:

```powershell
$body = @{
  jobId = "demo-001"
  printerName = "POS-80"
  format = "escpos"
  paperWidth = "80mm"
  options = @{
    cut = $true
    cutMode = "full"
    encodingProfile = "latin1"
    openDrawer = $false
    copies = 1
  }
  content = @(
    @{ type = "text"; value = "Open Thermal Print Agent"; align = "center"; bold = $true },
    @{ type = "text"; value = "Test receipt"; align = "center" },
    @{ type = "feed"; lines = 1 },
    @{ type = "text"; value = "Product         $ 1.000"; align = "left" },
    @{ type = "text"; value = "TOTAL           $ 1.000"; align = "left"; bold = $true },
    @{ type = "feed"; lines = 3 },
    @{ type = "cut"; mode = "full" }
  )
} | ConvertTo-Json -Depth 5

Invoke-RestMethod http://127.0.0.1:17890/api/v1/print -Method Post -ContentType "application/json" -Body $body
```

## Compatibility Warnings

ESC/POS is a de facto standard, not a single fully consistent implementation. Paper cut, drawer kick, code pages, and accented characters may vary by printer model, driver, and Windows queue configuration. Use `cutMode` when a printer needs a specific cut command, and `encodingProfile` when accented characters or currency symbols need a different code page.

No physical printer is required to run unit tests. Hardware tests must be performed manually.

## Documentation

- [Architecture](docs/architecture.md)
- [API](docs/api.md)
- [ESC/POS](docs/escpos.md)
- [Windows setup](docs/windows-setup.md)
- [Security](docs/security.md)
- [Compatibility](docs/compatibility.md)
- [Roadmap](docs/roadmap.md)

## License

MIT. See [LICENSE](LICENSE).
