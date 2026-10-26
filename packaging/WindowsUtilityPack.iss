; Build with scripts/Build-Installer.ps1. Payload must be a self-contained publish.
#ifndef AppVersion
  #error AppVersion must be provided
#endif
#ifndef PublishDir
  #error PublishDir must be provided
#endif
#ifndef InstallerOutput
  #error InstallerOutput must be provided
#endif

[Setup]
AppId={{CA86909A-47C7-43D5-9E5C-52C74B8E98B7}
AppName=Windows Utility Pack
AppVersion={#AppVersion}
AppPublisher=0langa
AppPublisherURL=https://github.com/0langa/Windows-Utility-Pack
AppMutex=Local\WindowsUtilityPack.Running
SetupMutex=Local\WindowsUtilityPack.Setup
DefaultDirName={localappdata}\Programs\WindowsUtilityPack
DefaultGroupName=Windows Utility Pack
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupArchitecture=x64
OutputDir={#InstallerOutput}
OutputBaseFilename=WindowsUtilityPack-{#AppVersion}-win-x64-setup
UninstallDisplayIcon={app}\WindowsUtilityPack.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\Windows Utility Pack"; Filename: "{app}\WindowsUtilityPack.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\WindowsUtilityPack.exe"; Description: "Open Windows Utility Pack"; Flags: nowait postinstall skipifsilent

; Intentionally no wildcard UninstallDelete entries. Inno Setup removes only
; registered payload files/shortcut. Both AppData profiles and unknown files remain.
