# 0.4.0-preview.1: experimental Windows installer

**Pre-release. Experimental. Unsigned. Controlled evaluation only.**

Windows x64, per-user setup without administrator rights, self-contained runtime,
hidden sign-in startup and current-browser POS pairing. No automatic updater.
Printer/driver/browser compatibility must be validated on the target workstation.

## Choose the installer

- `open-thermal-print-agent-0.4.0-preview.1-rabbitwarren-staging-win-x64.exe`: fresh RabbitWarren staging onboarding, preset only to `https://d2vfhhw5ryvsll.cloudfront.net`, with no technical origin field.
- `open-thermal-print-agent-0.4.0-preview.1-win-x64.exe`: generic integrations; enter the exact POS site origin. Fresh silent installation requires `/SITEORIGIN`.

Both variants preserve existing origins, installation identity and token. Changing
installers does not switch an existing configuration to staging. They share one
installation identity and cannot be installed side by side for the same user.

Verify the sibling `.exe.sha256` or `SHA256SUMS` before running setup. SHA-256 is
an integrity check, not a publisher signature. Do not bypass Windows/organization
security, firewall checks or certificate validation to use this unsigned preview.
No firewall exception or trusted certificate is installed. Wildcard CORS is not
required or recommended.

## Runtime and validation limitations

This preview bundles .NET 8.0.34, built with SDK 8.0.428. Microsoft ended .NET 8
support on November 10, 2026; this is not a supported long-term production runtime
deployment. A supported framework upgrade and code signing are separate work.

The CI workflow is designed to run all Release tests and disposable Windows
install/upgrade/uninstall smoke checks before uploading assets. The publishing
maintainer must verify that the tag run passed before publishing this text.
No claim of physical printer or interactive browser validation is made here.

`build-manifest.json` records the source commit, toolchain and installer hashes.
The `*-build-inputs.zip` asset contains exact dependency locks for replay. Builds
are traceable/repeatable, not guaranteed to have byte-identical installer hashes.

See `docs/windows-installer.md` in the tagged source for setup, upgrade,
configuration retention, manual cleanup and maintainer verification instructions.
