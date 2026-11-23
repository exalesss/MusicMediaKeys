@echo off
setlocal
cd /d "%~dp0"

echo [1/3] Cleaning old build files (Release)...
taskkill /F /IM MusicMediaKeys.exe >nul 2>&1
dotnet clean -c Release

echo.
echo [2/3] Building project (Release)...
dotnet build -c Release
if errorlevel 1 (
    echo.
    echo Error: Build failed.
    pause
    exit /b 1
)

set "OUT_DIR=bin\Release\net10.0-windows"

echo.
echo [3/3] Build completed successfully!
echo Ready application folder (with all DLLs and resources):
echo %OUT_DIR%
echo.
echo Automatically closing in 3 seconds...
timeout /t 3 >nul 2>&1 || ping 127.0.0.1 -n 4 >nul
exit /b 0
