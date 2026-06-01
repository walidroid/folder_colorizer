@echo off
:: Folder Colorizer — Uninstaller
:: Run as Administrator to remove the right-click context menu entry.

echo Removing Folder Colorizer context menu...
reg delete "HKEY_CLASSES_ROOT\Directory\shell\FolderColorizer" /f >nul 2>&1

if errorlevel 1 (
    echo Context menu entry not found (already removed).
) else (
    echo Context menu entry removed successfully.
)

:: Optionally remove the installed files
set /p REMOVE="Remove installed files from AppData? (y/n): "
if /i "%REMOVE%"=="y" (
    rmdir /s /q "%LOCALAPPDATA%\FolderColorizer" >nul 2>&1
    echo Installed files removed.
)

echo.
echo Uninstall complete.
pause
