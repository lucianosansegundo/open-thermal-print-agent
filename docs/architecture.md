# Architecture

Open Thermal Print Agent is a generic local print bridge between browser-based applications and thermal printers.

```text
web app or POS frontend
  -> HTTP request to 127.0.0.1
  -> local agent
  -> neutral print job validation
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

`OpenThermalPrintAgent.EscPos` converts neutral print commands into ESC/POS byte sequences. It supports the MVP command set: text, alignment, bold, feed, cut, and cash drawer kick.

### Windows

`OpenThermalPrintAgent.Windows` lists installed Windows printers and sends raw bytes to a selected printer queue.

## Generic Design

The agent intentionally uses generic terms such as print job, printer, receipt, ticket, drawer, and local agent. It does not model sales, invoices, fiscal documents, or any specific POS backend. Integrations should translate their own domain data into the generic print job API.
