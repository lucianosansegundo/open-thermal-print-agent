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

## Authentication

Token authentication is optional and disabled by default for local development.

When `Agent:Security:RequireToken` is enabled, print endpoints require one of these headers:

```http
X-OpenThermalPrintAgent-Token: your-local-token
```

or:

```http
Authorization: Bearer your-local-token
```

`GET /api/v1/health` and `GET /api/v1/printers` remain available without a token in the MVP.

WebSocket connections also require a token when token security is enabled. Browser clients may pass it as `?token=your-local-token`.

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

Prints a job using either the semantic `receipt` format or the low-level `escpos` format.

### Semantic Receipt Format

`format: "receipt"` is recommended for most applications. It accepts structured receipt or ticket data and renders it to ESC/POS internally. The agent does not apply legal, fiscal, country-specific, or business-specific rules; client applications are responsible for the content they send.

Request:

```json
{
  "jobId": "optional-client-job-id",
  "printerName": "POS-80",
  "format": "receipt",
  "paperWidth": "80mm",
  "options": {
    "cut": true,
    "cutMode": "full",
    "encodingProfile": "latin1",
    "openDrawer": false,
    "copies": 1
  },
  "receipt": {
    "title": "My Store",
    "subtitle": "Receipt",
    "blocks": [
      {
        "type": "text",
        "lines": ["123 Main Street", "VAT 00-00000000-0"],
        "align": "center"
      },
      {
        "type": "keyValue",
        "rows": [
          { "label": "Date", "value": "2026-06-08 15:30" },
          { "label": "Order", "value": "#1042" }
        ]
      },
      { "type": "separator" },
      {
        "type": "items",
        "items": [
          { "name": "Coffee", "quantity": "2", "unitPrice": "$ 1.000", "total": "$ 2.000" },
          { "name": "Croissant with a long name", "quantity": "1", "unitPrice": "$ 1.500", "total": "$ 1.500" }
        ]
      },
      {
        "type": "totals",
        "rows": [
          { "label": "Subtotal", "value": "$ 3.500" },
          { "label": "TOTAL", "value": "$ 3.500", "bold": true }
        ]
      },
      {
        "type": "text",
        "label": "Legal notice",
        "lines": ["Legal, tax, compliance, or country-specific text can go here."],
        "align": "left"
      },
      {
        "type": "text",
        "lines": ["Thank you!"],
        "align": "center"
      },
      { "type": "blank", "lines": 3 }
    ]
  }
}
```

Receipt-level fields:

- `title`: optional helper rendered centered and bold.
- `subtitle`: optional helper rendered centered.
- `blocks`: extensible ordered block list.

Supported receipt blocks:

- `text`: `label` optional, `lines` required string array, `align` optional (`left`, `center`, `right`), `bold` optional.
- `keyValue`: `rows` required array of `{ "label": "...", "value": "...", "bold": false }`; label is rendered left and value right.
- `separator`: `char` optional single character, default `-`; rendered as a full-width line.
- `items`: `items` required array of `{ "name": "...", "quantity": "...", "unitPrice": "...", "total": "...", "comment": "..." }`; long names wrap deterministically.
- `totals`: `rows` required array of `{ "label": "...", "value": "...", "bold": false }`.
- `blank`: `lines` optional integer from 1 to 20, default 1.

Receipt layout uses deterministic character widths: 32 characters for `58mm` and 42 characters for `80mm`. These are conservative defaults for common thermal printers; exact physical output still depends on printer font settings.

Validation rules:

- `format: "receipt"` requires `receipt`.
- `receipt` must include `title`, `subtitle`, or at least one block.
- `receipt.blocks[].type` must be one of the supported block types.
- Required fields are validated per block.
- Existing `paperWidth`, `copies`, `cutMode`, and `encodingProfile` validation still applies.

### Low-Level ESC/POS Format

`format: "escpos"` is the advanced escape hatch for clients that need direct control over command ordering and ESC/POS-specific features.

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
    { "type": "qrCode", "value": "https://example.test/receipt/demo-001" },
    { "type": "barcode", "barcodeType": "code128", "value": "ABC123" },
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

Supported QR/barcode/image commands:

```json
{ "type": "qrCode", "value": "https://example.test/receipt/demo-001" }
```

```json
{ "type": "barcode", "barcodeType": "code128", "value": "ABC123" }
```

```json
{
  "type": "image",
  "data": "base64-raster-data",
  "widthBytes": 48,
  "heightDots": 96
}
```

Image commands accept pre-rasterized ESC/POS-compatible image bytes as base64. The API does not accept local file paths.

Response:

```json
{
  "jobId": "optional-client-job-id",
  "status": "printed",
  "printerName": "POS-80",
  "printedAt": "2026-06-08T00:00:00Z"
}
```

When `Agent:Queue:Enabled` is `true`, this endpoint validates and enqueues the job instead of printing synchronously. The response status is `queued`; processing continues in the background with the configured retry policy.

## GET /api/v1/jobs/recent

Returns recent in-memory queued job status records.

Response:

```json
[
  {
    "jobId": "optional-client-job-id",
    "printerName": "POS-80",
    "status": "printed",
    "attempts": 1,
    "errorCode": null,
    "errorMessage": null,
    "createdAt": "2026-06-08T00:00:00Z",
    "updatedAt": "2026-06-08T00:00:01Z"
  }
]
```

Queue statuses:

- `queued`
- `printing`
- `printed`
- `failed`

The MVP queue is in-memory. Jobs are not persisted across agent restarts.

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

Authentication-related responses:

- Missing print token: `401`
- Invalid print token: `403`
- Token required but not configured: `403`

## WebSocket /api/v1/ws

HTTP remains the stable baseline. The WebSocket endpoint is available for clients that want realtime request/response messages over a single local connection.

Health message:

```json
{ "type": "health" }
```

Health response:

```json
{
  "type": "health",
  "status": "ok",
  "name": "open-thermal-print-agent",
  "agentVersion": "0.1.0",
  "apiVersion": "v1",
  "platform": "Windows"
}
```

Print message:

```json
{
  "type": "print",
  "payload": {
    "jobId": "ws-demo-001",
    "printerName": "POS-80",
    "format": "escpos",
    "paperWidth": "80mm",
    "options": {
      "cut": false,
      "copies": 1
    },
    "content": [
      { "type": "text", "value": "WebSocket receipt" }
    ]
  }
}
```

Print response:

```json
{
  "type": "printResult",
  "jobId": "ws-demo-001",
  "status": "printed",
  "printerName": "POS-80",
  "printedAt": "2026-06-08T00:00:00Z"
}
```

Error response:

```json
{
  "type": "error",
  "code": "printer_not_found",
  "message": "Printer was not found: POS-80.",
  "details": []
}
```
