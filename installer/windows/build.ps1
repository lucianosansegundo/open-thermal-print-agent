param(
    [string]$Iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    [string]$DefaultSiteOrigin = ''
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $repo
try {
    dotnet publish src/OpenThermalPrintAgent.Host -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:RestoreSources=https://api.nuget.org/v3/index.json -m:1 -nodeReuse:false -o artifacts/publish/OpenThermalPrintAgent.Host
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }
    & $Iscc "/DDefaultSiteOrigin=$DefaultSiteOrigin" installer/windows/open-thermal-print-agent.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    Get-FileHash artifacts/installer/open-thermal-print-agent-0.3.0-win-x64.exe -Algorithm SHA256
}
finally { Pop-Location }
