# ESC/POS

The MVP renderer supports a small, predictable ESC/POS subset.

## Supported Commands

- Initialize printer: `ESC @`
- Text with line ending: UTF-8 input encoded to Windows-1252 where possible.
- Alignment: left, center, right.
- Bold on/off.
- Feed N lines.
- Partial paper cut.
- Cash drawer kick using the common `ESC p` command.

## Paper Width

Supported paper widths:

- `58mm`
- `80mm`

The renderer validates this value. The MVP does not perform full layout or automatic wrapping based on width.

## Encoding

Input strings are .NET Unicode strings. The MVP encodes text to Windows-1252 to support common Latin characters such as accented vowels and `ñ` on many printers.

Printer firmware and configured code pages may differ. Future versions should support explicit ESC/POS code page selection and better internationalization.

## Limitations

- No QR codes.
- No barcodes.
- No images or logos.
- No printer status reads.
- No paper out/offline detection.
- No full-width layout engine.
- No automatic code page negotiation.
