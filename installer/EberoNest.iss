#define MyAppName "EberoNest"
#define MyAppVersion "1.0.2"
#define MyAppExeName "EberoNest.exe"
#define EberoNestPayloadSHA256 GetSHA256OfFile(AddBackslash(SourcePath) + "..\dist\EberoNest.exe")

[Setup]
AppId={{D647F9AD-809D-4C04-9E14-DB0038A9C315}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
DefaultDirName={localappdata}\Programs\EberoNest
DisableDirPage=yes
UsePreviousAppDir=no
DefaultGroupName=EberoNest
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64
AppMutex=Local\DownloadsOrganizer.Instance
CloseApplications=no
RestartApplications=no
Uninstallable=yes
UninstallDisplayName=EberoNest
UninstallDisplayIcon={app}\EberoNest.exe
SetupIconFile=..\assets\app-icon.ico
OutputDir=..\release
OutputBaseFilename=EberoNest-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
VersionInfoVersion=1.0.2.0
VersionInfoProductVersion=1.0.2.0
VersionInfoProductName=EberoNest
VersionInfoDescription=EberoNest Setup
VersionInfoOriginalFileName=EberoNest-Setup.exe
TimeStampsInUTC=yes

[Files]
Source: "..\dist\EberoNest.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\EberoNest\EberoNest"; Filename: "{app}\EberoNest.exe"; WorkingDir: "{app}"; IconFilename: "{app}\EberoNest.exe"
Name: "{userstartup}\EberoNest"; Filename: "{app}\EberoNest.exe"; Parameters: "--background"; WorkingDir: "{app}"; IconFilename: "{app}\EberoNest.exe"

[Run]
Filename: "{app}\EberoNest.exe"; Description: "Ejecutar EberoNest"; Flags: postinstall nowait skipifsilent

[Code]
function InstalledExe: string;
begin
  // InitializeSetup runs before the application path is initialized; the install path is canonical.
  Result := ExpandConstant('{localappdata}\Programs\EberoNest\EberoNest.exe');
end;

function DropNestLegacyExe: string;
begin
  Result := ExpandConstant('{localappdata}\Programs\Ebero DropNest\EberoDropNest.exe');
end;

function DownloadsOrganizerLegacyExe: string;
begin
  Result := ExpandConstant('{localappdata}\Programs\DownloadsOrganizer\DownloadsOrganizer.exe');
end;

function AdjacentDropNestLegacyExe: string;
begin
  // Keep early shortcut recognition independent of the application path.
  Result := ExpandConstant('{localappdata}\Programs\EberoNest\EberoDropNest.exe');
end;

function SamePath(A, B: string): Boolean;
begin
  Result := False;
  if (A = '') or (B = '') then Exit;
  try
    Result := CompareText(ExpandFileName(A), ExpandFileName(B)) = 0;
  except
    Result := False;
  end;
end;

function LinkMatches(LinkPath, ExpectedTarget, ExpectedArguments: string): Boolean;
var
  Shell, Shortcut: Variant;
begin
  Result := False;
  if not FileExists(LinkPath) then Exit;
  try
    Shell := CreateOleObject('WScript.Shell');
    Shortcut := Shell.CreateShortcut(LinkPath);
    Result := SamePath(Shortcut.TargetPath, ExpectedTarget) and
      (CompareStr(Shortcut.Arguments, ExpectedArguments) = 0);
  except
    Result := False;
  end;
end;

function LinkMatchesCurrentPayload(LinkPath, ExpectedArguments: string): Boolean;
var
  Shell, Shortcut: Variant;
  Target: string;
begin
  Result := False;
  if not FileExists(LinkPath) then Exit;
  try
    Shell := CreateOleObject('WScript.Shell');
    Shortcut := Shell.CreateShortcut(LinkPath);
    Target := Shortcut.TargetPath;
    if not FileExists(Target) then Exit;
    if CompareText(ExtractFileName(Target), 'EberoNest.exe') <> 0 then Exit;
    if CompareStr(Shortcut.Arguments, ExpectedArguments) <> 0 then Exit;
    Result := CompareText(GetSHA256OfFile(Target), '{#EberoNestPayloadSHA256}') = 0;
  except
    Result := False;
  end;
end;

function IsRecognizedCanonicalStartup(LinkPath: string): Boolean;
begin
  Result := LinkMatches(LinkPath, InstalledExe, '--background') or
    LinkMatches(LinkPath, DropNestLegacyExe, '--background') or
    LinkMatches(LinkPath, AdjacentDropNestLegacyExe, '--background') or
    LinkMatches(LinkPath, DownloadsOrganizerLegacyExe, '--background') or
    LinkMatchesCurrentPayload(LinkPath, '--background');
end;

function InitializeSetup(): Boolean;
var
  StartupLink, StartMenuLink: string;
begin
#if defined(InstallerInitSmoke)
  { Exercise every executable-path helper used by InitializeSetup, then abort before installation. }
  if (InstalledExe = '') or (DropNestLegacyExe = '') or
     (DownloadsOrganizerLegacyExe = '') or (AdjacentDropNestLegacyExe = '') then
    RaiseException('Installer initialization path helper returned an empty path.');
  Result := False;
#else
  Result := False;
  StartupLink := ExpandConstant('{userstartup}\EberoNest.lnk');
  StartMenuLink := ExpandConstant('{userprograms}\EberoNest\EberoNest.lnk');

  if FileExists(StartupLink) and not IsRecognizedCanonicalStartup(StartupLink) then
  begin
    MsgBox('No se puede continuar porque ya existe un acceso EberoNest.lnk que no coincide con una instalación reconocida. Se conservó sin cambios.', mbError, MB_OK);
    Exit;
  end;

  if FileExists(StartMenuLink) and not (LinkMatches(StartMenuLink, InstalledExe, '') or LinkMatchesCurrentPayload(StartMenuLink, '')) then
  begin
    MsgBox('No se puede continuar porque el acceso EberoNest del Menú Inicio no pertenece a esta instalación. Se conservó sin cambios.', mbError, MB_OK);
    Exit;
  end;

  Result := True;
#endif
end;

procedure RemoveRecognizedLegacyShortcuts();
var
  LinkPath: string;
begin
  LinkPath := ExpandConstant('{userstartup}\Ebero DropNest.lnk');
  if LinkMatches(LinkPath, DropNestLegacyExe, '--background') or
     LinkMatches(LinkPath, AdjacentDropNestLegacyExe, '--background') then
    DeleteFile(LinkPath);

  LinkPath := ExpandConstant('{userstartup}\DownloadsOrganizer.lnk');
  if LinkMatches(LinkPath, DownloadsOrganizerLegacyExe, '--background') or
     LinkMatches(LinkPath, DownloadsOrganizerLegacyExe, '') then
    DeleteFile(LinkPath);
end;

procedure RemoveRecognizedDoubleExtensionShortcuts();
var
  LinkPath: string;
begin
  LinkPath := ExpandConstant('{userprograms}\EberoNest\EberoNest.lnk.lnk');
  if LinkMatches(LinkPath, InstalledExe, '') or LinkMatchesCurrentPayload(LinkPath, '') then
    DeleteFile(LinkPath);

  LinkPath := ExpandConstant('{userstartup}\EberoNest.lnk.lnk');
  if LinkMatches(LinkPath, InstalledExe, '--background') or
     LinkMatchesCurrentPayload(LinkPath, '--background') then
    DeleteFile(LinkPath);
end;

procedure RemoveLegacyExecutables();
var
  Path: string;
begin
  Path := DropNestLegacyExe;
  if FileExists(Path) then DeleteFile(Path);
  RemoveDir(ExtractFileDir(Path));

  Path := DownloadsOrganizerLegacyExe;
  if FileExists(Path) then DeleteFile(Path);
  RemoveDir(ExtractFileDir(Path));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    RemoveRecognizedDoubleExtensionShortcuts();
    RemoveRecognizedLegacyShortcuts();
    RemoveLegacyExecutables();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  LinkPath: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    LinkPath := ExpandConstant('{userstartup}\EberoNest.lnk');
    if LinkMatches(LinkPath, InstalledExe, '--background') or LinkMatchesCurrentPayload(LinkPath, '--background') then DeleteFile(LinkPath);
    RemoveRecognizedDoubleExtensionShortcuts();
    RemoveRecognizedLegacyShortcuts();
    RemoveLegacyExecutables();
    LinkPath := ExpandConstant('{userprograms}\EberoNest\EberoNest.lnk');
    if LinkMatches(LinkPath, InstalledExe, '') or LinkMatchesCurrentPayload(LinkPath, '') then DeleteFile(LinkPath);
    RemoveDir(ExpandConstant('{userprograms}\EberoNest'));
  end;
end;





