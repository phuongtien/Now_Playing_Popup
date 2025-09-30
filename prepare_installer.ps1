# prepare_installer.ps1
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$publishDir = Join-Path $projectRoot "bin\Release\net9.0-windows10.0.17763.0\win-x64\publish"
$buildDir = Join-Path $projectRoot "installer_build"
$appDest = Join-Path $buildDir "App"
$thirdparty = Join-Path $projectRoot "thirdparty"
$webviewInstaller = Join-Path $thirdparty "MicrosoftEdgeWebView2RuntimeInstallerX64.exe"

Write-Host "Publish dir:" $publishDir
Write-Host "Installer build dir:" $buildDir

# Clean + create folders
if (Test-Path $buildDir) { Remove-Item $buildDir -Recurse -Force }
New-Item -ItemType Directory -Path $appDest | Out-Null

# Copy published app files
Write-Host "Copying published app..."
Copy-Item -Path (Join-Path $publishDir "*") -Destination $appDest -Recurse -Force

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
DefaultDirName={pf}\NowPlayingPopup
DefaultGroupName=NowPlayingPopup
OutputBaseFilename=NowPlayingPopupSetup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
DisableProgramGroupPage=yes

[Files]
; Copy all app files
Source: "App\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; WebView2 Evergreen Standalone (offline), will be copied to {tmp}
Source: "thirdparty\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\NowPlayingPopup"; Filename: "{app}\NowPlayingPopup.exe"
Name: "{userdesktop}\NowPlayingPopup"; Filename: "{app}\NowPlayingPopup.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\NowPlayingPopup.exe"; Description: "Run NowPlayingPopup"; Flags: nowait postinstall skipifsilent

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

procedure CurStepChanged(CurStep: Integer);
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
            MsgBox('WebView2 Runtime installation may have failed. The app may not run properly.', mbError, MB_OK);
        end else
          MsgBox('Failed to execute WebView2 installer.', mbError, MB_OK);
      end else
        MsgBox('WebView2 installer missing in temp folder.', mbError, MB_OK);
    end;
  end;
end;
"@

$issContent | Out-File -FilePath $issPath -Encoding UTF8
Write-Host "Created ISS at $issPath"
Write-Host "Installer build prepared at $buildDir. Open MyWidgetInstaller.iss in Inno Setup and build or run ISCC."
