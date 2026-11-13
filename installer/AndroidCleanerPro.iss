#define AppName "Android Cleaner Pro"
#ifndef AppVersion
#define AppVersion "0.3.0"
#endif
#define AppExe "AndroidCleanerPro.exe"
[Setup]
AppId={{9F0F41E1-6C38-4C5A-BDC7-2A5E1D9A72C8}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Android Cleaner Pro
DefaultDirName={autopf}\Android Cleaner Pro
DefaultGroupName={#AppName}
OutputDir=..\dist\Installer
OutputBaseFilename=AndroidCleanerPro-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#AppExe}
[Files]
Source: "..\dist\Windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"
[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch Android Cleaner Pro"; Flags: nowait postinstall skipifsilent
