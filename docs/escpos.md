# ESC/POS

The MVP renderer supports a small, predictable ESC/POS subset.

## Supported Commands

- Initialize printer: `ESC @`
- Text with line ending: .NET Unicode input encoded to Latin-1 compatible bytes where possible.
- Alignment: left, center, right.
- Bold on/off.
- Feed N lines.
- Configurable paper cut modes.
- Cash drawer kick using the common `ESC p` command.

## Paper Width

Supported paper widths:

- `58mm`
- `80mm`

The renderer validates this value. The MVP does not perform full layout or automatic wrapping based on width.

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

- No QR codes.
- No barcodes.
- No images or logos.
- No printer status reads.
- No paper out/offline detection.
- No full-width layout engine.
- No automatic code page negotiation.
- No ESC/POS code page selection command emission yet.
