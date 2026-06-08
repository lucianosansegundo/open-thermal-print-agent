# Windows Setup

## Install a Printer

1. Install the printer driver provided by the manufacturer, or use a compatible generic text/raw printer queue when appropriate.
2. Open Windows printer settings.
3. Confirm the printer appears in the installed printer list.
4. Print a Windows test page if the driver supports it.
5. Use `GET /printers` to confirm the local agent can see the printer name.

## Test Raw Printing

Raw ESC/POS printing depends on the Windows queue and driver. Some drivers pass bytes directly to the device. Other drivers transform data and may break ESC/POS commands.

Start with `POST /print/test`. If text prints but cut or drawer commands do not work, check the printer manual and driver mode.

## Common Issues

- Printer name mismatch: use the exact name returned by `GET /printers`.
- Driver transforms bytes: try a raw/generic queue or vendor ESC/POS driver.
- Accented characters print incorrectly: code page mismatch.
- Cutter does not trigger: model may require a different cut command or may not include an auto cutter.
- Drawer does not open: verify the drawer is connected to the printer and the printer supports drawer kick.
