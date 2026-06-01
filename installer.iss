; ============================================================
;  Folder Colorizer — Inno Setup Script
;  Builds a single-file Windows installer (.exe)
;
;  Compile with:
;    iscc installer.iss
;
;  Prerequisites (handled by GitHub Actions):
;    • PyInstaller already built folder_colorizer.exe into dist\
;    • Icons live in icons\
; ============================================================

#define MyAppName      "Folder Colorizer"
#define MyAppVersion   "1.0.0"
#define MyAppPublisher "walidroid"
#define MyAppURL       "https://github.com/walidroid/folder_colorizer"
#define MyAppExeName   "folder_colorizer.exe"
#define MyAppId        "{{A3F2E1D0-9B4C-4E7F-8A1D-2C6B5E3F0A9D}"

[Setup]
; Unique GUID — regenerate if you fork the project
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes

; Request admin rights so we can write to HKEY_CLASSES_ROOT
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Output
OutputDir=output
OutputBaseFilename=FolderColorizer-Setup-v{#MyAppVersion}
SetupIconFile=icons\orange.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; Minimum OS: Windows 10
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ── Files ─────────────────────────────────────────────────────────────────────
[Files]
; Main executable produced by PyInstaller
Source: "dist\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; All colour / texture icons
Source: "icons\*"; DestDir: "{app}\icons"; Flags: ignoreversion recursesubdirs

; ── Icons (Start Menu + Desktop) ──────────────────────────────────────────────
[Icons]
Name: "{group}\{#MyAppName}";           Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icons\orange.ico"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}";   Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icons\orange.ico"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

; ── Registry — right-click context menu ───────────────────────────────────────
[Registry]
; Add "Change Folder Color / Texture" to folder right-click menu
Root: HKCR; Subkey: "Directory\shell\FolderColorizer";            ValueType: string; ValueName: "";     ValueData: "Change Folder Color / Texture"; Flags: uninsdeletekey
Root: HKCR; Subkey: "Directory\shell\FolderColorizer";            ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\orange.ico"
Root: HKCR; Subkey: "Directory\shell\FolderColorizer\command";    ValueType: string; ValueName: "";     ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; ── Run after install ─────────────────────────────────────────────────────────
[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

; ── Cleanup on uninstall ──────────────────────────────────────────────────────
[UninstallRun]
; Refresh Explorer shell so icons revert immediately
Filename: "cmd.exe"; Parameters: "/c ie4uinit.exe -show"; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
