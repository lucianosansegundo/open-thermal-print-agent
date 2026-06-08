#define MyAppName "Open Thermal Print Agent"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Open Thermal Print Agent contributors"
#define MyAppExeName "OpenThermalPrintAgent.Host.exe"
#define MyServiceName "OpenThermalPrintAgent"

[Setup]
AppId={{52A79D02-60E7-4F58-9D64-8F9FA4E8B1F5}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Open Thermal Print Agent
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\..\artifacts\installer
OutputBaseFilename=open-thermal-print-agent-{#MyAppVersion}-win-x64
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "..\..\artifacts\publish\OpenThermalPrintAgent.Host\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Open Thermal Print Agent"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Open Thermal Print Agent Health"; Filename: "http://127.0.0.1:17890/api/v1/health"

[Tasks]
Name: "installservice"; Description: "Install Windows Service"; GroupDescription: "Service options:"; Flags: checkedonce
Name: "startservice"; Description: "Start service after installation"; GroupDescription: "Service options:"; Flags: checkedonce

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""if (-not (Get-Service -Name '{#MyServiceName}' -ErrorAction SilentlyContinue)) {{ New-Service -Name '{#MyServiceName}' -DisplayName '{#MyAppName}' -Description 'Local HTTP agent for thermal receipt printing.' -BinaryPathName '\""{app}\{#MyAppExeName}\""' -StartupType Automatic }}"""; Flags: runhidden; Tasks: installservice
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Start-Service -Name '{#MyServiceName}'"""; Flags: runhidden; Tasks: startservice

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""$service = Get-Service -Name '{#MyServiceName}' -ErrorAction SilentlyContinue; if ($service) {{ Stop-Service -Name '{#MyServiceName}' -Force -ErrorAction SilentlyContinue; sc.exe delete '{#MyServiceName}' | Out-Null }}"""; Flags: runhidden
