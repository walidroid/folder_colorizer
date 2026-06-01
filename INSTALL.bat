@echo off
:: Folder Colorizer — Windows Installer
:: Run this as Administrator to set up the right-click context menu.

echo ============================================
echo   Folder Colorizer — Setup
echo ============================================
echo.

:: Check for Python
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python not found. Please install Python 3.10+ from https://python.org
    pause
    exit /b 1
)

echo [1/3] Python found.

:: Install Pillow if not present (needed to regenerate icons if missing)
python -c "import PIL" >nul 2>&1
if errorlevel 1 (
    echo [2/3] Installing Pillow...
    pip install Pillow --quiet
) else (
    echo [2/3] Pillow already installed.
)

:: Regenerate icons if the icons directory is empty or missing
if not exist "%~dp0icons\yellow.ico" (
    echo [2b] Generating folder icons...
    python "%~dp0generate_icons.py"
)

:: Run the installer with admin rights
echo [3/3] Installing right-click context menu...
python "%~dp0folder_colorizer.py"

echo.
echo Done! Right-click any folder in Explorer to use Folder Colorizer.
pause
