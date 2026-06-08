# Windows Installer

The first installer configuration uses Inno Setup. It packages the published Host application and can optionally install/start the Windows Service.

## Requirements

- Windows.
- .NET 8 SDK.
- Inno Setup 6.
- Administrator rights when installing the service.

## Build Publish Output

```powershell
dotnet publish src/OpenThermalPrintAgent.Host `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o artifacts/publish/OpenThermalPrintAgent.Host
```

## Build Installer

From the repository root:

```powershell
iscc installer/windows/open-thermal-print-agent.iss
```

Expected output:

```text
artifacts/installer/open-thermal-print-agent-0.1.0-win-x64.exe
```

If `iscc` is not in `PATH`, run the command from the Inno Setup installation directory or add it to `PATH`.

## Install

Run the generated installer as Administrator if you want to install the Windows Service.

Installer tasks:

- Install Windows Service.
- Start service after installation.

## Uninstall

The uninstaller attempts to stop and delete the Windows Service before removing installed files.

Local configuration files may be retained by future installer versions if they are moved to a dedicated app data directory. The current MVP installer installs files under Program Files.

## Limitations

- No auto-update.
- No code signing.
- No per-user tray application.
- No CI release pipeline yet.
- The installer expects publish output to exist before running `iscc`.
