#define MyAppName "Open Thermal Print Agent"
#define MyAppVersion "0.2.0"
#define MyAppExeName "OpenThermalPrintAgent.Host.exe"

[Setup]
AppId={{B907D6FA-4C33-40D1-8BB6-3C6C0A86B6B4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Open Thermal Print Agent contributors
DefaultDirName={localappdata}\Programs\Open Thermal Print Agent
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
OutputDir=..\..\artifacts\installer
OutputBaseFilename=open-thermal-print-agent-{#MyAppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=no
RestartApplications=no
SetupMutex=OpenThermalPrintAgentInstaller

[Files]
Source: "..\..\artifacts\publish\OpenThermalPrintAgent.Host\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "OpenThermalPrintAgent"; ValueData: """{app}\{#MyAppExeName}"" --installed"

[Icons]
Name: "{group}\Open Thermal Print Agent"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--installed"
Name: "{group}\Open Thermal Print Agent Setup"; Filename: "{localappdata}\OpenThermalPrintAgent\setup.html"
Name: "{group}\Open Thermal Print Agent Health"; Filename: "http://127.0.0.1:17890/api/v1/health"
Name: "{group}\Open Thermal Print Agent Logs"; Filename: "{localappdata}\OpenThermalPrintAgent\logs"

[Run]
Filename: "{localappdata}\OpenThermalPrintAgent\setup.html"; Description: "Open local setup information to copy your token"; Flags: shellexec postinstall skipifsilent

[Code]
var
  OriginPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  OriginPage := CreateInputQueryPage(wpWelcome, 'Connect your POS site',
    'Enter the exact site origin permitted to use this printer agent.',
    'Use the HTTPS address of your POS site, without a path. Existing configuration and token are preserved on upgrade. To change sites later, edit agent.json and restart the agent.');
  OriginPage.Add('Site origin', False);
  OriginPage.Values[0] := ExpandConstant('{param:SITEORIGIN|}');
end;

function ValidOrigin(Origin: String): Boolean;
var
  Authority: String;
begin
  Result := False;
  if Copy(Origin, 1, 8) = 'https://' then Authority := Copy(Origin, 9, Length(Origin))
  else if Copy(Origin, 1, 7) = 'http://' then Authority := Copy(Origin, 8, Length(Origin))
  else Exit;
  if Copy(Authority, Length(Authority), 1) = '/' then Delete(Authority, Length(Authority), 1);
  Result := (Authority <> '') and (Pos('/', Authority) = 0) and
    (Pos('*', Authority) = 0) and (Pos('@', Authority) = 0) and
    (Pos('?', Authority) = 0) and (Pos('#', Authority) = 0) and
    (Pos('"', Authority) = 0) and (Pos(' ', Authority) = 0) and
    (Pos('\', Authority) = 0);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = OriginPage.ID then
  begin
    if (OriginPage.Values[0] = '') and FileExists(ExpandConstant('{localappdata}\OpenThermalPrintAgent\agent.json')) then Exit;
    Result := ValidOrigin(OriginPage.Values[0]);
    if not Result then MsgBox('Enter an exact HTTP(S) site origin without a path, credentials or wildcard.', mbError, MB_OK);
  end;
end;

function StopAgent(const ExePath: String): Boolean;
var
  Code: Integer;
begin
  Result := True;
  if FileExists(ExePath) then
    Result := Exec(ExePath, '--stop', '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if (OriginPage.Values[0] = '') and
     not FileExists(ExpandConstant('{localappdata}\OpenThermalPrintAgent\agent.json')) then
    Result := 'Enter a site origin, or provide /SITEORIGIN=https://your-pos.example when installing silently.';
  if (OriginPage.Values[0] <> '') and not ValidOrigin(OriginPage.Values[0]) then
    Result := 'The site origin must be an exact HTTP(S) origin without a path, credentials or wildcard.';
  if (Result = '') and not StopAgent(ExpandConstant('{app}\{#MyAppExeName}')) then
    Result := 'The installed agent is still stopping. Complete pending print jobs and run setup again.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
  Origin: String;
  ExePath: String;
begin
  if CurStep = ssPostInstall then
  begin
    ExePath := ExpandConstant('{app}\{#MyAppExeName}');
    Origin := OriginPage.Values[0];
    if Origin = '' then Origin := 'https://existing-configuration.invalid';
    if (Pos('"', Origin) > 0) or
       not Exec(ExePath, '--configure "' + Origin + '"', '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
      RaiseException('Configuration failed. Review the site origin and local agent logs, then run setup again.');
    if not Exec(ExePath, '--installed', '', SW_HIDE, ewNoWait, Code) then
      RaiseException('Agent could not start. Open the Start menu agent logs shortcut.');
    if not Exec(ExePath, '--check', '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
      RaiseException('Agent health check failed. Review the Start menu agent logs. Another application may occupy port 17890.');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Startup: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    if not StopAgent(ExpandConstant('{app}\{#MyAppExeName}')) then
      RaiseException('The agent is still stopping. Complete pending jobs and retry uninstall.');
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'OpenThermalPrintAgent', Startup) then
      if Startup = ExpandConstant('"{app}\{#MyAppExeName}" --installed') then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'OpenThermalPrintAgent');
  end;
end;
