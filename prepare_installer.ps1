$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$publishDir = Join-Path $projectRoot "bin\Release\net9.0-windows10.0.17763.0\win-x64\publish"
$buildDir = Join-Path $projectRoot "installer_build"
$appDest = Join-Path $buildDir "App"
$thirdparty = Join-Path $projectRoot "thirdparty"
$webviewInstaller = Join-Path $thirdparty "MicrosoftEdgeWebView2RuntimeInstallerX64.exe"
$iconFile = Join-Path $projectRoot "icon.ico"  # Đường dẫn icon của bạn

Write-Host "Publish dir:" $publishDir
Write-Host "Installer build dir:" $buildDir

# Clean + create folders
if (Test-Path $buildDir) { Remove-Item $buildDir -Recurse -Force }
New-Item -ItemType Directory -Path $appDest | Out-Null

# Copy published app files
Write-Host "Copying published app..."
Copy-Item -Path (Join-Path $publishDir "*") -Destination $appDest -Recurse -Force

# Copy icon file nếu tồn tại
if (Test-Path $iconFile) {
    Write-Host "Copying icon file..."
    Copy-Item -Path $iconFile -Destination $appDest -Force
} else {
    Write-Warning "Icon file not found at $iconFile"
}

# Copy webview installer
if (-Not (Test-Path $webviewInstaller)) {
    Write-Error "WebView2 installer not found at $webviewInstaller. Place MicrosoftEdgeWebView2RuntimeInstallerX64.exe there and rerun."
    exit 1
}
New-Item -ItemType Directory -Path (Join-Path $buildDir "thirdparty") | Out-Null
Copy-Item -Path $webviewInstaller -Destination (Join-Path $buildDir "thirdparty") -Force

# Create Inno Setup .iss file
$issPath = Join-Path $buildDir "MyWidgetInstaller.iss"
$issContent = @"
; Auto-generated Inno Setup script
[Setup]
AppName=NowPlayingPopup
AppVersion=1.0
DefaultDirName={autopf}\NowPlayingPopup
DefaultGroupName=NowPlayingPopup
OutputBaseFilename=NowPlayingPopupSetup
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
DisableProgramGroupPage=yes
SetupIconFile=App\icon.ico
UninstallDisplayIcon={app}\icon.ico
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Tạo shortcut trên Desktop"; GroupDescription: "Tùy chọn thêm:"; Flags: unchecked

[Files]
; Copy all app files
Source: "App\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; WebView2 Evergreen Standalone (offline), will be copied to {tmp}
Source: "thirdparty\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\NowPlayingPopup"; Filename: "{app}\NowPlayingPopup.exe"; IconFilename: "{app}\icon.ico"
Name: "{autodesktop}\NowPlayingPopup"; Filename: "{app}\NowPlayingPopup.exe"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\NowPlayingPopup.exe"; Description: "Chạy NowPlayingPopup ngay bây giờ"; Flags: nowait postinstall skipifsilent

[Code]
function WebView2Installed(): Boolean;
var
  s: string;
begin
  Result := False;
  { Check common registry locations for WebView2 pv }
  if RegQueryStringValue(HKLM32, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', s) then
  begin
    if (s <> '') and (s <> '0.0.0.0') then
    begin
      Result := True;
      Exit;
    end;
  end;
  if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', s) then
  begin
    if (s <> '') and (s <> '0.0.0.0') then
    begin
      Result := True;
      Exit;
    end;
  end;
  if RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', s) then
  begin
    if (s <> '') and (s <> '0.0.0.0') then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  InstallerPath: string;
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if not WebView2Installed() then
    begin
      InstallerPath := ExpandConstant('{tmp}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
      if FileExists(InstallerPath) then
      begin
        if Exec(InstallerPath, '/silent /install', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
        begin
          if not WebView2Installed() then
            MsgBox('Cài đặt WebView2 Runtime có thể đã thất bại. Ứng dụng có thể không chạy được.', mbError, MB_OK);
        end else
          MsgBox('Không thể chạy trình cài đặt WebView2.', mbError, MB_OK);
      end else
        MsgBox('Không tìm thấy trình cài đặt WebView2 trong thư mục tạm.', mbError, MB_OK);
    end;
  end;
end;
"@

$issContent | Out-File -FilePath $issPath -Encoding UTF8
Write-Host "Created ISS at $issPath"
Write-Host "Installer build prepared at $buildDir. Run build_release.cmd to continue."