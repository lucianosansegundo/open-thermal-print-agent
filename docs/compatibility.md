# Compatibility

ESC/POS is a de facto standard. Printers often implement slightly different command sets, code pages, cut behavior, and drawer behavior.

## Tested Matrix

| Printer model | Connection | Windows driver | Paper width | Text | Accented characters | Cut | Drawer | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| XP-80 | Pending | Windows installed printer queue named `XP-80` | 80mm | Passed | Pending | Pending physical confirmation for `cutMode=full` | Not tested | A previous physical test printed successfully with `cut=false`. The agent sent `/print/test` with `cutMode=full` to the queue successfully on 2026-06-08. |

## Reporting Compatibility

When reporting compatibility, include:

- Printer brand and model.
- Connection type: USB, serial, network, Bluetooth.
- Windows version.
- Driver name and version.
- Whether text, accented characters, cut, and drawer kick work.
- Any required printer settings.

## XP-80 Notes

The initial XP-80 hardware test printed successfully with `cut=false`.

Current cut validation:

- `cutMode=full`: sent successfully to the Windows printer queue named `XP-80`.
- Physical cut confirmation: pending.
- Modes not yet physically tested: `partial`, `feedAndFull`, `feedAndPartial`.

If `full` does not cut physically, test `partial`, then `feedAndFull`, then `feedAndPartial`, and document the first working mode.
