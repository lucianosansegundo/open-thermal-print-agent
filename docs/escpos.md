# ESC/POS

The renderer supports a predictable ESC/POS subset. The public API can receive semantic `receipt` jobs or low-level `escpos` jobs; both are rendered to ESC/POS bytes internally.

## Supported Commands

- Initialize printer: `ESC @`
- Text with line ending: .NET Unicode input encoded to Latin-1 compatible bytes where possible.
- Alignment: left, center, right.
- Bold on/off.
- Feed N lines.
- Configurable paper cut modes.
- Cash drawer kick using the common `ESC p` command.
- QR code command generation.
- CODE128 barcode command generation.
- Raster image command generation from pre-rasterized base64 data.

## Paper Width

Supported paper widths:

- `58mm`
- `80mm`

The renderer validates this value.

For `format: "receipt"`, semantic layout uses conservative fixed-width defaults:

- `58mm`: 32 characters per line.
- `80mm`: 42 characters per line.

For `format: "escpos"`, clients control layout directly through text and ESC/POS commands.

## Cut Modes

The renderer supports these cut modes:

| Mode | ESC/POS bytes | Notes |
| --- | --- | --- |
| `none` | none | Does not emit a cut command. |
| `full` | `1D 56 00` | Full cut using `GS V 0`. |
| `partial` | `1D 56 01` | Partial cut using `GS V 1`. This is the legacy command used by `{ "type": "cut" }`. |
| `feedAndFull` | `1D 56 41 03` | Feed 3 lines and full cut using `GS V A n`. |
| `feedAndPartial` | `1D 56 42 03` | Feed 3 lines and partial cut using `GS V B n`. |

Compatibility rules:

- `cutMode` overrides the legacy `cut` boolean.
- `cut=true` without `cutMode` maps to `full`.
- `cut=false` without `cutMode` maps to `none`.
- `{ "type": "cut" }` remains backward compatible and maps to `partial`.

## Encoding

Input strings are .NET Unicode strings. The renderer encodes text according to the job encoding profile.

Supported profiles:

| Profile | .NET encoding | Notes |
| --- | --- | --- |
| `latin1` | Latin-1 | Default. Direct byte mapping for common Western European characters. |
| `cp850` | IBM code page 850 | Common ESC/POS Western European code page. |
| `cp858` | IBM code page 858 | Similar to CP850 and includes the euro symbol. |

Printer firmware and configured code pages may differ. The MVP encodes bytes for the selected profile but does not yet emit ESC/POS code page selection commands. If a printer is configured to a different firmware code page, characters may still render incorrectly.

## Limitations

- Semantic receipt layout is deterministic but intentionally simple.
- No PNG/JPEG decoding. Image/logo input must be pre-rasterized bytes.
- No printer status reads.
- No paper out/offline detection.
- No full-width layout engine.
- No automatic code page negotiation.
- No ESC/POS code page selection command emission yet.

## QR Codes, Barcodes, And Raster Images

QR codes use the common ESC/POS `GS ( k` command sequence:

- Select model 2.
- Set module size. The default is 4; QR commands may override it with `size`.
- Set error correction level L.
- Store data.
- Print symbol.

Semantic receipt jobs can use a `qr` block. Low-level ESC/POS jobs can use a `qrCode` command. Both paths reuse the same QR command generation.

Barcodes currently support CODE128 using `GS k 73 n data`. If the value does not start with a CODE128 subset marker, the renderer prefixes `{B`.

Raster images use `GS v 0` and require:

- `data`: base64-encoded raster bytes.
- `widthBytes`: image width in bytes.
- `heightDots`: image height in dots.

The API intentionally does not accept local file paths for logos or images.
