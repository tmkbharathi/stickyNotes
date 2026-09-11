#define MyAppName "Sticky Notes"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "tmkb"
#define MyAppExeName "StickyNotes.exe"

[Setup]
AppId={{D37F8A45-927A-4D3B-A938-D0E1F4C9827B}
AppName={#MyAppName}
AppVersion=1.0.4
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=StickyNotes-setup
SetupIconFile=src\StickyNotes\Assets\appIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start Sticky Notes automatically on Windows startup"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "src\StickyNotes\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--startup"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent






