; HA DeskLink v2 Inno Setup Installer
#define Version ReadFile(FileCombine(SourcePath, "..\..\VERSION"))
#define AppName "HA DeskLink"
#define AppExe "HA_DeskLink.exe"

[Setup]
AppName={#AppName}
AppVersion={#Version}
AppPublisher=Fabian Kirchweger
AppPublisherURL=https://github.com/FKirchweger/ha-desklink-dotnet
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputBaseFilename=HA_DeskLink_Setup_{#Version}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
CloseApplications=force
RestartApplications=no

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\VERSION"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{#AppName} starten"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup: Boolean;
begin
  Result := True;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if MsgBox('Einstellungen behalten?', mbConfirmation, MB_YESNO) = IDNO then
    begin
      DelTree(ExpandConstant('{app}'), True, True, True);
    end;
  end;
end;