[Setup]
AppName=StormV
AppVersion=1.0.0
AppPublisher=StormV Team
DefaultDirName={autopf}\StormV
DefaultGroupName=StormV
OutputDir=Output
OutputBaseFilename=StormV-Setup
SetupIconFile=StormV\Assets\logo-sv.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать значок на рабочем столе"; GroupDescription: "Дополнительно:"; Flags: unchecked

[Files]
Source: "publish\StormV.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\sing-box.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\StormV"; Filename: "{app}\StormV.exe"
Name: "{group}\Удалить StormV"; Filename: "{uninstallexe}"
Name: "{autodesktop}\StormV"; Filename: "{app}\StormV.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\StormV.exe"; Description: "Запустить StormV"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\StormV.exe"; Parameters: "--quit"; Flags: runhidden; RunOnceId: "QuitStormV"
