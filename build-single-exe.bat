@echo off
setlocal
cd /d "%~dp0"

echo [1/3] Cleaning previous publish and build artifacts...
taskkill /F /IM MusicMediaKeys.exe >nul 2>&1
if exist "bin\SingleExe" rd /s /q "bin\SingleExe"
call dotnet clean -c Release >nul 2>&1

echo.
echo [2/3] Publishing to a single independent EXE file (Self-Contained)...
call dotnet publish -c Release -p:PublishProfile=SingleFile
if errorlevel 1 (
    echo.
    echo Error: Publish failed.
    pause
    exit /b 1
)

set "PUBLISH_DIR=bin\Release\net10.0-windows\win-x64\publish"
set "EXE=%PUBLISH_DIR%\MusicMediaKeys.exe"
set "OUT_DIR=bin\SingleExe"
set "OUT_EXE=%OUT_DIR%\MusicMediaKeys.exe"

if not exist "%EXE%" (
    echo Error: Compiled executable not found at %EXE%
    pause
    exit /b 1
)

if not exist "%OUT_EXE%" (
    if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"
    copy /Y "%EXE%" "%OUT_EXE%" >nul
)

echo.
echo [3/3] Publish completed successfully!
echo.
echo File is ready for distribution (does not require .NET installed):
echo %OUT_EXE%
echo.
echo Automatically closing in 3 seconds...
timeout /t 3 >nul 2>&1 || ping 127.0.0.1 -n 4 >nul
exit /b 0
