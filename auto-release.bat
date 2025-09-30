@echo off
setlocal

:: ==========================
:: Config
:: ==========================
set REPO=phuongtien/Now_Playing_Popup_clean
set INNO="E:\Inno Setup 6\ISCC.exe"
set ISS=installer_build\MyWidgetInstaller.iss
set OUTPUT_DIR=installer_build\Output
set SETUP_FILE=NowPlayingPopupSetup.exe

:: ==========================
:: 1. Nhập version mới
:: ==========================
set /p VERSION=Enter new version (e.g. 1.0.3): 

:: ==========================
:: 2. Build installer (Inno Setup)
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
:: 3. Update manifest.json
:: ==========================
echo Updating manifest...
powershell -ExecutionPolicy Bypass -File "%~dp0update-release.ps1" -Version "%VERSION%"
if errorlevel 1 (
    echo Manifest update failed!
    pause
    exit /b 1
)

:: ==========================
:: 4. GitHub Release + upload
:: ==========================
echo Creating GitHub Release v%VERSION%...

:: Tìm file setup mới build
set FILEPATH=
for /f "usebackq tokens=*" %%i in (`dir /b /od %OUTPUT_DIR%\%SETUP_FILE%`) do set FILEPATH=%OUTPUT_DIR%\%%i

if not exist "%FILEPATH%" (
    echo ERROR: Could not find setup file in %OUTPUT_DIR%
    pause
    exit /b 1
)

:: Xóa release/tag cũ (nếu có) rồi tạo lại
gh release delete "v%VERSION%" -R %REPO% -y >nul 2>&1
git tag -d "v%VERSION%" >nul 2>&1

:: Tạo release mới và upload file .exe + manifest.json
gh release create "v%VERSION%" "%FILEPATH%" "releases\manifest.json" ^
    -R %REPO% ^
    --title "NowPlayingPopup v%VERSION%" ^
    --notes "Auto release v%VERSION%: bug fixes and improvements"

echo ====================================
echo Release v%VERSION% has been created and files uploaded!
echo ====================================

pause
