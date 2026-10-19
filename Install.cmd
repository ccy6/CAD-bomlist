@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Install-Local.ps1"
set "BOM_INSTALL_EXIT=%ERRORLEVEL%"
echo.
if not "%BOM_INSTALL_EXIT%"=="0" echo Installation failed. See the message above.
pause
exit /b %BOM_INSTALL_EXIT%
