# Windows installer

The Windows x64 installer installs for the current Windows user without administrator rights. It includes the .NET 8 runtime and starts a hidden agent at that user's Windows sign-in. The operator must sign in before browser printing is available. Printer queues remain visible in the same user context as the POS browser.

## Install

Run `open-thermal-print-agent-0.3.0-win-x64.exe` and enter your POS site's exact origin, for example `https://pos.example.com`. Origins contain the scheme, hostname and optional port. Paths and wildcards are rejected.

For RabbitWarren staging, a silent installation is:

```powershell
.\open-thermal-print-agent-0.3.0-win-x64.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SITEORIGIN=https://d2vfhhw5ryvsll.cloudfront.net
```

When scripting installation, wait for the installer process itself using its process object. PowerShell's `Start-Process -Wait` waits for the child-process tree, which includes the long-running agent launched by setup.

```powershell
$setup = Start-Process .\open-thermal-print-agent-0.3.0-win-x64.exe -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SITEORIGIN=https://d2vfhhw5ryvsll.cloudfront.net' -PassThru
$setup.WaitForExit()
if ($setup.ExitCode -ne 0) { throw "Agent installation failed with exit code $($setup.ExitCode)." }
```

Use the same process-object wait when scripting the uninstaller. The installer starts the agent and checks its installation identity at `http://127.0.0.1:17890/api/v1/health`. A conflicting process on that port causes a visible installation error or a nonzero silent installer exit. Resolve the conflict and rerun setup. Setup only signals the installed agent belonging to the current user.

Interactive setup connects your POS in the default browser automatically. Select an installed Windows thermal printer in POS settings and print a test receipt. RabbitWarren stores the local print token in that browser profile. Each Windows user receives an independent token and configuration. To reconnect or use another browser profile, open **Connect Open Thermal Print Agent** from the Start menu, then open **Open Thermal Print Agent Setup** and follow its private connection link in the intended browser.

The setup information is a local HTML file. HTTP health responses contain an installation identifier and version. The permanent token stays in the current user's local configuration and the connected browser. The setup file contains a private one-use connection link that expires after five minutes. The agent stores only the nonce hash and validates the exact configured HTTPS origin, installation identity and expiry before consuming the handoff atomically. Keep the configuration and setup files private.

## Files and startup

- Binaries live in `%LOCALAPPDATA%\Programs\Open Thermal Print Agent`.
- Persistent configuration lives in `%LOCALAPPDATA%\OpenThermalPrintAgent\agent.json`.
- Local setup information lives in the same folder as `setup.html`.
- Lifecycle and warning/error metadata logs live in the `logs` subfolder. Logs omit request URLs, token values and receipt content. Startup failures are recorded in `startup-errors.log` with their exception type.
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\OpenThermalPrintAgent` contains the quoted executable path plus `--installed`.

To change allowed sites, edit `Agent.AllowedOrigins` in `agent.json`. Keep exact HTTP(S) origins, token security enabled, port 17890 and queue mode disabled. Stop and restart the agent after changes. The installed agent rejects insecure configuration. The request queue stays disabled to avoid automatically retrying a print with an ambiguous physical outcome.

```powershell
$agent = "$env:LOCALAPPDATA\Programs\Open Thermal Print Agent\OpenThermalPrintAgent.Host.exe"
Start-Process $agent -ArgumentList '--stop' -Wait -WindowStyle Hidden
Start-Process $agent -ArgumentList '--installed' -WindowStyle Hidden
Start-Process $agent -ArgumentList '--check' -Wait -WindowStyle Hidden -PassThru
```

The last command exits with code 0 when the running agent matches the local installation. A named per-user instance lock prevents duplicate agents. Startup may be disabled or delayed by Windows startup-app settings, which the operator can review in Task Manager.

## Upgrade and uninstall

Run a newer installer under the same Windows account. Setup gracefully stops the installed agent before replacing its binaries and verifies health after restart. Existing configuration, site origins and token are retained. `/SITEORIGIN` seeds only the first configuration. An invalid existing configuration is preserved and reported for correction.

Uninstall through Windows Installed Apps. The uninstaller gracefully stops this user's installed agent and removes its startup entry, shortcuts and binaries. Configuration, token, setup file and logs are retained for reinstalls. To erase pairing information, delete `%LOCALAPPDATA%\OpenThermalPrintAgent` after uninstall. Other Windows users maintain separate installations. A legacy administrator-installed Windows service must be removed separately using the service documentation before switching this user's POS to this installer.

## Reproducible build procedure

Build on Windows with the .NET 8 SDK and Inno Setup 6 installed. Customers receive the self-contained installer and need no SDK or runtime installation.

```powershell
./installer/windows/build.ps1 -DefaultSiteOrigin https://app.rabbitwarren.com.ar
```

Omit `-DefaultSiteOrigin` to build a generic installer with an empty site field. `/SITEORIGIN` overrides the preset for a new installation. Existing configuration and tokens are preserved during upgrades. Silent installation prepares the private link without opening a browser. To prepare a fresh link administratively, run the installed executable with `--connect --no-open`. Normal `--connect` also opens the default browser. Both commands verify the running installation identity first and print no credentials.

The script performs a Release self-contained win-x64 publish, compiles Inno Setup and prints the installer's SHA-256. Pass `-Iscc` to select a different Inno Setup compiler path. Output is `artifacts/installer/open-thermal-print-agent-0.3.0-win-x64.exe`. Build inputs include the repository commit and restored NuGet runtime/packages. The procedure is repeatable but installer timestamps and compiler compression metadata may produce different binary hashes.

## Limitations

The installer is unsigned. Automatic updating and a tray application are outside this release. Startup occurs at user sign-in, and local printing is unavailable while the user is signed out. Only one user-session agent can bind the default port at a time on a shared Windows machine. The agent remains experimental pending printer-specific validation and deployment monitoring.
