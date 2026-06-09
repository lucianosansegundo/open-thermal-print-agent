# Architecture

Open Thermal Print Agent is a generic local print bridge between browser-based applications and thermal printers.

```text
web app or POS frontend
  -> HTTP request to 127.0.0.1
  -> local agent
  -> neutral print job validation
  -> semantic receipt layout or low-level ESC/POS command handling
  -> ESC/POS renderer
  -> Windows raw printer adapter
  -> installed printer queue
  -> thermal printer
```

## Components

### Host

`OpenThermalPrintAgent.Host` is an ASP.NET Core Minimal API application. It owns HTTP endpoints, local binding, CORS, request size limits, configuration, and logging.

### Core

`OpenThermalPrintAgent.Core` contains neutral print job models, validation, result models, printer abstractions, and shared errors. It does not know about ESC/POS or Windows APIs.

### ESC/POS

`OpenThermalPrintAgent.EscPos` converts neutral print commands into ESC/POS byte sequences. It supports text, alignment, bold, feed, cut, cash drawer kick, QR code, CODE128 barcode, and pre-rasterized image commands.

For `format: "receipt"`, the renderer first converts semantic receipt blocks into deterministic text/layout commands, then emits ESC/POS bytes. For `format: "escpos"`, it treats the submitted content commands as the low-level command list directly.

### Windows

`OpenThermalPrintAgent.Windows` lists installed Windows printers and sends raw bytes to a selected printer queue.

## Generic Design

The agent intentionally uses generic terms such as print job, printer, receipt, ticket, drawer, and local agent. It does not model sales, invoices, fiscal documents, country-specific compliance rules, or any specific POS backend.

Integrations should translate their own domain data into either:

- `format: "receipt"` for semantic receipt blocks that the agent lays out.
- `format: "escpos"` for advanced low-level ESC/POS command control.
