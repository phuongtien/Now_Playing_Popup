@echo off
setlocal

:: ==========================
:: 1. Nhập version mới
:: ==========================
set /p VERSION=Enter new version (e.g. 1.0.1): 

:: ==========================
:: 2. Build installer (Inno Setup)
:: ==========================
echo Building installer...
set ISS=installer_build\MyWidgetInstaller.iss

if not exist "%ISS%" (
    echo ERROR: Could not find Inno Setup script file: %ISS%
    pause
    exit /b 1
)

"E:\Inno Setup 6\ISCC.exe" "%ISS%"
if errorlevel 1 (
    echo Inno Setup build failed!
    pause
    exit /b 1
)

:: ==========================
:: 3. Cập nhật manifest.json
:: ==========================
echo Updating manifest...
powershell -ExecutionPolicy Bypass -File "%~dp0update-release.ps1" -Version "%VERSION%"

echo ====================================
echo Done! 
echo - Manifest.json đã được cập nhật
echo - File setup mới đã build xong
echo - Upload file setup và manifest lên GitHub Release v%VERSION%
echo ====================================

pause
