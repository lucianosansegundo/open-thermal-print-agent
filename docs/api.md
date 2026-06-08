# API

The agent listens on `127.0.0.1` only. The default port is `17890`.

Canonical base URL:

```text
http://127.0.0.1:17890/api/v1
```

The legacy MVP endpoints without `/api/v1` remain available as compatibility aliases:

- `GET /health`
- `GET /printers`
- `POST /print/test`
- `POST /print`

Future stable integrations should use `/api/v1`.

## Compatibility Contract

The API version is part of the URL path. Version `v1` is the MVP compatibility contract.

During the experimental MVP phase, breaking changes may still happen, but they should be documented in release notes. Once the project reaches a stable release, backwards-compatible changes should be preferred within `/api/v1`, and breaking changes should move to a new API version.

## GET /api/v1/health

Returns local agent status.

Response:

```json
{
  "status": "ok",
  "name": "open-thermal-print-agent",
  "version": "0.1.0",
  "agentVersion": "0.1.0",
  "apiVersion": "v1",
  "platform": "Windows"
}
```

`version` is kept as a legacy alias for `agentVersion`.

## GET /api/v1/printers

Lists installed printers.

Response:

```json
[
  {
    "name": "POS-80",
    "isDefault": true,
    "driverName": "POS-80",
    "portName": "USB002",
    "status": "idle",
    "isOnline": true,
    "workOffline": false,
    "capabilities": ["raw", "escpos"]
  }
]
```

Printer status fields are best-effort diagnostics from the operating system. `isOnline=true` means the Windows queue appears usable; it does not guarantee that the printer has paper, that ESC/POS bytes are accepted by the driver, or that the device physically printed.

## POST /api/v1/print/test

Prints a test receipt.

Request:

```json
{
  "printerName": "POS-80",
  "paperWidth": "80mm",
  "cut": true,
  "cutMode": "full",
  "encodingProfile": "latin1",
  "openDrawer": false
}
```

`cutMode` is optional. When present, it overrides `cut`.

Response:

```json
{
  "jobId": "generated-job-id",
  "status": "printed",
  "printerName": "POS-80",
  "printedAt": "2026-06-08T00:00:00Z"
}
```

## POST /api/v1/print

Prints a generic ESC/POS job.

Request:

```json
{
  "jobId": "optional-client-job-id",
  "printerName": "POS-80",
  "format": "escpos",
  "paperWidth": "80mm",
  "options": {
    "cut": true,
    "cutMode": "full",
    "encodingProfile": "latin1",
    "openDrawer": false,
    "copies": 1
  },
  "content": [
    { "type": "text", "value": "Open Thermal Print Agent", "align": "center", "bold": true },
    { "type": "text", "value": "Test receipt", "align": "center" },
    { "type": "feed", "lines": 1 },
    { "type": "text", "value": "Product         $ 1.000", "align": "left" },
    { "type": "text", "value": "TOTAL           $ 1.000", "align": "left", "bold": true },
    { "type": "feed", "lines": 3 },
    { "type": "cut", "mode": "full" }
  ]
}
```

Supported cut modes:

- `none`
- `full`
- `partial`
- `feedAndFull`
- `feedAndPartial`

Compatibility rules:

- If `cutMode` is provided, it overrides the legacy `cut` boolean.
- If `cutMode` is omitted and `cut` is `true`, the agent uses `full`.
- If `cutMode` is omitted and `cut` is `false`, the agent uses `none`.
- A content command `{ "type": "cut" }` keeps the previous behavior and emits `partial`.
- A content command can specify a mode: `{ "type": "cut", "mode": "feedAndPartial" }`.

Supported encoding profiles:

- `latin1`: default profile, Latin-1 byte mapping.
- `cp850`: IBM code page 850, common on ESC/POS printers for Western European text.
- `cp858`: IBM code page 858, similar to CP850 with euro symbol support.

`encodingProfile` is optional. If omitted, the agent uses `latin1`.

Response:

```json
{
  "jobId": "optional-client-job-id",
  "status": "printed",
  "printerName": "POS-80",
  "printedAt": "2026-06-08T00:00:00Z"
}
```

## Errors

Errors use a consistent shape:

```json
{
  "code": "printer_not_found",
  "message": "Printer was not found.",
  "details": []
}
```

Known MVP error codes:

- `printer_not_found`
- `unsupported_format`
- `invalid_payload`
- `print_failed`
- `access_denied`
- `unsupported_platform`
