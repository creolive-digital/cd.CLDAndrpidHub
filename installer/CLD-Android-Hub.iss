#define AppName "CLD Android Hub"
#define AppVersion "2.1.0"
#define AppExe "CLD Android Hub.exe"

[Setup]
AppId={{39C437DC-411D-4351-A29D-A00920E9C390}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=CLD
DefaultDirName={localappdata}\Programs\CLD Android Hub
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\dist
OutputBaseFilename=CLD-Android-Hub-2.1.0-Setup-x64
SetupIconFile=..\BrandAssets\cld-hub.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
VersionInfoCompany=CLD
VersionInfoDescription=CLD Android Hub Installer
VersionInfoVersion=2.1.0.0
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
CloseApplicationsFilter=CLD Android Hub.exe,scrcpy.exe
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:";

[Files]
Source: "..\build\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Messages]
WelcomeLabel1=Welcome to CLD Android Hub
WelcomeLabel2=Setup will install CLD Android Hub on your computer.%n%nYour Android. Beyond the screen.%n%nIncludes the desktop app, mirroring engine, connection tools, and .NET runtime. No separate runtime download is required.
