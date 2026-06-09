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
- `GET /api/v1/jobs/recent`
- `POST /api/v1/print/test`
- `POST /api/v1/print`
- `GET /api/v1/ws` with WebSocket upgrade

Legacy MVP aliases without `/api/v1` are still available for compatibility.

`POST /api/v1/print` can optionally use an in-memory local queue when `Agent:Queue:Enabled` is set to `true`.

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

Print a semantic receipt job:

```powershell
$body = @{
  jobId = "demo-001"
  printerName = "POS-80"
  format = "receipt"
  paperWidth = "80mm"
  options = @{
    cut = $true
    cutMode = "full"
    encodingProfile = "latin1"
    openDrawer = $false
    copies = 1
  }
  receipt = @{
    title = "My Store"
    subtitle = "Receipt"
    blocks = @(
      @{ type = "text"; lines = @("123 Main Street", "VAT 00-00000000-0"); align = "center" },
      @{ type = "separator" },
      @{
        type = "items"
        items = @(
          @{ name = "Coffee"; quantity = "2"; unitPrice = "$ 1.000"; total = "$ 2.000" },
          @{ name = "Croissant with a long name"; quantity = "1"; unitPrice = "$ 1.500"; total = "$ 1.500" }
        )
      },
      @{
        type = "totals"
        rows = @(
          @{ label = "Subtotal"; value = "$ 3.500" },
          @{ label = "TOTAL"; value = "$ 3.500"; bold = $true }
        )
      },
      @{ type = "blank"; lines = 3 }
    )
  }
} | ConvertTo-Json -Depth 8

Invoke-RestMethod http://127.0.0.1:17890/api/v1/print -Method Post -ContentType "application/json" -Body $body
```

`format: "receipt"` is recommended for most applications because it accepts structured receipt blocks and lets the agent handle deterministic thermal layout. `format: "escpos"` remains available for advanced integrations that need direct control over low-level ESC/POS commands such as QR codes, barcodes, pre-rasterized images, or explicit command ordering.

## Compatibility Warnings

ESC/POS is a de facto standard, not a single fully consistent implementation. Paper cut, drawer kick, code pages, and accented characters may vary by printer model, driver, and Windows queue configuration. Use `cutMode` when a printer needs a specific cut command, and `encodingProfile` when accented characters or currency symbols need a different code page.

QR codes, CODE128 barcodes, and pre-rasterized image/logo bytes are supported by the ESC/POS renderer. Image commands do not accept local file paths.

No physical printer is required to run unit tests. Hardware tests must be performed manually.

## Documentation

- [Architecture](docs/architecture.md)
- [API](docs/api.md)
- [ESC/POS](docs/escpos.md)
- [Windows setup](docs/windows-setup.md)
- [Windows service mode](docs/windows-service.md)
- [Windows installer](docs/windows-installer.md)
- [Security](docs/security.md)
- [Compatibility](docs/compatibility.md)
- [Roadmap](docs/roadmap.md)

## License

MIT. See [LICENSE](LICENSE).
