; Installer für Prozessor Affinity Manager (Inno Setup 6)
; Erstellen: Projekt in "Release" bauen, dann diese Datei mit Inno Setup kompilieren.
; Ergebnis: installer\Output\Prozessor-Affinity-Manager-Setup-<Version>.exe

#define AppName "Prozessor Affinity Manager"
; Version kann beim Kompilieren überschrieben werden: ISCC /DAppVersion=1.2.3 setup.iss
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "Professor"
#define AppExe "Prozessor Affinity Manager.exe"
#define BuildDir "..\bin\Release\net48"

[Setup]
AppId={{7C3E2B9A-4F1D-4E8B-9A6C-2D5F8E1B3A47}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=© 2026 {#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=Prozessor-Affinity-Manager-Setup-{#AppVersion}
; Läuft das Tool noch (z. B. im Tray), fragt das Setup, ob es geschlossen werden soll
AppMutex=ProzessorAffinityManager
CloseApplications=yes
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#BuildDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\{#AppExe}.config"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
; Autostart-Aufgabe entfernen, falls "Start with Windows" aktiviert war
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""{#AppName}"" /F"; Flags: runhidden; RunOnceId: "RemoveAutostartTask"
