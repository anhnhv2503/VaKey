; Trình cài đặt VaKey (Inno Setup 6). Tạo bằng: ./build/publish.ps1 -Version 1.0.0
; hoặc trực tiếp: ISCC.exe /DAppVersion=1.0.0 installer\VaKey.iss (sau khi đã publish)

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "VaKey"
#define AppExe "VaKey.exe"
#define PublishDir "..\artifacts\publish"
#define AppUrl "https://github.com/vakey-app/vakey"

[Setup]
AppId={{6B2C4F0E-5E4B-4A39-9C1D-56414B590001}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=VaKey contributors
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Bộ gõ tiếng Việt VaKey - trình cài đặt
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
OutputDir=..\artifacts
OutputBaseFilename=VaKey-{#AppVersion}-setup-win-x64
SetupIconFile=..\src\VaKey.Win\Assets\vakey.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName} - Bộ gõ tiếng Việt
LicenseFile=..\LICENSE
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Mặc định cài cho người dùng hiện tại (không cần quyền Admin); có thể chọn cài cho mọi người dùng.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
CloseApplications=no
; Ký số (cấu hình SignTool "signtool" trong Inno Setup trước):
; SignTool=signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 /f "$qcert.pfx$q" $f

[Languages]
Name: "vietnamese"; MessagesFile: "Vietnamese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
vietnamese.Options=Tùy chọn:
vietnamese.StartWithWindows=Khởi động VaKey cùng Windows
vietnamese.DesktopIcon=Tạo biểu tượng trên màn hình Desktop
vietnamese.LaunchNow=Chạy VaKey ngay
vietnamese.Uninstall=Gỡ cài đặt VaKey
vietnamese.Guide=Hướng dẫn sử dụng
english.Options=Options:
english.StartWithWindows=Start VaKey with Windows
english.DesktopIcon=Create a desktop shortcut
english.LaunchNow=Launch VaKey now
english.Uninstall=Uninstall VaKey
english.Guide=User guide

[Tasks]
Name: "startup"; Description: "{cm:StartWithWindows}"; GroupDescription: "{cm:Options}"
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:Options}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "..\README.md"; DestDir: "{app}"; DestName: "HuongDan.md"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:Uninstall}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Cùng khóa mà VaKey dùng khi bật "Khởi động cùng Windows" trong bảng điều khiển.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "VaKey"; \
  ValueData: """{app}\{#AppExe}"" --startup"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchNow}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExe}"; Flags: runhidden; RunOnceId: "KillVaKey"
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /F /TN ""VaKey (admin)"""; Flags: runhidden; RunOnceId: "DelTask"

[Code]
// Đóng VaKey đang chạy trước khi ghi đè file (nâng cấp phiên bản).
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(300);
  Result := '';
end;
