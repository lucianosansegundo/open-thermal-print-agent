# API

The agent listens on `127.0.0.1` only. The default port is `17890`.

Base URL:

```text
http://127.0.0.1:17890
```

## GET /health

Returns local agent status.

Response:

```json
{
  "status": "ok",
  "name": "open-thermal-print-agent",
  "version": "0.1.0",
  "platform": "Windows"
}
```

## GET /printers

Lists installed printers.

Response:

```json
[
  {
    "name": "POS-80",
    "isDefault": true,
    "capabilities": ["raw", "escpos"]
  }
]
```

## POST /print/test

Prints a test receipt.

Request:

```json
{
  "printerName": "POS-80",
  "paperWidth": "80mm",
  "cut": true,
  "cutMode": "full",
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

## POST /print

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
