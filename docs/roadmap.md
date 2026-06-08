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
- Versioned local API under `/api/v1` with legacy MVP aliases.
- Windows raw printer adapter using installed printer queues.
- Unit tests for renderer output and payload validation.
- Hardware compatibility matrix and printer report template.
- Windows printer discovery with driver, port, default, queue status, and offline diagnostics.
- Configurable text encoding profiles: `latin1`, `cp850`, and `cp858`.
- Optional local token requirement for print endpoints.
- Windows Service hosting support for persistent deployments.
- Inno Setup installer configuration for Windows.
- QR code, CODE128 barcode, and pre-rasterized image command support.
- WebSocket API for health and print messages.
- Optional in-memory local print queue with retry and recent job status.

## Pending Manual Validation

- Physical printer tests for text output.
- Physical printer tests for accented characters.
- Physical printer tests for paper cut across all supported cut modes.
- Physical printer tests for cash drawer kick.
- Driver-specific raw printing compatibility checks.
- Additional hardware compatibility reports.
- Physical validation of encoding profiles per printer model.
- Tray UI remains pending.
- Persistent queue storage remains pending.

## Production Backlog

- Signed Windows installer and release pipeline.
- Tray application.
- Auto-start.
- Auto-update.
- Pairing flow and local token.
- Advanced WebSocket lifecycle events.
- Persistent local job queue and advanced retry policy.
- Backend polling connector.
- Linux and macOS adapters.
- Broader barcode formats and PNG/JPEG logo preprocessing.
- ESC/POS code page selection commands and broader internationalization.
- Better physical printer status, offline detection, and paper-out detection.
- Exportable logs.
- Expanded hardware compatibility matrix.
