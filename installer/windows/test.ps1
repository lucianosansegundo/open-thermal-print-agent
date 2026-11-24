[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$checks = 0
function Assert-Contract([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Installer contract failed: $Message" }
    $script:checks++
}

foreach ($script in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1') {
    $parseErrors = $null
    $tokens = $null
    $null = [Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$parseErrors)
    Assert-Contract ($parseErrors.Count -eq 0) "PowerShell syntax: $($script.Name): $parseErrors"
}

$config = Get-Content (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
$iss = (Get-Content (Join-Path $PSScriptRoot 'open-thermal-print-agent.iss') -Raw).Replace("`r`n", "`n")
$build = Get-Content (Join-Path $PSScriptRoot 'build.ps1') -Raw
$smoke = Get-Content (Join-Path $PSScriptRoot 'smoke-test.ps1') -Raw
$workflow = (Get-Content (Join-Path $repo '.github/workflows/windows-installer.yml') -Raw).Replace("`r`n", "`n")
$warning = Get-Content (Join-Path $PSScriptRoot 'preview-warning.txt') -Raw
[xml]$props = Get-Content (Join-Path $PSScriptRoot 'release.props') -Raw
[xml]$projectProps = Get-Content (Join-Path $repo 'Directory.Build.props') -Raw

Assert-Contract ($config.releaseVersion -cmatch '^0\.4\.0-preview\.[1-9][0-9]*$') 'Explicit experimental preview version.'
Assert-Contract ($config.releaseVersion.Split('-')[0] -ceq $projectProps.Project.PropertyGroup.Version) 'Installer base version matches the agent project.'
foreach ($name in @('build.ps1', 'smoke-test.ps1', 'open-thermal-print-agent.iss')) {
    Assert-Contract ((Get-Content (Join-Path $PSScriptRoot $name) -Raw).Contains($config.releaseVersion)) "Version default synchronized: $name."
}
Assert-Contract ($config.dotnetSdkVersion -cmatch '^8\.0\.[0-9]+$' -and $config.runtimeFrameworkVersion -cmatch '^8\.0\.[0-9]+$') 'Exact SDK and runtime pins.'
Assert-Contract ($config.innoSetupUrl -ceq "https://github.com/jrsoftware/issrc/releases/download/is-$($config.innoSetupVersion.Replace('.', '_'))/innosetup-$($config.innoSetupVersion).exe") 'Versioned official Inno Setup download.'
Assert-Contract ($config.innoSetupSha256 -cmatch '^[a-f0-9]{64}$') 'Inno Setup SHA-256 pin.'
$origin = [uri]$config.rabbitWarrenStagingOrigin
Assert-Contract ($origin.Scheme -ceq 'https' -and $origin.AbsolutePath -ceq '/' -and $origin.UserInfo -eq '' -and $origin.Query -eq '' -and $origin.Fragment -eq '' -and $config.rabbitWarrenStagingOrigin -ceq $origin.GetLeftPart([UriPartial]::Authority)) 'Canonical exact HTTPS staging origin.'
Assert-Contract ($config.rabbitWarrenStagingOrigin -ceq 'https://d2vfhhw5ryvsll.cloudfront.net') 'Only the reviewed RabbitWarren staging site is preset.'
Assert-Contract ($iss.Contains($config.rabbitWarrenStagingOrigin)) 'Direct Inno compile and build script share staging preset.'
Assert-Contract (-not $build.Contains('app.rabbitwarren.com.ar') -and -not $iss.Contains('app.rabbitwarren.com.ar')) 'Release builds do not silently preset production.'
Assert-Contract ($iss -cmatch '(?m)^PrivilegesRequired=lowest$') 'No administrator installation.'
Assert-Contract ($iss -cmatch '(?m)^AppId=\{\{B907D6FA-4C33-40D1-8BB6-3C6C0A86B6B4\}$') 'Stable per-user upgrade identity across presets.'
Assert-Contract ($iss.Contains('OutputBaseFilename=open-thermal-print-agent-{#MyAppVersion}{#PresetSuffix}-win-x64')) 'Versioned, distinct preset asset names.'
Assert-Contract ($iss.Contains('Source: "{#PublishDir}\*"') -and $build.Contains('"/DPublishDir=$publish"')) 'Installer consumes the versioned publish directory passed by the build.'
Assert-Contract ($iss.Contains('#define PresetSuffix "-rabbitwarren-staging"')) 'Staging artifact suffix.'
Assert-Contract ($iss.Contains('#error The staging artifact must contain only the reviewed staging preset')) 'Compiler rejects an incorrectly branded staging origin.'
Assert-Contract ($iss -notmatch '(?im)^\[(UninstallDelete|InstallDelete)\]' -and $iss -notmatch '(?im)^Root:\s*HKLM') 'No persistent configuration deletion or machine-wide registry writes.'
Assert-Contract ($iss.Contains('SW_HIDE, ewNoWait') -and $iss.Contains(' --installed"')) 'Hidden installed-mode startup.'
Assert-Contract ($iss.Contains('--configure "') -and $iss.Contains('FileExists(ExpandConstant(''{localappdata}\OpenThermalPrintAgent\agent.json''))')) 'Existing configuration follows host preservation path.'
Assert-Contract ($props.Project.PropertyGroup.RestorePackagesWithLockFile -eq 'true' -and $props.Project.PropertyGroup.NuGetLockFilePath.Contains('$(MSBuildProjectName)')) 'Per-project dependency locks outside host/test sources.'
Assert-Contract ($build.Contains('--locked-mode') -and $build.Contains('--no-restore')) 'Replay locks and no implicit restore during test/publish.'
Assert-Contract ($build.Contains('coreclr.dll') -and $build.Contains('$peOffset + 92')) 'Published runtime and hidden GUI subsystem are checked.'
Assert-Contract ($warning.Contains('UNSIGNED') -and $warning.Contains('EXPERIMENTAL') -and $warning.Contains('November 10, 2026')) 'Visible preview, unsigned and runtime lifecycle warnings.'
Assert-Contract ($smoke.Contains("`$env:RUNNER_ENVIRONMENT -ne 'github-hosted'") -and $smoke.Contains('finally {')) 'Smoke restricted to disposable hosted runners with uninstall cleanup.'
Assert-Contract ($smoke -notmatch 'Start-Process[^\r\n]+\s-Wait\b') 'Smoke waits only for setup process, not agent child tree.'
Assert-Contract ($workflow -match '(?m)^  contents: read$' -and $workflow -notmatch '(?m)^\s+contents: write$') 'Read-only workflow token.'
Assert-Contract ($workflow.Contains('persist-credentials: false') -and -not $workflow.Contains('pull_request_target')) 'No persisted git credential or privileged PR trigger.'
$actions = [regex]::Matches($workflow, '(?m)^\s*-?\s*uses:\s*([^\r\n#]+)')
Assert-Contract ($actions.Count -ge 3) 'Expected first-party workflow actions.'
foreach ($action in $actions) {
    Assert-Contract ($action.Groups[1].Value.Trim() -cmatch '^actions/[a-z-]+@[a-f0-9]{40}$') 'Every action pinned to a full immutable commit.'
}
Assert-Contract ($workflow.Contains('Get-FileHash') -and $workflow.Contains('innoSetupSha256')) 'Downloaded compiler is hash-verified before execution.'
Assert-Contract ($workflow.IndexOf('./installer/windows/smoke-test.ps1') -lt $workflow.IndexOf('name: Upload validated release assets')) 'Only smoke-validated assets are uploaded.'
Assert-Contract ($workflow -notmatch 'gh release|softprops/action-gh-release|SkipCertificateCheck|netsh|Import-Certificate') 'No automatic release publication, certificate bypass or firewall modification.'
Write-Host "PASS: $checks installer static contracts. No dotnet build, publish, install or registry writes."
