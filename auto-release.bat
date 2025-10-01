@echo off
setlocal

:: ==========================
:: Config
:: ==========================
set REPO=phuongtien/Now_Playing_Popup_clean
set BRANCH=Tien_main
set INNO="E:\Inno Setup 6\ISCC.exe"
set ISS=installer_build\MyWidgetInstaller.iss
set OUTPUT_DIR=installer_build\Output
set SETUP_FILE=NowPlayingPopupSetup.exe

:: ==========================
:: 1. Nhập version mới
:: ==========================
set /p VERSION=Enter new version (e.g. 1.0.3): 

:: ==========================
:: 2. Build & Publish dự án .NET
:: ==========================
echo Building project...
dotnet publish NowPlayingPopup.csproj -c Release -r win-x64 --self-contained true
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

:: ==========================
:: 3. Chuẩn bị folder installer
:: ==========================
echo Preparing installer...
powershell -ExecutionPolicy Bypass -File "%~dp0prepare_installer.ps1"
if errorlevel 1 (
    echo Prepare installer failed!
    pause
    exit /b 1
)

:: ==========================
:: 4. Build installer (Inno Setup)
:: ==========================
echo Building installer...
if not exist %ISS% (
    echo ERROR: Could not find Inno Setup script: %ISS%
    pause
    exit /b 1
)

%INNO% %ISS%
if errorlevel 1 (
    echo Inno Setup build failed!
    pause
    exit /b 1
)

:: ==========================
:: 5. Update manifest.json
:: ==========================
echo Updating manifest...
powershell -ExecutionPolicy Bypass -File "%~dp0update-release.ps1" -Version "%VERSION%"
if errorlevel 1 (
    echo Manifest update failed!
    pause
    exit /b 1
)

:: ==========================
:: 6. Commit & Push manifest.json
:: ==========================
echo Committing manifest.json...
git add releases\manifest.json
git commit -m "Update manifest v%VERSION%"
git push origin %BRANCH%
if errorlevel 1 (
    echo Git push failed!
    pause
    exit /b 1
)

:: ==========================
:: 7. GitHub Release + upload
:: ==========================
echo Creating GitHub Release v%VERSION%...

set FILEPATH=
for /f "usebackq tokens=*" %%i in (`dir /b /od %OUTPUT_DIR%\%SETUP_FILE%`) do set FILEPATH=%OUTPUT_DIR%\%%i

if not exist "%FILEPATH%" (
    echo ERROR: Could not find setup file in %OUTPUT_DIR%
    pause
    exit /b 1
)

gh release delete "v%VERSION%" -R %REPO% -y >nul 2>&1
git tag -d "v%VERSION%" >nul 2>&1

gh release create "v%VERSION%" "%FILEPATH%" "releases\manifest.json" ^
    -R %REPO% ^
    --title "NowPlayingPopup v%VERSION%" ^
    --notes "Auto release v%VERSION%: bug fixes and improvements"

echo ====================================
echo Release v%VERSION% has been created and files uploaded!
echo Manifest.json committed and pushed to branch %BRANCH%
echo ====================================

pause
