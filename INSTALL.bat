@echo off
:: Folder Colorizer — Windows Installer
:: Run this as Administrator to set up the right-click context menu.

echo ============================================
echo   Folder Colorizer — Setup
echo ============================================
echo.

:: Locate the EXE next to this script
set "EXE=%~dp0folder_colorizer.exe"

if not exist "%EXE%" (
    echo [ERROR] folder_colorizer.exe not found in:
    echo         %~dp0
    echo.
    echo Please run the installer from the folder containing folder_colorizer.exe.
    pause
    exit /b 1
)

echo [1/2] Found folder_colorizer.exe
echo.

:: Launch the app — it handles context-menu install when run as Admin
echo [2/2] Launching Folder Colorizer...
echo        (The app will prompt you to install the right-click context menu.)
echo.
"%EXE%"

echo.
echo Done! Right-click any folder in Explorer to use Folder Colorizer.
pause
