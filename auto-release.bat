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

if "%VERSION%"=="" (
    echo No version entered. Aborting.
    pause
    exit /b 1
)

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
if errorlevel 1 (
    echo No changes to commit or commit failed.
) else (
    git push origin %BRANCH%
    if errorlevel 1 (
        echo Git push failed!
        pause
        exit /b 1
    )
)

:: ==========================
:: 6.5 Clean any previous remote release/tag (optional but safe)
:: ==========================
echo Cleaning previous release/tag if exists...
:: Delete GitHub release (if exists) -- ignore errors
gh release delete "v%VERSION%" -R %REPO% -y >nul 2>&1

:: Delete remote tag if exists
git ls-remote --tags origin v%VERSION% | findstr /R /C:"refs/tags/v%VERSION%" >nul 2>&1
if %ERRORLEVEL%==0 (
    echo Remote tag v%VERSION% exists -> deleting it...
    git push --delete origin "v%VERSION%" >nul 2>&1
)

:: Delete local tag if exists
git tag -l "v%VERSION%" | findstr /R /C:"v%VERSION%" >nul 2>&1
if %ERRORLEVEL%==0 (
    git tag -d "v%VERSION%" >nul 2>&1
)

:: ==========================
:: 7. Create and push annotated tag for the release
:: ==========================
echo Tagging commit as v%VERSION%...
git tag -a "v%VERSION%" -m "Release v%VERSION%"
if errorlevel 1 (
    echo Tag creation failed!
    pause
    exit /b 1
)
git push origin "v%VERSION%"
if errorlevel 1 (
    echo Tag push failed!
    pause
    exit /b 1
)

:: ==========================
:: 8. Prepare assets + source zip (from tag)
:: ==========================
echo Locating setup file...
set FILEPATH=
for /f "usebackq tokens=*" %%i in (`dir /b /od "%OUTPUT_DIR%\%SETUP_FILE%"`) do set FILEPATH=%OUTPUT_DIR%\%%i

if not exist "%FILEPATH%" (
    echo ERROR: Could not find setup file in %OUTPUT_DIR%
    pause
    exit /b 1
)

:: Create a source zip from the tagged commit (reliable)
set SRCZIP=%OUTPUT_DIR%\NowPlayingPopup-%VERSION%-source.zip
if exist "%SRCZIP%" del /q "%SRCZIP%"
echo Creating source archive from tag v%VERSION%...
git archive --format=zip --output="%SRCZIP%" "v%VERSION%"
if errorlevel 1 (
    echo Failed to create source zip via git archive. Continuing without source zip.
    if exist "%SRCZIP%" del /q "%SRCZIP%"
)

:: ==========================
:: 9. Create GitHub Release and upload assets
:: ==========================
echo Creating GitHub Release v%VERSION%...

if exist "%SRCZIP%" (
    gh release create "v%VERSION%" "%FILEPATH%" "releases\manifest.json" "%SRCZIP%" ^
        -R %REPO% ^
        --title "NowPlayingPopup v%VERSION%" ^
        --notes "Auto release v%VERSION%: bug fixes and improvements"
) else (
    gh release create "v%VERSION%" "%FILEPATH%" "releases\manifest.json" ^
        -R %REPO% ^
        --title "NowPlayingPopup v%VERSION%" ^
        --notes "Auto release v%VERSION%: bug fixes and improvements"
)

if errorlevel 1 (
    echo gh release create failed!
    pause
    exit /b 1
)

echo ====================================
echo Release v%VERSION% has been created and files uploaded!
echo Manifest.json committed and pushed to branch %BRANCH%
echo ====================================
pause
