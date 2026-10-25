#define AppVersion "1.0.0.0"
#ifndef Arch
 #define Arch "x64"
#endif
#define ProjectRoot AddBackslash(SourcePath) + ".."
#ifndef PublishDir
 #define PublishDir ProjectRoot + "\..\release\notification-fix-win-x64"
#endif
#ifndef SetupOutputDir
 #define SetupOutputDir ProjectRoot + "\..\release"
#endif

[Setup]
AppId={{C96A9E53-AB05-4283-BA69-2507461ABDA7}
AppName=WeatherBeacon
AppVersion={#AppVersion}
AppVerName=WeatherBeacon {#AppVersion}
AppPublisher=WeatherBeacon
VersionInfoVersion={#AppVersion}
VersionInfoTextVersion={#AppVersion}
VersionInfoProductName=WeatherBeacon
VersionInfoProductVersion={#AppVersion}
VersionInfoProductTextVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\WeatherBeacon
DefaultGroupName=WeatherBeacon
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#elif Arch == "x86"
; 32-bit build: runs natively on x86 and under emulation on x64/ARM64 Windows.
ArchitecturesAllowed=x86compatible
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
MinVersion=10.0.18362
WizardStyle=modern
DisableWelcomePage=no
DisableDirPage=no
InfoBeforeFile=InstallInfo.txt
SetupIconFile={#ProjectRoot}\src\WeatherBeacon.App\Assets\WeatherBeacon.ico
UninstallDisplayIcon={app}\WeatherBeacon.exe
UninstallDisplayName=WeatherBeacon {#AppVersion}
OutputDir={#SetupOutputDir}
OutputBaseFilename=WeatherBeacon-{#AppVersion}-{#Arch}-Setup
Compression=lzma2
SolidCompression=yes
CloseApplications=no
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Default.isl,ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
chinesesimplified.LaunchApp=启动 WeatherBeacon
english.LaunchApp=Launch WeatherBeacon
chinesesimplified.CloseApp=请先从系统托盘退出 WeatherBeacon，然后重试。安装和卸载时需要退出所有正在运行的 WeatherBeacon 实例。
english.CloseApp=Please exit WeatherBeacon from the system tray, then retry. Close all running WeatherBeacon instances before installing or uninstalling.

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "logs,logs\*,*.log,settings.json,alert-delivery.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ProjectRoot}\src\WeatherBeacon.App\Assets\WeatherBeacon.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\WeatherBeacon"; Filename: "{app}\WeatherBeacon.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\WeatherBeacon.ico"; AppUserModelID: "WeatherBeacon.Desktop"; AppUserModelToastActivatorCLSID: "07C27A3F-50D8-4612-9D96-6EE4D755986C"; Flags: uninsneveruninstall

[Run]
Filename: "{app}\WeatherBeacon.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
function WeatherBeaconRunning: Boolean;
begin
  Result := FindWindowByWindowName('WeatherBeacon {#AppVersion}') <> 0;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if WeatherBeaconRunning then Result := CustomMessage('CloseApp');
end;

function InitializeUninstall: Boolean;
begin
  Result := not WeatherBeaconRunning;
  if not Result and not UninstallSilent then
    MsgBox(CustomMessage('CloseApp'), mbInformation, MB_OK);
end;

function TargetsThisInstall(const Command: String): Boolean;
var
  AppExe, QuotedExe: String;
begin
  AppExe := ExpandConstant('{app}\WeatherBeacon.exe');
  QuotedExe := '"' + AppExe + '"';
  Result := (CompareText(Command, AppExe) = 0) or
    (CompareText(Copy(Command, 1, Length(QuotedExe)), QuotedExe) = 0);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command, LinkPath: String;
  Shell, Link: Variant;
begin
  if CurUninstallStep <> usUninstall then Exit;
  { Preserve settings and deduplication data. Remove only entries owned by this install path. }
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'WeatherBeacon', Command) then
    if TargetsThisInstall(Command) then
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'WeatherBeacon');

  if RegQueryStringValue(HKCU, 'Software\Classes\CLSID\{07C27A3F-50D8-4612-9D96-6EE4D755986C}\LocalServer32', '', Command) then
    if TargetsThisInstall(Command) then begin
      RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\CLSID\{07C27A3F-50D8-4612-9D96-6EE4D755986C}');
      RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\AppUserModelId\WeatherBeacon.Desktop');
    end;

  LinkPath := ExpandConstant('{userprograms}\WeatherBeacon.lnk');
  if FileExists(LinkPath) then begin
    try
      Shell := CreateOleObject('WScript.Shell');
      Link := Shell.CreateShortcut(LinkPath);
      if TargetsThisInstall(Link.TargetPath) then DeleteFile(LinkPath);
    except
      Log('Could not inspect the WeatherBeacon shortcut; preserved it.');
    end;
  end;
end;
