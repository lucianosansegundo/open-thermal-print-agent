# Compatibility

ESC/POS is a de facto standard. Printers often implement slightly different command sets, code pages, cut behavior, drawer behavior, and image/QR/barcode support.

Raw spool success means the agent wrote bytes to the operating system printer queue. It does not guarantee that paper moved, text rendered correctly, or the cutter/drawer physically triggered.

## Status Values

- `Passed`: physically validated.
- `Queue passed`: bytes were accepted by the OS printer queue, but physical behavior was not confirmed.
- `Pending`: not tested yet.
- `Failed`: tested and did not work.
- `Not supported`: printer or driver does not support the feature.

## Tested Matrix

| Printer model | Vendor | Connection | OS | Driver / queue | Paper width | Text | Accented characters | Cut | Drawer | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| XP-80 | Pending vendor confirmation | USB (`USB002`) | Windows 10.0.26200 | Driver `XP-80`, queue `XP-80`, default printer | 80mm | Passed | Pending | Queue passed for `cutMode=full`; physical cut confirmation pending | Pending | Previous physical test printed successfully with `cut=false`. `/print/test` with `cutMode=full` was accepted by the Windows queue on 2026-06-08. |

## Feature Checklist

Use this checklist when validating a printer:

- Installed printer appears in `GET /api/v1/printers`.
- Plain text prints.
- Accented characters print correctly: `á é í ó ú ñ`.
- Currency symbols print correctly for the target locale.
- `cutMode=full` cuts correctly.
- `cutMode=partial` cuts correctly.
- `cutMode=feedAndFull` cuts correctly.
- `cutMode=feedAndPartial` cuts correctly.
- Drawer kick works when a drawer is connected.
- Driver does not transform or filter ESC/POS bytes.

## Report Template

```text
Printer model:
Vendor/brand:
Connection type: USB / LAN / serial / Bluetooth / other
Operating system:
Driver name and version:
Windows queue name:
Paper width: 58mm / 80mm / other

Agent version:
API version:

Plain text: Passed / Failed / Pending
Accented characters: Passed / Failed / Pending
Currency symbols: Passed / Failed / Pending
Cut mode tested: none / full / partial / feedAndFull / feedAndPartial
Cut result: Passed / Failed / Pending
Drawer kick: Passed / Failed / Pending / Not supported

Notes:
Workarounds:
```

## XP-80 Notes

Known local queue details:

- Queue name: `XP-80`
- Driver name: `XP-80`
- Port: `USB002`
- Default printer: yes
- Windows queue offline flag: false

Validation so far:

- Physical print with `cut=false`: passed.
- `/print/test` with `cutMode=full`: queue passed.
- Physical cut confirmation: pending.
- Accented characters: pending.
- Drawer kick: pending.

If `full` does not cut physically, test `partial`, then `feedAndFull`, then `feedAndPartial`, and document the first working mode.
