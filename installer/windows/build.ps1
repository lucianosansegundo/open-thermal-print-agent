[CmdletBinding()]
param(
    [string]$Iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    [ValidatePattern('^0\.4\.0-preview\.[1-9][0-9]*$')]
    [string]$Version = '0.4.0-preview.1',
    [ValidateSet('all', 'generic', 'rabbitwarren-staging')]
    [string]$Preset = 'all',
    [string]$DependencyLockDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$toolchain = Get-Content (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
$output = Join-Path $repo "artifacts/windows-installer/$Version"
$publish = Join-Path $output 'publish'
$locks = Join-Path $output 'dependencies'
$projects = @(Get-ChildItem (Join-Path $repo 'src'), (Join-Path $repo 'tests') -Recurse -Filter '*.csproj')

function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0]) failed (exit $LASTEXITCODE)." }
}

Push-Location $repo
try {
    & (Join-Path $PSScriptRoot 'test.ps1')
    if ((& dotnet --version) -ne $toolchain.dotnetSdkVersion) {
        throw "Use .NET SDK $($toolchain.dotnetSdkVersion). CI installs this exact SDK; see docs/windows-installer.md."
    }
    if (-not (Test-Path -LiteralPath $Iscc -PathType Leaf)) { throw 'Inno Setup compiler not found; supply -Iscc.' }
    # ISCC's PE ProductVersion can be 0.0.0.0; its banner reports the compiler engine version.
    $compilerBanner = (& $Iscc '/?' 2>&1 | Out-String)
    if ($compilerBanner -notmatch ('\bInno Setup ' + [regex]::Escape($toolchain.innoSetupVersion) + '(\D|$)')) {
        throw "Use Inno Setup $($toolchain.innoSetupVersion). Compiler reported: $compilerBanner"
    }
    $commit = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
    $dirty = @(& git status --porcelain --untracked-files=normal)
    if ($LASTEXITCODE -ne 0 -or $dirty.Count -ne 0) { throw 'Build release assets from a clean checkout.' }
    if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -cne "v$Version") {
        throw 'Release tag and installer version do not match.'
    }
    if (Test-Path -LiteralPath $output) { throw "Output already exists: $output. Use a fresh checkout or move the previous output first." }
    New-Item -ItemType Directory -Path $publish, $locks | Out-Null

    # Store dependency locks outside source projects; a previous run can be replayed in locked mode.
    if ($DependencyLockDirectory) {
        foreach ($project in $projects) {
            $lock = Join-Path $DependencyLockDirectory "$($project.BaseName).packages.lock.json"
            if (-not (Test-Path -LiteralPath $lock -PathType Leaf)) { throw "Missing replay lock: $lock" }
            Copy-Item -LiteralPath $lock -Destination $locks
        }
    }
    $properties = @(
        "-p:DirectoryBuildPropsPath=$(Join-Path $PSScriptRoot 'release.props')",
        "-p:InstallerDependencyLockDirectory=$locks",
        "-p:RuntimeFrameworkVersion=$($toolchain.runtimeFrameworkVersion)",
        "-p:Version=$Version",
        '-p:ContinuousIntegrationBuild=true', '-p:Deterministic=true', '-p:SelfContained=true',
        '-m:1', '-nodeReuse:false'
    )
    $restore = @('restore', 'OpenThermalPrintAgent.sln', '-r', 'win-x64', '--source', 'https://api.nuget.org/v3/index.json') + $properties
    if ($DependencyLockDirectory) { $restore += '--locked-mode' }
    Invoke-Dotnet $restore
    Invoke-Dotnet (@('test', 'OpenThermalPrintAgent.sln', '-c', 'Release', '-r', 'win-x64', '--no-restore', '--logger', 'trx', '--results-directory', 'artifacts/test-results') + $properties)
    Invoke-Dotnet (@('publish', 'src/OpenThermalPrintAgent.Host', '-c', 'Release', '-r', 'win-x64', '--no-restore', '--self-contained', 'true', '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '-p:PublishReadyToRun=false', '-o', $publish) + $properties)

    foreach ($file in @('OpenThermalPrintAgent.Host.exe', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'Microsoft.AspNetCore.Server.Kestrel.Core.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $publish $file) -PathType Leaf)) { throw "Incomplete self-contained publish: $file" }
    }
    $runtime = Get-Content (Join-Path $publish 'OpenThermalPrintAgent.Host.runtimeconfig.json') -Raw | ConvertFrom-Json
    foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
        if ($framework.version -ne $toolchain.runtimeFrameworkVersion) { throw 'Published runtime version differs from the pinned toolchain.' }
    }
    $exe = [IO.File]::ReadAllBytes((Join-Path $publish 'OpenThermalPrintAgent.Host.exe'))
    $peOffset = [BitConverter]::ToInt32($exe, 0x3c)
    if ([BitConverter]::ToUInt16($exe, $peOffset + 92) -ne 2) { throw 'Installed host must be a Windows GUI executable (hidden startup).' }

    $assets = @()
    $presets = if ($Preset -eq 'all') { @('generic', 'rabbitwarren-staging') } else { @($Preset) }
    foreach ($selected in $presets) {
        $origin = if ($selected -eq 'rabbitwarren-staging') { $toolchain.rabbitWarrenStagingOrigin } else { '' }
        $suffix = if ($selected -eq 'generic') { '' } else { '-rabbitwarren-staging' }
        $name = "open-thermal-print-agent-$Version$suffix-win-x64.exe"
        & $Iscc "/DMyAppVersion=$Version" "/DPresetName=$selected" "/DDefaultSiteOrigin=$origin" "/DPublishDir=$publish" "/DInstallerOutputDir=$output" (Join-Path $PSScriptRoot 'open-thermal-print-agent.iss')
        if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed: $selected." }
        $path = Join-Path $output $name
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText("$path.sha256", "$hash  $name`n", [Text.UTF8Encoding]::new($false))
        $assets += [ordered]@{ name = $name; sha256 = $hash; preset = $selected; defaultSiteOrigin = $origin }
    }
    foreach ($project in $projects) {
        if (-not (Test-Path -LiteralPath (Join-Path $locks "$($project.BaseName).packages.lock.json"))) { throw 'Restore did not generate all dependency locks.' }
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'toolchain.json') -Destination $output
    $inputsName = "open-thermal-print-agent-$Version-build-inputs.zip"
    Compress-Archive -Path $locks, (Join-Path $output 'toolchain.json') -DestinationPath (Join-Path $output $inputsName)
    $manifest = [ordered]@{
        version = $Version
        sourceCommit = $commit
        sourceRepository = 'https://github.com/lucianosansegundo/open-thermal-print-agent'
        experimental = $true
        signed = $false
        runtimeIdentifier = 'win-x64'
        selfContained = $true
        toolchain = $toolchain
        dependencyLocks = $inputsName
        dependencyLocksSha256 = (Get-FileHash -LiteralPath (Join-Path $output $inputsName) -Algorithm SHA256).Hash.ToLowerInvariant()
        assets = $assets
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'build-manifest.json') -Encoding utf8
    $releaseFiles = @(Get-ChildItem -LiteralPath $output -File | Where-Object { $_.Name -match '\.(exe|sha256|zip)$' -or $_.Name -eq 'build-manifest.json' } | Sort-Object Name)
    $checksums = @($releaseFiles | ForEach-Object { "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name })
    [IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS'), $checksums, [Text.UTF8Encoding]::new($false))
    Write-Host "Experimental UNSIGNED installers prepared in $output. No release was published."
    $assets | Format-Table name, sha256
}
finally { Pop-Location }
