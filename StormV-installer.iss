[Setup]
AppName=StormV
AppVersion=1.1.1
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
; Удалять оставшиеся файлы при деинсталляции
Uninstallable=yes
CreateUninstallRegKey=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать значок на рабочем столе"; GroupDescription: "Дополнительно:"; Flags: unchecked

[Files]
Source: "publish\StormV.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\sing-box.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "publish\*.json"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "publish\*.pdb"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\StormV"; Filename: "{app}\StormV.exe"
Name: "{group}\Удалить StormV"; Filename: "{uninstallexe}"
Name: "{autodesktop}\StormV"; Filename: "{app}\StormV.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\StormV.exe"; Description: "Запустить StormV"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Завершаем процессы перед удалением
Filename: "taskkill.exe"; Parameters: "/F /IM StormV.exe"; Flags: runhidden; RunOnceId: "KillStormV"
Filename: "taskkill.exe"; Parameters: "/F /IM sing-box.exe"; Flags: runhidden; RunOnceId: "KillSingBox"

[UninstallDelete]
; Удаляем конфиги, логи и все пользовательские данные
Type: filesandordirs; Name: "{localappdata}\StormV"
Type: filesandordirs; Name: "{app}"
