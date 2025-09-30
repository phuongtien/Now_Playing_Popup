@echo off
setlocal

:: Ask for version number
set /p VERSION=Enter new version (e.g. 1.0.1): 

:: Run PowerShell script
powershell -ExecutionPolicy Bypass -File "%~dp0update-release.ps1" -Version "%VERSION%"

pause
