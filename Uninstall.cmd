@echo off
powershell.exe -NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Install-Mod.ps1" -Mode Uninstall %*
set "modExit=%errorlevel%"
pause
exit /b %modExit%
