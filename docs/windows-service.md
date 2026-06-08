# Windows Service Mode

Open Thermal Print Agent can run as a console application for development or as a Windows Service for persistent local deployments.

The first production-friendly persistent mode is Windows Service support. A tray application is still deferred because it requires a separate UI surface and installer flow.

## Publish

```powershell
dotnet publish src/OpenThermalPrintAgent.Host `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o artifacts/publish/OpenThermalPrintAgent.Host
```

## Install Service

Run PowerShell as Administrator:

```powershell
New-Service `
  -Name "OpenThermalPrintAgent" `
  -DisplayName "Open Thermal Print Agent" `
  -Description "Local HTTP agent for thermal receipt printing." `
  -BinaryPathName "C:\Path\To\OpenThermalPrintAgent.Host.exe" `
  -StartupType Automatic
```

Start it:

```powershell
Start-Service OpenThermalPrintAgent
```

Check status:

```powershell
Get-Service OpenThermalPrintAgent
Invoke-RestMethod http://127.0.0.1:17890/api/v1/health
```

## Stop And Remove

Run PowerShell as Administrator:

```powershell
Stop-Service OpenThermalPrintAgent
sc.exe delete OpenThermalPrintAgent
```

## Configuration

Keep configuration next to the published executable or provide configuration through environment variables. The service still binds to `127.0.0.1` by default.

For production-like use, enable print token security before running as a service.

## Limitations

- No tray UI yet.
- No built-in installer yet.
- Logs use the configured ASP.NET Core logging providers.
- Operators must manage service install/uninstall manually until the installer issue is implemented.
