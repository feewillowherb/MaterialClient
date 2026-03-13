; MaterialClient Inno Setup 安装脚本
; 用于创建 MaterialClient 应用程序的安装程序

#define MyAppName "MaterialClient"
#define MyAppVersion "1.0.7"
#define MyAppPublisher "FindongSoft"
#define MyAppURL "http://www.example.com/"
#define MyAppExeName "MaterialClient.exe"
#define SourceDir "MaterialClient\bin\Release\net10.0\win-x64\publish"
#define OutputDir "Installer"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
OutputDir={#OutputDir}
OutputBaseFilename={#MyAppName}_Setup_{#MyAppVersion}
SetupIconFile=MaterialClient\Assets\fd-ico.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; 主程序文件（单文件发布，包含所有托管依赖、.NET 运行时和原生 DLL）
Source: "{#SourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; 配置文件
Source: "{#SourceDir}\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist
Source: "{#SourceDir}\appsettings.secret.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist


; 注意：以下文件/文件夹不需要包含（运行时不需要）：
; - *.pdb 文件（调试符号）
; - *.lib 文件（链接库，包括根目录和 HCNetSDKCom 文件夹中的所有 .lib 文件）
; - BuildHost-* 文件夹（构建主机文件）
; - MaterialClient.db（数据库文件会在首次运行时自动创建）
; - HCNetSDKCom 文件夹（如果里面只有 .lib 文件，运行时不需要）

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  // 文件已经在编译时打包进安装程序
  // 运行时不需要检查开发机器的源路径（这会导致在其他机器上安装失败）
  Result := True;
end;

function InitializeUninstall(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := (Version.Major >= 10);
  if not Result then
    MsgBox('此应用程序需要 Windows 10 或更高版本。', mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if not FileExists(ExpandConstant('{app}\{#MyAppExeName}')) then
      MsgBox('警告: 主程序文件未找到！', mbError, MB_OK);
  end;
end;