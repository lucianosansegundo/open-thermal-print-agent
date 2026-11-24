# Windows installer

The Windows x64 installer installs for the current Windows user without administrator rights. It includes the .NET 8 runtime and starts a hidden agent at that user's Windows sign-in. The operator must sign in before browser printing is available. Printer queues remain visible in the same user context as the POS browser.

## Preview release and downloads

Proposed release tag: **`v0.4.0-preview.1`**, marked **pre-release / experimental / unsigned**. The URLs below become available only after a maintainer publishes the reviewed release; CI does not publish it automatically.

- Generic installer: [open-thermal-print-agent-0.4.0-preview.1-win-x64.exe](https://github.com/lucianosansegundo/open-thermal-print-agent/releases/download/v0.4.0-preview.1/open-thermal-print-agent-0.4.0-preview.1-win-x64.exe).
- RabbitWarren staging, recommended for staging onboarding: [open-thermal-print-agent-0.4.0-preview.1-rabbitwarren-staging-win-x64.exe](https://github.com/lucianosansegundo/open-thermal-print-agent/releases/download/v0.4.0-preview.1/open-thermal-print-agent-0.4.0-preview.1-rabbitwarren-staging-win-x64.exe).
- Each installer has a sibling download with `.sha256` appended. The release also includes `SHA256SUMS`, `build-manifest.json` and `open-thermal-print-agent-0.4.0-preview.1-build-inputs.zip` (dependency locks and toolchain pins).

Obtain the installer and checksum from the same trusted project release. Compare the hash before running setup:

```powershell
$name = 'open-thermal-print-agent-0.4.0-preview.1-rabbitwarren-staging-win-x64.exe'
$expected = (Get-Content "$name.sha256" -Raw).Split(' ')[0]
if ((Get-FileHash -LiteralPath $name -Algorithm SHA256).Hash -ine $expected) { throw 'Installer checksum mismatch.' }
```

A checksum detects changed bytes; it does not establish publisher identity or replace Authenticode signing. Windows SmartScreen or organizational policy may block this unsigned preview. Do not disable antivirus, firewall, browser checks or certificate validation to run it. Ask the operator or administrator to evaluate the preview instead. Setup adds no firewall rule or trusted certificate.

The current project targets .NET 8. Its [official support ended on November 10, 2026](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core). This preview pins the final published .NET 8 SDK/runtime pair for repeatability but does not turn an unsupported framework into a supported production deployment. A framework upgrade and a signed release remain separate work.

## Install

For RabbitWarren staging, run the staging-named installer. On a **fresh installation**, it permits exactly `https://d2vfhhw5ryvsll.cloudfront.net` and skips the technical site-origin page. No production origin or wildcard is included. Return to the same staging browser and use **Connect printer**. No manually entered origin or copied token is needed for this flow.

The generic installer asks for the exact POS site origin, for example `https://pos.example.com`. Origins contain the scheme, hostname and optional port. Paths, credentials and wildcards are rejected. Generic silent setup requires `/SITEORIGIN` on a fresh installation. Never use wildcard CORS to fix onboarding.

Both variants have the same application identity and installation path. They are alternatives, not side-by-side installations. **An existing configuration always wins**: neither a different preset nor `/SITEORIGIN` switches existing origins or rotates a token. If this Windows account already uses the production site, ask the operator to review the configured exact origins before connecting staging. Do not erase or overwrite `agent.json` just to change presets.

For a fresh RabbitWarren staging installation, no origin argument is needed:

```powershell
.\open-thermal-print-agent-0.4.0-preview.1-rabbitwarren-staging-win-x64.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

When scripting installation, wait for the installer process itself using its process object. PowerShell's `Start-Process -Wait` waits for the child-process tree, which includes the long-running agent launched by setup.

```powershell
$setup = Start-Process .\open-thermal-print-agent-0.4.0-preview.1-rabbitwarren-staging-win-x64.exe -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -PassThru
$setup.WaitForExit()
if ($setup.ExitCode -ne 0) { throw "Agent installation failed with exit code $($setup.ExitCode)." }
```

Use the same process-object wait when scripting the uninstaller. The installer starts the agent and checks its installation identity at `http://127.0.0.1:17890/api/v1/health`. A conflicting process on that port causes a visible installation error or a nonzero silent installer exit. Resolve the conflict and rerun setup. Setup only signals the installed agent belonging to the current user.

After setup, return to RabbitWarren in the browser profile you already use. In POS document settings select **Connect**, approve opening Open Thermal Print Agent when the browser asks, choose your installed thermal printer and send a test print. Setup leaves the current browser available. Each browser profile connects independently. The protocol URI contains a random request identifier. A separate secret stays in browser memory and travels only to the local agent. The installed app checks this Windows user's installation identity and approves the pending request in private local state. The browser polls until approval and receives the existing print token once. Requests expire after five minutes. A new attempt replaces an older pending request. Select Connect again if approval is cancelled or expires.

The legacy Start menu **Connect Open Thermal Print Agent** and private HTML handoff remain available for compatible clients. The setup information is a local HTML file. HTTP health responses contain an installation identifier and version. The permanent token stays in the current user's local configuration and the connected browser. The setup file contains a private one-use connection link that expires after five minutes. The agent stores only the nonce hash and validates the exact configured HTTPS origin, installation identity and expiry before consuming the handoff atomically. Keep the configuration and setup files private.

Automatic connection uses the first configured HTTPS origin. Existing HTTP-only installations retain their configuration and token through upgrades. For these installations, setup and `--connect` prepare a private local helper with the token under **Advanced manual connection** for the POS advanced settings. This fallback creates no browser handoff grant. Silent setup prepares the helper without opening a browser. Configuring an HTTPS origin enables automatic connection. Allow local-network access when the browser asks.

## Files and startup

- Binaries live in `%LOCALAPPDATA%\Programs\Open Thermal Print Agent`.
- Persistent configuration lives in `%LOCALAPPDATA%\OpenThermalPrintAgent\agent.json`.
- Local setup information lives in the same folder as `setup.html`.
- The per-user `HKCU\Software\Classes\open-thermal-print-agent` protocol opens the installed executable with `--approve-connection` and the canonical request URI. Setup checks ownership before registration. Uninstall removes this handler only when its command still points to this installation.
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

## CI build and smoke validation

The `Windows installer preview` workflow runs on disposable GitHub-hosted `windows-2022` runners for pull requests, `main`, `v0.4.0-preview.*` tags and manual dispatch. It uses a read-only token, no release credentials, immutable action commits and no dependency cache shared with untrusted PRs. It does not install the agent on a developer machine or a self-hosted runner.

The workflow installs the exact SDK into an isolated runner directory and downloads the versioned official Inno compiler only after validating SHA-256. Pins live in `installer/windows/toolchain.json`: SDK `8.0.428`, runtime/ASP.NET Core `8.0.34`, Inno Setup `6.5.4`. The SDK/runtime pair was verified together against [Microsoft's release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json); the compiler hash was checked against the [official executable](https://github.com/jrsoftware/issrc/releases/download/is-6_5_4/innosetup-6.5.4.exe) and the [Microsoft WinGet manifest](https://github.com/microsoft/winget-pkgs/blob/master/manifests/j/JRSoftware/InnoSetup/6.5.4/JRSoftware.InnoSetup.installer.yaml).

The build script runs static packaging contracts, restores the entire solution, runs all Release tests and publishes the host as self-contained `win-x64` without trimming or single-file bundling. It checks runtime files, pinned framework versions and the Windows GUI subsystem required for hidden startup. A single publish is packaged into both presets. It does not install the application.

Only the separate `smoke-test.ps1` installs the application, and it refuses to run outside disposable GitHub-hosted Windows runners. It rejects pre-existing binaries, configuration, protocol/startup entries or a busy port before touching state. It verifies installer hashes, rejects fresh generic silent setup without an origin, installs the staging preset without `/SITEORIGIN`, checks installation identity and secure configuration, verifies per-user startup/protocol commands and upgrades using the generic installer with a different origin. The full `agent.json` hash must remain unchanged, preserving origins, token and installation identity. A `finally` block uninstalls the application and checks removal of binaries/startup/protocol plus retention of pairing data. The discarded runner removes that retained data. A real printer and interactive browser are not exercised.

The artifact `windows-installer-0.4.0-preview.1` is uploaded **only after the smoke test passes**, and contains only installers, checksums, manifest and the small build-inputs archive, not the raw self-contained publish tree. Test reports are uploaded separately even on failure. CI artifacts expire after 14 days; a GitHub Release is the durable customer download location.

## Repeatable build and replay

Build from a **clean Windows checkout** with the exact pinned SDK and compiler. Customers need no SDK/runtime installation. A regular local build never runs installation smoke tests:

```powershell
./installer/windows/test.ps1
./installer/windows/build.ps1 -Version 0.4.0-preview.1 -Preset all -Iscc 'C:\tools\Inno Setup 6\ISCC.exe'
```

The build script intentionally removes the ambiguous `-DefaultSiteOrigin` build option. Select `-Preset generic`, `-Preset rabbitwarren-staging` or `-Preset all` (default). Only the reviewed staging preset receives a staging-branded name; arbitrary production origins cannot silently produce that asset. `/SITEORIGIN` remains an installation-time override for a new configuration. Setup prepares the private legacy link using `--connect --no-open`, without switching browsers; the current-browser POS connection remains the recommended flow.

Output is `artifacts/windows-installer/0.4.0-preview.1/`. Existing output is refused, not recursively deleted or reused. The manifest records the exact source commit, toolchain, runtime, presets and installer SHA-256 values. Per-project NuGet lock files, including package content hashes, are generated outside source projects and archived in `*-build-inputs.zip`.

For dependency-identical replay, check out the manifest's source commit, extract the build-inputs archive outside the checkout and pass its `dependencies` directory:

```powershell
./installer/windows/build.ps1 -Version 0.4.0-preview.1 -Preset all -Iscc 'C:\tools\Inno Setup 6\ISCC.exe' -DependencyLockDirectory 'C:\build-inputs\dependencies'
```

Replay uses NuGet `--locked-mode` and fails on missing or inconsistent locks. The initial run resolves dependencies from NuGet; it is not a committed lock baseline. Keep the build-inputs archive with the release. This is a repeatable, traceable procedure, **not a claim of byte-for-byte reproducibility**: runner image updates, compiler/ZIP timestamps and compression metadata can change hashes. The published hashes identify the exact released bytes.

## Maintainer publication

The maintainer, not CI or this script, reviews and publishes the PR and release. Before publishing:

1. Review host security and the pairing implementation separately; passing installer smoke is not a security audit.
2. Merge the reviewed PR, create `v0.4.0-preview.1` on that exact commit and let the tag workflow finish. Do not publish a PR merge-ref artifact as a final release.
3. Download the successful tag run's `windows-installer-0.4.0-preview.1` artifact and confirm `build-manifest.json.sourceCommit` equals the tag's commit, both installer hashes match and the archived dependency-lock hash matches the manifest.
4. Create a GitHub **pre-release**, not latest/stable, and attach both `.exe` files, both `.exe.sha256` files, `SHA256SUMS`, `build-manifest.json` and `open-thermal-print-agent-0.4.0-preview.1-build-inputs.zip`. Use `installer/windows/release-notes.md` as a reviewed starting point.
5. Verify the exact versioned URLs above anonymously. Point staging onboarding at the staging-named asset, never a mutable `latest/download` URL or the generic installer. The `.sha256` URL is the installer URL plus `.sha256`.

Neither the workflow nor build script commits, pushes, creates tags, opens PRs or creates/updates GitHub Releases. Do not claim hardware/browser validation until those checks have actually been performed.

## Limitations

The installer is unsigned. Automatic updating and a tray application are outside this release. Startup occurs at user sign-in, and local printing is unavailable while the user is signed out. Only one user-session agent can bind the default port at a time on a shared Windows machine. The agent remains experimental pending printer-specific validation and deployment monitoring.
