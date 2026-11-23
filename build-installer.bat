@echo off
setlocal
cd /d "%~dp0"

echo [1/2] Building unified SingleFile executable...
taskkill /F /IM MusicMediaKeys.exe >nul 2>&1
taskkill /F /IM MusicMediaKeys-Setup.exe >nul 2>&1
call dotnet publish MusicMediaKeys.csproj -c Release -p:PublishProfile=SingleFile
if errorlevel 1 (
    echo Error: SingleFile publish failed.
    exit /b 1
)

if not exist "bin\SingleExe\MusicMediaKeys.exe" (
    echo Error: bin\SingleExe\MusicMediaKeys.exe not found.
    exit /b 1
)

echo.
echo [2/2] Creating dual-mode MusicMediaKeys-Setup.exe...
copy /Y "bin\SingleExe\MusicMediaKeys.exe" "bin\SingleExe\MusicMediaKeys-Setup.exe" >nul
if errorlevel 1 (
    echo Error: Failed to create MusicMediaKeys-Setup.exe.
    exit /b 1
)

echo.
echo Done! Outputs:
echo   bin\SingleExe\MusicMediaKeys.exe
echo   bin\SingleExe\MusicMediaKeys-Setup.exe
exit /b 0
