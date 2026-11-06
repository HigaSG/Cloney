; Instalador do Cloney (Inno Setup 6). Gerado pelo build.ps1.
; Instala por usuário (sem UAC), em %LOCALAPPDATA%\Programs\Cloney.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{8E0C3B52-6C1F-4C8F-9C5E-1D2B7A9E4F10}
AppName=Cloney
AppVersion={#AppVersion}
AppVerName=Cloney {#AppVersion}
AppPublisher=Uso pessoal
DefaultDirName={autopf}\Cloney
DefaultGroupName=Cloney
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=Cloney-Setup-{#AppVersion}
SetupIconFile=..\src\Cloney\Assets\cloney.ico
UninstallDisplayIcon={app}\Cloney.exe
UninstallDisplayName=Cloney
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Cloney"; Filename: "{app}\Cloney.exe"; Comment: "Gerenciar perfis do Cloney"
Name: "{autodesktop}\Cloney"; Filename: "{app}\Cloney.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Cloney.exe"; Description: "{cm:LaunchProgram,Cloney}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Atalhos dos perfis. Os perfis e os dados (%LOCALAPPDATA%\Cloney) são mantidos.
Type: filesandordirs; Name: "{userprograms}\Cloney Apps"
