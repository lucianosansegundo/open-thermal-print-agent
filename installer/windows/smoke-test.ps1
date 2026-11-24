[CmdletBinding()]
param(
    [ValidatePattern('^0\.4\.0-preview\.[1-9][0-9]*$')]
    [string]$Version = '0.4.0-preview.2'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:OS -ne 'Windows_NT') {
    throw 'Installer smoke tests run ONLY on disposable GitHub-hosted Windows runners, never a developer workstation or self-hosted runner.'
}

$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$assets = Join-Path $repo "artifacts/windows-installer/$Version"
$agentDirectory = Join-Path $env:LOCALAPPDATA 'Programs/Open Thermal Print Agent'
$dataDirectory = Join-Path $env:LOCALAPPDATA 'OpenThermalPrintAgent'
$configPath = Join-Path $dataDirectory 'agent.json'
$agent = Join-Path $agentDirectory 'OpenThermalPrintAgent.Host.exe'
$uninstaller = Join-Path $agentDirectory 'unins000.exe'
$startupKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$protocolKey = 'HKCU:\Software\Classes\open-thermal-print-agent'
$toolchain = Get-Content (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
$generic = Join-Path $assets "open-thermal-print-agent-$Version-win-x64.exe"
$staging = Join-Path $assets "open-thermal-print-agent-$Version-rabbitwarren-staging-win-x64.exe"

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Invoke-Setup([string]$Path, [string]$Extra = '') {
    Write-Host "Running $(Split-Path $Path -Leaf) $Extra"
    # Do not use Start-Process -Wait: setup launches a long-running child agent.
    $process = Start-Process -FilePath $Path -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART $Extra" -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(120000)) { throw "Setup/uninstall timed out: $(Split-Path $Path -Leaf)." }
    return $process.ExitCode
}

function Assert-Running {
    $health = Invoke-RestMethod 'http://127.0.0.1:17890/api/v1/health' -TimeoutSec 5
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    Assert-True ($health.installationId -ceq $config.InstallationId) 'Health must identify this installation.'
    $check = Start-Process -FilePath $agent -ArgumentList '--check' -WindowStyle Hidden -PassThru
    Assert-True ($check.WaitForExit(45000)) 'Installed health check timed out.'
    Assert-True ($check.ExitCode -eq 0) 'Installed health check failed.'
}

foreach ($path in @($agentDirectory, $dataDirectory, $protocolKey)) {
    Assert-True (-not (Test-Path -LiteralPath $path)) 'Runner already contains an installation; refusing to touch existing state.'
}
Assert-True ($null -eq (Get-ItemProperty -LiteralPath $startupKey -Name OpenThermalPrintAgent -ErrorAction SilentlyContinue)) 'Runner already contains an agent startup entry.'
Assert-True (@(Get-NetTCPConnection -LocalPort 17890 -State Listen -ErrorAction SilentlyContinue).Count -eq 0) 'Runner port 17890 is already in use.'
foreach ($path in @($generic, $staging)) {
    $expected = (Get-Content -LiteralPath "$path.sha256" -Raw).Split(' ')[0]
    Assert-True ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ieq $expected) 'Installer checksum mismatch before smoke test.'
}

$configHash = $null
try {
    Assert-True ((Invoke-Setup $generic) -ne 0) 'Fresh generic silent setup must reject a missing exact site origin.'
    Assert-True (-not (Test-Path -LiteralPath $configPath)) 'Rejected generic setup must not create configuration.'
    Assert-True ((Invoke-Setup $staging) -eq 0) 'Staging preset silent setup failed.'
    Assert-Running
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    Assert-True ($config.Agent.AllowedOrigins.Count -eq 1 -and $config.Agent.AllowedOrigins[0] -ceq $toolchain.rabbitWarrenStagingOrigin) 'Fresh staging setup must allow only its exact staging origin.'
    Assert-True ($config.Agent.Security.RequireToken -eq $true -and $config.Agent.Security.Token.Length -ge 32) 'Installed token security must be enabled.'
    Assert-True ($config.Agent.Queue.Enabled -eq $false) 'Installed queue retries must remain disabled.'
    $configHash = (Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash
    $startup = (Get-ItemProperty -LiteralPath $startupKey -Name OpenThermalPrintAgent).OpenThermalPrintAgent
    Assert-True ($startup -ceq ('"{0}" --installed' -f $agent)) 'Per-user sign-in startup command must be quoted and use installed mode.'
    $protocol = (Get-Item -LiteralPath "$protocolKey\shell\open\command").GetValue('')
    Assert-True ($protocol -ceq ('"{0}" --approve-connection "%1"' -f $agent)) 'Per-user browser approval handler must use the installed executable.'

    Assert-True ((Invoke-Setup $generic '/SITEORIGIN=https://pos.example.com') -eq 0) 'Generic upgrade failed.'
    Assert-Running
    Assert-True ((Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash -ceq $configHash) 'Upgrade must preserve configuration, installation identity and token byte-for-byte.'
    Write-Host 'PASS: silent staging preset, own health, secure config, startup, protocol and generic upgrade preservation. No printer required.'
}
finally {
    if (Test-Path -LiteralPath $uninstaller -PathType Leaf) {
        Assert-True ((Invoke-Setup $uninstaller) -eq 0) 'Uninstall failed during smoke cleanup.'
        Assert-True (-not (Test-Path -LiteralPath $agent)) 'Uninstall left the agent executable.'
        Assert-True (-not (Test-Path -LiteralPath $protocolKey)) 'Uninstall left the owned protocol registration.'
        Assert-True ($null -eq (Get-ItemProperty -LiteralPath $startupKey -Name OpenThermalPrintAgent -ErrorAction SilentlyContinue)) 'Uninstall left the owned startup entry.'
        if ($configHash) {
            Assert-True ((Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash -ceq $configHash) 'Uninstall must retain pairing data unchanged.'
        }
        Write-Host 'PASS: uninstalled binaries/startup/protocol; retained pairing data disappears when the disposable runner is destroyed.'
    }
}
