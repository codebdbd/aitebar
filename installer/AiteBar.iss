#define AppName "Aite Bar"
#define AppDisplayName "AiteBar"
#define AppPublisher "Codebdbd"
#define AppExeName "AiteBar.exe"
#ifndef AppVersion
  #define AppVersion "1.11.1"
#endif
#define PublishDir "..\artifacts\publish\win-x64"

[Setup]
AppId={{0B8E4B6C-6DB0-4E14-9DA1-68A7AAB95571}
AppName={#AppDisplayName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppDisplayName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\artifacts\installer
OutputBaseFilename=AiteBar-Setup
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppDisplayName} Installer
VersionInfoProductName={#AppDisplayName}
VersionInfoProductVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
UninstallDisplayIcon={app}\{#AppExeName}
DirExistsWarning=no
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\AiteBar\Resources\app.ico
AppMutex=Global\AiteBar_Mutex_Unique_String_123
CloseApplications=yes
RestartApplications=yes

[InstallDelete]
Type: filesandordirs; Name: "{app}\*"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные параметры:"
Name: "autostart"; Description: "Запускать при входе в Windows"; GroupDescription: "Дополнительные параметры:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppDisplayName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppDisplayName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Code]
// Mutex for installer itself to prevent multiple instances
const
  InstallerMutexName = 'Global\AiteBar_Installer_Mutex_Unique_456';
var
  InstallerMutex: Integer;

function CreateMutex(lpMutexAttributes: Integer; bInitialOwner: Integer; lpName: string): Integer;
external 'CreateMutexW@kernel32.dll stdcall';
function ReleaseMutex(hMutex: Integer): Integer;
external 'ReleaseMutex@kernel32.dll stdcall';
function CloseHandle(hObject: Integer): Integer;
external 'CloseHandle@kernel32.dll stdcall';
function GetLastError(): Integer;
external 'GetLastError@kernel32.dll stdcall';

const
  INFINITE = $FFFFFFFF;
  WAIT_OBJECT_0 = 0;
  ERROR_ALREADY_EXISTS = 183;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  // Create installer mutex to prevent multiple instances
  InstallerMutex := CreateMutex(0, 1, InstallerMutexName);
  ErrorCode := GetLastError();
  
  if ErrorCode = ERROR_ALREADY_EXISTS then
  begin
    MsgBox('Инсталлятор AiteBar уже запущен!', mbError, MB_OK);
    Result := False;
    Exit;
  end;
  
  Result := True;
end;

procedure DeinitializeSetup();
begin
  // Release installer mutex
  if InstallerMutex <> 0 then
  begin
    ReleaseMutex(InstallerMutex);
    CloseHandle(InstallerMutex);
  end;
end;

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Запустить {#AppDisplayName}"; Flags: nowait postinstall skipifsilent



