@echo off
setlocal
cd /d "%~dp0"

echo [1/3] Cleaning old build files (Debug)...
dotnet clean -c Debug

echo.
echo [2/3] Building project (Debug)...
dotnet build -c Debug
if errorlevel 1 (
    echo.
    echo Error: Build failed.
    pause
    exit /b 1
)

set "OUT_DIR=bin\Debug\net10.0-windows"
set "EXE=%OUT_DIR%\MusicMediaKeys.exe"

echo.
echo [3/3] Starting application in debug mode...
echo (Close the application or this console to exit)
echo.
"%EXE%"
