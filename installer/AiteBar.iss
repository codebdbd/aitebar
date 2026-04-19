#define AppName "Aite Bar"
#define AppDisplayName "AiteBar"
#define AppPublisher "Codebdbd"
#define AppExeName "AiteBar.exe"
#ifndef AppVersion
  #define AppVersion "1.4.0"
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
UninstallDisplayIcon={app}\{#AppExeName}
DirExistsWarning=no
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\AiteBar\Resources\app.ico
AppMutex=AiteBar_Mutex_Unique_String_123
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

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Запустить {#AppDisplayName}"; Flags: nowait postinstall skipifsilent



