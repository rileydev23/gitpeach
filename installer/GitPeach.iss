; Windows installer: GitPeach-Setup.exe (Inno Setup 6). Per user, no administrator: installs the same portable exe
; into %LOCALAPPDATA%\Programs\GitPeach, adds it to the Start menu (so Windows search finds it) and to
; Settings > Apps with its uninstaller. Settings in %APPDATA%\peachapps\GitPeach are kept on uninstall.
; Build after build.cmd:  iscc /DAppVersion=1.2.3 installer\GitPeach.iss   ->  dist\GitPeach-Setup.exe
; The app updates itself by running a newer setup with /SILENT; installed.txt tells it that it was installed.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{8DD68DD4-0EBC-4432-8238-73584656288E}
AppName=GitPeach
AppVersion={#AppVersion}
AppVerName=GitPeach {#AppVersion}
AppPublisher=GitPeach
AppPublisherURL=https://github.com/rileydev23/gitpeach
AppSupportURL=https://github.com/rileydev23/gitpeach/issues
AppUpdatesURL=https://github.com/rileydev23/gitpeach/releases
DefaultDirName={localappdata}\Programs\GitPeach
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=GitPeach-Setup
SetupIconFile=..\src\GitPeach\Assets\app.ico
UninstallDisplayIcon={app}\GitPeach.exe
UninstallDisplayName=GitPeach
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Closes a running GitPeach before replacing it (the app also exits by itself when it updates).
CloseApplications=force
RestartApplications=no

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\GitPeach.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "installed.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\GitPeach"; Filename: "{app}\GitPeach.exe"
Name: "{userdesktop}\GitPeach"; Filename: "{app}\GitPeach.exe"; Tasks: desktopicon

[Run]
; Interactive install: a "Launch GitPeach" checkbox. Silent install (an update from the app): reopen it right away.
Filename: "{app}\GitPeach.exe"; Description: "{cm:LaunchProgram,GitPeach}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\GitPeach.exe"; Flags: nowait; Check: WizardSilent

[UninstallDelete]
; Left behind by an update that replaced the exe in place.
Type: files; Name: "{app}\GitPeach.exe.old"
Type: files; Name: "{app}\GitPeach.exe.new"
Type: dirifempty; Name: "{app}"
