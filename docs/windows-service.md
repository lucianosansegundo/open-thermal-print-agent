# Windows service mode

The supported customer installer uses per-user sign-in startup. See [Windows installer](windows-installer.md).

The host retains legacy Windows Service integration for deployments managed by an administrator. A service runs under its configured service account. Printer visibility and access must be verified for that account. Remove an existing service before migrating the same port to a per-user installation.

Publish a separate service deployment, preserving the console/service host build:

```powershell
dotnet publish src/OpenThermalPrintAgent.Host -c Release -r win-x64 --self-contained true -p:OutputType=Exe -o artifacts/service
```

Configure explicit allowed origins and enable print token security in that deployment's `appsettings.json`. The `--installed` argument is reserved for per-user deployments. Service configuration remains alongside the service executable. Use a dedicated service account and validate access to its Windows print queues.

Run PowerShell as Administrator to register and start the service:

```powershell
New-Service -Name OpenThermalPrintAgent -DisplayName 'Open Thermal Print Agent' -BinaryPathName '"C:\AgentService\OpenThermalPrintAgent.Host.exe"' -StartupType Automatic
Start-Service OpenThermalPrintAgent
Invoke-RestMethod http://127.0.0.1:17890/api/v1/health
```

To remove it, run PowerShell as Administrator:

```powershell
Stop-Service OpenThermalPrintAgent
sc.exe delete OpenThermalPrintAgent
```

Administrators manage service upgrades, account permissions and logging providers themselves. The per-user installer manages its own startup registry value and leaves legacy Windows services under administrator control.
