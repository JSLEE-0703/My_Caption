#define MyAppName "My Caption"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "My Caption"
#define MyAppExeName "MyCaption.exe"
#define MyAppSourceDir "..\bin\Release"

[Setup]
AppId={{7DAB4F0B-28B5-4B74-A475-FD21BDAA2B19}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=MyCaptionSetup-{#MyAppVersion}
SetupIconFile=..\assets\icon\MyCaption.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
SetupLogging=yes
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Excludes: "settings.json"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\runtime"
Type: filesandordirs; Name: "{app}\tools"
Type: filesandordirs; Name: "{app}\dictionary"
Type: dirifempty; Name: "{app}"

[Code]
function IsDotNet48Installed(): Boolean;
var
  Release: Cardinal;
begin
  Result :=
    RegQueryDWordValue(
      HKLM,
      'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
      'Release',
      Release) and
    (Release >= 528040);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;

  if not IsDotNet48Installed() then
  begin
    MsgBox(
      '.NET Framework 4.8 was not detected.' + #13#10 +
      'My Caption may not run until .NET Framework 4.8 is installed.',
      mbInformation,
      MB_OK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  SettingsDir: string;
begin
  if CurUninstallStep <> usPostUninstall then
  begin
    Exit;
  end;

  SettingsDir := ExpandConstant('{userappdata}\{#MyAppName}');
  if not DirExists(SettingsDir) then
  begin
    Exit;
  end;

  if MsgBox(
    'Do you want to delete your My Caption user settings?' + #13#10 +
    SettingsDir,
    mbConfirmation,
    MB_YESNO) = IDYES then
  begin
    DelTree(SettingsDir, True, True, True);
  end;
end;
