# Roadmap

## MVP Scope

- Windows-first local HTTP agent.
- Bind to `127.0.0.1`.
- List installed printers.
- Print a test receipt.
- Print simple generic ESC/POS jobs.
- Support 58mm and 80mm paper width values.
- Support text, alignment, bold, feed, paper cut, and cash drawer kick.
- Support configurable ESC/POS cut modes.
- Unit tests for ESC/POS byte generation and validation.

## Implemented in Initial MVP

- .NET 8 solution with Core, ESC/POS, Windows, Host, and test projects.
- MIT license and English technical documentation.
- Local Minimal API on `127.0.0.1:17890`.
- Configured CORS with explicit local origins.
- ESC/POS renderer for text, alignment, bold, feed, cut, and drawer kick.
- ESC/POS cut modes: `none`, `full`, `partial`, `feedAndFull`, and `feedAndPartial`.
- Windows raw printer adapter using installed printer queues.
- Unit tests for renderer output and payload validation.

## Pending Manual Validation

- Physical printer tests for text output.
- Physical printer tests for accented characters.
- Physical printer tests for paper cut across all supported cut modes.
- Physical printer tests for cash drawer kick.
- Driver-specific raw printing compatibility checks.

## Production Backlog

- Windows installer.
- Tray application or Windows service.
- Auto-start.
- Auto-update.
- Pairing flow and local token.
- WebSocket API.
- Local job queue and retry policy.
- Backend polling connector.
- Linux and macOS adapters.
- QR codes, barcodes, and logos.
- Better code pages and internationalization.
- Printer status, offline detection, and paper-out detection.
- Exportable logs.
- Hardware compatibility matrix.
